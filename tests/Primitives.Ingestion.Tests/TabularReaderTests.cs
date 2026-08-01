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
	void OpenDelimited_reads_a_delimited_stream()
	{
		using MemoryStream stream = new("Name,Code\nNigeria,566\nAlgeria,012\n"u8.ToArray());
		// ReSharper disable once SuggestVarOrType_SimpleTypes
		using ITabularReader reader = TabularReader.OpenDelimited(stream, ',');

		reader.FieldCount.ShouldBe(2);
		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
		reader[0].ToString().ShouldBe("Nigeria");
		reader[1].ToString().ShouldBe("566");

		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Code")].ToString().ShouldBe("012");

		reader.Read().ShouldBeFalse();
	}

	[Fact]
	void OpenDelimited_disposes_the_stream_by_default()
	{
		MemoryStream stream = new("Name,Code\nNigeria,566\n"u8.ToArray());
		using (TabularReader.OpenDelimited(stream, ','))
		{ }

		stream.CanRead.ShouldBeFalse();
	}

	[Fact]
	void OpenDelimited_leaves_the_stream_open_when_requested()
	{
		using MemoryStream stream = new("Name,Code\nNigeria,566\n"u8.ToArray());
		using (TabularReader.OpenDelimited(stream, ',', leaveOpen: true))
		{ }

		stream.CanRead.ShouldBeTrue();
	}

	[Fact]
	void OpenDelimited_reads_delimited_bytes()
	{
		var contents = "Name,Code\nNigeria,566\nAlgeria,012\n"u8.ToArray();
		// ReSharper disable once SuggestVarOrType_SimpleTypes
		using ITabularReader reader = TabularReader.OpenDelimited(contents, ',');

		reader.FieldCount.ShouldBe(2);
		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
		reader[0].ToString().ShouldBe("Nigeria");
		reader[1].ToString().ShouldBe("566");

		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Code")].ToString().ShouldBe("012");

		reader.Read().ShouldBeFalse();
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

	[Fact]
	void OpenExcelWorksheet_reads_a_workbook_stream()
	{
		using MemoryStream stream = new(WriteWorkbookBytes());
		// ReSharper disable once SuggestVarOrType_SimpleTypes
		using ITabularReader reader = TabularReader.OpenExcelWorksheet(stream, "workbook.xlsx");

		reader.FieldCount.ShouldBe(2);
		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
		reader[reader.Ordinal("Code")].ToString().ShouldBe("566");

		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Algeria");

		reader.Read().ShouldBeFalse();
	}

	[Fact]
	void OpenExcelWorksheet_disposes_the_stream_by_default()
	{
		MemoryStream stream = new(WriteWorkbookBytes());
		using (TabularReader.OpenExcelWorksheet(stream, "workbook.xlsx"))
		{ }

		stream.CanRead.ShouldBeFalse();
	}

	[Fact]
	void OpenExcelWorksheet_leaves_the_stream_open_when_requested()
	{
		using MemoryStream stream = new(WriteWorkbookBytes());
		using (TabularReader.OpenExcelWorksheet(stream, "workbook.xlsx", leaveOpen: true))
		{ }

		stream.CanRead.ShouldBeTrue();
	}

	[Fact]
	void OpenExcelWorksheet_reads_workbook_bytes()
	{
		var contents = WriteWorkbookBytes();
		// ReSharper disable once SuggestVarOrType_SimpleTypes
		using ITabularReader reader = TabularReader.OpenExcelWorksheet(contents, "workbook.xlsx");

		reader.FieldCount.ShouldBe(2);
		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Nigeria");
		reader[reader.Ordinal("Code")].ToString().ShouldBe("566");

		reader.Read().ShouldBeTrue();
		reader[reader.Ordinal("Name")].ToString().ShouldBe("Algeria");

		reader.Read().ShouldBeFalse();
	}

	[Fact]
	void OpenExcelWorksheet_throws_when_the_file_name_extension_is_unrecognized()
	{
		using MemoryStream stream = new(WriteWorkbookBytes());
		Should.Throw<NotSupportedException>(() => TabularReader.OpenExcelWorksheet(stream, "workbook.docx"));
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
		File.WriteAllBytes(path, WriteWorkbookBytes());
		return path;
	}

	static byte[] WriteWorkbookBytes()
	{
		using DataTable table = new();
		table.Columns.Add("Name", typeof(string));
		table.Columns.Add("Code", typeof(string));
		table.Rows.Add("Nigeria", "566");
		table.Rows.Add("Algeria", "012");

		using MemoryStream stream = new();
		using (var writer = ExcelDataWriter.Create(stream, ExcelWorkbookType.ExcelXml))
			writer.Write(table.CreateDataReader(), "Sheet1");

		return stream.ToArray();
	}
}
