# Norse.Primitives.Ingestion

Norse forward-only tabular ingestion: a canonical `ITabularReader` contract over Sep (delimited) and Sylvan.Data.Excel (single-sheet Excel), for turning untrusted source files into cell spans. Scalar-value conversion of those spans into typed values is `Norse.Primitives`' job, composed by the caller — this project carries no dependency on it.

Native AOT: Sep is fully AOT-clean. `ExcelTabularReader`/Sylvan.Data.Excel publish under Native AOT with one documented, suppressed trim-analysis finding (IL2093), inherent to Sylvan.Data.Excel 0.5.6 itself and not this project's own code, pending an upstream fix.

Part of the [Norse Architecture](https://github.com/NorseArchitecture) platform.
