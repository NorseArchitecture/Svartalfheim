using System.Text;

namespace Norse.Primitives.Pii;

/// <summary>
/// A single personal-name component as PII. Deliberately not a composite: an entity declares
/// <c>GivenName</c>/<c>MiddleName</c>/<c>FamilyName</c> each as its own <see cref="PersonalName"/>
/// field — component count and cultural ordering are the consumer's rendering concern, never the
/// primitive's. Mask: single uppercased initial with a period (<c>B.</c>); a grouped rendering
/// (<c>B.B.</c>) is display-layer composition over N masked components.
/// </summary>
/// <remarks><c>default(PersonalName)</c> is malformed by construction; members throw on it.</remarks>
public readonly record struct PersonalName : IPiiScalar<PersonalName>
{
	/// <summary>Component length bound.</summary>
	public const int MaxLength = 128;

	string Value { get; init; }

	PersonalName(string value) => Value = value;

	/// <summary>The canonical wire string (trimmed, Unicode NFC). Deliberate egress only.</summary>
	public string WireValue =>
		Value ?? throw new InvalidOperationException("default(PersonalName) is malformed — construct via Parse.");

	/// <summary>The search-normalization form: NFC, uppercase invariant. Not blind-indexed in this scope.</summary>
	public string Normalized =>
		WireValue.ToUpperInvariant();

	/// <inheritdoc />
	public string Masked =>
		$"{char.ToUpperInvariant(WireValue[0])}.";

	/// <inheritdoc />
	public string ToMasked(DateOnly asOf) =>
		Masked;

	/// <inheritdoc />
	public override string ToString() =>
		Masked;

	/// <summary>Parses one name component: 1–128 chars, no control characters, at least one letter.</summary>
	public static Result<PersonalName> Parse(ReadOnlySpan<char> value)
	{
		var trimmed = value.Trim();
		if (trimmed.IsEmpty)
			return new(new Failure(ParseFailure.Empty, trimmed, nameof(PersonalName)));
		if (trimmed.Length > MaxLength || !HasValidShape(trimmed))
			return new(new Failure(ParseFailure.Malformed, trimmed, nameof(PersonalName)));
		var canonical = trimmed.ToString();
		if (!canonical.IsNormalized(NormalizationForm.FormC))
			canonical = canonical.Normalize(NormalizationForm.FormC);
		return new(new Success<PersonalName>(new(canonical)));
	}

	/// <summary>String overload forwarding to the span parser.</summary>
	public static Result<PersonalName> Parse(string? value) =>
		Parse(value.AsSpan());

	/// <summary>Try-pattern over <see cref="Parse(ReadOnlySpan{char})"/>; <c>false</c> leaves default.</summary>
	public static bool TryParse(ReadOnlySpan<char> value, out PersonalName name)
	{
		if (Parse(value).TryGetValue(out Success<PersonalName> success))
		{
			name = success.Value;
			return true;
		}
		name = default;
		return false;
	}

	static bool HasValidShape(ReadOnlySpan<char> value)
	{
		var hasLetter = false;
		foreach (var c in value)
		{
			if (char.IsControl(c) || char.IsDigit(c))
				return false;
			hasLetter |= char.IsLetter(c);
		}
		return hasLetter;
	}
}
