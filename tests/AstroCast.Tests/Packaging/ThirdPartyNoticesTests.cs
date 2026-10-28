using System.Xml.Linq;

using FluentAssertions;

namespace AstroCast.Tests.Packaging;

/// <summary>
/// The third-party notices ship beside the application and list every package the shipped projects reference
/// directly.
/// </summary>
/// <remarks>
/// <c>THIRD-PARTY-NOTICES.md</c> lives at the repository root, and <c>AstroCast.Core</c> copies it into every build and
/// publish output that references it, as it copies <c>ephe/</c>, so both installers carry it. Until 1.3.3 AstroCast
/// shipped third-party code and data with no notices file. These tests check the direct references only; a package
/// reached transitively has its row from the inventory of the heads' <c>.deps.json</c> files.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ThirdPartyNoticesTests
{
    /// <summary>The notices file's name, at the repository root and in every output.</summary>
    private const string FileName = "THIRD-PARTY-NOTICES.md";

    /// <summary>The projects whose package references ship: Core, Presentation and the two heads.</summary>
    private static readonly IReadOnlyList<string> ShippedProjects =
    [
        Path.Combine("src", "AstroCast.Core", "AstroCast.Core.csproj"),
        Path.Combine("src", "AstroCast.Presentation", "AstroCast.Presentation.csproj"),
        Path.Combine("src", "AstroCast.Avalonia", "AstroCast.Avalonia.csproj"),
        Path.Combine("src", "AstroCast", "AstroCast.csproj"),
    ];

    /// <summary>
    /// <c>THIRD-PARTY-NOTICES.md</c> is in the test's own output folder, copied there through its reference to
    /// <c>AstroCast.Core</c> as it is into each head's output.
    /// </summary>
    [Fact]
    public void Notices_AreCopiedToTheOutput()
    {
        File.Exists(Path.Combine(AppContext.BaseDirectory, FileName)).Should().BeTrue(
            $"AstroCast.Core copies {FileName} into every output that references it");
    }

    /// <summary>
    /// Every <c>PackageReference</c> in the Core, Presentation, Avalonia-head and WPF-head projects, other than one
    /// with a <c>PrivateAssets</c> element or attribute (the Debug-only <c>AvaloniaUI.DiagnosticsSupport</c>), has a
    /// row in the repository's notices file that starts <c>| `{Include}` | {Version} |</c>.
    /// </summary>
    [Fact]
    public void Notices_ListEveryShippedPackageReference()
    {
        string root = RepositoryRoot();
        string[] lines = File.ReadAllLines(Path.Combine(root, FileName));
        List<(string Include, string Version)> references =
            [.. ShippedProjects.SelectMany(project => PackageReferences(Path.Combine(root, project))).Distinct()];

        references.Should().NotBeEmpty();
        foreach ((string include, string version) in references)
        {
            string row = $"| `{include}` | {version} |";
            lines.Should().Contain(
                line => line.StartsWith(row, StringComparison.Ordinal),
                $"{include} {version} ships, so {FileName} has a row starting \"{row}\"");
        }
    }

    /// <summary>
    /// Both heads exclude the Svg packages' runtime assets (branch SvgExclude, 1.7.1): Mapsui's raster tile layer
    /// never touches the SVG path (the probe of 2026-10-24: tiles, lines and markers draw with every Svg DLL
    /// deleted), so each head pins <c>Svg.Custom</c>, <c>Svg.Model</c> and <c>Svg.Skia</c> at the version Mapsui
    /// resolves, with <c>ExcludeAssets="runtime"</c>, and ships none of them; the notices keep their rows because
    /// the packages stay in the dependency graph.
    /// </summary>
    [Fact]
    public void SvgPackages_ExcludeRuntimeAssetsInBothHeads()
    {
        string root = RepositoryRoot();
        string[] heads =
        [
            Path.Combine("src", "AstroCast.Avalonia", "AstroCast.Avalonia.csproj"),
            Path.Combine("src", "AstroCast", "AstroCast.csproj"),
        ];

        foreach (string head in heads)
        {
            XDocument project = XDocument.Load(Path.Combine(root, head));
            foreach (string package in new[] { "Svg.Custom", "Svg.Model", "Svg.Skia" })
            {
                XElement? reference = project.Descendants("PackageReference")
                    .SingleOrDefault(r => (string?)r.Attribute("Include") == package);
                reference.Should().NotBeNull($"{head} pins {package} to exclude its runtime assets");
                ((string?)reference?.Attribute("Version")).Should().Be("3.2.1",
                    $"{head}'s {package} reference pins the version Mapsui resolves transitively");
                ((string?)reference?.Attribute("ExcludeAssets")).Should().Be("runtime",
                    $"{head}'s {package} reference keeps the assembly out of the build output");
            }
        }
    }

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

    /// <summary>
    /// A project's package references, each as its <c>Include</c> and <c>Version</c>, leaving out any with a
    /// <c>PrivateAssets</c> element or attribute.
    /// </summary>
    /// <param name="project">The project file's path.</param>
    /// <returns>The references, in the project's order.</returns>
    /// <exception cref="InvalidDataException">A reference has no <c>Include</c> or no <c>Version</c>.</exception>
    private static IEnumerable<(string Include, string Version)> PackageReferences(string project) =>
        XDocument.Load(project)
            .Descendants("PackageReference")
            .Where(reference => reference.Attribute("PrivateAssets") is null && reference.Element("PrivateAssets") is null)
            .Select(reference => (
                (string?)reference.Attribute("Include")
                    ?? throw new InvalidDataException($"A PackageReference in {project} has no Include."),
                (string?)reference.Attribute("Version") ?? (string?)reference.Element("Version")
                    ?? throw new InvalidDataException($"A PackageReference in {project} has no Version.")));
}
