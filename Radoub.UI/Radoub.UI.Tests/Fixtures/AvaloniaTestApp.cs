using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Radoub.UI.Tests.Fixtures;

/// <summary>
/// Minimal <see cref="Application"/> subclass for headless test runs.
/// Referenced by the assembly-level
/// <c>[AvaloniaTestApplication(typeof(AvaloniaTestApp))]</c> attribute in
/// AssemblyInfo.cs. The Avalonia.Headless.XUnit test framework looks up
/// <see cref="BuildAvaloniaApp"/> via reflection to construct the headless
/// application before any test runs.
/// </summary>
public class AvaloniaTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AvaloniaTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });
}
