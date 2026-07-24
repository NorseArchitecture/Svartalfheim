using System.Runtime.CompilerServices;

namespace Norse.Primitives;

/// <summary>
/// Generic parse gateway over <see cref="ISpanParsable{TSelf}"/>: the bridge from the
/// span world into <see cref="Result{T}"/> with uniform failure semantics.
/// </summary>
/// <remarks>
/// <para>
/// Hot-path specialists are routed by <c>typeof</c> branches resolved at JIT/AOT compile
/// time — <see cref="bool"/> routes to <see cref="BooleanParser"/>; the integer family
/// (<see cref="byte"/>, <see cref="sbyte"/>, <see cref="short"/>, <see cref="ushort"/>,
/// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>) routes to
/// <see cref="IntegerParser"/>; the real family (<see cref="float"/>, <see cref="double"/>,
/// <see cref="decimal"/>) routes to <see cref="RealParser"/>; <see cref="char"/> routes to
/// <see cref="CharParser"/>; <see cref="Guid"/> routes to <see cref="GuidParser"/>; and the
/// temporal family (<see cref="DateOnly"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>,
/// <see cref="TimeOnly"/>, <see cref="TimeSpan"/>) routes to their respective ISO specialists
/// (<see cref="DateOnlyParser"/>, <see cref="DateTimeParser"/>, <see cref="DateTimeOffsetParser"/>,
/// <see cref="TimeOnlyParser"/>, <see cref="TimeSpanParser"/>).
/// Each specialist carries richer vocabulary than the bare <see cref="ISpanParsable{TSelf}"/>
/// path. <see cref="char"/>, <see cref="Guid"/>, and all temporal types deliberately do not
/// receive the provider — they are culture-insensitive on the ISO door, exactly as
/// <see cref="bool"/>. Every other type falls through to the generic
/// <c>T.TryParse(span, provider)</c> path. There is no runtime registry: a type that cannot
/// parse does not compile.
/// Leading and trailing whitespace is trimmed on every route. The provider null-check is
/// uniform — it precedes specialist routing, so even culture-insensitive routes demand a
/// declared culture at the call site.
/// </para>
/// <para>
/// The provider is required. A call site parsing culture-sensitive text declares its culture
/// out loud (e.g. <see cref="System.Globalization.CultureInfo.InvariantCulture"/>) or it does
/// not compile — there is no defaulting overload.
/// </para>
/// </remarks>
public static class Parser
{
	/// <summary>
	/// Parses required scalar text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
		{
			// In this JIT-eliminated branch T is statically bool; the reinterpret is an
			// identity the type system cannot express (the BCL generic-specialization pattern).
			var routed = BooleanParser.ParseRequired(input);
			return Unsafe.As<Result<bool>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(byte))
		{
			var routed = IntegerParser.ParseRequired<byte>(input, provider);
			return Unsafe.As<Result<byte>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(sbyte))
		{
			var routed = IntegerParser.ParseRequired<sbyte>(input, provider);
			return Unsafe.As<Result<sbyte>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(short))
		{
			var routed = IntegerParser.ParseRequired<short>(input, provider);
			return Unsafe.As<Result<short>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(ushort))
		{
			var routed = IntegerParser.ParseRequired<ushort>(input, provider);
			return Unsafe.As<Result<ushort>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(int))
		{
			var routed = IntegerParser.ParseRequired<int>(input, provider);
			return Unsafe.As<Result<int>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(uint))
		{
			var routed = IntegerParser.ParseRequired<uint>(input, provider);
			return Unsafe.As<Result<uint>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(long))
		{
			var routed = IntegerParser.ParseRequired<long>(input, provider);
			return Unsafe.As<Result<long>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(ulong))
		{
			var routed = IntegerParser.ParseRequired<ulong>(input, provider);
			return Unsafe.As<Result<ulong>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(float))
		{
			var routed = RealParser.ParseRequired<float>(input, provider);
			return Unsafe.As<Result<float>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(double))
		{
			var routed = RealParser.ParseRequired<double>(input, provider);
			return Unsafe.As<Result<double>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(decimal))
		{
			var routed = RealParser.ParseRequired<decimal>(input, provider);
			return Unsafe.As<Result<decimal>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(char))
		{
			var routed = CharParser.ParseRequired(input);
			return Unsafe.As<Result<char>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(Guid))
		{
			var routed = GuidParser.ParseRequired(input);
			return Unsafe.As<Result<Guid>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(DateOnly))
		{
			var routed = DateOnlyParser.ParseRequired(input);
			return Unsafe.As<Result<DateOnly>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(DateTime))
		{
			var routed = DateTimeParser.ParseRequired(input);
			return Unsafe.As<Result<DateTime>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(DateTimeOffset))
		{
			var routed = DateTimeOffsetParser.ParseRequired(input);
			return Unsafe.As<Result<DateTimeOffset>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(TimeOnly))
		{
			var routed = TimeOnlyParser.ParseRequired(input);
			return Unsafe.As<Result<TimeOnly>, Result<T>>(ref routed);
		}
		if (typeof(T) == typeof(TimeSpan))
		{
			var routed = TimeSpanParser.ParseRequired(input);
			return Unsafe.As<Result<TimeSpan>, Result<T>>(ref routed);
		}
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name) :
			Parse<T>(trimmed, provider);
	}

	/// <summary>
	/// Parses optional scalar text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
		{
			// Identity reinterpret as in ParseRequired; Nullable<X> layout is a function of X alone.
			var routed = BooleanParser.ParseOptional(input);
			return Unsafe.As<Result<bool>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(byte))
		{
			var routed = IntegerParser.ParseOptional<byte>(input, provider);
			return Unsafe.As<Result<byte>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(sbyte))
		{
			var routed = IntegerParser.ParseOptional<sbyte>(input, provider);
			return Unsafe.As<Result<sbyte>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(short))
		{
			var routed = IntegerParser.ParseOptional<short>(input, provider);
			return Unsafe.As<Result<short>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(ushort))
		{
			var routed = IntegerParser.ParseOptional<ushort>(input, provider);
			return Unsafe.As<Result<ushort>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(int))
		{
			var routed = IntegerParser.ParseOptional<int>(input, provider);
			return Unsafe.As<Result<int>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(uint))
		{
			var routed = IntegerParser.ParseOptional<uint>(input, provider);
			return Unsafe.As<Result<uint>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(long))
		{
			var routed = IntegerParser.ParseOptional<long>(input, provider);
			return Unsafe.As<Result<long>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(ulong))
		{
			var routed = IntegerParser.ParseOptional<ulong>(input, provider);
			return Unsafe.As<Result<ulong>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(float))
		{
			var routed = RealParser.ParseOptional<float>(input, provider);
			return Unsafe.As<Result<float>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(double))
		{
			var routed = RealParser.ParseOptional<double>(input, provider);
			return Unsafe.As<Result<double>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(decimal))
		{
			var routed = RealParser.ParseOptional<decimal>(input, provider);
			return Unsafe.As<Result<decimal>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(char))
		{
			var routed = CharParser.ParseOptional(input);
			return Unsafe.As<Result<char>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(Guid))
		{
			var routed = GuidParser.ParseOptional(input);
			return Unsafe.As<Result<Guid>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(DateOnly))
		{
			var routed = DateOnlyParser.ParseOptional(input);
			return Unsafe.As<Result<DateOnly>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(DateTime))
		{
			var routed = DateTimeParser.ParseOptional(input);
			return Unsafe.As<Result<DateTime>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(DateTimeOffset))
		{
			var routed = DateTimeOffsetParser.ParseOptional(input);
			return Unsafe.As<Result<DateTimeOffset>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(TimeOnly))
		{
			var routed = TimeOnlyParser.ParseOptional(input);
			return Unsafe.As<Result<TimeOnly>?, Result<T>?>(ref routed);
		}
		if (typeof(T) == typeof(TimeSpan))
		{
			var routed = TimeSpanParser.ParseOptional(input);
			return Unsafe.As<Result<TimeSpan>?, Result<T>?>(ref routed);
		}
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return Parse<T>(trimmed, provider);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : ISpanParsable<T> =>
		T.TryParse(trimmed, provider, out var value) ?
			new Success<T>(value) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
}
