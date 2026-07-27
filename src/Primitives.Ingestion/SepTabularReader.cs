using nietras.SeparatedValues;

namespace Norse.Primitives.Ingestion;

/// <summary>
/// An <see cref="ITabularReader"/> over a delimited file, backed by Sep.
/// Opens <paramref name="path"/> for forward-only reading.
/// </summary>
/// <param name="path">The delimited file's path.</param>
/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
sealed class SepTabularReader(string path, char separator) : ITabularReader
{
	readonly SepReader _reader = Sep.New(separator).Reader().FromFile(path);

	public int FieldCount =>
		_reader.Header.ColNames.Count;

	public int Ordinal(string headerName) =>
		_reader.Header.IndexOf(headerName);

	public bool Read() =>
		_reader.MoveNext();

	public ReadOnlySpan<char> this[int ordinal] =>
		_reader.Current[ordinal].Span;

	public void Dispose() =>
		_reader.Dispose();
}
