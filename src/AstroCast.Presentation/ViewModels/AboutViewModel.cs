using System.ComponentModel;
using System.Reflection;
using System.Security;

namespace AstroCast.ViewModels;

/// <summary>
/// The About window's content, the same in both heads: the version, the copyright, the licence notice, where the
/// source code is, and the text of the licence or of the third-party notices.
/// </summary>
/// <remarks>
/// <para>
/// These are the GNU Affero General Public License's "Appropriate Legal Notices" (section 0, required of an
/// interactive user interface by section 5(d)): the copyright, that there is no warranty, that the work may be conveyed
/// under the licence, and how to view the licence. AstroCast is under the AGPL, version 3 or any later version, since
/// 1.3.5.
/// </para>
/// <para>
/// The two texts are read from the folder the view model is given, the application's own folder in both heads, where
/// <c>AstroCast.Core</c> copies <c>LICENSE</c> and <c>THIRD-PARTY-NOTICES.md</c> beside the executable. A file that is
/// missing or cannot be read is shown as a sentence saying where it was looked for; nothing throws.
/// </para>
/// </remarks>
public sealed class AboutViewModel : INotifyPropertyChanged
{
    /// <summary>The licence file's name in the application folder.</summary>
    private const string LicenceFileName = "LICENSE";

    /// <summary>The third-party notices file's name in the application folder.</summary>
    private const string NoticesFileName = "THIRD-PARTY-NOTICES.md";

    /// <summary>The repository that holds AstroCast's source code, the one place it is named.</summary>
    private const string SourceCodeUrl = "https://github.com/sthornhill008/AstroCast";

    /// <summary>The folder the two texts are read from.</summary>
    private readonly string _folder;

    /// <summary>
    /// Initialises the About window's content and shows the licence.
    /// </summary>
    /// <param name="appVersion">The application version, as <see cref="MainViewModel.AppVersion"/> gives it.</param>
    /// <param name="folder">The folder holding <c>LICENSE</c> and <c>THIRD-PARTY-NOTICES.md</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="appVersion"/> or <paramref name="folder"/> is null.</exception>
    public AboutViewModel(string appVersion, string folder)
    {
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(folder);

        _folder = folder;
        VersionLine = $"AstroCast v{appVersion}";
        CopyrightLine = typeof(AboutViewModel).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        ShowLicenceCommand = new RelayCommand(_ => DisplayedText = ReadText(LicenceFileName));
        ShowNoticesCommand = new RelayCommand(_ => DisplayedText = ReadText(NoticesFileName));
        DisplayedText = ReadText(LicenceFileName);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the version line, <c>AstroCast v{version}</c>.</summary>
    public string VersionLine { get; }

    /// <summary>
    /// Gets the copyright line: the assembly's <see cref="AssemblyCopyrightAttribute"/>, from
    /// <c>Directory.Build.props</c>.
    /// </summary>
    public string CopyrightLine { get; }

    /// <summary>
    /// Gets the licence notice: three paragraphs, separated by a blank line — the licence grant, the disclaimer of
    /// warranty, and the third-party components with Swiss Ephemeris's licence option.
    /// </summary>
    public string Notice { get; } =
        "AstroCast is free software: you can redistribute it and/or modify it under the terms of the GNU Affero "
        + "General Public License as published by the Free Software Foundation, either version 3 of the License, or "
        + "(at your option) any later version."
        + "\n\n"
        + "AstroCast is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the "
        + "implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public "
        + "License, shown below, for more details."
        + "\n\n"
        + "AstroCast includes third-party components under their own licences, listed in the third-party notices. "
        + "Swiss Ephemeris is used under its GNU Affero General Public License option.";

    /// <summary>Gets the line naming the source code's repository.</summary>
    public string SourceLine { get; } = $"Source code: {SourceCodeUrl}";

    /// <summary>
    /// Gets the text the window shows: the licence when it opens, then whichever of the licence and the third-party
    /// notices was asked for last.
    /// </summary>
    public string DisplayedText
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayedText)));
            }
        }
    } = string.Empty;

    /// <summary>Gets the command that shows the licence, <c>LICENSE</c>.</summary>
    public RelayCommand ShowLicenceCommand { get; }

    /// <summary>Gets the command that shows the third-party notices, <c>THIRD-PARTY-NOTICES.md</c>.</summary>
    public RelayCommand ShowNoticesCommand { get; }

    /// <summary>
    /// Reads one of the two texts from the folder.
    /// </summary>
    /// <param name="fileName">The file's name.</param>
    /// <returns>The file's text, or <c>{fileName} was not found in {folder}.</c> when it is missing or unreadable.</returns>
    private string ReadText(string fileName)
    {
        try
        {
            return File.ReadAllText(Path.Combine(_folder, fileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or SecurityException or ArgumentException)
        {
            return $"{fileName} was not found in {_folder}.";
        }
    }
}
