namespace Norse.Primitives.Pii;

/// <summary>
/// An email address as PII: carries the normalization law (<see cref="Normalized"/> is the exact
/// string the blind-index HMAC is computed over) and the masking law
/// (<c>j***@d***.com</c> — first character each side of the <c>@</c>, final domain label kept).
/// <see cref="object.ToString"/> renders the mask; <see cref="WireValue"/> is the deliberate egress.
/// </summary>
/// <remarks>
/// <c>default(EmailAddress)</c> is malformed by construction (the <c>default(Result&lt;T&gt;)</c>
/// footgun class) — every member throws <see cref="InvalidOperationException"/> on it. Equality is
/// wire-value equality; identity-level sameness is a <see cref="Normalized"/> comparison.
/// </remarks>
public readonly record struct EmailAddress : IPiiScalar<EmailAddress>
{
	/// <summary>RFC 5321 total-length bound.</summary>
	public const int MaxLength = 254;

	string Value { get; init; }

	EmailAddress(string value) => Value = value;

	/// <summary>The canonical wire string (trimmed, as entered). Deliberate egress only.</summary>
	public string WireValue =>
		Value ?? throw new InvalidOperationException("default(EmailAddress) is malformed — construct via Parse.");

	/// <summary>The blind-index input: the wire value case-folded to lowercase invariant.</summary>
	public string Normalized =>
		WireValue.ToLowerInvariant();

	/// <inheritdoc />
	public string Masked
	{
		get
		{
			var value = WireValue;
			var at = value.IndexOf('@');
			var domain = value[(at + 1)..];
			var lastDot = domain.LastIndexOf('.');
			return $"{value[0]}***@{domain[0]}***{domain[lastDot..]}";
		}
	}

	/// <inheritdoc />
	public string ToMasked(DateOnly asOf) =>
		Masked;

	/// <inheritdoc />
	public override string ToString() =>
		Masked;

	/// <summary>Parses an email address shape: one <c>@</c>, non-empty local part, dotted domain.</summary>
	public static Result<EmailAddress> Parse(ReadOnlySpan<char> value)
	{
		var trimmed = value.Trim();
		if (trimmed.IsEmpty)
			return new(new Failure(ParseFailure.Empty, trimmed, nameof(EmailAddress)));
		if (trimmed.Length > MaxLength || !HasValidShape(trimmed))
			return new(new Failure(ParseFailure.Malformed, trimmed, nameof(EmailAddress), format: "local@domain.tld"));
		return new(new Success<EmailAddress>(new(trimmed.ToString())));
	}

	/// <summary>String overload forwarding to the span parser.</summary>
	public static Result<EmailAddress> Parse(string? value) =>
		Parse(value.AsSpan());

	/// <summary>Try-pattern over <see cref="Parse(ReadOnlySpan{char})"/>; <c>false</c> leaves default.</summary>
	public static bool TryParse(ReadOnlySpan<char> value, out EmailAddress email)
	{
		if (Parse(value).TryGetValue(out Success<EmailAddress> success))
		{
			email = success.Value;
			return true;
		}
		email = default;
		return false;
	}

	static bool HasValidShape(ReadOnlySpan<char> value)
	{
		var at = value.IndexOf('@');
		if (at < 1 || at != value.LastIndexOf('@'))
			return false;
		var domain = value[(at + 1)..];
		if (domain.Length < 3 || domain[0] == '.' || domain[^1] == '.' || domain.IndexOf('.') < 0)
			return false;
		foreach (var c in value)
		{
			if (char.IsWhiteSpace(c) || char.IsControl(c))
				return false;
		}
		return true;
	}
}
