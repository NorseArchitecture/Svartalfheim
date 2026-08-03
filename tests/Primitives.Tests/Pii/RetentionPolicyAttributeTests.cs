namespace Norse.Primitives.Tests.Pii;

public sealed class RetentionPolicyAttributeTests
{
	[Fact]
	void Should_throw_when_basis_is_the_unspecified_sentinel() =>
		Should.Throw<ArgumentOutOfRangeException>(() => new RetentionPolicyAttribute(RetentionBasis.Unspecified));

	[Fact]
	void Should_carry_basis_and_citation_when_constructed()
	{
		RetentionPolicyAttribute attribute = new(RetentionBasis.StatutoryEpoch, "GDPR Art. 17(3)(b)");
		attribute.Basis.ShouldBe(RetentionBasis.StatutoryEpoch);
		attribute.Citation.ShouldBe("GDPR Art. 17(3)(b)");
	}

	[Fact]
	void Should_target_properties_and_fields_only()
	{
		var usage = typeof(RetentionPolicyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.Single();
		usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
	}
}
