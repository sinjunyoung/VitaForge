using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class ExecutableResources : IDisposable
{
    private readonly FileStream _executable;

    private readonly Stream _image;

    private readonly Dictionary<string, ManagedResource> _resources;

    private readonly string _marker;

    private ExecutableResources(FileStream executable, Stream image, Dictionary<string, ManagedResource> resources, string marker)
    {
        _executable = executable;
        _image = image;
        _resources = resources;
        _marker = marker;
    }

    public static async Task<ExecutableResources> OpenAsync(ISourceAccessor accessor, string rawPath, string? assemblyName, string marker, string tempDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(tempDirectory);

        var executable = CreateTempFile(tempDirectory, ".exe");
        Stream? image = null;

        try
        {
            await Task.Run(() => accessor.CopyTo(rawPath, executable), ct);

            executable.Flush();

            image = OpenImage(executable, assemblyName, tempDirectory);

            return new ExecutableResources(executable, image, ReadResourceTable(image), marker);
        }
        catch
        {
            image?.Dispose();
            executable.Dispose();

            throw;
        }
    }

    public byte[] Read(string name)
    {
        var resource = _resources.FirstOrDefault(r => r.Key.EndsWith($"{_marker}{name}", StringComparison.OrdinalIgnoreCase)).Value
            ?? throw new InvalidDataException($"embedded resource not found: {name}");

        byte[] data = new byte[resource.Length];

        _image.Position = resource.Offset;
        _image.ReadExactly(data);

        return data;
    }

    public void Dispose()
    {
        _image.Dispose();
        _executable.Dispose();
    }

    private static FileStream CreateTempFile(string tempDirectory, string extension)
        => new(Path.Combine(tempDirectory, $"vf_{Guid.NewGuid():N}{extension}"), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, FileOptions.DeleteOnClose);

    private static Stream OpenImage(FileStream executable, string? assemblyName, string tempDirectory)
    {
        var bundle = SingleFileBundle.TryRead(executable);

        if (bundle is null)
            return new BoundedReadStream(executable, 0, executable.Length);

        var entry = bundle.FindAssembly(assemblyName) ?? throw new InvalidDataException("no managed assembly found in the single-file bundle");

        if (!entry.IsCompressed)
            return new BoundedReadStream(executable, entry.Offset, entry.Size);

        var inflated = CreateTempFile(tempDirectory, ".dll");

        try
        {
            using var deflate = new DeflateStream(new BoundedReadStream(executable, entry.Offset, entry.CompressedSize), CompressionMode.Decompress);

            deflate.CopyTo(inflated);

            inflated.Position = 0;

            return inflated;
        }
        catch
        {
            inflated.Dispose();

            throw;
        }
    }

    private static Dictionary<string, ManagedResource> ReadResourceTable(Stream image)
    {
        var located = new List<(string Name, long Offset)>();

        using (var peReader = new PEReader(image, PEStreamOptions.LeaveOpen))
        {
            if (!peReader.HasMetadata || peReader.PEHeaders.CorHeader is not { } corHeader || !peReader.PEHeaders.TryGetDirectoryOffset(corHeader.ResourcesDirectory, out int resourcesOffset))
                throw new InvalidDataException("assembly has no embedded resources");

            var metadata = peReader.GetMetadataReader();

            foreach (var handle in metadata.ManifestResources)
            {
                var resource = metadata.GetManifestResource(handle);

                if (resource.Implementation.IsNil)
                    located.Add((metadata.GetString(resource.Name), resourcesOffset + resource.Offset));
            }
        }

        var table = new Dictionary<string, ManagedResource>(StringComparer.OrdinalIgnoreCase);
        byte[] lengthBuffer = new byte[sizeof(uint)];

        foreach (var (name, offset) in located)
        {
            image.Position = offset;
            image.ReadExactly(lengthBuffer);

            uint length = BitConverter.ToUInt32(lengthBuffer);

            if (length > int.MaxValue)
                continue;

            table[name] = new ManagedResource(offset + sizeof(uint), (int)length);
        }

        return table;
    }
}
