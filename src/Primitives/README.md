# Norse.Primitives

Norse forged primitives: the `Result<T>` discriminated union, closed parse-failure vocabulary, and hot-path scalar parsers for every boundary crossing into the Norse ecosystem from untrusted sources. Also carries `Identifiers` — `SequentialGuid`/`DeterministicGuid` (time-ordered and content-addressed GUID generation), `GuidVersionBits`/`GuidByteOrder`, and the `INorseGuid` contract. `Pii` governs PII scalars — `EmailAddress`, `PhoneNumber`, `PersonalName`, `BirthDate` — masked by default, plus the compile-time NORSE061/NORSE062 retention-policy analyzer.

Part of the [Norse Architecture](https://github.com/NorseArchitecture) platform.
