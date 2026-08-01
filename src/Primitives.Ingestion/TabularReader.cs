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

	/// <summary>Opens a delimited stream (e.g. CSV, TSV) for forward-only reading.</summary>
	/// <param name="stream">The delimited content stream.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	/// <param name="leaveOpen">When <see langword="false"/> (the default), the returned reader disposes <paramref name="stream"/> along with itself — set <see langword="true"/> when the caller (or a framework above it) owns the stream's lifetime instead.</param>
	/// <returns>An <see cref="ITabularReader"/> over the stream.</returns>
	public static ITabularReader OpenDelimited(Stream stream, char separator, bool leaveOpen = false) =>
		new SepTabularReader(stream, separator, leaveOpen);

	/// <summary>Opens delimited content already in memory (e.g. CSV, TSV) for forward-only reading.</summary>
	/// <param name="contents">The delimited content bytes.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	/// <returns>An <see cref="ITabularReader"/> over the content.</returns>
	public static ITabularReader OpenDelimited(byte[] contents, char separator) =>
		new SepTabularReader(contents, separator);

	/// <summary>Opens the first worksheet of an Excel workbook for forward-only reading.</summary>
	/// <param name="path">The workbook's path (.xlsx, .xlsb, or .xls).</param>
	/// <returns>An <see cref="ITabularReader"/> over the worksheet.</returns>
	public static ITabularReader OpenExcelWorksheet(string path) =>
		new ExcelTabularReader(path);

	/// <summary>Opens the first worksheet of an Excel workbook stream for forward-only reading.</summary>
	/// <param name="stream">The workbook content stream.</param>
	/// <param name="fileName">A name carrying the workbook's extension (.xlsx, .xlsb, or .xls) — only <see cref="Path.GetExtension(string)"/> is consulted, so a synthetic name is fine.</param>
	/// <param name="leaveOpen">When <see langword="false"/> (the default), the returned reader disposes <paramref name="stream"/> along with itself — set <see langword="true"/> when the caller (or a framework above it) owns the stream's lifetime instead.</param>
	/// <returns>An <see cref="ITabularReader"/> over the worksheet.</returns>
	/// <exception cref="NotSupportedException"><paramref name="fileName"/>'s extension is not a recognized workbook format.</exception>
	public static ITabularReader OpenExcelWorksheet(Stream stream, string fileName, bool leaveOpen = false) =>
		new ExcelTabularReader(stream, fileName, leaveOpen);

	/// <summary>Opens the first worksheet of an Excel workbook already in memory for forward-only reading.</summary>
	/// <param name="contents">The workbook content bytes.</param>
	/// <param name="fileName">A name carrying the workbook's extension (.xlsx, .xlsb, or .xls) — only <see cref="Path.GetExtension(string)"/> is consulted, so a synthetic name is fine.</param>
	/// <returns>An <see cref="ITabularReader"/> over the worksheet.</returns>
	/// <exception cref="NotSupportedException"><paramref name="fileName"/>'s extension is not a recognized workbook format.</exception>
	public static ITabularReader OpenExcelWorksheet(byte[] contents, string fileName) =>
		new ExcelTabularReader(contents, fileName);
}
