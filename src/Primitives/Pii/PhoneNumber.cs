namespace Norse.Primitives.Pii;

/// <summary>
/// A phone number as PII, canonicalized to E.164 — shape validation only (leading <c>+</c>, first
/// digit 1–9, 8–15 digits); regional validity is a service concern. <see cref="Normalized"/> equals
/// <see cref="WireValue"/> because E.164 is already the canonical blind-index form. Mask: last four
/// digits (<c>***4567</c> — country-code-agnostic, no region leak).
/// </summary>
/// <remarks><c>default(PhoneNumber)</c> is malformed by construction; members throw on it.</remarks>
public readonly record struct PhoneNumber : IPiiScalar<PhoneNumber>
{
	const int MinDigits = 8, MaxDigits = 15;

	string Value { get; }

	PhoneNumber(string value) =>
		Value = value;

	/// <summary>The canonical E.164 wire string. Deliberate egress only.</summary>
	public string WireValue =>
		Value ?? throw new InvalidOperationException("default(PhoneNumber) is malformed — construct via Parse.");

	/// <summary>The blind-index input — identical to <see cref="WireValue"/> for E.164.</summary>
	public string Normalized =>
		WireValue;

	/// <inheritdoc />
	public string Masked =>
		$"***{WireValue[^4..]}";

	/// <inheritdoc />
	public string ToMasked(DateOnly asOf) =>
		Masked;

	/// <inheritdoc />
	public override string ToString() =>
		Masked;

	/// <summary>Parses to E.164, stripping common separators (space, hyphen, dot, parentheses).</summary>
	public static Result<PhoneNumber> Parse(ReadOnlySpan<char> value)
	{
		var trimmed = value.Trim();
		if (trimmed.IsEmpty)
			return new(new Failure(ParseFailure.Empty, [], nameof(PhoneNumber)));
		if (trimmed[0] != '+')
			return new(new Failure(ParseFailure.Malformed, [], nameof(PhoneNumber), format: "+15551234567"));

		Span<char> digits = stackalloc char[MaxDigits + 1];
		var count = 0;
		foreach (var c in trimmed[1..])
		{
			if (c is ' ' or '-' or '.' or '(' or ')')
				continue;
			if (!char.IsAsciiDigit(c) || count == MaxDigits)
				return new(new Failure(ParseFailure.Malformed, [], nameof(PhoneNumber), format: "+15551234567"));
			digits[count++] = c;
		}
		if (count < MinDigits || digits[0] == '0')
			return new(new Failure(ParseFailure.Malformed, [], nameof(PhoneNumber), format: "+15551234567"));

		return new(new Success<PhoneNumber>(new($"+{digits[..count]}")));
	}

	/// <summary>String overload forwarding to the span parser.</summary>
	public static Result<PhoneNumber> Parse(string? value) =>
		Parse(value.AsSpan());

	/// <summary>Try-pattern over <see cref="Parse(ReadOnlySpan{char})"/>; <c>false</c> leaves default.</summary>
	public static bool TryParse(ReadOnlySpan<char> value, out PhoneNumber phone)
	{
		if (Parse(value).TryGetValue(out Success<PhoneNumber> success))
		{
			phone = success.Value;
			return true;
		}
		phone = default;
		return false;
	}
}
