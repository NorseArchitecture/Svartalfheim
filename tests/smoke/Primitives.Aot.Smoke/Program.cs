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
