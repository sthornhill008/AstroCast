// Reads the version a built AstroCast.Presentation.dll reports, the way the app does.
//
//   dotnet run read-app-version.cs -- <path to AstroCast.Presentation.dll> [--expect X.Y.Z]
//
// Loads the assembly in a private load context, resolving its dependencies from
// its own output directory, and invokes MainViewModel.GetAppVersion(): the private
// method behind the window title and status bar of both heads, invoked rather than
// reimplemented, so its trimming of a "+<hash>" suffix is exercised too. Prints the
// four version values. With --expect, exits 1 unless GetAppVersion returns X.Y.Z,
// AssemblyVersion and FileVersion read X.Y.Z.0, and InformationalVersion is X.Y.Z
// or X.Y.Z+<suffix>. A copy left from before the bump reads the old version and
// fails, so a stale build cannot pass.
//
// One copy per run, so each copy is read in its own process. A .NET 10 program
// rather than PowerShell because the assembly targets net10.0 and pwsh 7.5 runs
// on .NET 9.
//
// Exit codes: 0 read (and matched, with --expect); 1 mismatch; 2 usage or load error.

// A file-based app defaults to native AOT, whose trimming analysis flags every
// reflection call below. This probe is never published, so turn it off.
#:property PublishAot=false

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

string? dllArg = null;
string? expect = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--expect" && i + 1 < args.Length) { expect = args[++i]; }
    else if (dllArg is null) { dllArg = args[i]; }
    else { return Usage($"unexpected argument '{args[i]}'"); }
}
if (dllArg is null) { return Usage("no assembly path given"); }

string path = Path.GetFullPath(dllArg);
if (!File.Exists(path)) { return Usage($"not found: {path}"); }
if (Path.GetDirectoryName(path) is not string directory) { return Usage($"no directory in: {path}"); }

AssemblyLoadContext context = new("release-bump-probe", isCollectible: true);
context.Resolving += (ctx, name) =>
{
    string candidate = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
};

string informational;
string assemblyVersion;
string fileVersion;
string shown;
try
{
    Assembly assembly = context.LoadFromAssemblyPath(path);
    informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "(none)";
    assemblyVersion = assembly.GetName().Version?.ToString() ?? "(none)";
    fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion ?? "(none)";

    Type viewModel = FindMainViewModel(assembly)
        ?? throw new InvalidOperationException("no type named MainViewModel in the assembly");
    MethodInfo getAppVersion = viewModel.GetMethod("GetAppVersion", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{viewModel.FullName} has no static GetAppVersion()");
    shown = getAppVersion.Invoke(null, null) as string ?? throw new InvalidOperationException($"{viewModel.FullName}.GetAppVersion() returned no string");
}
catch (Exception ex)
{
    Exception inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
    Console.Error.WriteLine($"error: could not read {path}: {inner.GetType().Name}: {inner.Message}");
    return 2;
}

Console.WriteLine($"assembly:             {path}");
Console.WriteLine($"GetAppVersion():      {shown}");
Console.WriteLine($"InformationalVersion: {informational}");
Console.WriteLine($"AssemblyVersion:      {assemblyVersion}");
Console.WriteLine($"FileVersion:          {fileVersion}");

if (expect is null) { return 0; }

List<string> mismatches = [];
if (shown != expect) { mismatches.Add($"GetAppVersion() is {shown}, expected {expect}"); }
if (informational != expect && !informational.StartsWith(expect + "+", StringComparison.Ordinal))
{
    mismatches.Add($"InformationalVersion is {informational}, expected {expect} or {expect}+<suffix>");
}
if (assemblyVersion != expect + ".0") { mismatches.Add($"AssemblyVersion is {assemblyVersion}, expected {expect}.0"); }
if (fileVersion != expect + ".0") { mismatches.Add($"FileVersion is {fileVersion}, expected {expect}.0"); }

foreach (string mismatch in mismatches) { Console.WriteLine($"MISMATCH: {mismatch}"); }
Console.WriteLine(mismatches.Count == 0 ? $"OK: every value reads {expect}" : $"FAILED: {mismatches.Count} mismatch(es)");
return mismatches.Count == 0 ? 0 : 1;

static Type? FindMainViewModel(Assembly assembly)
{
    Type? exact = assembly.GetType("AstroCast.ViewModels.MainViewModel");
    if (exact is not null) { return exact; }
    Type?[] types;
    try { types = assembly.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { types = ex.Types; }
    return types.FirstOrDefault(t => t is not null && t.Name == "MainViewModel");
}

static int Usage(string problem)
{
    Console.Error.WriteLine($"error: {problem}");
    Console.Error.WriteLine("usage: dotnet run read-app-version.cs -- <path to AstroCast.Presentation.dll> [--expect X.Y.Z]");
    return 2;
}
