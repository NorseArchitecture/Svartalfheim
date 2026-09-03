namespace Norse.Primitives;

/// <summary>
/// The failure case of <see cref="Result{T}"/>: a closed conversion reason
/// plus bounded diagnostics for logs and error rendering.
/// </summary>
/// <remarks>
/// <c>default(Failure)</c> bypasses the constructor guards and is not a valid value:
/// its <see cref="Reason"/> is the <see cref="ParseFailure.Unspecified"/> sentinel and its
/// strings are null. Like <c>default(ImmutableArray&lt;T&gt;)</c>, never default this type.
/// </remarks>
public readonly record struct Failure
{
	/// <summary>Upper bound on captured input length — keeps failures log-safe.</summary>
	public const int MaxInputLength = 256;

	/// <summary>Creates a failure, truncating <paramref name="input"/> to <see cref="MaxInputLength"/>.</summary>
	/// <param name="reason">The conversion reason. The <see cref="ParseFailure.Unspecified"/> sentinel is rejected.</param>
	/// <param name="input">The raw input that failed. Captured bounded, never null.</param>
	/// <param name="expectedType">The CLR type name the input was expected to convert to, e.g. "Boolean".</param>
	/// <param name="format">The declared format, when an explicit one was given.</param>
	/// <param name="detail">Optional human-readable detail from richer parsers.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a real failure reason.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="expectedType"/> is null, empty, or whitespace.</exception>
	public Failure(ParseFailure reason, string input, string expectedType, string? format = null, string? detail = null)
	{
		if (reason is not (ParseFailure.Empty or ParseFailure.Malformed or ParseFailure.OutOfRange or ParseFailure.Duplicate))
			throw new ArgumentOutOfRangeException(nameof(reason), reason, "Reason must be a real failure, not the Unspecified sentinel.");
		ArgumentNullException.ThrowIfNull(input);
		ArgumentException.ThrowIfNullOrWhiteSpace(expectedType);
		Reason = reason;
		Input = input.Length <= MaxInputLength ? input : input[..MaxInputLength];
		ExpectedType = expectedType;
		Format = format;
		Detail = detail;
	}

	/// <summary>
	/// Creates a failure from span input, materializing at most <see cref="MaxInputLength"/>
	/// characters — the bounded capture happens before any string allocation.
	/// </summary>
	/// <param name="reason">The conversion reason. The <see cref="ParseFailure.Unspecified"/> sentinel is rejected.</param>
	/// <param name="input">The raw input that failed. Captured bounded.</param>
	/// <param name="expectedType">The CLR type name the input was expected to convert to, e.g. "Boolean".</param>
	/// <param name="format">The declared format, when an explicit one was given.</param>
	/// <param name="detail">Optional human-readable detail from richer parsers.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a real failure reason.</exception>
	/// <exception cref="ArgumentException"><paramref name="expectedType"/> is null, empty, or whitespace.</exception>
	public Failure(ParseFailure reason, ReadOnlySpan<char> input, string expectedType, string? format = null, string? detail = null)
		: this(reason, Bound(input), expectedType, format, detail)
	{
	}

	static string Bound(ReadOnlySpan<char> input) =>
		input.Length <= MaxInputLength ? input.ToString() : input[..MaxInputLength].ToString();

	/// <summary>The closed-set conversion reason.</summary>
	public ParseFailure Reason { get; }

	/// <summary>The raw input, truncated to <see cref="MaxInputLength"/>.</summary>
	public string Input { get; }

	/// <summary>The CLR type name the input was expected to convert to.</summary>
	public string ExpectedType { get; }

	/// <summary>The declared format, when an explicit one was given; otherwise null.</summary>
	public string? Format { get; }

	/// <summary>Optional human-readable detail; otherwise null.</summary>
	public string? Detail { get; }
}
