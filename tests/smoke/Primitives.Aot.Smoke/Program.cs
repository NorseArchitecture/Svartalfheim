using System.Globalization;
using Norse.Primitives;
using Norse.Primitives.Identifiers;

var invariant = CultureInfo.InvariantCulture;
var failures = 0;

Check("gateway routes the bool specialist's vocabulary", () =>
	Parser.ParseRequired<bool>("yes", invariant) == (Result<bool>)new Success<bool>(true));

Check("gateway parses int through the generic ISpanParsable path", () =>
	Parser.ParseRequired<int>("42", invariant) == (Result<int>)new Success<int>(42));

Check("gateway honors the declared provider", () =>
	Parser.ParseRequired<decimal>("1,5", CultureInfo.GetCultureInfo("de-DE")) == (Result<decimal>)new Success<decimal>(1.5m));

Check("combinator chain composes through the pathway", () =>
	Parser.ParseRequired<int>("21", invariant)
		.Map(x => x * 2)
		.Match(value => value == 42, _ => false));

Check("failure diagnostics survive the generic path", () =>
	Parser.ParseRequired<int>("bogus", invariant).TryGetValue(out Failure failure)
		&& failure is { Reason: ParseFailure.Malformed, Input: "bogus", ExpectedType: "Int32" });

Check("optional absence is null, not a failure", () =>
	Parser.ParseOptional<int>("   ", invariant) is null);

Check("gateway routes integer grouping vocabulary", () =>
	Parser.ParseRequired<int>("1,234", invariant) == (Result<int>)new Success<int>(1234));

Check("gateway routes hex integer through generic math", () =>
	Parser.ParseRequired<int>("0x2A", invariant) == (Result<int>)new Success<int>(42));

Check("gateway routes real percentage", () =>
	Parser.ParseRequired<double>("50%", invariant) == (Result<double>)new Success<double>(0.5));

Check("gateway routes char code point", () =>
	Parser.ParseRequired<char>("65", invariant) == (Result<char>)new Success<char>('A'));

Check("gateway routes guid prefix stripping", () =>
	Parser.ParseRequired<Guid>("urn:uuid:01020304-0506-0708-090a-0b0c0d0e0f10", invariant)
		== (Result<Guid>)new Success<Guid>(new Guid("01020304-0506-0708-090a-0b0c0d0e0f10")));

Check("gateway routes an ISO date", () =>
	Parser.ParseRequired<DateOnly>("2026-01-02", invariant) == (Result<DateOnly>)new Success<DateOnly>(new DateOnly(2026, 1, 2)));

Check("gateway normalizes an offset datetime to UTC", () =>
	Parser.ParseRequired<DateTimeOffset>("2026-01-02T15:04:05+05:00", invariant)
		== (Result<DateTimeOffset>)new Success<DateTimeOffset>(new DateTimeOffset(2026, 1, 2, 10, 4, 5, TimeSpan.Zero)));

Check("gateway rejects a zone-less datetime", () =>
	Parser.ParseRequired<DateTimeOffset>("2026-01-02T15:04:05", invariant).TryGetValue(out Failure _));

Check("gateway routes an ISO-8601 duration", () =>
	Parser.ParseRequired<TimeSpan>("PT1H30M", invariant) == (Result<TimeSpan>)new Success<TimeSpan>(new TimeSpan(1, 30, 0)));

Check("declared unix epoch parses off-gateway", () =>
	DateTimeOffsetParser.ParseUnix("1700000000", UnixPrecision.Seconds)
		.TryGetValue(out Success<DateTimeOffset> epoch) && epoch.Value.Year == 2023);

Check("TimeZoneParser resolves a known IANA id off-gateway", () =>
	TimeZoneParser.ParseRequired("America/Chicago").TryGetValue(out Success<TimeZoneInfo> _));

// Also transitively exercises TimeOnlyParser.ParseIso's native Cast.Time branch (via
// TimeOnlyParser.ParseRequired, called internally by TemporalFusion.Fuse for the "10:00:00"
// argument below) -- covers Task 15's native call path under AOT publish without a redundant
// direct check.
Check("TemporalFusion fuses ISO date, time, and IANA zone to UTC", () =>
	TemporalFusion.FuseRequired("2026-06-15", "10:00:00", "America/Chicago")
		.TryGetValue(out Success<DateTime> fused)
		&& fused.Value.Kind == DateTimeKind.Utc
		&& fused.Value == new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc));

Check("SequentialGuid generates a well-formed, current-time-stamped value", () =>
{
	SequentialGuid value = new();
	return value.Order == GuidByteOrder.Rfc9562 && value.Timestamp > DateTime.UtcNow.AddMinutes(-1);
});

Check("SequentialGuid round-trips through SQL Server byte order", () =>
{
	SequentialGuid original = new();
	return original.ToSqlOrder().ToRfcOrder() == original;
});

Check("SequentialGuid CompareTo respects SQL Server ordering when tagged SqlServer", () =>
{
	SequentialGuid
		first = new(),
		second = new();
	var firstSql = first.ToSqlOrder();
	var secondSql = second.ToSqlOrder();
	return firstSql.CompareTo(secondSql) == first.CompareTo(second);
});

Check("DeterministicGuid derives a stable value from namespace and name", () =>
{
	DeterministicGuid
		first = new(DeterministicGuid.Namespaces.Dns, "example.com"),
		second = new(DeterministicGuid.Namespaces.Dns, "example.com");
	return first == second;
});

Check("SequentialGuid() publishes clean under AOT", () =>
{
	var value = new SequentialGuid();
	_ = new SequentialGuid(value.Value, GuidByteOrder.Rfc9562); // throws if version/variant bits are wrong
	return true;
});

Check("DeterministicGuid publishes clean under AOT", () =>
	new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "aot-smoke").Value != Guid.Empty);

Check("SQL byte-order round trip publishes clean under AOT", () =>
{
	var value = new SequentialGuid();
	return value.ToSqlOrder().ToRfcOrder().Value == value.Value;
});

if (failures > 0)
{
	Console.Error.WriteLine($"AOT smoke FAILED: {failures} check(s) failed.");
	return 1;
}

Console.WriteLine("AOT smoke passed: the pathway survives native compilation.");
return 0;

void Check(string description, Func<bool> probe)
{
	bool passed;
	try
	{
		passed = probe();
	}
	catch (Exception exception)
	{
		Console.Error.WriteLine($"FAIL {description}: {exception}");
		failures++;
		return;
	}
	if (passed)
	{
		Console.WriteLine($"ok   {description}");
	}
	else
	{
		Console.Error.WriteLine($"FAIL {description}");
		failures++;
	}
}
