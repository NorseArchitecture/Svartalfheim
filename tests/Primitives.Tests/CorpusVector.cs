using System.Text.Json;
using System.Text.Json.Serialization;

namespace Norse.Primitives.Tests;

/// <summary>
/// A corpus vector's <c>"format"</c> object — HyperCast's declared numeric notation, snake_case on
/// the wire (<c>decimal_sep</c>/<c>group_sep</c>/<c>flags</c>) unlike every other corpus field, so
/// each property carries an explicit <see cref="JsonPropertyNameAttribute"/> rather than relying on
/// <see cref="CorpusVector"/>'s shared camelCase <c>Web</c> naming policy.
/// </summary>
/// <param name="DecimalSep">The declared decimal separator character.</param>
/// <param name="GroupSep">The declared digit-group separator character.</param>
/// <param name="Flags">
/// HyperCast's <c>NumStyles</c> bit-for-bit (<c>Grouping</c> = 1, <c>Parentheses</c> = 2,
/// <c>Exponent</c> = 4, <c>RadixPrefixes</c> = 8, <c>Percent</c> = 16, <c>SeparatorDetect</c> = 32,
/// <c>All</c> = 63).
/// </param>
sealed record CorpusNumFormat(
	[property: JsonPropertyName("decimal_sep")] string DecimalSep,
	[property: JsonPropertyName("group_sep")] string GroupSep,
	[property: JsonPropertyName("flags")] int Flags)
{
	const int SeparatorDetectFlag = 32;

	/// <summary><see langword="true"/> when this format's flags declare HyperCast's <c>SeparatorDetect</c> lenience.</summary>
	internal bool IsSeparatorDetect =>
		(Flags & SeparatorDetectFlag) != 0;
}

/// <summary>
/// One HyperCast corpus test vector: an input string and its expected verdict. Matches the
/// real vendored corpus shape (<c>tests/Primitives.Tests/TestData/HyperCastCorpus/*.json</c>):
/// <c>{ "input": "...", "expect": "ok"|"empty"|"malformed"|"out_of_range", "value": ... }</c>,
/// with an optional <c>fault</c> offset/length pair this harness does not model. Shared model —
/// every subsequent parser task's corpus tests load vectors through this same type.
/// </summary>
/// <param name="Input">The raw text to parse.</param>
/// <param name="Expect">
/// One of <c>"ok"</c>, <c>"empty"</c>, <c>"malformed"</c>, <c>"out_of_range"</c> — HyperCast's
/// own vocabulary, which maps directly onto <see cref="ParseFailure"/>'s renumbered members.
/// </param>
/// <param name="Value">
/// The expected parsed value when <see cref="Expect"/> is <c>"ok"</c>; otherwise
/// <see langword="null"/>. Type varies by door (a JSON boolean for <c>boolean.json</c>, a
/// hex-string GUID representation for <c>uuid.json</c>).
/// </param>
/// <param name="Type">
/// The target scalar width this vector is written against (<c>integer.json</c>: <c>"i8"</c>/
/// <c>"i16"</c>/<c>"i32"</c>/<c>"i64"</c>/<c>"u8"</c>/<c>"u16"</c>/<c>"u32"</c>/<c>"u64"</c>;
/// <c>real.json</c>: <c>"f32"</c>/<c>"f64"</c>). <see langword="null"/> for corpora with a single
/// fixed target type (<c>boolean.json</c>, <c>uuid.json</c>).
/// </param>
/// <param name="Format">
/// The declared numeric notation override (<c>real.json</c>/<c>integer.json</c> only) — a
/// non-default decimal/group separator pair and/or lenience flag set. <see langword="null"/> when
/// the vector uses the corpus's own implicit invariant-with-every-lenience-on default.
/// </param>
/// <param name="Seconds">
/// The expected whole-second component (<c>timestamp.json</c>/<c>duration.json</c> only) —
/// Unix epoch seconds for a timestamp, elapsed seconds for a duration. <see langword="null"/> for
/// every other corpus.
/// </param>
/// <param name="Nanos">
/// The expected nanosecond component (<c>timestamp.json</c>/<c>duration.json</c>: the fractional
/// remainder alongside <see cref="Seconds"/>, negative when the duration itself is negative;
/// <c>time.json</c>: the full nanoseconds-since-midnight value, standing alone with no
/// <see cref="Seconds"/> sibling). <see langword="null"/> for every other corpus.
/// </param>
/// <param name="Year">The expected calendar year (<c>date.json</c> only). <see langword="null"/> for every other corpus.</param>
/// <param name="Month">The expected calendar month (<c>date.json</c> only). <see langword="null"/> for every other corpus.</param>
/// <param name="Day">The expected calendar day (<c>date.json</c> only). <see langword="null"/> for every other corpus.</param>
sealed record CorpusVector(string Input, string Expect, object? Value, string? Type = null, CorpusNumFormat? Format = null,
	long? Seconds = null, long? Nanos = null, int? Year = null, int? Month = null, int? Day = null)
{
	static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

	/// <summary><see langword="true"/> when this vector expects a successful parse.</summary>
	internal bool ExpectSuccess =>
		Expect == "ok";

	/// <summary>The <see cref="ParseFailure"/> this vector expects, or <see langword="null"/> when <see cref="Expect"/> is "ok".</summary>
	internal ParseFailure? ExpectedFailure =>
		Expect switch
		{
			"ok" => null,
			"empty" => ParseFailure.Empty,
			"malformed" => ParseFailure.Malformed,
			"out_of_range" => ParseFailure.OutOfRange,
			_ => throw new NotSupportedException($"Corpus vector declares unsupported expect '{Expect}'."),
		};

	internal bool AsBoolean() => ((JsonElement)Value!).GetBoolean();
	internal string AsGuidHex() => ((JsonElement)Value!).GetString()!;
	internal long AsSignedInteger() => ((JsonElement)Value!).GetInt64();
	internal ulong AsUnsignedInteger() => ((JsonElement)Value!).GetUInt64();
	internal double AsDouble() => ((JsonElement)Value!).GetDouble();

	// Corpus nanos are exact-nanosecond values (up to 9 fractional digits); Svartálfheim's
	// temporal types are tick-resolution (100ns). Truncate toward zero to the nearest 100ns
	// before comparing -- matching this plan's established "truncate, never round" doctrine
	// (DateTimeOffsetParser/TimeOnlyParser/TimeSpanParser all truncate an 8th/9th fractional
	// digit the same way). C#'s integer division truncates toward zero for both signs, so this
	// is correct for duration.json's negative nanos values too (e.g. seconds: -1, nanos:
	// -500000000).
	internal static long TruncateNanosToTickResolution(long nanos) =>
		nanos / 100 * 100;

	/// <summary>Loads every corpus vector from <paramref name="fileName"/> under the vendored corpus directory.</summary>
	/// <param name="fileName">The corpus file's name, e.g. <c>"boolean.json"</c>.</param>
	/// <returns>Theory data — one <see cref="object"/>[] per vector, for <c>[MemberData]</c>.</returns>
	internal static IEnumerable<object[]> Load(string fileName)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "TestData", "HyperCastCorpus", fileName);
		var json = File.ReadAllText(path);
		var vectors = JsonSerializer.Deserialize<CorpusVector[]>(json, _options) ?? [];
		return vectors.Select(v => new object[] { v });
	}

	/// <summary>Renders the input text so theory failures name the offending vector, not an index.</summary>
	public override string ToString() =>
		$"\"{Input}\" -> {Expect}";
}
