# Norse.Primitives.Ingestion

Norse forward-only tabular ingestion: a canonical `ITabularReader` contract over Sep (delimited) and Sylvan.Data.Excel (single-sheet Excel), for turning untrusted source files into cell spans. Scalar-value conversion of those spans into typed values is `Norse.Primitives`' job, composed by the caller — this project carries no dependency on it.

Native AOT: Sep and Sylvan.Data.Excel (0.5.8+) are both fully AOT-clean.

Part of the [Norse Architecture](https://github.com/NorseArchitecture) platform.
