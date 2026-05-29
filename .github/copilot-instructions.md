# DependencyCop – Copilot Instructions

## What this repo is

A Roslyn diagnostic analyzer (NuGet package `Lindhart.DependencyCop`) that enforces namespace dependency rules in C# codebases. It is a fork of `Liversen.DependencyCop` with an added **CodeFixProvider** for rule DC1001.

Rules: DC1001 (disallowed using statements), DC1002 (descendant namespace access), DC1003 (namespace cycle), DC1004 (DC1001 not configured).

---

## Build, test, and lint commands

All build/test orchestration goes through `_Development/Solution.proj`:

```sh
# Restore
dotnet msbuild _Development/Solution.proj /t:_Restore /p:Configuration=Release

# Build
dotnet msbuild _Development/Solution.proj /t:_Build /p:Configuration=Release

# Test (with coverage report)
dotnet msbuild _Development/Solution.proj /t:_Test /p:Configuration=Release
```

Run a **single test** by display name filter:

```sh
dotnet test Lindhart.DependencyCop.slnx --filter "DisplayName~SimpleTest"
```

`TreatWarningsAsErrors=true` is enforced globally via `_Development/Project.targets`. All projects run StyleCop, Sonar, and .NET analyzers.

---

## Architecture

```
Lindhart.DependencyCop/          # Analyzer library (netstandard2.0)
  DottedName.cs                  # Core value type for dotted namespace names
  Equatable.cs                   # Generic equality base class
  Helpers.cs                     # Shared Roslyn helpers (type/namespace resolution)
  UsingNamespaceStatement/       # DC1001 + DC1004 analyzer
  DescendantNamespaceAccess/     # DC1002 analyzer
  NamespaceCycle/                # DC1003 analyzer

Lindhart.DependencyCop.CodeFixes/  # Code fix providers (separate project)
  UsingNamespaceStatement/
    FixProvider.cs               # Registers the code fix for DC1001
    SingleViolationFixer.cs      # Core fix logic: removes using + qualifies all references
    StaticUsingsSet.cs
    Violation.cs

Lindhart.DependencyCop.Tests/    # xUnit test project (net10.0)
  UsingNamespaceStatement/
    AnalyzerTest.cs
    FixerTest.cs
    TestData/Analyzer/*.cs       # Embedded resource test inputs
    TestData/Fixer/*Code.cs      # Input code for fixer tests
    TestData/Fixer/*FixedCode.cs # Expected output code for fixer tests

Lindhart.DependencyCop.Package/  # NuGet packaging project
_Development/                    # MSBuild orchestration (not a real project)
```

Each analyzer rule lives in its own subfolder with an `Analyzer.cs` class. CodeFixes are in a separate assembly (`Lindhart.DependencyCop.CodeFixes`).

---

## Key conventions

### DottedName is the central domain type
All namespace name comparisons go through `DottedName`. Use `IsEqualToOrDescendantOf`, `SkipCommonPrefix`, `TakeParts`, `SkipParts` rather than raw string operations.

### Analyzer library targets netstandard2.0
The analyzer must stay on `netstandard2.0` (required by Roslyn/MSBuild analyzer hosting). Tests and tooling target `net10.0`.

### Test data as embedded .cs files
Analyzer and fixer tests do **not** use inline code strings. Test inputs and expected outputs are standalone `.cs` files stored under `TestData/Analyzer/` and `TestData/Fixer/`, embedded as resources in the test project. Add new test cases by creating `{TestName}Code.cs` / `{TestName}FixedCode.cs` pairs and registering them in the `.csproj` `<EmbeddedResource>` and `<Compile Remove>` blocks.

### Testing framework
Tests use `Microsoft.CodeAnalysis.Testing` (`CSharpAnalyzerTest<TAnalyzer>` / `CSharpCodeFixTest<TAnalyzer, TFix>`), xUnit theories, and `Shouldly` for assertions. The DC1001 disallowed namespace prefixes are injected via a `globalconfig` file in test setup, using either `dotnet_diagnostic.DC1001_NamespacePrefixes` or `build_property.DC1001_NamespacePrefixes` keys.

### DC1001 configuration
Consumers configure DC1001 in `.editorconfig` or `.globalconfig`:
```
dotnet_diagnostic.DC1001_NamespacePrefixes = My.Namespace,Other.Namespace
```
The analyzer emits DC1004 at compilation end when this key is absent.

### EnforceExtendedAnalyzerRules
The analyzer project sets `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` — this enforces Roslyn analyzer API constraints; keep it enabled.
