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
	void Should_throw_when_wrapped_value_is_not_a_version7_guid(GuidByteOrder order)
	{
		Should.Throw<ArgumentException>(() => new SequentialGuid(Guid.NewGuid(), order));
	}

	[Fact]
	void Should_throw_when_order_is_unspecified()
	{
		SequentialGuid generated = new();

		Should.Throw<ArgumentOutOfRangeException>(() => new SequentialGuid(generated.Value, GuidByteOrder.Unspecified));
	}

	[Fact]
	void Should_round_trip_through_sql_order_and_back()
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
	void Should_be_equal_regardless_of_byte_order_tag()
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

		var expectedBySqlGuid = sqlTaggedValues.OrderBy(x => new SqlGuid(x.Value)).ToArray();
		var actualByCompareTo = sqlTaggedValues.OrderBy(x => x).ToArray();

		actualByCompareTo.ShouldBe(expectedBySqlGuid);
	}

	[Fact]
	void Should_unwrap_implicitly_to_guid()
	{
		SequentialGuid value = new();

		Guid unwrapped = value;

		unwrapped.ShouldBe(value.Value);
	}
}
