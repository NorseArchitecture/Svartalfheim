# Norse.Primitives

Norse forged primitives: the `Result<T>` discriminated union, its closed parse-failure vocabulary (`Empty`, `Malformed`, `OutOfRange`, `Duplicate`), and the `Parser` gateway every boundary crossing into the Norse ecosystem from an untrusted source goes through. The gateway routes to [HyperCast](https://github.com/SkunkWerkx/HyperCast)'s doors — the grammar is HyperCast's, proven by its corpus — and falls through to `ISpanParsable<T>` for everything else. Also carries `Identifiers` — `SequentialGuid`/`DeterministicGuid` (time-ordered and content-addressed GUID generation over [HyperUuid](https://github.com/SkunkWerkx/HyperUuid)), `GuidByteOrder`, and the `INorseGuid` contract — and `Pii`, the masked-by-default PII scalars `EmailAddress`, `PhoneNumber`, `PersonalName`, `BirthDate`, plus the compile-time NORSE061/NORSE062 retention-policy analyzer.

Part of the [Norse Architecture](https://github.com/NorseArchitecture) platform.
