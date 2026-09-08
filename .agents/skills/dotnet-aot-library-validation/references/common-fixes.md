# Common fixes

Before/after recipes for the patterns that produce most trim/AOT warnings in
libraries. Order of preference: eliminate > annotate > redesign > suppress.
Source: Microsoft Learn trimming docs + "How to make libraries compatible with
native AOT" (Eric Erhardt, devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/).

## 1. Attribute flow (annotate)

Warnings say the _source_ of a value doesn't match the _target's_ requirement.
Add matching annotations at the source; propagate up the call graph until either
a statically known type flows in or the annotation reaches a public API.

```csharp
// Before: IL2067 — Activator.CreateInstance requires PublicParameterlessConstructor
public static object CreateNewObject(Type t) => Activator.CreateInstance(t);

// After: requirement stated on the parameter; callers now checked too
public static object CreateNewObject(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type t) => Activator.CreateInstance(t);
```

Annotate fields and generic type parameters the same way:

```csharp
// Field
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
static Type type;

// Generic parameter
static void Use<T>([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TParameters p) { }
```

Rules of thumb:

- Start at the lowest layer; expect new warnings to surface in higher layers
  afterwards — that is the flow being made visible, not a regression.
- Do not annotate virtual or interface methods — every override must carry a
  matching annotation (IL2046/IL3051). Prefer annotating concrete entry points.
- Never put analyzable-on-reflection code in static constructors; the warning
  propagates to every member of the class.
- Once annotations reach public APIs only, consumer code gets warnings solely
  when _it_ calls incompatible members — and IL2104 rollups disappear.

## 2. `MakeGenericType` / `MakeGenericMethod` (IL3050)

The AOT compiler pre-generates code per generic instantiation. Unknown
instantiations — especially value-type arguments — are unavailable at runtime.

**Restrict to known types** (OpenTelemetry `RegisterSlot` fix):

```csharp
// Before: open generic filled at runtime
var slotType = typeof(ContextSlot<>).MakeGenericType(valueType);

// After: hard-code the instantiations consumers actually use
static readonly Dictionary<Type, Type> Known = new()
{
    [typeof(long)] = typeof(ContextSlot<long>),
    [typeof(double)] = typeof(ContextSlot<double>),
    [typeof(int)] = typeof(ContextSlot<int>),
};
```

**Reference-type-only trick**: closed generics over _reference_ types share one
native body, so `MakeGenericType` over class arguments is safe even if
individually unknown — guard with a generic constraint (`where T : class`) so a
struct can never reach it.

**Bridge reflection at a known type**: `Type.GetType` with a _constant_ string,
or `typeof(SomeType)` directly, makes the tooling preserve what reflection
touches. `Type.GetType(variableString)` cannot be verified.

## 3. `Reflection.Emit` / `DynamicMethod` optimizations

AOT has no JIT. Performance optimizations that emit IL (fast field readers,
delegate builders) must degrade gracefully:

```csharp
if (RuntimeFeature.IsDynamicCodeSupported)
{
    // JIT path: keep the fast emitted-IL optimization
    EmitAndCacheGetter();
}
else
{
    // Native AOT path: plain reflection fallback
    UseSlowGetter();
}
```

Better long-term: remove the need entirely (StackExchange.Redis replaced private
`MulticastDelegate` field reflection with supported APIs; Pipelines.Sockets.Unofficial
removed a generic constraint that required reflection bridging, eliminating the
optimization's need for IL emit in all modes).

## 4. Reflection-based serialization

Reflection serializers cannot work in trimmed apps (walked type graphs get
trimmed). Replace, don't patch:

- `Newtonsoft.Json` / private forks → `System.Text.Json` source generation
  (see `references/source-gen-recipes.md`). IdentityModel case study.
- `JsonSerializer.Serialize(value)` bare calls → pass `JsonTypeInfo<T>` /
  `JsonSerializerContext` overloads; add source-gen context.
- APIs taking `object` then calling `.GetType()` (StackExchange.Redis
  `LuaScript.Prepare` case) → mark `[RequiresUnreferencedCode]` and add a
  generic sibling that uses `typeof(TParameters)`:
  `ScriptEvaluate<TParameters>(...)` with
  `[DynamicallyAccessedMembers(PublicProperties)]`.

`XmlSerializer` and `BinaryFormatter` are not viable under trim/AOT.

## 5. `Enum.GetValues(typeof(T))` and friends

Creating `TEnum[]` at runtime needs generated code for that array type:

```csharp
// Before (IL3050 in AOT)
var values = Enum.GetValues(typeof(MyEnum));

// After (.NET 5+, guaranteed generated)
var values = Enum.GetValues<MyEnum>();
```

## 6. `System.Linq.Expressions`

`.Compile()` runs interpreted under AOT (correct but slower). Prefer direct
calls. `Expression.Property(expr, "name")` warns (string-unknown member) — use
the `PropertyInfo` overload with a statically known `PropertyInfo`. OpenTelemetry
eventually removed all `Expression` usage; incremental path is static `PropertyInfo`
first.

## 7. `EventSource.WriteEvent` with object[]

Overloads taking `object[]` are `[RequiresUnreferencedCode]` (payload serialized
via reflection). Pass 3 or fewer primitive/enum/string args to hit typed
overloads; .NET 8's new `EventSource` overloads removed most of this
false-positive class.

## 8. Private reflection against other libraries

Reflection into another library's internals (even "just for speed") breaks when
the dependency changes shape. Prefer public APIs; often a newer dependency
version adds the missing member (Google.Protobuf `.Clear()` case). If the
underlying dependency is itself AOT-incompatible, accept it:
`[RequiresUnreferencedCode]` the wrapper (OpenTelemetry SqlClient).

## 9. Suppression — last resort

```csharp
[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2063",
    Justification = "Only types stored through the annotated setter reach this array.")]
get => types[i];
```

- `SuppressMessage` / `#pragma` do not reach publish-time analysis — use
  `UnconditionalSuppressMessage` only.
- Valid justification = the reflected members are _visible targets of reflection_
  (preserved via `[DynamicallyAccessedMembers]`, `DynamicDependency`, or root-keeping
  code paths) elsewhere.
- **Invalid** justification: "the app only reflects over types it uses" —
  non-reflection usage can be inlined, renamed, or removed; Native AOT already
  optimizes such members away.
- Every suppression must be covered by a runtime test in the AOT test app
  (`references/test-app-validation.md`), since the analyzer is silent there by
  definition.
- `[DynamicDependency]` only _keeps_ members; it does not express reflection
  behavior. Combine with suppression, but prefer the other attributes first.
