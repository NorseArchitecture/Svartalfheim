using System.Globalization;
using Sylvan.Data.Excel;

namespace Norse.Primitives.Ingestion;

/// <summary>
/// An <see cref="ITabularReader"/> over a single Excel worksheet, backed by Sylvan.Data.Excel.
/// Opens the first worksheet of <paramref name="path"/> for forward-only reading.
/// </summary>
/// <param name="path">The workbook's path (.xlsx, .xlsb, or .xls).</param>
/// <remarks>
/// Unlike <see cref="SepTabularReader"/>, cell access here is not zero-allocation: Excel
/// stores cells as typed values (numeric, date, boolean, text), not as slices of a flat
/// character stream, so each cell's text is materialized as a <see cref="string"/> before
/// this type exposes it as a span. This is a documented asymmetry, not a defect.
/// </remarks>
sealed class ExcelTabularReader(string path) : ITabularReader
{
	readonly ExcelDataReader _reader = ExcelDataReader.Create(path);

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
