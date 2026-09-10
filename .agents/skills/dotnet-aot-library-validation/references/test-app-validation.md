# Test-app validation

Roslyn analyzers analyze one project at a time and cannot see dependency
implementations — they are necessary but not complete. AOT-publishing a test
app that references the library gives the compiler full visibility: it
analyzes every method and type in the library and emits the complete warning
set. Zero warnings = the library is verified AOT compatible.

This is the pattern Microsoft ships and the OpenTelemetry .NET repo uses
(`AotCompatibility.TestApp.csproj` + publish script + CI workflow).

## 1. Test app project

Create a sibling console project, e.g. `AotCompatibility.TestApp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\src\MyLibrary\MyLibrary.csproj" />
    <TrimmerRootAssembly Include="MyLibrary" />
  </ItemGroup>

</Project>
```

Key points:

- `PublishAot` in the project file (not the command line) — it controls
  analysis during build/edit, not just publish.
- `TrimmerRootAssembly` names the library assembly (file name without
  extension). It roots _everything_ in the library, so the compiler treats it
  as if all code is called — no member escapes analysis.
- Use `TrimmerRootAssembly`, _not_ `PublishTrimmed` alone. (For the
  trim-only variant of this pattern, swap `PublishAot` for
  `PublishTrimmed`; everything else is identical.)
- One app can root several libraries (all as `ProjectReference` +
  `TrimmerRootAssembly`); isolating per library makes attribution clearer.
- If the library uses `#if`-conditional behavior per TFM, add a test app per
  relevant TFM.

Add a `Program.cs` that exercises key library APIs when the app runs — this
becomes the execution harness for suppressed warnings:

```csharp
using MyLibrary;

// Exercise public surface; return non-zero on unexpected behavior.
Console.WriteLine(Codec.Encode("sample"));
```

## 2. Publish and check

```bash
dotnet publish AotCompatibility.TestApp -c Release -r <RID>
```

- `<RID>`: use the host RID for a runnable binary (`osx-arm64`, `win-x64`,
  `linux-x64`...). CI may cross-compile other RIDs for warning analysis only —
  cross-compiled binaries cannot execute on the build host.
- Native toolchain prerequisites: clang + zlib dev packages (Linux distros;
  see Native AOT overview docs), Xcode Command Line Tools (macOS), VS 2022
  "Desktop development with C++" workload (Windows).

## 3. Publish script (fail on new warnings)

```bash
#!/usr/bin/env bash
set -euo pipefail

case "$(uname -s)/$(uname -m)" in
  Darwin/arm64)  RID=osx-arm64 ;;
  Darwin/x86_64) RID=osx-x64 ;;
  Linux/x86_64)  RID=linux-x64 ;;
  Linux/aarch64) RID=linux-arm64 ;;
  MINGW*/x86_64|CYGWIN*/x86_64) RID=win-x64 ;;
  *) echo "Unsupported host: $(uname -s)/$(uname -m)"; exit 1 ;;
esac

LOG=$(mktemp)
dotnet publish AotCompatibility.TestApp -c Release -r "$RID" > "$LOG" 2>&1 || { cat "$LOG"; exit 1; }

# Trimming analysis warnings IL2xxx and AOT warnings IL3xxx indicate breaks.
grep -E 'warning (IL2[0-9]{3}|IL3[0-9]{3})' "$LOG" | sort -u > warnings.txt

if [ -s warnings.txt ]; then
  echo "AOT compatibility warnings found:"
  cat warnings.txt
  exit 1
fi

echo "AOT compatibility: clean."
./bin/Release/net8.0/$RID/publish/AotCompatibility.TestApp   # exercise APIs
```

Keep an allowlist if you deliberately suppress warnings: diff current warnings
against the allowlist and fail on _new_ codes only — new warnings are treated
as breaking changes for AOT consumers.

## 4. GitHub Actions workflow

```yaml
name: aot-compatibility
on:
  pull_request:
  push:
    branches: [main]

jobs:
  aot:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      # Linux native toolchain
      - run: sudo apt-get install -y clang zlib1g-dev
        if: runner.os == 'Linux'
      - run: ./build/test-aot-compatibility.sh
```

Reference implementation (OpenTelemetry .NET):

- `test/OpenTelemetry.AotCompatibility.TestApp/OpenTelemetry.AotCompatibility.TestApp.csproj`
- `build/test-aot-compatibility.ps1`
- `.github/workflows/ci-aot.yml`

## 5. Run the published binary when suppressions exist

Suppressed warnings have no static safety net — the only proof is execution.
The test app must call the code paths behind every suppression and assert
correct behavior (the OpenTelemetry approach: app returns failure exit code if
an API misbehaves). Without suppressions, running the app is still cheap
insurance.

## Common pitfalls

- Rooting the _test app's_ assembly instead of the library — nothing useful
  is analyzed; the library gets trimmed away entirely.
- Analyzing only `net8.0` when conditional compilation changes behavior per
  TFM.
- Forgetting the toolchain install step in Linux CI — publish fails with
  confusing linker errors.
- Treating IL2104 ("assembly produced trim warnings") as ignorable — it is a
  rollup of real site warnings in dependencies.
