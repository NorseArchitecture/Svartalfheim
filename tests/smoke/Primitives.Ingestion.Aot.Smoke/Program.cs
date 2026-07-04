using System.Data;
using Norse.Primitives.Ingestion;
using Sylvan.Data.Excel;

var failures = 0;
var tempDir = Directory.CreateTempSubdirectory("norse-ingestion-smoke");
try
{
	var csvPath = Path.Combine(tempDir.FullName, "smoke.csv");
	File.WriteAllText(csvPath, "Name,Code\nNigeria,566\nAlgeria,012\n");

#pragma warning disable CA1859 // deliberately exercised through ITabularReader, not the concrete reader - that abstraction surviving native compilation is this smoke test's whole point.
	Check("SepTabularReader reads a delimited row by name and by ordinal", () =>
	{
		using ITabularReader reader = new SepTabularReader(csvPath, ',');
		return reader.Read()
			&& reader[reader.Ordinal("Name")].SequenceEqual("Nigeria")
			&& reader[0].SequenceEqual("Nigeria")
			&& reader.Read()
			&& reader[reader.Ordinal("Code")].SequenceEqual("012")
			&& !reader.Read();
	});
#pragma warning restore CA1859

	var xlsxPath = Path.Combine(tempDir.FullName, "smoke.xlsx");
	using DataTable table = new();
	table.Columns.Add("Name", typeof(string));
	table.Columns.Add("Code", typeof(string));
	table.Rows.Add("Nigeria", "566");
	table.Rows.Add("Algeria", "012");
	using (var excelWriter = ExcelDataWriter.Create(xlsxPath))
		excelWriter.Write(table.CreateDataReader(), "Sheet1");

#pragma warning disable CA1859 // see rationale above
	Check("ExcelTabularReader reads a single sheet forward-only", () =>
	{
		using ITabularReader reader = new ExcelTabularReader(xlsxPath);
		return reader.Read()
			&& reader[reader.Ordinal("Name")].SequenceEqual("Nigeria")
			&& reader.Read()
			&& reader[reader.Ordinal("Code")].SequenceEqual("012")
			&& !reader.Read();
	});
#pragma warning restore CA1859
}
finally
{
	tempDir.Delete(recursive: true);
}

if (failures > 0)
{
	Console.Error.WriteLine($"AOT smoke FAILED: {failures} check(s) failed.");
	return 1;
}

Console.WriteLine("AOT smoke passed: Sep and Sylvan.Data.Excel survive native compilation.");
return 0;

void Check(string description, Func<bool> probe)
{
	bool passed;
	try
	{
		passed = probe();
	}
	catch (Exception exception)
	{
		Console.Error.WriteLine($"FAIL {description}: {exception}");
		failures++;
		return;
	}
	if (passed)
	{
		Console.WriteLine($"ok   {description}");
	}
	else
	{
		Console.Error.WriteLine($"FAIL {description}");
		failures++;
	}
}
