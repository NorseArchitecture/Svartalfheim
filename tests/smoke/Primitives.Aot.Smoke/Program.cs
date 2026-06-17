using System.Globalization;
using Norse.Primitives;

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
