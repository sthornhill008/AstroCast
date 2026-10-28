using System.Text;

using AstroCast.Core.Interop;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Integration tests that check each <see cref="FixedStarCatalog"/> entry against the star
/// the Swiss Ephemeris actually resolves its <see cref="FixedStarEntry.SwissEphName"/> to.
/// <c>swe_fixstar2_ut</c> matches names loosely and writes the record it found back into
/// the name buffer as "traditional name,nomenclature"; the nomenclature names the star.
/// Uses the shared "Ephemeris" collection fixture, which sets the ephemeris path.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class FixedStarCatalogResolutionTests
{
    /// <summary>
    /// The intended star of every catalogue entry, in the nomenclature of <c>sefstars.txt</c>
    /// (Greek letter, component after a hyphen, IAU constellation). Typed from the Yale Bright
    /// Star Catalogue, 5th Revised Ed. (Hoffleit &amp; Warren 1991; CDS V/50), by HR number;
    /// each star was matched to the catalogue through its Hipparcos number (ESA 1997, CDS I/239)
    /// and that entry's HD number. The star file names alpha Centauri and alpha Herculis
    /// without the component that the catalogue gives (alpha-1 in both). Praesepe is a
    /// cluster, not in the catalogue: Messier 44.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> IntendedStar = new Dictionary<string, string>
    {
        ["Difda"] = "beCet",           // HR 188, beta Ceti
        ["Mirach"] = "beAnd",          // HR 337
        ["Sharatan"] = "beAri",        // HR 553
        ["Hamal"] = "alAri",           // HR 617
        ["Almach"] = "ga-1And",        // HR 603, gamma-1 Andromedae
        ["Algol"] = "bePer",           // HR 936
        ["Alcyone"] = "etTau",         // HR 1165
        ["Hyades"] = "gaTau",          // HR 1346, Prima Hyadum
        ["Aldebaran"] = "alTau",       // HR 1457
        ["Rigel"] = "beOri",           // HR 1713
        ["Bellatrix"] = "gaOri",       // HR 1790
        ["Capella"] = "alAur",         // HR 1708
        ["Alnilam"] = "epOri",         // HR 1903
        ["Polaris"] = "alUMi",         // HR 424
        ["Betelgeuse"] = "alOri",      // HR 2061
        ["Sirius"] = "alCMa",          // HR 2491
        ["Canopus"] = "alCar",         // HR 2326
        ["Castor"] = "alGem",          // HR 2891
        ["Pollux"] = "beGem",          // HR 2990
        ["Procyon"] = "alCMi",         // HR 2943
        ["Praesepe"] = "M44",          // Messier 44
        ["North Asellus"] = "gaCnc",   // HR 3449
        ["South Asellus"] = "deCnc",   // HR 3461
        ["Alphard"] = "alHya",         // HR 3748
        ["Regulus"] = "alLeo",         // HR 3982
        ["Zosma"] = "deLeo",           // HR 4357
        ["Denebola"] = "beLeo",        // HR 4534
        ["Vindemiatrix"] = "epVir",    // HR 4932
        ["Algorab"] = "deCrv",         // HR 4757
        ["Spica"] = "alVir",           // HR 5056
        ["Arcturus"] = "alBoo",        // HR 5340
        ["Alphecca"] = "alCrB",        // HR 5793
        ["South Scale"] = "al-2Lib",   // HR 5531, alpha-2 Librae
        ["North Scale"] = "beLib",     // HR 5685
        ["Unukalhai"] = "alSer",       // HR 5854
        ["Toliman"] = "alCen",         // HR 5459, alpha-1 Centauri
        ["Antares"] = "alSco",         // HR 6134
        ["Ras Algethi"] = "alHer",     // HR 6406, alpha-1 Herculis
        ["Sabik"] = "etOph",           // HR 6378
        ["Ras Alhague"] = "alOph",     // HR 6556
        ["Lesath"] = "upSco",          // HR 6508
        ["Etamin"] = "gaDra",          // HR 6705
        ["Vega"] = "alLyr",            // HR 7001
        ["Altair"] = "alAql",          // HR 7557
        ["Dorsum"] = "thCap",          // HR 8075
        ["Nashira"] = "gaCap",         // HR 8278
        ["Deneb Algedi"] = "deCap",    // HR 8322
        ["Sadalsuud"] = "beAqr",       // HR 8232
        ["Sadalmelek"] = "alAqr",      // HR 8414
        ["Fomalhaut"] = "alPsA",       // HR 8728
        ["Deneb Adige"] = "alCyg",     // HR 7924
        ["Skat"] = "deAqr",            // HR 8709
        ["Achernar"] = "alEri",        // HR 472
        ["Markab"] = "alPeg",          // HR 8781
        ["Scheat"] = "bePeg",          // HR 8775
    };

    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    public FixedStarCatalogResolutionTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    [Fact]
    public void SwissEphName_Should_ResolveToTheIntendedStar_ForEveryCatalogEntry()
    {
        // Arrange
        IntendedStar.Keys.Should().BeEquivalentTo(FixedStarCatalog.Stars.Select(s => s.DisplayName),
            "the table of intended stars covers the whole catalogue");
        List<string> wrong = [];

        // Act — the record the library writes back names the star it actually computed
        foreach (FixedStarEntry star in FixedStarCatalog.Stars)
        {
            StringBuilder record = new(star.SwissEphName, 64);
            StringBuilder error = new(SwissEphInterop.MaxErrorLength);
            int flags = SwissEphInterop.swe_fixstar2_ut(
                record, 2451545.0, SwissEphInterop.SEFLG_SWIEPH, new double[6], error);

            string resolved = record.ToString();
            string nomenclature = resolved[(resolved.IndexOf(',') + 1)..];
            if (flags < 0 || nomenclature != IntendedStar[star.DisplayName])
            {
                wrong.Add($"{star.DisplayName}: '{star.SwissEphName}' resolves to '{resolved}' {error}".TrimEnd());
            }
        }

        // Assert
        wrong.Should().BeEmpty("every catalogue name must resolve to the star the catalogue intends");
    }

    [Fact]
    public void Difda_Should_BeComputedAtBetaCeti_AtJ2000()
    {
        // Arrange — beta Ceti (HIP 3419) at JD 2451545.0 UT, apparent of date, from the Hipparcos
        // position through Skyfield and DE440: 2.578790 deg, 2°34.7′ Aries. Iota Ceti is at 0°54.7′.
        const double expected = 2.0 + 34.7 / 60.0;
        FixedStarEntry difda = FixedStarCatalog.Stars.Single(s => s.DisplayName == "Difda");

        // Act
        double longitude = _ephemeris.CalculateFixedStarLongitude(difda.SwissEphName, 2451545.0);

        // Assert
        longitude.Should().BeApproximately(expected, 1.0 / 60.0,
            "Difda is beta Ceti, at 2°34.7′ Aries at J2000");
    }
}
