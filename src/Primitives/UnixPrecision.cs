namespace Norse.Primitives;

/// <summary>
/// The declared unit of a Unix-epoch value. There is no magnitude guessing — the caller states the
/// unit, so a bare number is never silently interpreted as seconds or milliseconds.
/// </summary>
public enum UnixPrecision
{
	/// <summary>Sentinel CLR default — never a valid precision; rejected by the Unix parse doors.</summary>
	Unspecified = 0,

	/// <summary>Seconds since 1970-01-01T00:00:00Z.</summary>
	Seconds = 1,

	/// <summary>Milliseconds since 1970-01-01T00:00:00Z.</summary>
	Milliseconds = 2,
}
