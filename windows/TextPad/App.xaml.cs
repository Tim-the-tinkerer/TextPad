using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using TextPad.Services;

namespace TextPad;

public partial class App : Application
{
    private SingleInstanceManager? _singleInstance;

    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        BundledFonts.Register();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        _singleInstance = SingleInstanceManager.Acquire();
        if (!_singleInstance.IsPrimary)
        {
            if (TryForwardToRunningInstance(e.Args))
            {
                Shutdown();
                return;
            }

            System.Windows.MessageBox.Show(
                "TextPad is already running, but the open request could not be delivered.\n\nClose the other TextPad window and try again.",
                "TextPad",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        var window = new MainWindow(e.Args);
        MainWindow = window;
        _singleInstance.StartListening(
            paths => window.Dispatcher.BeginInvoke(() => window.OpenExternalFiles(paths)),
            () => window.Dispatcher.BeginInvoke(window.ActivateWindow));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLogger.Log(e.Exception, "UI thread");

        if (IsRecoverableException(e.Exception))
        {
            System.Windows.MessageBox.Show(
                $"TextPad encountered an unexpected error and will try to continue.\n\n{e.Exception.Message}",
                "TextPad",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            return;
        }

        System.Windows.MessageBox.Show(
            $"TextPad encountered a serious error and must close.\n\n{e.Exception.Message}",
            "TextPad",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = false;
    }

    private static bool IsRecoverableException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or FormatException;

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            CrashLogger.Log(ex, "AppDomain");
    }

    private static bool TryForwardToRunningInstance(string[] args)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (SingleInstanceManager.TryForwardToRunningInstance(args))
                return true;
            Thread.Sleep(50);
        }

        return false;
    }
}