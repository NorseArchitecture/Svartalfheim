using FsCheck;
using FsCheck.Fluent;

namespace Norse.Primitives.Tests;

public sealed class ResultLawTests
{
	static Gen<Failure> FailureGen =>
		from reason in Gen.Elements(ParseFailure.Empty, ParseFailure.Malformed)
		from input in ArbMap.Default.GeneratorFor<NonNull<string>>()
		select new Failure(reason, input.Get, "Int32");

	static Gen<Result<int>> ResultGen =>
		Gen.OneOf(
			ArbMap.Default.GeneratorFor<int>().Select(value => (Result<int>)new Success<int>(value)),
			FailureGen.Select(failure => (Result<int>)failure));

	static Gen<Func<int, Result<int>>> BinderGen =>
		from addend in ArbMap.Default.GeneratorFor<int>()
		from threshold in ArbMap.Default.GeneratorFor<int>()
		from failure in FailureGen
		select (Func<int, Result<int>>)(x =>
		{
			if (x < threshold)
				return new Success<int>(unchecked(x + addend));
			return failure;
		});

	[Fact]
	void Should_preserve_result_when_mapped_with_identity() =>
		Prop.ForAll(ResultGen.ToArbitrary(), result => result.Map(x => x) == result)
			.QuickCheckThrowOnFailure();

	[Fact]
	void Should_compose_transforms_when_mapped_in_sequence() =>
		Prop.ForAll(ResultGen.ToArbitrary(), ArbMap.Default.ArbFor<int>(), ArbMap.Default.ArbFor<int>(), (result, a, b) =>
		{
			Func<int, int>
				f = x => unchecked(x + a),
				g = x => unchecked(x * b);
			return result.Map(f).Map(g) == result.Map(x => g(f(x)));
		})
			.QuickCheckThrowOnFailure();

	[Fact]
	void Should_satisfy_left_identity_when_lifted_value_is_bound() =>
		Prop.ForAll(ArbMap.Default.ArbFor<int>(), BinderGen.ToArbitrary(), (value, binder) =>
		{
			Result<int> lifted = new Success<int>(value);
			return lifted.Bind(binder) == binder(value);
		})
			.QuickCheckThrowOnFailure();

	[Fact]
	void Should_satisfy_right_identity_when_bound_with_lift() =>
		Prop.ForAll(ResultGen.ToArbitrary(), result =>
			result.Bind(x => (Result<int>)new Success<int>(x)) == result)
			.QuickCheckThrowOnFailure();

	[Fact]
	void Should_satisfy_associativity_when_bound_in_sequence() =>
		Prop.ForAll(ResultGen.ToArbitrary(), BinderGen.ToArbitrary(), BinderGen.ToArbitrary(), (result, f, g) =>
			result.Bind(f).Bind(g) == result.Bind(x => f(x).Bind(g)))
			.QuickCheckThrowOnFailure();
}
