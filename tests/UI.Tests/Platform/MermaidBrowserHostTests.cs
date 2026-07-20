using System.IO;
using Shouldly;
using UI.Platform;
using Xunit;

namespace UI.Tests.Platform;

/// <summary>
/// Tests that the <see cref="MermaidBrowserHost"/> resolves the browser profile outside the
/// installation directory while still reading the Mermaid assets from beside the executable
/// (INV-047).
/// </summary>
/// <remarks>
/// These are the regression tests for a diagram that renders for a developer but never for an
/// installed user: the browser defaulted its profile beside the executable, which an installed app
/// may only read, so the browser never started and every diagram fell back to its source text.
/// </remarks>
public sealed class MermaidBrowserHostTests
{
    [Fact]
    public void UserDataFolder_IsNotInsideTheInstallationDirectory_INV047()
    {
        var installation = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

        var profile = MermaidBrowserHost.UserDataFolder;

        profile.ShouldNotStartWith(installation, Case.Insensitive);
    }

    [Fact]
    public void UserDataFolder_IsUnderTheLocalApplicationData_INV047()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var profile = MermaidBrowserHost.UserDataFolder;

        profile.ShouldStartWith(localApplicationData, Case.Insensitive);
    }

    [Fact]
    public void UserDataFolder_IsAnAbsolutePath_INV047()
    {
        Path.IsPathFullyQualified(MermaidBrowserHost.UserDataFolder).ShouldBeTrue();
    }

    [Fact]
    public void AssetsFolder_ResolvesBesideTheExecutable_INV047()
    {
        var expected = Path.Combine(AppContext.BaseDirectory, "Assets", "Mermaid");

        MermaidBrowserHost.AssetsFolder.ShouldBe(expected);
    }
}
