using System.Globalization;
using System.Runtime.CompilerServices;

namespace Norse.Primitives.Tests;

public sealed class ResultCombinatorTests
{
	static Failure MalformedBoolean(string input = "bogus") =>
		new(ParseFailure.Malformed, input, "Boolean");

	[Fact]
	void Should_transform_value_when_mapping_success()
	{
		Result<int> result = new Success<int>(21);
		var mapped = result.Map(x => x * 2);
		mapped.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(42);
	}

	[Fact]
	void Should_change_type_when_mapping_success()
	{
		Result<int> result = new Success<int>(42);
		var mapped = result.Map(x => x.ToString(CultureInfo.InvariantCulture));
		mapped.TryGetValue(out Success<string> success).ShouldBeTrue();
		success.Value.ShouldBe("42");
	}

	[Fact]
	void Should_flow_failure_through_when_mapping_failure()
	{
		Result<int> result = MalformedBoolean();
		var invoked = false;
		var mapped = result.Map(x =>
		{
			invoked = true;
			return x;
		});
		invoked.ShouldBeFalse();
		mapped.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(MalformedBoolean());
	}

	[Fact]
	void Should_throw_when_mapping_with_null_selector()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<ArgumentNullException>(() => result.Map<int>(null!));
	}

	[Fact]
	void Should_throw_when_mapping_defaulted_result() =>
		Should.Throw<SwitchExpressionException>(() => default(Result<int>).Map(x => x));

	[Fact]
	void Should_propagate_selector_exception_when_mapping()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<InvalidOperationException>(() => result.Map<int>(_ => throw new InvalidOperationException("boom")));
	}

	[Fact]
	void Should_chain_to_new_result_when_binding_success()
	{
		Result<int> result = new Success<int>(21);
		var bound = result.Bind<int>(x => new Success<int>(x * 2));
		bound.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(42);
	}

	[Fact]
	void Should_chain_to_failure_when_binder_fails()
	{
		Result<int> result = new Success<int>(21);
		var bound = result.Bind<int>(_ => MalformedBoolean());
		bound.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(MalformedBoolean());
	}

	[Fact]
	void Should_flow_failure_through_when_binding_failure()
	{
		Result<int> result = MalformedBoolean();
		var invoked = false;
		var bound = result.Bind<int>(x =>
		{
			invoked = true;
			return new Success<int>(x);
		});
		invoked.ShouldBeFalse();
		bound.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(MalformedBoolean());
	}

	[Fact]
	void Should_throw_when_binding_with_null_binder()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<ArgumentNullException>(() => result.Bind<int>(null!));
	}

	[Fact]
	void Should_throw_when_binding_defaulted_result() =>
		Should.Throw<SwitchExpressionException>(() =>
			default(Result<int>).Bind<int>(x => new Success<int>(x)));

	[Fact]
	void Should_invoke_success_arm_when_matching_success()
	{
		Result<int> result = new Success<int>(42);
		var rendered = result.Match(value => $"ok:{value}", failure => $"fail:{failure.Reason}");
		rendered.ShouldBe("ok:42");
	}

	[Fact]
	void Should_invoke_failure_arm_when_matching_failure()
	{
		Result<int> result = MalformedBoolean();
		var rendered = result.Match(value => $"ok:{value}", failure => $"fail:{failure.Reason}");
		rendered.ShouldBe("fail:Malformed");
	}

	[Fact]
	void Should_throw_when_matching_with_null_success_arm()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<ArgumentNullException>(() => result.Match(null!, failure => failure.Reason.ToString()));
	}

	[Fact]
	void Should_throw_when_matching_with_null_failure_arm()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<ArgumentNullException>(() => result.Match(value => value.ToString(CultureInfo.InvariantCulture), null!));
	}

	[Fact]
	void Should_throw_when_matching_defaulted_result() =>
		Should.Throw<SwitchExpressionException>(() =>
			default(Result<int>).Match(value => value, _ => 0));

	[Fact]
	void Should_compose_pathway_when_chaining_combinators()
	{
		Result<int> result = new Success<int>(10);
		var rendered = result
			.Map(x => x + 11)
			.Bind<int>(x =>
			{
				if (x % 2 == 1)
					return new Success<int>(x * 2);
				return MalformedBoolean();
			})
			.Match(value => value.ToString(CultureInfo.InvariantCulture), failure => failure.Reason.ToString());
		rendered.ShouldBe("42");
	}

	[Fact]
	void Should_propagate_binder_exception_when_binding()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<InvalidOperationException>(() => result.Bind<int>(_ => throw new InvalidOperationException("boom")));
	}

	[Fact]
	void Should_propagate_success_handler_exception_when_matching()
	{
		Result<int> result = new Success<int>(1);
		Should.Throw<InvalidOperationException>(() => result.Match<int>(_ => throw new InvalidOperationException("boom"), _ => 0));
	}

	[Fact]
	void Should_propagate_failure_handler_exception_when_matching()
	{
		Result<int> result = MalformedBoolean();
		Should.Throw<InvalidOperationException>(() => result.Match<int>(_ => 0, _ => throw new InvalidOperationException("boom")));
	}

	[Fact]
	void Should_allow_nullable_return_when_matching()
	{
		// Pins the deliberate absence of a notnull constraint on Match's TResult.
		Result<int> result = MalformedBoolean();
		var matched = result.Match(value => value.ToString(CultureInfo.InvariantCulture), _ => null as string);
		matched.ShouldBeNull();
	}
}
