using System.Text.Json;

namespace Norse.Primitives.Tests;

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
sealed record CorpusVector(string Input, string Expect, object? Value)
{
	static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

	/// <summary><see langword="true"/> when this vector expects a successful parse.</summary>
	internal bool ExpectSuccess =>
		Expect == "ok";

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
