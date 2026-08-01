using nietras.SeparatedValues;

namespace Norse.Primitives.Ingestion;

/// <summary>
/// An <see cref="ITabularReader"/> over delimited content, backed by Sep. Reads from a file
/// path, an already-open <see cref="Stream"/>, or an in-memory byte array — the same three
/// input shapes ASP.NET Core's request/response types and <see cref="System.Net.Http.HttpClient"/>
/// accept, for the same reason: callers arrive with whichever one they already hold.
/// </summary>
sealed class SepTabularReader : ITabularReader
{
	readonly SepReader _reader;

	/// <summary>Opens <paramref name="path"/> for forward-only reading.</summary>
	/// <param name="path">The delimited file's path.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	internal SepTabularReader(string path, char separator) :
		this(options => options.FromFile(path), separator)
	{ }

	/// <summary>Opens <paramref name="stream"/> for forward-only reading.</summary>
	/// <param name="stream">The delimited content stream.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	/// <param name="leaveOpen">When <see langword="false"/>, disposing this reader also disposes <paramref name="stream"/>.</param>
	internal SepTabularReader(Stream stream, char separator, bool leaveOpen) :
		this(options => options.From(stream, leaveOpen), separator)
	{ }

	/// <summary>Opens <paramref name="contents"/> for forward-only reading.</summary>
	/// <param name="contents">The delimited content bytes.</param>
	/// <param name="separator">The field separator (e.g. <c>','</c> for CSV, <c>'\t'</c> for TSV).</param>
	internal SepTabularReader(byte[] contents, char separator) :
		this(options => options.From(contents), separator)
	{ }

	SepTabularReader(Func<SepReaderOptions, SepReader> factory, char separator) =>
		_reader = factory(Sep.New(separator).Reader());

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
