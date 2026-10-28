using System.Text;

using AstroCast.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins where the base map's key comes from (rule 2 of the map-tiles branch): the override
/// file <c>carto-key.txt</c> in the app's data directory, then the built-in key, then none;
/// content trimmed, and empty or whitespace-only content counting as absent. The key never
/// appears in a <see cref="object.ToString"/> or an exception message.
/// </summary>
/// <remarks>
/// Each test makes its own data directory and passes its own built-in key, so nothing here
/// depends on whether a key file exists on the machine. The keys are made up.
/// </remarks>
public sealed class BaseMapKeyLoaderTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "astrocast-basemapkey-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the test's own empty data directory.</summary>
    public BaseMapKeyLoaderTests()
    {
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <summary>Removes the test's data directory.</summary>
    public void Dispose()
    {
        Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>An override file wins over the built-in key.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_OverrideFilePresent_TakesItBeforeTheBuiltInKey()
    {
        WriteOverride("override-key");

        BaseMapKey key = Required(BaseMapKeyLoader.Load(_dataDirectory, "built-in-key"));

        key.Source.Should().Be(BaseMapKeySource.OverrideFile);
        key.Value.Should().Be("override-key");
    }

    /// <summary>With no override file, the built-in key.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_NoOverrideFile_TakesTheBuiltInKey()
    {
        BaseMapKey key = Required(BaseMapKeyLoader.Load(_dataDirectory, "built-in-key"));

        key.Source.Should().Be(BaseMapKeySource.BuiltIn);
        key.Value.Should().Be("built-in-key");
    }

    /// <summary>With no data directory, the built-in key.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_NoDataDirectory_TakesTheBuiltInKey()
    {
        BaseMapKey key = Required(BaseMapKeyLoader.Load(null, "built-in-key"));

        key.Source.Should().Be(BaseMapKeySource.BuiltIn);
    }

    /// <summary>With neither source, no key.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_NoSource_IsNone()
    {
        BaseMapKeyLoader.Load(_dataDirectory, null).Should().BeNull();
        BaseMapKeyLoader.Load(null, null).Should().BeNull();
    }

    /// <summary>An empty or whitespace-only override file counts as absent: the built-in key is used.</summary>
    /// <param name="content">The override file's content.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData(" \t\r\n ")]
    public void Load_EmptyOrWhitespaceOverrideFile_CountsAsAbsent(string content)
    {
        WriteOverride(content);

        BaseMapKey key = Required(BaseMapKeyLoader.Load(_dataDirectory, "built-in-key"));

        key.Source.Should().Be(BaseMapKeySource.BuiltIn);
    }

    /// <summary>An empty or whitespace-only built-in key counts as absent: no key.</summary>
    /// <param name="content">The built-in key's content.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\r\n")]
    public void Load_EmptyOrWhitespaceBuiltInKey_CountsAsAbsent(string content)
    {
        BaseMapKeyLoader.Load(_dataDirectory, content).Should().BeNull();
    }

    /// <summary>Both sources are trimmed, a byte-order mark included.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_TrimsTheKey()
    {
        Required(BaseMapKeyLoader.Load(_dataDirectory, "\tbuilt-in-key\n")).Value.Should().Be("built-in-key");

        File.WriteAllText(Path.Combine(_dataDirectory, "carto-key.txt"), "  override-key \r\n", new UTF8Encoding(true));
        Required(BaseMapKeyLoader.Load(_dataDirectory, null)).Value.Should().Be("override-key");
    }

    /// <summary>
    /// The key appears in no <see cref="object.ToString"/> and in no message of an exception the
    /// URL builder raises.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Key_AppearsInNoToStringOrExceptionMessage()
    {
        const string secret = "secret-test-key-7f3a";
        WriteOverride(secret);
        BaseMapKey key = Required(BaseMapKeyLoader.Load(_dataDirectory, null));

        key.ToString().Should().NotContain(secret).And.Be("CARTO key (OverrideFile)");
        $"{key}".Should().NotContain(secret);

        List<Exception> thrown = [];
        foreach ((int z, int x, int y) in new[] { (-1, 0, 0), (31, 0, 0), (2, 4, 0), (2, 0, 4) })
        {
            try
            {
                BaseMapSettings.TileUrl(z, x, y, key);
            }
            catch (ArgumentOutOfRangeException e)
            {
                thrown.Add(e);
            }
        }

        thrown.Should().HaveCount(4);
        thrown.Should().OnlyContain(e => !e.Message.Contains(secret, StringComparison.Ordinal)
            && !e.ToString().Contains(secret, StringComparison.Ordinal));
    }

    /// <summary>An assembly that carries no key resource gives no built-in key.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ReadBuiltInKey_FromAnAssemblyWithoutTheResource_IsNone()
    {
        BaseMapKeyLoader.ReadBuiltInKey(typeof(object).Assembly).Should().BeNull();
    }

    /// <summary>The key the loader returned, failing the test when it returned none.</summary>
    private static BaseMapKey Required(BaseMapKey? key) =>
        key ?? throw new Xunit.Sdk.XunitException("The loader returned no key.");

    /// <summary>Writes the override file in the test's data directory.</summary>
    private void WriteOverride(string content) =>
        File.WriteAllText(Path.Combine(_dataDirectory, BaseMapKeyLoader.OverrideFileName), content);
}
