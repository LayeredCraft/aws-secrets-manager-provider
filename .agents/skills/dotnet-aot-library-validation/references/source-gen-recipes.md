# Source generation recipes

Compile-time code generation replaces the runtime codegen that AOT forbids.
General rule: whenever a library calls an API whose implementation reflects
over unknown types or emits IL, look for a source-generator-backed variant.

## System.Text.Json source generation

Reflection `JsonSerializer.Serialize/Deserialize<T>` calls are
`[RequiresUnreferencedCode]` + `[RequiresDynamicCode]`. Create a context:

```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WeatherForecast))]
[JsonSerializable(typeof(List<WeatherForecast>))]   // collections need explicit entries
internal partial class WeatherForecastContext : JsonSerializerContext { }
```

Call the context-aware overloads:

```csharp
json = JsonSerializer.Serialize(value, WeatherForecastContext.Default.WeatherForecast);
obj  = JsonSerializer.Deserialize(json, WeatherForecastContext.Default.WeatherForecast);
```

Notes:

- Members declared `object` (and unknown runtime types) need their own
  `[JsonSerializable(typeof(...))]` entries; unsupported runtime types fail
  consistently both with and without AOT.
- Set options via `[JsonSourceGenerationOptions]` so `Context.Default` is
  preconfigured; or resolve through `JsonSerializerOptions`:
  `TypeInfoResolver = Context.Default`.
- Chain multiple contexts with `JsonTypeInfoResolver.Combine(...)` or
  `options.TypeInfoResolverChain`.
- Enums as strings: `[JsonConverter(typeof(JsonStringEnumConverter<TEnum>))]` —
  the non-generic `JsonStringEnumConverter` is **not** supported under AOT.
  Blanket option: `[JsonSourceGenerationOptions(UseStringEnumConverter = true)]`.
- App-side hardening: `JsonSerializerIsReflectionEnabledByDefault=false` makes
  any remaining reflection serialization throw `InvalidOperationException`
  with a clear message on every runtime, instead of breaking unpredictably
  under AOT only.
- Public API design: add `JsonTypeInfo<T>` parameters to public serialize
  methods so consumers can feed their own contexts.

Docs: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation

## P/Invoke: `[LibraryImport]` instead of `[DllImport]`

`DllImport` marshalling stubs are IL emitted at runtime — blocked under AOT.
The .NET 7+ SDK's P/Invoke source generator (on by default) compiles marshalling
at build time from `[LibraryImport]`:

```csharp
// Before
[DllImport("nativelib", EntryPoint = "to_lower", CharSet = CharSet.Unicode)]
internal static extern string ToLower(string str);

// After
[LibraryImport("nativelib", EntryPoint = "to_lower", StringMarshalling = StringMarshalling.Utf16)]
internal static partial string ToLower(string str);   // partial, not extern
```

Migration deltas:

- `CharSet` → `StringMarshalling` (ANSI removed; UTF-8 first-class: `Utf8`)
- `CallingConvention` → `[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]`
- No equivalents: `BestFitMapping`, `ThrowOnUnmappableChar`, `ExactSpelling`,
  `PreserveSig` — entry point name is exact; signature translates directly
- Requires `AllowUnsafeBlocks` for marshalling-generated unsafe code
- Unsupported `MarshalAs` settings emit compile errors, not runtime surprises

Analyzers (SYSLIB1050–1069) + code fixers automate the conversion.

## Regular expressions: `[GeneratedRegex]`

```csharp
[GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
private static partial Regex DatePattern();
```

Compile-time generation replaces `new Regex(pattern)` / `Regex.*(pattern)` calls
that parse patterns at runtime. Not strictly forbidden under AOT, but generated
regexes are faster everywhere and are the expected library pattern.

## Options validation: `[OptionsValidator]`

Replaces reflection-based validation of option objects with
`DataAnnotations` attributes:

```csharp
[OptionsValidator]
internal sealed partial class HttpStandardResilienceOptionsValidator
    : IValidateOptions<HttpStandardResilienceOptions> { }
```

Generator inspects the options type at build time and emits validation code.
Register in DI. (dotnet/extensions pattern.)

## Configuration binding: `EnableConfigurationBindingGenerator`

```xml
<PropertyGroup>
  <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

Replaces reflection in `ConfigurationBinder` with generated property setters;
no call-site changes needed. (dotnet/extensions case study.)

## Other useful generators

- YamlDotNet source generator — strongly-typed YAML serialization under AOT
  (Andrew Lock: andrewlock.net/using-the-yamldotnet-source-generator-for-native-aot/)
- Dapper.AOT — build-time ADO.NET code generation replacing Dapper's runtime
  IL emit; also faster at startup in non-AOT apps
- ASP.NET Core minimal APIs / JSON: `[JsonPropertyName]`-annotated DTOs with
  source-gen contexts (aspnetcore native AOT support)

## Decision shortcut

| Library uses                           | Replace with                                         |
| -------------------------------------- | ---------------------------------------------------- |
| Newtonsoft.Json / reflection S.T.J     | STJ source-gen context                               |
| `[DllImport]`                          | `[LibraryImport]`                                    |
| `new Regex(...)` with constant pattern | `[GeneratedRegex]`                                   |
| `IConfiguration` → options binding     | `EnableConfigurationBindingGenerator`                |
| Options DataAnnotations validation     | `[OptionsValidator]`                                 |
| Dynamic IL emit for perf               | `IsDynamicCodeSupported` guard + fallback, or remove |
