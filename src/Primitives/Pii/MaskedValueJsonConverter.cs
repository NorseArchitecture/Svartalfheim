using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure — PII types in flat namespace for Roslyn analyzer resolution.
namespace Norse.Primitives;
#pragma warning restore IDE0130

/// <summary>
/// Defense-in-depth for serialization paths the analyzer cannot see: writes the masked rendering,
/// throws on read. Reading is refused because masked forms can be syntactically valid inputs
/// (<c>j***@d***.com</c> parses as an email address) — a lossy round-trip that succeeds would
/// fabricate a well-formed value that silently is not the person's data. Wire DTOs are unaffected:
/// transport contracts carry plain strings filled explicitly at the disclosure edge.
/// </summary>
public sealed class MaskedValueJsonConverter<T> : JsonConverter<T> where T : struct, IMaskedValue
{
	/// <inheritdoc />
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		throw new NotSupportedException($"{typeToConvert.Name} is masked-write-only JSON; PII never rehydrates from JSON — parse the wire string at the boundary instead.");

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.Masked);
}
