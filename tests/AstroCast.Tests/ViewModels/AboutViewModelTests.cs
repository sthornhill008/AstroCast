using System.ComponentModel;

using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="AboutViewModel"/>, the About window's content in both heads: the AGPL's
/// "Appropriate Legal Notices" (section 0, required by section 5(d)) — the copyright, that there is no warranty, that
/// the work may be conveyed under the licence, and how to view the licence.
/// </summary>
/// <remarks>
/// Each test reads its two files from a folder of its own under the temporary path, written with text that is not the
/// real files', so a test can tell which file is shown; the folder is deleted on dispose.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class AboutViewModelTests : IDisposable
{
    /// <summary>The licence file's text in the test folder.</summary>
    private const string LicenceText = "The licence's text.\nSecond line.";

    /// <summary>The notices file's text in the test folder.</summary>
    private const string NoticesText = "The notices' text.\nSecond line.";

    /// <summary>The test's own folder, created empty.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"about-{Guid.NewGuid():N}");

    /// <summary>Creates the test's folder.</summary>
    public AboutViewModelTests() => Directory.CreateDirectory(_folder);

    /// <summary>Deletes the test's folder and everything in it.</summary>
    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Writes both files into the test folder.</summary>
    private void WriteBothFiles()
    {
        File.WriteAllText(Path.Combine(_folder, "LICENSE"), LicenceText);
        File.WriteAllText(Path.Combine(_folder, "THIRD-PARTY-NOTICES.md"), NoticesText);
    }

    /// <summary>The version line names the app version it was built from.</summary>
    [Fact]
    public void VersionLine_NamesTheVersion()
    {
        new AboutViewModel("1.3.5", _folder).VersionLine.Should().Be("AstroCast v1.3.5");
    }

    /// <summary>The copyright line is the assembly's copyright, from <c>Directory.Build.props</c>.</summary>
    [Fact]
    public void CopyrightLine_IsTheAssemblysCopyright()
    {
        new AboutViewModel("1.3.5", _folder).CopyrightLine.Should().Be(
            "Copyright © 2026 Thornhill Computer Software");
    }

    /// <summary>
    /// The notice is the three paragraphs: the licence grant, the disclaimer of warranty and the third-party
    /// components, separated by a blank line.
    /// </summary>
    [Fact]
    public void Notice_IsTheThreeParagraphs()
    {
        new AboutViewModel("1.3.5", _folder).Notice.Should().Be(
            "AstroCast is free software: you can redistribute it and/or modify it under the terms of the GNU Affero "
            + "General Public License as published by the Free Software Foundation, either version 3 of the License, "
            + "or (at your option) any later version."
            + "\n\n"
            + "AstroCast is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the "
            + "implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General "
            + "Public License, shown below, for more details."
            + "\n\n"
            + "AstroCast includes third-party components under their own licences, listed in the third-party notices. "
            + "Swiss Ephemeris is used under its GNU Affero General Public License option.");
    }

    /// <summary>The source line names the repository that holds the source code.</summary>
    [Fact]
    public void SourceLine_NamesTheRepository()
    {
        new AboutViewModel("1.3.5", _folder).SourceLine.Should().Be(
            "Source code: https://github.com/sthornhill008/AstroCast");
    }

    /// <summary>The window opens on the licence.</summary>
    [Fact]
    public void DisplayedText_IsTheLicenceFirst()
    {
        WriteBothFiles();

        new AboutViewModel("1.3.5", _folder).DisplayedText.Should().Be(LicenceText);
    }

    /// <summary>
    /// <see cref="AboutViewModel.ShowNoticesCommand"/> shows the notices and
    /// <see cref="AboutViewModel.ShowLicenceCommand"/> the licence again, each change announced.
    /// </summary>
    [Fact]
    public void Commands_ShowTheNotices_ThenTheLicenceAgain()
    {
        WriteBothFiles();
        AboutViewModel vm = new("1.3.5", _folder);
        List<string?> changed = [];
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowNoticesCommand.CanExecute(null).Should().BeTrue();
        vm.ShowNoticesCommand.Execute(null);
        vm.DisplayedText.Should().Be(NoticesText);

        vm.ShowLicenceCommand.CanExecute(null).Should().BeTrue();
        vm.ShowLicenceCommand.Execute(null);
        vm.DisplayedText.Should().Be(LicenceText);

        changed.Should().Equal(nameof(AboutViewModel.DisplayedText), nameof(AboutViewModel.DisplayedText));
    }

    /// <summary>
    /// A missing file shows <c>{name} was not found in {folder}.</c> in its place, and nothing throws.
    /// </summary>
    [Fact]
    public void MissingFiles_ShowWhereTheyWereLookedFor()
    {
        AboutViewModel vm = new("1.3.5", _folder);

        vm.DisplayedText.Should().Be($"LICENSE was not found in {_folder}.");

        vm.ShowNoticesCommand.Execute(null);
        vm.DisplayedText.Should().Be($"THIRD-PARTY-NOTICES.md was not found in {_folder}.");
        vm.ShowNoticesCommand.LastException.Should().BeNull();
    }

    /// <summary>
    /// A file that cannot be read — here a folder by the licence's name — shows the same text as a missing one, and
    /// nothing throws.
    /// </summary>
    [Fact]
    public void UnreadableFile_ShowsTheSameText()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "LICENSE"));

        AboutViewModel vm = new("1.3.5", _folder);

        vm.DisplayedText.Should().Be($"LICENSE was not found in {_folder}.");
    }

    /// <summary>
    /// <see cref="MainViewModel.OpenAboutWindowCommand"/> is always enabled and invokes
    /// <see cref="MainViewModel.OpenAboutWindowAction"/>, the seam each head fills to open its About window.
    /// </summary>
    [Fact]
    public void OpenAboutWindowCommand_InvokesTheAction()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        int calls = 0;
        vm.OpenAboutWindowAction = () => calls++;

        vm.OpenAboutWindowCommand.CanExecute(null).Should().BeTrue();
        vm.OpenAboutWindowCommand.Execute(null);

        calls.Should().Be(1);
    }
}
