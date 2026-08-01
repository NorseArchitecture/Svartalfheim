using System.Globalization;
using Sylvan.Data.Excel;

namespace Norse.Primitives.Ingestion;

/// <summary>
/// An <see cref="ITabularReader"/> over a single Excel worksheet, backed by Sylvan.Data.Excel.
/// Reads from a file path, an already-open <see cref="Stream"/>, or an in-memory byte array —
/// the same three input shapes <see cref="SepTabularReader"/> accepts. Opens the first worksheet
/// for forward-only reading.
/// </summary>
/// <remarks>
/// Unlike <see cref="SepTabularReader"/>, cell access here is not zero-allocation: Excel
/// stores cells as typed values (numeric, date, boolean, text), not as slices of a flat
/// character stream, so each cell's text is materialized as a <see cref="string"/> before
/// this type exposes it as a span. This is a documented asymmetry, not a defect.
/// </remarks>
sealed class ExcelTabularReader : ITabularReader
{
	readonly ExcelDataReader _reader;

	/// <summary>Opens the first worksheet of <paramref name="path"/> for forward-only reading; workbook type is inferred from the extension.</summary>
	/// <param name="path">The workbook's path (.xlsx, .xlsb, or .xls).</param>
	internal ExcelTabularReader(string path) :
		this(() => ExcelDataReader.Create(path))
	{ }

	/// <summary>Opens the first worksheet of <paramref name="stream"/> for forward-only reading; workbook type is inferred from <paramref name="fileName"/>'s extension.</summary>
	/// <param name="stream">The workbook content stream.</param>
	/// <param name="fileName">A name carrying the workbook's extension — only the extension is consulted.</param>
	/// <param name="leaveOpen">When <see langword="false"/>, disposing this reader also disposes <paramref name="stream"/>.</param>
	/// <exception cref="NotSupportedException"><paramref name="fileName"/>'s extension is not a recognized workbook format.</exception>
	internal ExcelTabularReader(Stream stream, string fileName, bool leaveOpen) :
		this(() => ExcelDataReader.Create(stream, WorkbookTypeFor(fileName), new ExcelDataReaderOptions { OwnsStream = !leaveOpen }))
	{ }

	/// <summary>Opens the first worksheet of <paramref name="contents"/> for forward-only reading; workbook type is inferred from <paramref name="fileName"/>'s extension.</summary>
	/// <param name="contents">The workbook content bytes.</param>
	/// <param name="fileName">A name carrying the workbook's extension — only the extension is consulted.</param>
	/// <exception cref="NotSupportedException"><paramref name="fileName"/>'s extension is not a recognized workbook format.</exception>
	internal ExcelTabularReader(byte[] contents, string fileName) :
		this(new MemoryStream(contents, writable: false), fileName, leaveOpen: false)
	{ }

	ExcelTabularReader(Func<ExcelDataReader> factory) =>
		_reader = factory();

	static ExcelWorkbookType WorkbookTypeFor(string fileName) =>
		Path.GetExtension(fileName).ToLowerInvariant() switch
		{
			".xlsx" => ExcelWorkbookType.ExcelXml,
			".xls" => ExcelWorkbookType.Excel,
			".xlsb" => ExcelWorkbookType.ExcelBinary,
			var extension => throw new NotSupportedException($"'{extension}' is not a recognized Excel workbook extension."),
		};

	public int FieldCount =>
		_reader.FieldCount;

	public int Ordinal(string headerName) =>
		_reader.GetOrdinal(headerName);

	public bool Read() =>
		_reader.Read();

	public ReadOnlySpan<char> this[int ordinal]
	{
		get
		{
			if (_reader.IsDBNull(ordinal))
				return [];
			var value = _reader.GetValue(ordinal);
			var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
			return text.AsSpan();
		}
	}

	public void Dispose() =>
		_reader.Dispose();
}
