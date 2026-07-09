using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Probe.DbEditor;

public partial class App : Application
{
    private const string EnableHardwareRenderingVariable = "PROBE_DB_EDITOR_ENABLE_HARDWARE_RENDERING";

    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProbeDbEditor",
        "startup-error.log");

    public App()
    {
        ConfigureProcessRendering();
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private static void ConfigureProcessRendering()
    {
        var enableHardwareRendering = Environment.GetEnvironmentVariable(EnableHardwareRenderingVariable);
        if (string.Equals(enableHardwareRendering, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enableHardwareRendering, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            ReportFatalStartupError(ex);
        }
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportFatalStartupError(e.Exception);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteStartupError(ex);
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ReportFatalStartupError(e.Exception);
    }

    private static void ReportFatalStartupError(Exception ex)
    {
        WriteStartupError(ex);
        MessageBox.Show(
            $"Probe DB Editor failed to start.\n\nDetails were written to:\n{StartupLogPath}\n\n{ex.Message}",
            "Probe DB Editor startup error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Current.Shutdown(1);
    }

    private static void WriteStartupError(Exception ex)
    {
        var directory = Path.GetDirectoryName(StartupLogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var message = new StringBuilder()
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"OS: {Environment.OSVersion}")
            .AppendLine($".NET: {Environment.Version}")
            .AppendLine()
            .AppendLine(ex.ToString())
            .ToString();

        File.WriteAllText(StartupLogPath, message);
    }
}
