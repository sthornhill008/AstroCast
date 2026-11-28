using Serilog;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

// ──────────────────────────────────────────────────────────────
// AstroCast CLI — Smoke Test
// Calculates a complete chart at J2000.0 epoch for New York City
// using the ChartEngine, then displays all results and generates
// JSON, Markdown, and PDF output files.
// ──────────────────────────────────────────────────────────────

// Enable UTF-8 output for box-drawing characters and special symbols
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Configure Serilog for console output
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console()
    .CreateLogger();

// Resolve ephemeris path relative to the executable
string exeDir = AppContext.BaseDirectory;
string ephePath = Path.Combine(exeDir, "ephe");

if (!Directory.Exists(ephePath))
{
    Console.WriteLine($"ERROR: Ephemeris data directory not found: {ephePath}");
    return 1;
}

Console.WriteLine("=====================================================");
Console.WriteLine("  AstroCast -- Complete Chart Engine Smoke Test");
Console.WriteLine("=====================================================");
Console.WriteLine();

// Create services and chart engine
using EphemerisService ephemeris = new(ephePath, Log.Logger);

// The ephemeris files are read once, now (branch EphePath, 1.7.2; finding N4): one position
// that needs them, discarded. When they cannot be read the shared check's plain message is
// printed as this program prints its other start-up problems, and the run stops.
if (EphemerisFilesCheck.Run(ephemeris, ephePath, Log.Logger) is string unreadable)
{
    Console.WriteLine($"ERROR: {unreadable}");
    return 1;
}

Console.WriteLine($"Ephemeris files read from: {ephePath}");
AspectService aspectService = new(ephemeris, Log.Logger);
MidpointService midpointService = new(Log.Logger);
DignityService dignityService = new();
MoonConditionService moonConditionService = new();
SectService sectService = new();
ChartContextService chartContextService = new(dignityService, sectService);
FixedStarService fixedStarService = new(ephemeris, Log.Logger);
DeclinationAspectService declinationAspectService = new();
PlanetConditionService planetConditionService = new();
MoonMansionService moonMansionService = new();
PlanetaryHourCalculator planetaryHourCalculator = new(ephemeris);
ArabicPartService arabicPartService = new(dignityService);
AntisciaService antisciaService = new();
StationService stationService = new();
ParanService paranService = new(ephemeris, Log.Logger);
ChartEngine chartEngine = new(ephemeris, aspectService, midpointService, dignityService, moonConditionService, chartContextService, fixedStarService, declinationAspectService, planetConditionService, moonMansionService, planetaryHourCalculator, arabicPartService, antisciaService, stationService, paranService, Log.Logger);
RelocationService relocationService = new(chartEngine, Log.Logger);

// Display Swiss Ephemeris version
Console.WriteLine($"Swiss Ephemeris version: {ephemeris.Version}");
Console.WriteLine();

// ── Chart Input ─────────────────────────────────────────────

// J2000.0 epoch is 2000-01-01 12:00 UT = 2000-01-01 07:00 EST
ChartInput chartInput = new()
{
    Name = "J2000.0 Epoch -- New York City",
    LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
    TimeZone = "America/New_York",
    Latitude = 40.7128,
    Longitude = -74.006,
    LocationName = "New York City",
    EventType = "Event",
    EventDescription = "J2000.0 epoch smoke test",
    Gender = Gender.Unspecified,
    SourceNotes = "Astronomical epoch",
};

// ── Calculate Complete Chart ────────────────────────────────

ChartResult result = chartEngine.Calculate(chartInput);

// ── Console Display ─────────────────────────────────────────

Console.WriteLine($"Chart: {result.Input.Name}");
Console.WriteLine($"Date:  {result.Input.LocalDateTime:yyyy-MM-dd HH:mm:ss} ({result.Input.TimeZone})");
Console.WriteLine($"UTC:   {CalendarDateFormatter.DateTime(result.UtcDateTime)}");
Console.WriteLine($"JD:    {result.JulianDay:F6}");
Console.WriteLine($"Location: {result.Input.LocationName} ({result.Input.Latitude:F4}, {result.Input.Longitude:F4})");
Console.WriteLine();

// Positions
Console.WriteLine("Body          Position              House  Dir   Speed");
Console.WriteLine("------------  --------------------  -----  ---   ----------");

foreach (PlanetPosition position in result.Positions)
{
    string houseStr = position.House.HasValue ? $"{position.House,3}" : "  -";
    Console.WriteLine(
        $"{position.Name,-12}  {PositionFormatter.FormatPosition(position),-20}  {houseStr}    {position.DirectionCode,1}    {position.SpeedInLongitude,9:F3}/day");
}

Console.WriteLine();
Console.WriteLine($"Total bodies: {result.Positions.Count}");

// House Cusps
Console.WriteLine();
Console.WriteLine($"House Cusps ({result.HouseSystemName})");
Console.WriteLine("----------------------");

for (int i = 0; i < result.Houses.Cusps.Count; i++)
{
    string label = (i + 1) switch
    {
        1 => "  (ASC)",
        10 => "  (MC)",
        _ => ""
    };

    Console.WriteLine(
        $"House {i + 1,2}: {PositionFormatter.FormatLongitude(result.Houses.Cusps[i])}{label}");
}

// Angles
Console.WriteLine();
Console.WriteLine("Angles");
Console.WriteLine("------");
Console.WriteLine($"Ascendant:   {PositionFormatter.FormatLongitude(result.Houses.Ascendant)}");
Console.WriteLine($"Midheaven:   {PositionFormatter.FormatLongitude(result.Houses.Midheaven)}");
Console.WriteLine($"Descendant:  {PositionFormatter.FormatLongitude(result.Houses.Descendant)}");
Console.WriteLine($"IC:          {PositionFormatter.FormatLongitude(result.Houses.ImumCoeli)}");
Console.WriteLine($"Vertex:      {PositionFormatter.FormatLongitude(result.Houses.Vertex)}");

// Aspects
int majorCount = result.Aspects.Count(a => a.Aspect.Category == AspectCategory.Major);
int ebertinCount = result.Aspects.Count - majorCount;

Console.WriteLine();
Console.WriteLine("Aspects");
Console.WriteLine("-------");

foreach (AspectResult aspect in result.Aspects)
{
    Console.WriteLine($"  {PositionFormatter.FormatAspect(aspect)}");
}

Console.WriteLine();
Console.WriteLine($"Total aspects: {result.Aspects.Count} ({majorCount} major, {ebertinCount} Ebertin)");

// Midpoint Activations
Console.WriteLine();
Console.WriteLine("Midpoint Activations (Ebertin 45 Dial)");
Console.WriteLine("---------------------------------------");

foreach (MidpointActivation activation in result.MidpointActivations)
{
    Console.WriteLine($"  {PositionFormatter.FormatActivation(activation)}");
}

Console.WriteLine();
Console.WriteLine($"Total midpoints: {result.Midpoints.Count}");
Console.WriteLine($"Total activations: {result.MidpointActivations.Count}");

// ── Database: Save and list charts ──────────────────────────

string dbPath = Path.Combine(exeDir, "astrocast.db");
string connectionString = $"Data Source={dbPath}";
SqliteChartRepository repo = new(connectionString, Log.Logger);
await repo.InitializeAsync();

// Check if chart already exists (avoid duplicate name constraint violation)
ChartEntity? existing = await repo.GetByNameAsync(chartInput.Name);

if (existing is null)
{
    ChartEntity entity = ChartMapper.ToEntity(chartInput, ephemeris);
    long id = await repo.SaveAsync(entity);
    Console.WriteLine();
    Console.WriteLine($"Chart saved with ID: {id}");
}
else
{
    Console.WriteLine();
    Console.WriteLine($"Chart already saved (ID: {existing.Id})");
}

// List all saved charts
IReadOnlyList<ChartEntity> allCharts = await repo.ListAllAsync();
Console.WriteLine();
Console.WriteLine($"Saved Charts ({allCharts.Count}):");
foreach (ChartEntity c in allCharts)
{
    string genderDisplay = c.Gender is not null && c.Gender != "Unspecified"
        ? $" ({c.Gender})"
        : "";
    Console.WriteLine($"  [{c.Id}] {c.Name}{genderDisplay} -- {c.LocalDateTime} -- {c.LocationName}");
}

// ── Output Formats ──────────────────────────────────────────

string baseName = Path.Combine(exeDir, FileNameSanitiser.Sanitise(result.Input.Name));

string jsonPath = $"{baseName}.json";
string mdPath = $"{baseName}.md";
string pdfPath = $"{baseName}.pdf";

await ChartJsonFormatter.SaveAsync(result, jsonPath);
await ChartMarkdownFormatter.SaveAsync(result, mdPath);
ChartPdfFormatter.Save(result, pdfPath);

Console.WriteLine();
Console.WriteLine("Output saved:");
Console.WriteLine($"  JSON:     {jsonPath}");
Console.WriteLine($"  Markdown: {mdPath}");
Console.WriteLine($"  PDF:      {pdfPath}");

return 0;
