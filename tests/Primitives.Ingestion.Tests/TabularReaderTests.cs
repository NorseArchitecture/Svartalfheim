using System.Data;
using Sylvan.Data.Excel;

namespace Norse.Primitives.Ingestion.Tests;

public sealed class TabularReaderTests
{
	[Fact]
	void OpenDelimited_reads_a_delimited_file()
	{
		var path = WriteTempFile("Name,Code\nNigeria,566\nAlgeria,012\n");
		try
		{
			// ReSharper disable once SuggestVarOrType_SimpleTypes
			using ITabularReader reader = TabularReader.OpenDelimited(path, ',');

			reader.FieldCount.ShouldBe(2);
			reader.Read().ShouldBeTrue();
			reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
			reader[0].ToString().ShouldBe("Nigeria");
			reader[1].ToString().ShouldBe("566");

			reader.Read().ShouldBeTrue();
			reader[reader.Ordinal("Code")].ToString().ShouldBe("012");

			reader.Read().ShouldBeFalse();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	void OpenExcelWorksheet_reads_the_first_worksheet()
	{
		var path = WriteTempWorkbook();
		try
		{
			// ReSharper disable once SuggestVarOrType_SimpleTypes
			using ITabularReader reader = TabularReader.OpenExcelWorksheet(path);

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

	static string WriteTempFile(string content)
	{
		var path = Path.GetTempFileName();
		File.WriteAllText(path, content);
		return path;
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
