using System.Reflection;

namespace AstroCast.Services;

/// <summary>
/// Finds the CARTO key for the map's base-map tiles: the override file in the app's data
/// directory first, then the key built into the app, then none.
/// </summary>
/// <remarks>
/// <para>
/// <b>Override file.</b> <see cref="OverrideFileName"/> in the folder the database lives
/// in, by each head's own rule (the Avalonia head's <c>AppPaths.DataDirectory</c>; the WPF
/// head's folder beside the executable).
/// </para>
/// <para>
/// <b>Built-in key.</b> Read at build time from the file the <c>CartoKeyFile</c> MSBuild
/// property names (by default <c>.astrocast/carto-key.txt</c> under the user's profile
/// folder) and embedded in this assembly only when that file exists; see
/// <c>AstroCast.Presentation.csproj</c>. A build without the file has no built-in key.
/// </para>
/// <para>
/// Content is trimmed, and empty or whitespace-only content counts as absent. An override
/// file that exists but cannot be read also counts as absent, so the next source is tried.
/// The loader takes its sources as parameters so that tests decide them; nothing here
/// depends on whether a key file exists on the machine running it.
/// </para>
/// </remarks>
public static class BaseMapKeyLoader
{
    /// <summary>The override file's name, in the app's data directory.</summary>
    public const string OverrideFileName = "carto-key.txt";

    /// <summary>The manifest-resource name the build gives the built-in key.</summary>
    internal const string BuiltInResourceName = "AstroCast.Presentation.CartoKey";

    /// <summary>
    /// Finds the key: the override file in <paramref name="dataDirectory"/>, then
    /// <paramref name="builtInKey"/>, then none.
    /// </summary>
    /// <param name="dataDirectory">
    /// The app's data directory, where an override file may be; null for none.
    /// </param>
    /// <param name="builtInKey">
    /// The built-in key's content (<see cref="ReadBuiltInKey()"/> in the app); null for none.
    /// </param>
    /// <returns>The key, or null when no source has one.</returns>
    public static BaseMapKey? Load(string? dataDirectory, string? builtInKey)
    {
        string? overrideKey = ReadOverrideFile(dataDirectory);
        if (overrideKey is not null)
        {
            return new BaseMapKey(overrideKey, BaseMapKeySource.OverrideFile);
        }

        string? builtIn = Normalise(builtInKey);
        return builtIn is null ? null : new BaseMapKey(builtIn, BaseMapKeySource.BuiltIn);
    }

    /// <summary>
    /// Reads the key built into this assembly.
    /// </summary>
    /// <returns>The built-in key's content, or null when the build embedded none.</returns>
    public static string? ReadBuiltInKey() => ReadBuiltInKey(typeof(BaseMapKeyLoader).Assembly);

    /// <summary>
    /// Reads the built-in key resource from <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly that may carry the resource.</param>
    /// <returns>The resource's content, or null when the assembly has none.</returns>
    internal static string? ReadBuiltInKey(Assembly assembly)
    {
        using Stream? stream = assembly.GetManifestResourceStream(BuiltInResourceName);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the override file in <paramref name="dataDirectory"/>, if there is one.
    /// </summary>
    /// <returns>The trimmed key, or null when the file is absent, empty or unreadable.</returns>
    private static string? ReadOverrideFile(string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return null;
        }

        string path = Path.Combine(dataDirectory, OverrideFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Normalise(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Trims a key's content; empty or whitespace-only content is no key.</summary>
    private static string? Normalise(string? content)
    {
        string? trimmed = content?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
