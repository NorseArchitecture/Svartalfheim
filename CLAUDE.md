# CLAUDE.md — Svartálfheim (`Norse.Primitives`)

## 0. Wrong Root — Halt

Session root must be **Bifröst**, never this repo. Org-wide settings (`superpowers`, permission rules) only apply from the actual root, and Claude Code never merges a submodule's own `.claude/settings.json` into a parent-launched session. If `claude` was launched inside Svartálfheim: stop — don't read further, don't propose changes, don't run anything — tell the user to `cd ../Bifrost` and start there. (A `SessionStart` hook should block this before you ever see this file; if you're reading this anyway, halt regardless.)

> **Never commit, push, or rewrite git history** — stage (`git add`), show the diff, stop; the human commits. This holds even when a skill's flow includes a commit step. **US English spelling** everywhere — code, comments, docs, commits.

## What This Realm Is

The forge — `Norse.Primitives`, the platform's foundational realm. Everything crossing a trust boundary into the ecosystem from an untrusted source flows through the types here. **Scalar→domain conversion only:** application error categories (validation/not-found/conflict) belong to the mediator; transport conditions to the host pipeline. `Result<T>`'s relationship to Asgard's `Outcome<T>` — same shape, opposite purpose, never siblings with style drift — is platform doctrine: `../Glitnir/docs/the-two-unions.md`.

Three capability families:

- `src/Primitives` — `Result<T>` and `ParseFailure`, the `Parser` gateway, the scalar-parser specialists (boolean, numeric, `char`, `Guid`, temporal, `TimeZoneParser` + `TemporalFusion`), `Identifiers` (`SequentialGuid`/`DeterministicGuid`), and `Pii` (masked-by-default PII scalars).
- `src/Primitives.Ingestion` — `ITabularReader`/`SepTabularReader`/`ExcelTabularReader`, the tabular-reader abstraction Mímisbrunnr's seed tooling consumes.
- `gen/` — `Primitives.Analyzers` (NORSE060–062) and `Architecture.Analyzers` (NORSE070–079), each with a `tests/` sibling.

**Spec index** — under `../Glitnir/docs/Svartalfheim/specs/` unless noted; execution plans sit beside each spec under `plans/`. **Amendments ARE the record — trust them over the parent spec where they differ.**

| Subject | Spec |
|---|---|
| Parent design | `2026-05-20-svartalfheim-primitives-design.md` |
| `Result<T>` + `BooleanParser` | `2026-06-11-svartalfheim-result-union-boolean-parser-design.md` |
| Gateway, combinators, evidence rigs | `2026-06-11-svartalfheim-pathway-proof-design.md` (benchmark findings §8, deferral ledger §7) |
| Numeric, `char`, `Guid` | `2026-06-17-svartalfheim-numeric-char-guid-parsers-design.md` |
| Temporal fusion | `2026-06-17-svartalfheim-temporal-fusion-design.md` |
| Identifiers | `2026-07-03-svartalfheim-identifiers-design.md` |
| Pii + retention analyzers | `../Glitnir/docs/Platform/specs/2026-08-03-pii-primitives-identity-erasure-seam-design.md` |
| Law of the Realms | `../Glitnir/docs/Platform/specs/2026-08-03-realm-dependency-law-compiler-enforcement-design.md` |
| HyperUuid/HyperCast ingestion | `2026-09-03-hyperuuid-hypercast-ingestion-design.md` |

Prior art (the pre-union `Result<T>`, parser vocabulary, and test matrices this realm learned from) is private and lives outside this workspace by design — don't go looking for it.

## Build & Test

- `dotnet build Svartalfheim.slnx` — warnings are errors (WarningLevel 9999, EnforceCodeStyleInBuild); a single warning fails.
- `dotnet test Svartalfheim.slnx` — xUnit v3 + Shouldly on Microsoft.Testing.Platform. **VSTest `--filter` does NOT work** — use `dotnet test tests/Primitives.Tests -- --filter-class "*.ResultTests"`.
- **NEVER `dotnet test` a test project containing zero tests** — xUnit v3 fails the run.
- SDK pinned by `global.json`: `11.0.100-` prerelease, rollForward latestFeature; `LangVersion=preview`.
- Benchmarks (manual, Release): `dotnet run -c Release --project benchmarks/Primitives.Benchmarks -- --filter *`. Findings are court filings — file them as amendments to the pathway spec in Glitnir, never as loose notes.
- AOT smoke: `dotnet publish tests/smoke/Primitives.Aot.Smoke -c Release`, then run the native exe — zero AOT warnings and exit 0 required. Needs the VS "Desktop development with C++" workload on Windows.
- **Corpus conformance runs twice** — `dotnet test tests/Primitives.Tests -- --filter-class "*.CorpusConformanceTests"` exercises both the native and (`NativeCapability.ForManagedOnly`-forced) managed paths against HyperCast's own corpus in the same run. Every dev/CI box available today is native-capable, so this is the only thing that keeps the managed fallback proven rather than dead code until a real MAUI target exists.

## Architecture Facts (decided — do not re-litigate)

- **The Law of the Realms (NORSE070–073 + NORSE079)** — `gen/Architecture.Analyzers` ships `Norse.Architecture.Analyzers`, a standalone build-time analyzer enforcing realm dependency doctrine platform-wide (delivered via Ginnungagap scatter; jurisdiction derived entirely from assembly names — brand-blind, family-inferred). Five strikes, all `NotConfigurable` errors: wire format outside Midgard/Yggdrasil (NORSE070); Midgard taken as a dependency (NORSE071); cross-realm reach outside published surfaces (NORSE072); component impurity (NORSE073); and the meta-strike — a `[SuppressMessage]` naming any NORSE07x rule is itself a conviction (NORSE079). Statutes: the Law of the Realms spec (index above).
- **The NORSE ledger** — NORSE060–069 is Svartálfheim's block: NORSE060 — `Result<T>` reachable in a `[ServiceContract]` response; NORSE061/NORSE062 — the compile-time PII retention gate (no `[RetentionPolicy]`, and PII not a direct scalar, respectively); NORSE063 reserved for a future generic decrypted-PII query surface. NORSE070–079 is architecture law.
- **`Result<T>` is a hand-authored C# 15 custom union**, not a shorthand `union` declaration — shorthand declarations store `object?` and box every value-type case; the custom pattern (`[Union]` readonly record struct + per-case public ctors + `Value` + `HasValue`/`TryGetValue`) keeps both cases inline, zero boxing on both paths (IL-verified).
- **`result is Result<T>` is a compile error (CS8121)** — union patterns unwrap to contents. Match `Success<T>` / `Failure`; a two-arm switch is exhaustive without a discard.
- **`default(Result<T>)` / `default(Failure)` are malformed by construction** (the `ImmutableArray<T>` footgun class) — XML-documented, canary-test-pinned. `Result(Failure)` throws on a smuggled `Unspecified` sentinel.
- **Truncation knowledge lives in `Failure` alone** — parsers pass their trimmed `ReadOnlySpan<char>` to the span ctor overload, which bounds to `MaxInputLength` (256) before allocating. Never pre-truncate in a parser.
- **Parser template** (BooleanParser is the precedent): static class, `ParseRequired(ReadOnlySpan<char>) → Result<T>` + `ParseOptional(ReadOnlySpan<char>) → Result<T>?`, shared private `Parse`, honest signatures — no `IFormatProvider` on culture-insensitive parsers (a parameter documented as ignored is a lie). Empty→`ParseFailure.Empty`/absent; unrecognized→`Malformed`. **No implicit `string → Result<T>` conversions, ever** — parsing is an explicit named call.
- **`ParseFailure` is closed** (Unspecified sentinel / Empty / Malformed / OutOfRange / Duplicate) — adding a member is a deliberate breaking change. `Duplicate` was added 2026-08-09 (a token individually valid but repeated where each may appear once; first consumer is flags-enum array parsing in Midgard's text channels). `OutOfRange` was added 2026-09-03 (a scalar token that is individually valid but fails grammar-range constraints; first consumer is HyperCast-sourced range-vs-grammar failures in Identifiers and scalar parsers).
- **`Parser` is the generic gateway** over `where T : notnull, ISpanParsable<T>` — `bool` routes to `BooleanParser` via a JIT-eliminated `typeof` branch (`Unsafe.As` identity reinterpret, sound because `T` is statically `bool` inside the branch; benchmark-verified at ratio 1.02); everything else goes through `T.TryParse(span, provider)`. **The provider is required and non-nullable — no defaulting overload, ever.** No runtime registry: a type that cannot parse does not compile.
- **The two-unions doctrine is live on the wire, not prospective** — Heimdall's `RegisterRequest` (`AuthN.Services`, the shipped gRPC contract assembly) carries `Result<EmailAddress> EmailParsed`, hydrated by the `Email` setter on every assignment including protobuf-net deserialization; the union itself is never serialized (no `[DataMember]`) — the raw string rides the wire and deserialization is the parse event: the verdict exists by construction the moment the request does, never deferred to a handler. That is `Result<T>`'s true purpose on a wire request — parsing untrusted scalars at the boundary crossing itself. NORSE060 (`gen/Primitives.Analyzers/ResultInServiceResponseAnalyzer.cs`) polices the opposite direction: `Result<T>` reachable from a `[ServiceContract]` response is a build error — responses are `Outcome<T>` territory, erased at the edge.
- **Combinators are `Map`/`Bind`/`Match` only** (law-proving core; `Combine`/async/`*Present` wait for a consumer — see Deferred Increments). Instance methods are union switches — a defaulted `Result<T>` throws `SwitchExpressionException` through them identically to a hand-written switch. The five functor/monad laws are FsCheck-pinned in `ResultLawTests` (portable `Prop.ForAll` style, no integration package). Measured tax: ~2.8× a hand-rolled switch, zero allocation — ergonomics, not the hot path.
- **The native-engine seam (`NativeCapability`)** — `Identifiers` and the scalar parsers route to HyperUuid/HyperCast on platforms/RIDs they cover (a trimmer-foldable `OperatingSystem` check plus a cached native probe for RID-family gaps like glibc vs. musl), falling back to the original managed implementation everywhere else, including the not-yet-existing MAUI target. Public API is unchanged; translation from `Verdict<T>`/`Fault` to `Result<T>`/`Failure` happens at the call site. HyperCast is the source of truth for parsing grammar — its `corpus/*.json` vectors are the cross-engine, cross-platform conformance authority, run in CI against both engines via `NativeCapability.ForManagedOnly`. Design: `2026-09-03-hyperuuid-hypercast-ingestion-design.md`.

## Toolchain Gotchas (.NET 11 preview 5)

- The runtime SHIPS `UnionAttribute`/`IUnion` — the ref pack's XML doc file omits them (false negative). Probe ref assemblies, not XML docs. Never re-add local polyfills (CS0436).
- xUnit v3 on MTP requires `<OutputType>Exe</OutputType>` on test projects (lives in `tests/Directory.Build.props`).
- IDE0005 in build mode requires `GenerateDocumentationFile=true` — tests keep doc generation on and suppress CS1591 instead. Don't invert that trade.
- **ReSharper/Rider do not understand C# 15 unions** (even EAP builds) — union-related squiggles are visual noise. The compiler is the truth; never alter working union code to appease R#.
- **BenchmarkDotNet 0.15.x does not recognize the net11.0 preview moniker** — the default out-of-process toolchain crashes in SDK validation. The benchmarks bake `InProcessEmitToolchain` into their config; revisit when BDN learns .NET 11.
- **.NET 11 escape analysis stack-allocates single-frame boxes** — a micro-benchmark that constructs and consumes a boxing type in one frame reports 0 B allocated. Force escape with `[MethodImpl(MethodImplOptions.NoInlining)]` factory boundaries when the design question is about values crossing method boundaries.

## House Style

- **Project names are brand-free** (`src/Primitives/Primitives.csproj`); root `Directory.Build.props` injects `AssemblyName`/`RootNamespace` as `Norse.$(MSBuildProjectName)`. A fork rebrands by editing that one file — build derivatives (`InternalsVisibleTo` via `$(AssemblyName)`) follow automatically; `namespace Norse.*` declarations in code stay until the fork deliberately culls them.
- Tabs (except YAML/MD per .editorconfig) · `var` for returns, explicit type + `new()` for construction · omit default accessibility modifiers · XML docs mandatory on all public src members (CS1591 is an error in src) · test classes `public sealed`, test methods omit access modifiers · Shouldly/Xunit usings are global (injected via tests props — never add them per-file).
- **Test naming — two forms, chosen by what's under test:** `Should_{behavior}_when_{condition}` for scalar-parser tests (one input→outcome pair; `Primitives.Tests`). `{Action}_{observed_behavior}` for stateful cursor/reader tests (`Primitives.Ingestion.Tests`, e.g. `Read_throws_on_a_corrupt_workbook`). Match the project's existing files; never mix forms within one test class.

## Deferred Increments (pathway-proof spec §7 ledger — in rough order)

Seven increments are landed — `Result<T>`+`BooleanParser`, gateway+combinators, numeric/`char`/`Guid`, temporal parsers, `TimeZoneParser`+`TemporalFusion`, `Identifiers`, `Pii` — specs in the index above. Each future increment is spec-first (brainstorm → spec → plan → code, explicit human greenlight at every transition), and each deferred item below waits for a real consumer:

1. **`Combine`, async combinator siblings, `*Present` variants** — the fusion consumer proved fusion-with-a-failing-combiner is not `Combine`; the deferral stands. Pathway spec §7.
2. **Caller-declared DST resolution door** (`Earliest`/`Latest`/`Reject`) — off-default `TemporalFusion` door for callers that legitimately resolve an ambiguous wall-clock by policy rather than re-prompt. Temporal-fusion spec §11.
3. **Pre-parsed fusion overload** — `Fuse(DateOnly, TimeOnly, TimeZoneInfo)` for ingress paths whose fields arrive in a declared non-ISO format and were parsed through `ParseExact` doors first. §11 ibid.
4. **Collect-all failure aggregation** — when an ingress path genuinely needs every bad field reported at once; likely a validation-layer concern above the forge. §11 ibid.
5. **NuGet packaging metadata** — when something consumes the package.

Implementation is subagent-orchestrated and test-driven, always: every plan's REQUIRED SUB-SKILL line names `superpowers:subagent-driven-development` (the default; `superpowers:executing-plans` is the narrow separate-session fallback) paired with `superpowers:test-driven-development`. Full rule: `../Glitnir/CLAUDE.md` §2.8.
