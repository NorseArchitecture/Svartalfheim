using System.Data.SqlTypes;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class SequentialGuidTests
{
	[Fact]
	void Should_generate_a_well_formed_rfc_ordered_value_when_constructed()
	{
		SequentialGuid value = new();

		value.Order.ShouldBe(GuidByteOrder.Rfc9562);
		GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
	}

	[Fact]
	void Should_embed_the_current_time_when_constructed()
	{
		var before = DateTime.UtcNow;
		SequentialGuid value = new();
		var after = DateTime.UtcNow;

		value.Timestamp.ShouldBeInRange(before.AddSeconds(-1), after.AddSeconds(1));
	}

	[Theory]
	[InlineData(GuidByteOrder.Rfc9562)]
	[InlineData(GuidByteOrder.SqlServer)]
	void Should_throw_when_wrapped_value_is_not_a_version7_guid(GuidByteOrder order) =>
		Should.Throw<ArgumentException>(() => new SequentialGuid(Guid.NewGuid(), order));

	[Fact]
	void Should_throw_when_order_is_unspecified()
	{
		SequentialGuid generated = new();

		Should.Throw<ArgumentOutOfRangeException>(() => new SequentialGuid(generated.Value, GuidByteOrder.Unspecified));
	}

	[Fact]
	void Should_round_trip_through_sql_order_and_back_when_converted_both_ways()
	{
		SequentialGuid original = new();

		var roundTripped = original.ToSqlOrder().ToRfcOrder();

		roundTripped.ShouldBe(original);
		roundTripped.Value.ShouldBe(original.Value);
	}

	[Fact]
	void Should_be_a_no_op_when_already_in_the_requested_order()
	{
		SequentialGuid value = new();

		value.ToRfcOrder().ShouldBe(value);
		value.ToSqlOrder().ToSqlOrder().ShouldBe(value.ToSqlOrder());
	}

	[Fact]
	void Should_be_equal_when_byte_order_tags_differ_but_identity_matches()
	{
		SequentialGuid rfcTagged = new();
		var sqlTagged = rfcTagged.ToSqlOrder();

		rfcTagged.Equals(sqlTagged).ShouldBeTrue();
		rfcTagged.GetHashCode().ShouldBe(sqlTagged.GetHashCode());
	}

	[Fact]
	void Should_compare_equal_to_zero_when_instances_are_equal()
	{
		SequentialGuid rfcTagged = new();
		var sqlTagged = rfcTagged.ToSqlOrder();

		rfcTagged.CompareTo(sqlTagged).ShouldBe(0);
	}

	[Fact]
	void Should_sort_using_sql_server_semantics_when_tagged_sql_server()
	{
		List<SequentialGuid> sqlTaggedValues = [];
		for (var i = 0; i < 20; i++)
			sqlTaggedValues.Add(new SequentialGuid().ToSqlOrder());

		SequentialGuid[]
			expectedBySqlGuid = [.. sqlTaggedValues.OrderBy(x => new SqlGuid(x.Value))],
			actualByCompareTo = [.. sqlTaggedValues.OrderBy(x => x)];

		actualByCompareTo.ShouldBe(expectedBySqlGuid);
	}

	[Fact]
	void Should_unwrap_to_guid_when_used_in_a_guid_typed_context()
	{
		SequentialGuid value = new();

		Guid unwrapped = value;

		unwrapped.ShouldBe(value.Value);
	}

	[Fact]
	void Should_throw_a_clear_exception_when_ToRfcOrder_is_called_on_a_default_value()
	{
		Action act = () => default(SequentialGuid).ToRfcOrder();

		var ex = Should.Throw<InvalidOperationException>(act);
		ex.Message.ShouldContain("malformed by construction");
	}

	[Fact]
	void Should_throw_a_clear_exception_when_ToSqlOrder_is_called_on_a_default_value()
	{
		Action act = () => default(SequentialGuid).ToSqlOrder();

		var ex = Should.Throw<InvalidOperationException>(act);
		ex.Message.ShouldContain("malformed by construction");
	}

	[Fact]
	void Should_throw_a_clear_exception_when_Equals_is_called_on_a_default_value()
	{
		// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
		Action act = () => default(SequentialGuid).Equals(new SequentialGuid());

		Should.Throw<InvalidOperationException>(act);
	}

	[Fact]
	void Should_throw_a_clear_exception_when_GetHashCode_is_called_on_a_default_value()
	{
		// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
		Action act = () => default(SequentialGuid).GetHashCode();

		Should.Throw<InvalidOperationException>(act);
	}

	[Fact]
	void Constructor_produces_a_well_formed_v7_value_on_the_native_path()
	{
		var value = new SequentialGuid();

		GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
		value.Order.ShouldBe(GuidByteOrder.Rfc9562);
	}

	[Fact]
	void Constructor_produces_a_well_formed_v7_value_on_the_managed_path()
	{
		SequentialGuid value = default;

		NativeCapability.ForManagedOnly(() =>
			value = new SequentialGuid());

		GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
		value.Order.ShouldBe(GuidByteOrder.Rfc9562);
	}

	[Fact]
	void Native_sql_order_transform_matches_the_managed_permutation_byte_for_byte()
	{
		var rfcOrdered = new SequentialGuid();

		var managedSqlOrder = default(SequentialGuid);
		NativeCapability.ForManagedOnly(() =>
			managedSqlOrder = rfcOrdered.ToSqlOrder());

		var nativeSqlOrder = new SequentialGuid(HyperUuid.UuidGenerator.V7ToSqlOrder(rfcOrdered.Value), GuidByteOrder.SqlServer);

		nativeSqlOrder.Value.ShouldBe(managedSqlOrder.Value);
	}

	[Fact]
	void Native_sql_order_round_trip_reproduces_the_original_value()
	{
		var rfcOrdered = new SequentialGuid();

		var sqlOrdered = HyperUuid.UuidGenerator.V7ToSqlOrder(rfcOrdered.Value);
		var roundTripped = HyperUuid.UuidGenerator.V7FromSqlOrder(sqlOrdered);

		roundTripped.ShouldBe(rfcOrdered.Value);
	}
}
