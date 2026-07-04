namespace Norse.Primitives.Ingestion.Tests;

public sealed class SepTabularReaderTests
{
	[Fact]
	void Read_exposes_cells_by_ordinal_and_by_name()
	{
		var path = WriteTempFile("Name,Code\nNigeria,566\nAlgeria,012\n");
		try
		{
			using ITabularReader reader = new SepTabularReader(path, ',');

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
	void Read_honors_a_custom_separator()
	{
		var path = WriteTempFile("Name\tCode\nNigeria\t566\n");
		try
		{
			using ITabularReader reader = new SepTabularReader(path, '\t');

			reader.Read().ShouldBeTrue();
			reader[reader.Ordinal("Code")].ToString().ShouldBe("566");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	void Read_throws_on_a_structurally_malformed_row()
	{
		var path = WriteTempFile("Name,Code\nNigeria,566,extra\n");
		try
		{
			using ITabularReader reader = new SepTabularReader(path, ',');

			Should.Throw<Exception>(() => reader.Read());
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
}
