using System.Globalization;

namespace Norse.Primitives.Pii;

/// <summary>
/// A birthdate as PII — not a <see cref="DateOnly"/> alias: the type is what the analyzer keys on.
/// The pure mask is a zero-information redaction (<c>****-**-**</c>); the disclosure mask is the
/// exact current age as of a caller-supplied date — computed at disclosure time, never stored, no
/// clock in the primitive. No <c>Over18</c>-style predicates ship: threshold consumers compute from
/// the disclosed age; a no-disclosure threshold check is a purpose-built endpoint if ever needed.
/// </summary>
/// <remarks><c>default(BirthDate)</c> is malformed by construction; members throw on it.</remarks>
public readonly record struct BirthDate : IPiiScalar<BirthDate>
{
	const string WireFormat = "yyyy-MM-dd";

	readonly DateOnly? _value;

	BirthDate(DateOnly value) =>
		_value = value;

	/// <summary>The birthdate.</summary>
	public DateOnly Value =>
		_value ?? throw new InvalidOperationException("default(BirthDate) is malformed — construct via Parse.");

	/// <summary>The canonical ISO 8601 wire string. Deliberate egress only.</summary>
	public string WireValue =>
		Value.ToString(WireFormat, CultureInfo.InvariantCulture);

	/// <summary>The blind-index input — identical to <see cref="WireValue"/> since ISO 8601 is already the canonical form.</summary>
	public string Normalized =>
		WireValue;

	/// <inheritdoc />
	public string Masked =>
		"****-**-**";

	/// <summary>The exact age in whole years as of <paramref name="asOf"/>, clamped at zero.</summary>
	public string ToMasked(DateOnly asOf)
	{
		var value = Value;
		var age = asOf.Year - value.Year;
		if (asOf.Month < value.Month || (asOf.Month == value.Month && asOf.Day < value.Day))
			age--;
		return Math.Max(age, 0).ToString(CultureInfo.InvariantCulture);
	}

	/// <inheritdoc />
	public override string ToString() =>
		Masked;

	/// <summary>Parses strict ISO 8601 (<c>yyyy-MM-dd</c>) only — no culture inference, ever.</summary>
	public static Result<BirthDate> Parse(ReadOnlySpan<char> value)
	{
		var trimmed = value.Trim();
		if (trimmed.IsEmpty)
			return new(new Failure(ParseFailure.Empty, [], nameof(BirthDate)));
		return DateOnly.TryParseExact(trimmed, WireFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ?
			new(new Success<BirthDate>(new(date))) :
			new(new Failure(ParseFailure.Malformed, [], nameof(BirthDate), format: WireFormat));
	}

	/// <summary>String overload forwarding to the span parser.</summary>
	public static Result<BirthDate> Parse(string? value) =>
		Parse(value.AsSpan());

	/// <summary>Try-pattern over <see cref="Parse(ReadOnlySpan{char})"/>; <c>false</c> leaves default.</summary>
	public static bool TryParse(ReadOnlySpan<char> value, out BirthDate birthDate)
	{
		if (Parse(value).TryGetValue(out Success<BirthDate> success))
		{
			birthDate = success.Value;
			return true;
		}
		birthDate = default;
		return false;
	}
}
