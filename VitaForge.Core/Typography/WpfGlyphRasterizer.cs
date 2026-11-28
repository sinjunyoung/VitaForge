using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vita.Core.Typography;

namespace VitaForge.Core.Typography;

public sealed class WpfGlyphRasterizer : IGlyphRasterizer, IDisposable
{
    private const int Margin = 2;

    private readonly BlockingCollection<Action> queue = [];
    private readonly Dictionary<string, GlyphTypeface> typefaces = new(StringComparer.Ordinal);
    private readonly List<string> temporaryFiles = [];
    private readonly Thread worker;

    public WpfGlyphRasterizer()
    {
        worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "GlyphRasterizer"
        };

        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    public GlyphInk? Render(GlyphInkRequest request)
    {
        GlyphInk? result = null;
        Exception? error = null;

        using var completed = new ManualResetEventSlim();

        queue.Add(() =>
        {
            try
            {
                result = RenderOnWorker(request);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        completed.Wait();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }

    public void Dispose()
    {
        queue.CompleteAdding();
        worker.Join();
    }

    private void Run()
    {
        foreach (var action in queue.GetConsumingEnumerable())
            action();

        typefaces.Clear();

        foreach (string file in temporaryFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }

    private GlyphInk? RenderOnWorker(GlyphInkRequest request)
    {
        var typeface = GetTypeface(request);
        int codePoint = char.ConvertToUtf32(request.Character, 0);

        if (!typeface.CharacterToGlyphMap.TryGetValue(codePoint, out ushort glyphIndex))
            glyphIndex = 0;

        double emSize = request.EmSizePixels;
        var run = new GlyphRun(typeface, 0, false, emSize, 1.0f, [glyphIndex], new Point(0, 0), [typeface.AdvanceWidths[glyphIndex] * emSize], null, null, null, null, null, null);
        var geometry = run.BuildGeometry();
        var pen = request.StrokePixels > 0 ? CreatePen(request) : null;
        var bounds = pen is null ? geometry.Bounds : geometry.GetRenderBounds(pen);

        if (bounds.IsEmpty)
            return null;

        int left = (int)Math.Floor(bounds.Left) - Margin;
        int top = (int)Math.Floor(bounds.Top) - Margin;
        int width = (int)Math.Ceiling(bounds.Right) - left + Margin;
        int height = (int)Math.Ceiling(bounds.Bottom) - top + Margin;
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(-left, -top));

            if (pen is not null)
                context.DrawGeometry(null, pen, geometry);

            context.DrawGeometry(new SolidColorBrush(ToColor(request.Fill)), null, geometry);
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        bitmap.Render(visual);

        byte[] bgra = new byte[width * height * 4];

        bitmap.CopyPixels(bgra, width * 4, 0);

        return Crop(bgra, width, height);
    }

    private static GlyphInk? Crop(byte[] bgra, int width, int height)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (bgra[(y * width + x) * 4 + 3] == 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0)
            return null;

        int croppedWidth = maxX - minX + 1;
        int croppedHeight = maxY - minY + 1;
        byte[] rgba = new byte[croppedWidth * croppedHeight * 4];

        for (int y = 0; y < croppedHeight; y++)
        {
            for (int x = 0; x < croppedWidth; x++)
            {
                int source = ((minY + y) * width + minX + x) * 4;
                int target = (y * croppedWidth + x) * 4;

                rgba[target] = bgra[source + 2];
                rgba[target + 1] = bgra[source + 1];
                rgba[target + 2] = bgra[source];
                rgba[target + 3] = bgra[source + 3];
            }
        }

        return new GlyphInk(croppedWidth, croppedHeight, rgba);
    }

    private static Pen CreatePen(GlyphInkRequest request) => new(new SolidColorBrush(ToColor(request.StrokeFill)), request.StrokePixels * 2)
    {
        LineJoin = PenLineJoin.Round,
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    };

    private static System.Windows.Media.Color ToColor(GlyphColor color) => System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);

    private GlyphTypeface GetTypeface(GlyphInkRequest request)
    {
        if (typefaces.TryGetValue(request.FontKey, out var cached))
            return cached;

        string extension = Path.GetExtension(request.FontKey);
        string path = Path.Combine(Path.GetTempPath(), $"vitaforge-{Guid.NewGuid():N}{(extension.Length > 0 ? extension : ".ttf")}");

        File.WriteAllBytes(path, request.FontData);

        temporaryFiles.Add(path);

        var typeface = new GlyphTypeface(new Uri(path));

        typefaces[request.FontKey] = typeface;

        return typeface;
    }
}