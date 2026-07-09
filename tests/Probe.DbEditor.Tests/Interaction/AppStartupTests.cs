namespace Probe.DbEditor.Tests.Interaction;

[TestClass]
public sealed class AppStartupTests
{
    [TestMethod]
    public async Task AppStartup_DefaultsToSoftwareRenderingToKeepIdleMemoryLow()
    {
        var source = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "App.xaml.cs"));

        StringAssert.Contains(source, "ConfigureProcessRendering();");
        StringAssert.Contains(source, "PROBE_DB_EDITOR_ENABLE_HARDWARE_RENDERING");
        StringAssert.Contains(source, "RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;");
        StringAssert.Contains(source, "System.Windows.Interop");
        StringAssert.Contains(source, "System.Windows.Media");
    }
}
