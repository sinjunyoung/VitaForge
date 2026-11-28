using Common.Localization;
using Vita.Core.Typography;
using VitaForge.Core;
using VitaForge.Core.Typography;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace VitaForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!string.IsNullOrEmpty(AppConfig.Instance.Common.Language))
            Loc.Instance.Set(AppConfig.Instance.Common.Language);

        GlyphRasterizerProvider.Current = new WpfGlyphRasterizer();

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteLog(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteLog(args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        e.Handled = true;
        System.Windows.MessageBox.Show(e.Exception.Message, Loc.S.MsgTitleError);
    }

    private static void WriteLog(Exception? ex)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }
}