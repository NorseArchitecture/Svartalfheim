# Svartálfheim

> Home of the dvergar, the master smiths who forged Mjölnir, Gleipnir, and Gungnir.

<p align="center">
  <img src="https://github.com/user-attachments/assets/bf97a1f1-3a7b-42d0-930d-6b8c6c3d4063" alt="Svartálfheim — the underground realm of the dvergar, where the fires of the forge never die and the finest weapons in the nine realms are hammered into being" title="Svartálfheim — home of the dvergar, master smiths of the nine realms" />
</p>

*Image credit: [@norsemythologyclips](https://www.instagram.com/norsemythologyclips/) — go follow them.*

The forge of the Norse Architecture — **`Norse.Primitives`**, the foundational realm. Everything crossing a trust boundary into the ecosystem from an untrusted source flows through the types forged here: the `Result<T>` discriminated union, its closed parse-failure vocabulary, and the hot-path scalar parsers.

## What's forged here

- **[`Result<T>`](src/Primitives/Result%7BT%7D.cs)** — a hand-authored C# custom native union over [`Success<T>`](src/Primitives/Success%7BT%7D.cs) / [`Failure`](src/Primitives/Failure.cs): zero boxing on both paths, exhaustive two-arm switches, no way to construct an invalid value, and a law-proven combinator core (`Map` / `Bind` / `Match` — the functor and monad laws are FsCheck-pinned). It is one of the platform's **two unions**: `Result<T>` faces the boundary and describes *data* — the customs stamp on each untrusted scalar crossing the border — while Asgard's `Outcome<T>` faces the interior and describes *an event*. Same shape, opposite polarity, never siblings with style drift: [the two unions](https://github.com/NorseArchitecture/Glitnir/blob/master/docs/the-two-unions.md). This is shipped, not aspirational — `Result<T>` rides the request wire shapes of the live gRPC services today (Heimdall's [`RegisterRequest`](https://github.com/NorseArchitecture/Heimdall/blob/master/src/AuthN.Services/RegisterRequest.cs) hydrates a `Result<EmailAddress>` through its `Email` setter — deserialization *is* the parse event: the untrusted scalar is parsed the moment it crosses the boundary, never later in a handler), and [NORSE060](gen/Primitives.Analyzers/ResultInServiceResponseAnalyzer.cs) guards the opposite direction: `Result<T>` reachable from a `[ServiceContract]` response is a build error, because responses belong to `Outcome<T>`, which is erased at the transport edge and never serialized.
- **[`ParseFailure`](src/Primitives/ParseFailure.cs)** — the closed conversion-failure vocabulary (`Empty`, `Malformed`); adding a member is a deliberate breaking change.
- **[`Parser`](src/Primitives/Parser.cs)** — the generic gateway over `ISpanParsable<T>`: span in, `Result<T>` out, uniform failure semantics, required format provider. Specialists ride JIT-eliminated `typeof` routes; there is no runtime registry — a type that cannot parse does not compile.
- **Hot-path parsers** — static specialists with `ParseRequired` / `ParseOptional` entry points over `ReadOnlySpan<char>` and honest signatures: [`BooleanParser`](src/Primitives/BooleanParser.cs), the generic-math numeric cores [`IntegerParser`](src/Primitives/IntegerParser.cs) (`IBinaryInteger<T>`) and [`RealParser`](src/Primitives/RealParser.cs) (`IFloatingPoint<T>` — `float`/`double`/`decimal`, finite values only), [`CharParser`](src/Primitives/CharParser.cs), [`GuidParser`](src/Primitives/GuidParser.cs), and the temporal family [`DateOnlyParser`](src/Primitives/DateOnlyParser.cs) / [`TimeOnlyParser`](src/Primitives/TimeOnlyParser.cs) / [`DateTimeOffsetParser`](src/Primitives/DateTimeOffsetParser.cs) / [`DateTimeParser`](src/Primitives/DateTimeParser.cs) / [`TimeSpanParser`](src/Primitives/TimeSpanParser.cs). They carry the real ingestion vocabulary (grouping, currency, parentheses, hex/binary, percentage, code points, URN prefixes, ISO 8601 temporal representations) the bare BCL `TryParse` lacks. Ambiguous input fails loudly; nothing is guessed, nothing falls back silently.
- **[`TemporalFusion`](src/Primitives/TemporalFusion.cs)** — three spans in (ISO 8601 date, ISO 8601 time, IANA zone id), one UTC `DateTime` out. [`TimeZoneParser`](src/Primitives/TimeZoneParser.cs) is its companion, resolving IANA zone identifiers to `TimeZoneInfo`. Both DST seams are hard failures: a spring-forward gap is `Malformed` with detail `"DST gap"`; a fall-back ambiguity is `Malformed` with detail `"DST ambiguous"`. The BCL's silent standard-time pick never occurs.
- **[`Identifiers`](src/Primitives/Identifiers)** — [`SequentialGuid`](src/Primitives/Identifiers/SequentialGuid.cs) and [`DeterministicGuid`](src/Primitives/Identifiers/DeterministicGuid.cs): time-ordered and content-addressed GUID generation ([`GuidVersionBits`](src/Primitives/Identifiers/GuidVersionBits.cs), [`GuidByteOrder`](src/Primitives/Identifiers/GuidByteOrder.cs), [`INorseGuid`](src/Primitives/Identifiers/INorseGuid.cs)), landed 2026-07-03.
- **[`Pii`](src/Primitives/Pii)** — masked-by-default PII scalars: [`EmailAddress`](src/Primitives/Pii/EmailAddress.cs), [`PhoneNumber`](src/Primitives/Pii/PhoneNumber.cs), [`PersonalName`](src/Primitives/Pii/PersonalName.cs), [`BirthDate`](src/Primitives/Pii/BirthDate.cs), each an [`IPiiScalar<TSelf>`](src/Primitives/Pii/IPiiScalar.cs) over [`IMaskedValue`](src/Primitives/Pii/IMaskedValue.cs) (masked `ToString`, deliberate `WireValue`/`Normalized` egress). Paired with the NORSE061/NORSE062 compile-time [retention-policy analyzer](gen/Primitives.Analyzers/RetentionPolicyAnalyzer.cs): a persisted root's PII property with no [`[RetentionPolicy]`](src/Primitives/Pii/RetentionPolicyAttribute.cs) is an error, and PII reachable any way other than a direct scalar (nested, collection, array) is banned outright, no cure.
- **[`Primitives.Ingestion`](src/Primitives.Ingestion)** — [`ITabularReader`](src/Primitives.Ingestion/ITabularReader.cs)/[`SepTabularReader`](src/Primitives.Ingestion/SepTabularReader.cs)/[`ExcelTabularReader`](src/Primitives.Ingestion/ExcelTabularReader.cs): the tabular-reader abstraction Mímisbrunnr's `SeedTool` consumes to convert raw CSV/Excel sources into committed TSV seed data.
- **[Architecture Analyzers](gen/Architecture.Analyzers)** — the Law of the Realms (NORSE070–073, plus the NORSE079 meta-strike): compiler-enforced realm dependency doctrine. Wire-format machinery stays in Midgard/Yggdrasil. Midgard publishes no surface. Components are platform-free RCLs. Cross-realm reach flows through published surfaces. All NotConfigurable errors, and NORSE079 convicts any `[SuppressMessage]` that names them. Standalone analyzer delivered platform-wide; violations are build errors.

Scalar → domain conversion only: application error categories and transport conditions belong to other realms by design.

## Build and test

```shell
dotnet build Svartalfheim.slnx   # warnings are errors — a single warning fails
dotnet test Svartalfheim.slnx    # xUnit v3 + Shouldly on Microsoft.Testing.Platform
```

Requires the .NET 11 preview SDK pinned by `global.json`. The realm builds standalone — it is its own clone target, not only a Bifröst submodule.

Evidence rigs: [`benchmarks/Primitives.Benchmarks`](benchmarks/Primitives.Benchmarks) (BenchmarkDotNet — storage, dispatch, and combinator cost, run manually in Release) and [`tests/smoke/Primitives.Aot.Smoke`](tests/smoke/Primitives.Aot.Smoke) (the pathway must survive `PublishAot` with zero warnings and exit 0; needs the VS C++ build tools).

## The naming law

Project folders and `.csproj` files are brand-free (`src/Primitives/Primitives.csproj`); the realm's root `Directory.Build.props` injects `AssemblyName` and `RootNamespace` as `Norse.$(MSBuildProjectName)`. Fork it, change `Norse` once, and every build output carries your brand — the `namespace Norse.*` declarations in code are yours to cull deliberately, with no filesystem change either way.

## The cosmos

Svartálfheim is one realm of the [Norse Architecture](https://github.com/NorseArchitecture). The whole platform composes at [Bifröst](https://github.com/NorseArchitecture/Bifrost) — clone once, cross the bridge. Every design is tried in [Glitnir](https://github.com/NorseArchitecture/Glitnir), the design court, before code is forged here; this realm's specs and plans live in the court's [docs/Svartálfheim/](https://github.com/NorseArchitecture/Glitnir/tree/master/docs/Svartalfheim).

## Soundtrack: Svartálfheim | God of War Ragnarök (Original Soundtrack)
[![Soundtrack: Svartálfheim | God of War Ragnarök (Original Soundtrack)](https://img.youtube.com/vi/BCk4E0me2GE/maxresdefault.jpg)](https://www.youtube.com/watch?v=BCk4E0me2GE)

