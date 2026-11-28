using System.Xml.Linq;

using FluentAssertions;

namespace AstroCast.Tests.Packaging;

/// <summary>
/// The two Windows programs that load the Swiss Ephemeris library, the WPF head and the CLI, each carry an
/// application manifest that makes UTF-8 the process's code page (branch EphePath, 1.7.2; finding N4). The
/// library takes its ephemeris folder as a C string and opens its files through its own C runtime, both in
/// the process's ANSI code page; with UTF-8 there every folder name survives, and without it a name the
/// system's code page cannot hold (a Polish, Greek or Japanese one on a Western system) lost its letters on
/// the way and no chart could be cast.
/// </summary>
/// <remarks>
/// <para>
/// These tests read the project files and the manifests from the source tree, as
/// <see cref="ThirdPartyNoticesTests"/> reads the projects. They hold what must not be undone: the project
/// names its manifest, the manifest sets the code page, and it repeats the one entry of the default manifest
/// it replaces (the execution level), so the program asks for nothing it did not ask for before.
/// </para>
/// <para>
/// What they cannot hold is that Chiron is then read from a folder outside the code page: the test host is
/// not built from either project and has no such manifest, so its own code page is the system's. That is
/// proved on the built programs, run from folders with such names (the branch's report).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class WindowsManifestTests
{
    private static readonly XNamespace Asm3 = "urn:schemas-microsoft-com:asm.v3";
    private static readonly XNamespace Settings2019 = "http://schemas.microsoft.com/SMI/2019/WindowsSettings";

    /// <summary>The project names its manifest, so the build embeds it in place of the default one.</summary>
    /// <param name="project">The project file, from the repository root.</param>
    [Theory]
    [InlineData("src/AstroCast/AstroCast.csproj")]
    [InlineData("src/AstroCast.Cli/AstroCast.Cli.csproj")]
    public void Project_NamesItsApplicationManifest(string project)
    {
        XDocument document = XDocument.Load(FromRoot(project));

        document.Descendants("ApplicationManifest").Should().ContainSingle()
            .Which.Value.Should().Be("app.manifest");
        File.Exists(Path.Combine(Path.GetDirectoryName(FromRoot(project))!, "app.manifest")).Should().BeTrue();
    }

    /// <summary>The manifest makes UTF-8 the process's code page: the entry that lets every folder name reach the library.</summary>
    /// <param name="manifest">The manifest, from the repository root.</param>
    [Theory]
    [InlineData("src/AstroCast/app.manifest")]
    [InlineData("src/AstroCast.Cli/app.manifest")]
    public void Manifest_SetsUtf8AsTheProcessCodePage(string manifest)
    {
        XDocument document = XDocument.Load(FromRoot(manifest));

        document.Descendants(Settings2019 + "activeCodePage").Should().ContainSingle()
            .Which.Value.Should().Be("UTF-8");
    }

    /// <summary>
    /// The manifest repeats the default manifest's one entry, the execution level of the invoker with no UI
    /// access: a manifest of the project's own replaces the default, so what the default gave must be in it.
    /// </summary>
    /// <param name="manifest">The manifest, from the repository root.</param>
    [Theory]
    [InlineData("src/AstroCast/app.manifest")]
    [InlineData("src/AstroCast.Cli/app.manifest")]
    public void Manifest_KeepsTheDefaultExecutionLevel(string manifest)
    {
        XDocument document = XDocument.Load(FromRoot(manifest));

        XElement level = document.Descendants(Asm3 + "requestedExecutionLevel").Should().ContainSingle().Which;
        ((string?)level.Attribute("level")).Should().Be("asInvoker");
        ((string?)level.Attribute("uiAccess")).Should().Be("false");
    }

    /// <summary>A path from the repository root, with the platform's separators.</summary>
    /// <param name="relativePath">The path, with forward slashes.</param>
    /// <returns>The full path.</returns>
    private static string FromRoot(string relativePath) =>
        Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>The repository root: the nearest folder above the test's output that holds <c>AstroCast.slnx</c>.</summary>
    /// <returns>The repository root's full path.</returns>
    /// <exception cref="DirectoryNotFoundException">No folder above the output holds <c>AstroCast.slnx</c>.</exception>
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "AstroCast.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No folder above {AppContext.BaseDirectory} holds AstroCast.slnx.");
    }
}
