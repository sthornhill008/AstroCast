using FluentAssertions;

namespace AstroCast.Tests.Packaging;

/// <summary>
/// AstroCast's own licence, the GNU Affero General Public License version 3, ships beside the application.
/// </summary>
/// <remarks>
/// <c>LICENSE</c> lives at the repository root, the GNU's text byte for byte, and <c>AstroCast.Core</c> copies it into
/// every build and publish output that references it, as it copies <c>THIRD-PARTY-NOTICES.md</c>, so both installers
/// carry it and the About window can show it. AstroCast is under the AGPL, version 3 or any later version, since 1.3.5.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class LicenceTests
{
    /// <summary>The licence file's name, at the repository root and in every output.</summary>
    private const string FileName = "LICENSE";

    /// <summary>
    /// <c>LICENSE</c> is in the test's own output folder, copied there through its reference to
    /// <c>AstroCast.Core</c> as it is into each head's output.
    /// </summary>
    [Fact]
    public void Licence_IsCopiedToTheOutput()
    {
        File.Exists(Path.Combine(AppContext.BaseDirectory, FileName)).Should().BeTrue(
            $"AstroCast.Core copies {FileName} into every output that references it");
    }

    /// <summary>
    /// The copied <c>LICENSE</c> opens with the GNU Affero General Public License's title and version, each line
    /// trimmed of the text's centring.
    /// </summary>
    [Fact]
    public void Licence_OpensWithTheAgplTitleAndVersion()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FileName);
        File.Exists(path).Should().BeTrue($"AstroCast.Core copies {FileName} into every output that references it");

        string[] firstTwo = [.. File.ReadLines(path).Take(2).Select(line => line.Trim())];

        firstTwo.Should().Equal("GNU AFFERO GENERAL PUBLIC LICENSE", "Version 3, 19 November 2007");
    }
}
