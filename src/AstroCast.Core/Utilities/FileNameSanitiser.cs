namespace AstroCast.Core.Utilities;

/// <summary>
/// Provides file name sanitisation to prevent path traversal and invalid
/// characters in user-supplied file names used for chart exports.
/// </summary>
public static class FileNameSanitiser
{
    /// <summary>
    /// The printable characters Windows refuses in a file name, removed on every platform
    /// with the control characters 0 to 31.
    /// </summary>
    private const string ReservedCharacters = "<>:\"/\\|?*";

    /// <summary>
    /// Sanitises a string for safe use as a file name by removing path separators,
    /// invalid file name characters, and directory traversal sequences.
    /// </summary>
    /// <param name="fileName">The raw file name to sanitise.</param>
    /// <returns>A sanitised file name safe for use in file paths.</returns>
    /// <remarks>
    /// The invalid characters are the same on every platform: <c>&lt; &gt; : " / \ | ? *</c> and the
    /// control characters 0 to 31, the set Windows refuses. They are not
    /// <see cref="Path.GetInvalidFileNameChars"/>, which on macOS and Linux is only NUL and '/',
    /// so a name made there could not be used on Windows.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="fileName"/> is null, empty, or contains only
    /// invalid characters resulting in an empty sanitised output.
    /// </exception>
    public static string Sanitise(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

        // Remove directory traversal sequences
        string sanitised = fileName
            .Replace("..", string.Empty)
            .Replace("/", string.Empty)
            .Replace("\\", string.Empty);

        // Remove all invalid file name characters, the same on every platform
        sanitised = string.Concat(sanitised.Where(c => c >= ' ' && !ReservedCharacters.Contains(c)));

        // Replace spaces with underscores for cleaner file names
        sanitised = sanitised.Replace(' ', '_');

        // Trim and validate
        sanitised = sanitised.Trim();
        if (string.IsNullOrWhiteSpace(sanitised))
            throw new ArgumentException(
                "File name contains only invalid characters.", nameof(fileName));

        return sanitised;
    }
}
