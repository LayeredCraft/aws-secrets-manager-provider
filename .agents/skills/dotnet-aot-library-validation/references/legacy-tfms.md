# Legacy TFM support

Trim/AOT attributes did not exist before .NET 5 (`RequiresUnreferencedCode`,
`DynamicallyAccessedMembers`); `[RequiresDynamicCode]` arrived in .NET 7.
Libraries still targeting `netstandard2.0` / `net472` hit
`CS0246: The type or namespace name 'DynamicallyAccessedMembersAttribute' could not be found`.

The .NET team deliberately does **not** ship these attributes as a
netstandard NuGet package: they are meaningless on .NET Framework, and shipping
them would imply otherwise (same reasoning as the nullable-annotation attributes).

## Option 1: `#if` directives

```csharp
public static object CreateNewObject(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
    Type t)
{
    return Activator.CreateInstance(t);
}
```

Drawbacks:

- Readability erodes as annotations accumulate.
- Missed TFMs are easy — especially with **shared source files** compiled into
  multiple projects: a project that never targets net5.0+ silently builds the
  attribute-less variant in _all_ its TFMs. The Roslyn analyzers will not warn
  about it.
- Consumers using the netstandard2.0 binary in a .NET 7+ app get warnings from
  inside the library during publish (no annotations in IL).

## Option 2: Define the attributes internally (recommended for multi-targeting)

The trim/AOT tooling matches these attributes **by fully-qualified name and
namespace, not by assembly**. A library can define its own copies; the tooling
respects them identically:

- Keep one shared file (e.g. `TrimAttributes.cs`) containing
  `[RequiresUnreferencedCode]`, `[DynamicallyAccessedMembers]`,
  `[RequiresDynamicCode]`, `[UnconditionalSuppressMessage]` in their canonical
  `System.Diagnostics.CodeAnalysis` namespaces, guarded with
  `#if !NET5_0_OR_GREATER` (and `#if !NET7_0_OR_GREATER` for
  `RequiresDynamicCode`) so real BCL types win on modern TFMs.
- Include the file in every project needing annotations on old TFMs.
- Reference copy: github.com/eerhardt/blog-resources —
  `creating-aot-compatible-libraries/TrimmingAttributes.cs`

Advantages: no per-annotation noise, applies on _every_ TFM, one-time setup.

## Option 3: PolySharp

The [PolySharp](https://www.nuget.org/packages/PolySharp/) package generates
polyfill definitions (including these attributes) at build time as needed —
same mechanics as Option 2 without maintaining the file yourself.

## Verifying legacy-TFM binaries

Roslyn analyzers still require targeting net7.0+ (they read annotations from
the reference assemblies). To verify an annotation-complete netstandard2.0
binary, use the AOT-published test app pattern
(`references/test-app-validation.md`) — the AOT compiler sees the IL
annotations regardless of TFM.

## Practical guidance

- Analyzers keep their value: still multi-target `net8.0` (at least) and
  conditionally set `IsAotCompatible` there, even when also shipping
  netstandard2.0 with internal attribute copies.
- Never let the netstandard2.0-only build be what AOT consumers resolve —
  ship a net8.0+ TFM in the same package so annotated metadata travels with it.
- Do not pretend netstandard2.0 "supports trimming": trimming itself requires
  .NET 6+; a trim test app has no benefit against netstandard/net4x targets.
