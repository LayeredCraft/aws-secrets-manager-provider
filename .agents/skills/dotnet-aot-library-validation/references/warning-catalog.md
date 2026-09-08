# Warning catalog

Codes emitted by the trim/AOT/single-file Roslyn analyzers and by
`dotnet publish` with `PublishAot` or `PublishTrimmed`. Trim-family docs:
https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings/
AOT-family docs:
https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/

## Trim warnings (IL2xxx)

| Code   | Meaning                                                                                              | Typical fix                                                                             |
| ------ | ---------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| IL2026 | Calling a member annotated `[RequiresUnreferencedCode]`                                              | Eliminate the call, propagate the annotation up to a public API, or use an alternative  |
| IL2046 | Member annotated `[RequiresUnreferencedCode]` mismatch across override/interface                     | Align annotations on base/interface and implementations; avoid annotating virtuals      |
| IL2065 | Value passed where `[DynamicallyAccessedMembers]` requirement not met (in attribute context)         | Annotate the flow source                                                                |
| IL2067 | Parameter value does not satisfy the target's `[DynamicallyAccessedMembers]` requirement             | Add matching `[DynamicallyAccessedMembers]` to the parameter (or source field/property) |
| IL2070 | `this` argument does not satisfy requirement in call to reflection method (e.g. `Type.GetMethods()`) | Annotate the receiving parameter; if the `Type` is statically known, drop reflection    |
| IL2072 | Return value does not satisfy the target's requirement                                               | Annotate the return type or restructure so the source is statically known               |
| IL2075 | Reflected member not statically known to exist (member not preserved)                                | Statically know the type or annotate the flow; do not suppress blindly                  |
| IL2077 | Field value does not satisfy the target's requirement                                                | Add `[DynamicallyAccessedMembers]` to the field                                         |
| IL2104 | `Assembly 'X' produced trim warnings` — dependency has un-actioned warnings                          | Fix or acknowledge dependency issues; verify with test app                              |
| IL2125 | Referenced assembly lacks `IsTrimmable` metadata (only with `VerifyReferenceTrimCompatibility`)      | Dependency not annotated; opt-in check, noisy by design                                 |

## AOT warnings (IL3xxx, RequiresDynamicCode family)

| Code   | Meaning                                                                                             | Typical fix                                                                                                                             |
| ------ | --------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| IL3050 | Calling a member annotated `[RequiresDynamicCode]` (e.g. `Type.MakeGenericType`, `Reflection.Emit`) | Restrict to statically known instantiations, guard with `RuntimeFeature.IsDynamicCodeSupported`, or mark caller `[RequiresDynamicCode]` |
| IL3051 | `[RequiresDynamicCode]` mismatch between virtual/interface method and its override/implementation   | Make annotations consistent across base/interface and implementations                                                                   |
| IL3058 | Referenced assembly lacks `IsAotCompatible` metadata (only with `VerifyReferenceAotCompatibility`)  | Dependency not annotated; metadata only written by .NET 10+ builds — expect noise on older packages                                     |

## Assembly-level summary warnings

| Code                                       | Meaning                                                                    |
| ------------------------------------------ | -------------------------------------------------------------------------- |
| IL2104                                     | Assembly produced trim warnings (dependency contains un-actioned warnings) |
| IL2111 / IL2121 / IL2122 / IL2123 / IL2124 | Feature-level trim incompatibilities (framework assemblies)                |

## Reading strategy

1. Fix leaf codes first (`IL2026`–`IL2087` family): each is a concrete site.
2. Assembly-level codes (`IL2104`) are rollups — they clear when the underlying
   site warnings clear.
3. The same pattern often produces several codes along one flow
   (e.g. one un-annotated `Type` field can yield `IL2077` then `IL2067`).
   Fixing the source (the field) clears the whole chain.
4. Unknown code? Search the warning docs: `.../trimming/trim-warnings/<code>` or
   `.../native-aot/warnings/<code>`.
