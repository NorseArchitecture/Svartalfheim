namespace Norse.Primitives.Ingestion;

/// <summary>
/// A forward-only, single-source, single-sheet cursor over tabular data (a delimited file
/// or a single Excel worksheet), surfacing every cell as a <see cref="ReadOnlySpan{Char}"/>
/// regardless of the underlying format.
/// </summary>
/// <remarks>
/// Structural failures (a malformed delimited row, a corrupt workbook) are this contract's
/// own concern and throw — they are not <c>Result&lt;T&gt;</c> territory. Turning a cell's
/// span into a typed scalar value, and deciding what a bad value means, belongs to the
/// caller via <c>Norse.Primitives.Parser</c>.
/// </remarks>
public interface ITabularReader : IDisposable
{
	/// <summary>The number of columns, resolved from the header row.</summary>
	int FieldCount { get; }

	/// <summary>Resolves a column's ordinal from its header name once, for reuse in a hot read loop.</summary>
	/// <param name="headerName">The header name to look up.</param>
	/// <returns>The zero-based column ordinal.</returns>
	int Ordinal(string headerName);

	/// <summary>Advances to the next row.</summary>
	/// <returns><see langword="false"/> when there are no more rows.</returns>
	bool Read();

	/// <summary>The current row's cell at <paramref name="ordinal"/>, as raw text.</summary>
	/// <param name="ordinal">The zero-based column ordinal.</param>
	ReadOnlySpan<char> this[int ordinal] { get; }
}
