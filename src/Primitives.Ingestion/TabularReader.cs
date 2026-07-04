namespace Norse.Primitives.Ingestion;

/// <summary>
/// Opens an <see cref="ITabularReader"/> over a delimited file or a single Excel worksheet.
/// The concrete reader types are internal — this is the only supported way to construct one.
/// </summary>
public static class TabularReader
{
	/// <summary>Opens a delimited file (e.g. CSV, TSV) for forward-only reading.</summary>
	/// <param name="path">The delimited file's path.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	/// <returns>An <see cref="ITabularReader"/> over the file.</returns>
	public static ITabularReader OpenDelimited(string path, char separator) =>
		new SepTabularReader(path, separator);

	/// <summary>Opens the first worksheet of an Excel workbook for forward-only reading.</summary>
	/// <param name="path">The workbook's path (.xlsx, .xlsb, or .xls).</param>
	/// <returns>An <see cref="ITabularReader"/> over the worksheet.</returns>
	public static ITabularReader OpenExcelWorksheet(string path) =>
		new ExcelTabularReader(path);
}
