using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ShutdownTimer;

public partial class App : Application
{
    private const string MutexName = @"Local\ShutdownTimer.SingleInstance.v1";
    private const string ActivationEventName = @"Local\ShutdownTimer.Activate.v1";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activationEvent;
    private Thread? _activationThread;
    private volatile bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        StartActivationListener();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;

        try
        {
            _activationEvent?.Set();
            _activationEvent?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
            // App is exiting; cleanup failures should not block shutdown.
        }

        base.OnExit(e);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using EventWaitHandle activationEvent =
                EventWaitHandle.OpenExisting(ActivationEventName);

            activationEvent.Set();
        }
        catch
        {
            // The first instance may still be finishing startup.
        }
    }

    private void StartActivationListener()
    {
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ActivationEventName);

        _activationThread = new Thread(() =>
        {
            while (!_isExiting)
            {
                try
                {
                    _activationEvent.WaitOne();

                    if (_isExiting)
                        break;

                    Dispatcher.BeginInvoke(BringMainWindowToFront);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (_isExiting)
                        break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "ShutdownTimer.ActivationListener"
        };

        _activationThread.Start();
    }

    private void BringMainWindowToFront()
    {
        Window? window = MainWindow ??
            Windows.OfType<Window>().FirstOrDefault();

        if (window is null)
            return;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        if (!window.IsVisible)
            window.Show();

        window.Topmost = true;
        window.Activate();
        window.Focus();
        window.Topmost = false;
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShutdownTimer");

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "crash.log");

            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{exception}\n\n");
        }
        catch
        {
            // Never let diagnostics cause another crash.
        }
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);

        MessageBox.Show(
            "Shutdown Timer mengalami error saat dijalankan.\n\n" +
            "Log tersimpan di %LOCALAPPDATA%\\ShutdownTimer\\crash.log",
            "Shutdown Timer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(-1);
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            WriteCrashLog(exception);
    }
}
