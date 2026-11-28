using AstroCast.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// The no-key message says where the key file goes (branch WindowsFit, 1.7.2; finding A7): it is built once, in
/// Presentation, from the path the loader itself reads, so both heads print the same words. The loader reads
/// from two places, the override file in the app's data folder and the key built into the app at build time;
/// the message names the first, the only place a user's own key belongs.
/// </summary>
/// <remarks>
/// Each test makes its own data directory. The one key written here is made up, and no test reads a key file
/// that was on the machine.
/// </remarks>
public sealed class BaseMapNoKeyMessageTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "astrocast-nokeymessage-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the test's own empty data directory.</summary>
    public BaseMapNoKeyMessageTests()
    {
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <summary>Removes the test's data directory.</summary>
    public void Dispose()
    {
        Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>
    /// The loader's own path for a data folder is the folder's full path and the override file's name, whether
    /// or not the folder is given with a closing separator (the WPF head's is), and there is none without a
    /// folder.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OverrideFilePath_IsTheFileTheLoaderReads()
    {
        string expected = Path.Combine(Path.GetFullPath(_dataDirectory), "carto-key.txt");

        BaseMapKeyLoader.OverrideFilePath(_dataDirectory).Should().Be(expected);
        BaseMapKeyLoader.OverrideFilePath(_dataDirectory + Path.DirectorySeparatorChar).Should().Be(expected);
        BaseMapKeyLoader.OverrideFilePath(null).Should().BeNull();
        BaseMapKeyLoader.OverrideFilePath("   ").Should().BeNull();
    }

    /// <summary>
    /// The message for a data folder is the no-key line, the instruction and the loader's own path for that
    /// folder; and a key put at the path the message names is the key the loader then finds.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NoKeyMessageFor_NamesTheFileTheLoaderReads()
    {
        const string instruction = "Base map unavailable: no CARTO key. Put your key in a text file named carto-key.txt at: ";

        string message = BaseMapSettings.NoKeyMessageFor(_dataDirectory);

        message.Should().Be(instruction + BaseMapKeyLoader.OverrideFilePath(_dataDirectory));
        message.Should().StartWith(BaseMapSettings.NoKeyMessage + ".");

        BaseMapKeyLoader.Load(_dataDirectory, null).Should().BeNull("the folder holds no key yet");
        string named = message[instruction.Length..];
        File.WriteAllText(named, "made-up-key-for-this-test");
        BaseMapKey? found = BaseMapKeyLoader.Load(_dataDirectory, null);
        found.Should().NotBeNull("the message names the file the loader reads");
        found?.Source.Should().Be(BaseMapKeySource.OverrideFile);
    }

    /// <summary>Building the message reads no key: with a key file in the folder its content is not in the message.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NoKeyMessageFor_NeverCarriesAKey()
    {
        const string madeUp = "made-up-key-7f3a-not-printed";
        File.WriteAllText(Path.Combine(_dataDirectory, BaseMapKeyLoader.OverrideFileName), madeUp);

        BaseMapSettings.NoKeyMessageFor(_dataDirectory).Should().NotContain(madeUp);
    }

    /// <summary>Without a data folder there is no place to name, and the message is the no-key line alone.</summary>
    /// <param name="dataDirectory">The data folder given.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoKeyMessageFor_WithoutADataFolder_IsTheNoKeyLineAlone(string? dataDirectory)
    {
        BaseMapSettings.NoKeyMessageFor(dataDirectory).Should().Be("Base map unavailable: no CARTO key");
    }

    /// <summary>
    /// Both heads' map windows take their no-key notice from the one builder, handing it the data folder they
    /// were given, and neither prints the bare constant. The WPF head has no test project of its own, so both
    /// are read from their source.
    /// </summary>
    /// <param name="relativePath">The map window's source file, from the repository root.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("src/AstroCast/AcgMapWindow.xaml.cs")]
    [InlineData("src/AstroCast.Avalonia/Views/AcgMapWindow.cs")]
    public void BothHeads_BuildTheirNoticeFromTheOneBuilder(string relativePath)
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        source.Should().Contain("Text = BaseMapSettings.NoKeyMessageFor(dataDirectory),");
        source.Should().Contain("CreateNoKeyNotice(dataDirectory)");
        source.Should().NotContain("Text = BaseMapSettings.NoKeyMessage,");
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
}
