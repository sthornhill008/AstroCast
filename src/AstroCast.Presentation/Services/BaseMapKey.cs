namespace AstroCast.Services;

/// <summary>
/// The CARTO key the map's base-map tiles are requested with, and where it came from.
/// <see cref="BaseMapKeyLoader"/> makes one; <see cref="BaseMapSettings"/> puts it in the
/// tile URL.
/// </summary>
/// <remarks>
/// The key is a secret. Its value is reachable only inside Presentation and its tests, and
/// <see cref="ToString"/> names the source, never the value, so the key cannot reach a log
/// line or a message by being formatted.
/// </remarks>
public sealed class BaseMapKey
{
    /// <summary>
    /// Initialises a key from its trimmed, non-empty value.
    /// </summary>
    /// <param name="value">The key, trimmed and not empty.</param>
    /// <param name="source">Where the key came from.</param>
    internal BaseMapKey(string value, BaseMapKeySource source)
    {
        Value = value;
        Source = source;
    }

    /// <summary>Gets where the key came from.</summary>
    public BaseMapKeySource Source { get; }

    /// <summary>Gets the key itself. Never log or display it.</summary>
    internal string Value { get; }

    /// <summary>
    /// Names the key's source without its value.
    /// </summary>
    /// <returns>A description such as "CARTO key (BuiltIn)".</returns>
    public override string ToString() => $"CARTO key ({Source})";
}

/// <summary>
/// Where a <see cref="BaseMapKey"/> came from, in the order <see cref="BaseMapKeyLoader"/>
/// tries them.
/// </summary>
public enum BaseMapKeySource
{
    /// <summary>
    /// The override file, <see cref="BaseMapKeyLoader.OverrideFileName"/> in the app's data
    /// directory.
    /// </summary>
    OverrideFile,

    /// <summary>The key built into the app.</summary>
    BuiltIn,
}
