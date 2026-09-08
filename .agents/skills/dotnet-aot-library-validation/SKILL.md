---
name: dotnet-aot-library-validation
description: >-
  Makes .NET class libraries Native AOT compatible: enables AOT/trim analyzers,
  fixes IL2xxx/IL3xxx warnings, replaces reflection with source generators, and
  validates with an AOT-published test app in CI. Use when the user wants to add
  AOT support or trim compatibility to a .NET library, mentions IsAotCompatible,
  PublishAot, or Native AOT for a NuGet/class library, asks about trim or AOT
  warnings (IL2026, IL2067, IL2070, IL2104, IL2125, IL3050, IL3051, IL3058),
  RequiresUnreferencedCode, RequiresDynamicCode, DynamicallyAccessedMembers,
  MakeGenericType or Activator.CreateInstance failures under AOT, System.Text.Json
  source generation, LibraryImport vs DllImport, or CI validation of AOT
  compatibility. Library-authoring focused; not for publishing end-user apps.
---

# .NET library AOT compatibility

Native AOT compiles IL to native code at publish time. There is no JIT at runtime,
so nothing can be generated or discovered dynamically that was not known at build
time. Trim analysis enforces this: the AOT compiler must trim, and reflection-based
patterns break.

The guiding principle from the .NET team: **if an app publishes for AOT with zero
warnings, it will behave the same after AOT as without.** Warnings mean the tooling
cannot guarantee correctness. A library that publishes clean against a
`TrimmerRootAssembly`-rooted test app is trustworthy for AOT consumers.

Scope: class libraries meant to be _consumed by_ AOT apps. This is not about
publishing an app itself, nor about publishing a library _as_ a native library
(`[UnmanagedCallersOnly]` — a different, unrelated scenario).

## Workflow

Work through these phases in order. Fixes at earlier phases reduce work in later
ones. Always fix dependencies before dependents (bottom-up) — annotations added
low in the stack surface warnings in higher layers, and a library cannot be
verified while a dependency still warns.

### 1. Assess the project

Read the library's .csproj and scan the source. Record:

- `TargetFrameworks` (analyzers need `net7.0`+ annotations; `net8.0` recommended for the fullest set — multi-target if needed)
- Existing `IsAotCompatible` / `IsTrimmable` / `EnableAotAnalyzer` properties
- Dependencies (`PackageReference`) — an un-annotated dependency caps what this
  library can achieve; the OpenTelemetry SqlClient case: when the underlying
  library is not AOT compatible, the wrapper gets `[RequiresUnreferencedCode]`
- Reflection usage: `GetType()`, `GetMembers`, `Activator.CreateInstance`,
  `MakeGenericType`, `Assembly.Load*`, `Type.GetType`
- Serialization: reflection-based (`Newtonsoft.Json`, `JsonSerializer` without
  context, `XmlSerializer`, `BinaryFormatter`)
- Interop: `[DllImport]` (runtime IL stubs are JIT-generated — blocked under AOT)
- `System.Linq.Expressions` `.Compile()` usage, `Reflection.Emit`, regex parsing

### 2. Enable analyzers

The `IsAotCompatible` property enables `IsTrimmable`, `EnableTrimAnalyzer`,
`EnableSingleFileAnalyzer`, and `EnableAotAnalyzer` in one shot:

```xml
<PropertyGroup>
  <TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
  <IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>
</PropertyGroup>
```

The `Condition` is required when the library multi-targets older frameworks —
the analyzers depend on annotations that only exist in the `net7.0`+ reference
assemblies (and `RequiresDynamicCode` in `net7.0`; prefer `net8.0` for the
fullest annotations).

Optional, opt-in dependency verification (noisy, but catches un-annotated deps):

```xml
<VerifyReferenceAotCompatibility>true</VerifyReferenceAotCompatibility> <!-- IL3058 -->
<VerifyReferenceTrimCompatibility>true</VerifyReferenceTrimCompatibility> <!-- IL2125 -->
```

Build and collect the full warning list (`dotnet build` for the net8.0+ TFM).
Map each warning with `references/warning-catalog.md`.

### 3. Fix warnings, in this order of preference

1. **Eliminate** the dynamic code. Often reflection exists only for an
   optimization or a workaround; a public API on the dependency or a modern
   framework API replaces it outright.
2. **Annotate** so the tooling can see what reflection will touch. Attributes
   flow bottom-up through the call graph: `[DynamicallyAccessedMembers]`,
   `[RequiresUnreferencedCode]`, `[RequiresDynamicCode]`. Propagate until
   warnings land only on deliberately-incompatible _public_ APIs.
3. **Redesign the API** for AOT: source-generated serialization, `JsonTypeInfo<T>`
   overloads, `[LibraryImport]`, generated regex. See
   `references/source-gen-recipes.md`.
4. **Suppress** with `[UnconditionalSuppressMessage]` only as a last resort, only
   with proof that the reflected members are visible targets of reflection
   elsewhere, and only with a runtime test in the AOT-published test app.
   Suppressing because "the app only passes types the app uses" is invalid —
   such members get inlined, renamed, or removed. See `references/common-fixes.md`.

Concrete before/after recipes for the highest-frequency patterns live in
`references/common-fixes.md`. Anti-patterns to avoid: annotating virtual or
interface methods (every override must match), reflection inside static
constructors (warning spreads to the whole class).

### 4. Validate with an AOT-published test app

Roslyn analyzers see one project at a time; they cannot analyze dependency
implementations, so they can miss warnings. The AOT compiler can. The complete
validation loop is:

- A console test app with `PublishAot` referencing the library
- `TrimmerRootAssembly` to force analysis of every library member
- A publish script that fails on any warning, plus a CI workflow

Scaffold and wiring details: `references/test-app-validation.md`. If the fix
phase introduced any suppression, the test app _must also execute_ library code
paths (the OpenTelemetry pattern) — static analysis is blind to suppressed
warnings by definition.

### 5. Legacy TFM support (only if multi-targeting below net5.0)

Trim/AOT attributes do not exist before `net5.0` (`net7.0` for
`RequiresDynamicCode`). Options: `#if` directives, internal attribute
definitions, or PolySharp. Trade-offs matter; see `references/legacy-tfms.md`.

### 6. Ship

Keep `IsAotCompatible` set in the shipped package. Note for consumers: the
`IsAotCompatible` _assembly metadata_ consumed by `VerifyReferenceAotCompatibility`
(IL3058) is only written by .NET 10+ builds, so older annotated packages still
trigger IL3058 — that check is opt-in for a reason.

## Key principles

- Zero warnings is the goal; each warning is a possible runtime break in a
  consumer's AOT app, even if it happens to work today.
- Annotate the lowest layers first; re-run after each dependency fix.
- `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` on a public API is a
  _communication mechanism_, not a failure — it moves the warning to the caller
  who chose the incompatible path, mirroring how `System.Text.Json` shipped.
- Introducing new trim/AOT warnings is a breaking change for
  `PublishTrimmed`/`PublishAot` consumers — CI validation belongs on every PR.

## Reference files

| File                                | When to read                                                       |
| ----------------------------------- | ------------------------------------------------------------------ |
| `references/warning-catalog.md`     | Mapping build output to meaning + typical fix                      |
| `references/common-fixes.md`        | Before/after recipes for recurring patterns                        |
| `references/source-gen-recipes.md`  | Serialization, interop, regex, options/config replacement patterns |
| `references/test-app-validation.md` | Test app csproj, publish script, CI workflow                       |
| `references/legacy-tfms.md`         | Multi-targeting below net5.0 without losing annotations            |
