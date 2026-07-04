using System.Data;
using Sylvan.Data.Excel;

namespace Norse.Primitives.Ingestion.Tests;

public sealed class ExcelTabularReaderTests
{
	[Fact]
	void Read_exposes_cells_by_ordinal_and_by_name()
	{
		var path = WriteTempWorkbook();
		try
		{
			using ITabularReader reader = new ExcelTabularReader(path);

			reader.FieldCount.ShouldBe(2);
			reader.Read().ShouldBeTrue();
			reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
			reader[reader.Ordinal("Code")].ToString().ShouldBe("566");

			reader.Read().ShouldBeTrue();
			reader[reader.Ordinal("Name")].ToString().ShouldBe("Algeria");

			reader.Read().ShouldBeFalse();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	void Read_throws_on_a_corrupt_workbook()
	{
		var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
		File.WriteAllText(path, "this is not a real xlsx file");
		try
		{
			Should.Throw<Exception>(() =>
			{
				using ITabularReader reader = new ExcelTabularReader(path);
				reader.Read();
			});
		}
		finally
		{
			File.Delete(path);
		}
	}

	static string WriteTempWorkbook()
	{
		var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
		using DataTable table = new();
		table.Columns.Add("Name", typeof(string));
		table.Columns.Add("Code", typeof(string));
		table.Rows.Add("Nigeria", "566");
		table.Rows.Add("Algeria", "012");

		using var writer = ExcelDataWriter.Create(path);
		writer.Write(table.CreateDataReader(), "Sheet1");

		return path;
	}
}
