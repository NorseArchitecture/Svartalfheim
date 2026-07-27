using System.Runtime.CompilerServices;

namespace Norse.Primitives.Tests;

public sealed class ResultTests
{
	static Failure MalformedBoolean(string input = "bogus") =>
		new(ParseFailure.Malformed, input, "Boolean");

	[Fact]
	void Should_match_success_case_when_constructed_from_success()
	{
		// Assignment without an explicit constructor call IS the implicit union conversion.
		Result<bool> result = new Success<bool>(true);
		var matched = result switch
		{
			Success<bool>(var value) => value,
			Failure => false,
		};
		matched.ShouldBeTrue();
	}

	[Fact]
	void Should_match_failure_case_when_constructed_from_failure()
	{
		Result<bool> result = MalformedBoolean();
		var matched = result switch
		{
			Success<bool> => null as ParseFailure?,
			Failure failure => failure.Reason,
		};
		matched.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_expose_boxed_success_when_value_read_directly()
	{
		Result<bool> result = new Success<bool>(true);
		result.Value.ShouldBeOfType<Success<bool>>().Value.ShouldBeTrue();
	}

	[Fact]
	void Should_expose_boxed_failure_when_value_read_directly()
	{
		Result<bool> result = MalformedBoolean();
		result.Value.ShouldBeOfType<Failure>().Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_report_access_pattern_consistent_with_value_when_success()
	{
		Result<bool> result = new Success<bool>(true);
		result.HasValue.ShouldBeTrue();
		result.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBeTrue();
		result.TryGetValue(out Failure _).ShouldBeFalse();
	}

	[Fact]
	void Should_report_access_pattern_consistent_with_value_when_failure()
	{
		Result<bool> result = MalformedBoolean();
		result.HasValue.ShouldBeTrue();
		result.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		result.TryGetValue(out Success<bool> _).ShouldBeFalse();
	}

	[Fact]
	void Should_have_null_value_when_defaulted()
	{
		// The struct-union footgun, pinned: default(Result<T>) is neither case.
		var result = default(Result<bool>);
		result.Value.ShouldBeNull();
		result.HasValue.ShouldBeFalse();
		result.TryGetValue(out Success<bool> _).ShouldBeFalse();
		result.TryGetValue(out Failure _).ShouldBeFalse();
	}

	[Fact]
	void Should_throw_when_switching_defaulted_result() =>
		Should.Throw<SwitchExpressionException>(() =>
			default(Result<bool>) switch
			{
				Success<bool> => "success",
				Failure => "failure",
			});

	[Fact]
	void Should_be_equal_when_same_success_value()
	{
		Result<bool>
			left = new Success<bool>(true),
			right = new Success<bool>(true);
		left.ShouldBe(right);
	}

	[Fact]
	void Should_not_be_equal_when_cases_differ()
	{
		Result<bool>
			left = new Success<bool>(false),
			right = MalformedBoolean();
		left.ShouldNotBe(right);
	}

	[Fact]
	void Should_render_case_shape_when_converted_to_string()
	{
		Result<bool>
			success = new Success<bool>(true),
			failure = MalformedBoolean();
		success.ToString().ShouldBe("Success(True)");
		failure.ToString().ShouldBe("Failure(Malformed, \"bogus\")");
	}

	[Fact]
	void Should_carry_must_consume_attribute_when_inspected() =>
		typeof(Result<>).IsDefined(typeof(MustConsumeAttribute), inherit: false).ShouldBeTrue();

	[Fact]
	void Should_throw_when_constructed_from_defaulted_failure() =>
		Should.Throw<ArgumentOutOfRangeException>(() => new Result<bool>(default(Failure)));

	[Fact]
	void Should_match_nested_property_pattern_when_failure()
	{
		Result<bool> result = MalformedBoolean();
		var matched = result switch
		{
			Failure { Reason: ParseFailure.Malformed } => true,
			Success<bool> => false,
			Failure => false,
		};
		matched.ShouldBeTrue();
	}

	[Fact]
	void Should_hash_equal_when_same_success_value()
	{
		Result<bool>
			left = new Success<bool>(true),
			right = new Success<bool>(true);
		left.GetHashCode().ShouldBe(right.GetHashCode());
	}

	[Fact]
	void Should_render_default_shape_when_defaulted() =>
		default(Result<bool>).ToString().ShouldBe("Default(invalid)");

	[Fact]
	void Should_round_trip_when_value_type_is_wider_than_bool()
	{
		Result<decimal> result = new Success<decimal>(1234.56m);
		var matched = result switch
		{
			Success<decimal>(var value) => value,
			Failure => 0m,
		};
		matched.ShouldBe(1234.56m);
		result.TryGetValue(out Success<decimal> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.56m);
	}

	[Fact]
	void Should_round_trip_when_value_type_is_reference_type()
	{
		Result<string> result = new Success<string>("forged");
		var matched = result switch
		{
			Success<string>(var value) => value,
			Failure => null,
		};
		matched.ShouldBe("forged");
		Result<string> same = new Success<string>("forged");
		result.ShouldBe(same);
		default(Result<string>).HasValue.ShouldBeFalse();
	}
}
