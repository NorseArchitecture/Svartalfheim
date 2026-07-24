using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class SequentialGuidBatchTests
{
	[Fact]
	void Should_fill_destination_with_distinct_well_formed_values()
	{
		Span<SequentialGuid> destination = new SequentialGuid[10];

		SequentialGuid.Fill(destination);

		SequentialGuid[] array = [.. destination];
		array.Distinct().Count().ShouldBe(10);
		foreach (var value in array)
			GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
	}

	[Fact]
	void Should_share_one_timestamp_capture_across_the_batch()
	{
		Span<SequentialGuid> destination = new SequentialGuid[25];

		SequentialGuid.Fill(destination);

		DateTime[] distinctTimestamps = [.. destination.ToArray().Select(x => x.Timestamp).Distinct()];
		distinctTimestamps.Length.ShouldBe(1);
	}

	[Fact]
	void Should_produce_a_contiguous_increasing_sequence()
	{
		Span<SequentialGuid> destination = new SequentialGuid[25];

		SequentialGuid.Fill(destination);

		SequentialGuid[] array = [.. destination];
		for (var i = 1; i < array.Length; i++)
			array[i].CompareTo(array[i - 1]).ShouldBeGreaterThan(0);
	}

	[Fact]
	void Should_do_nothing_when_destination_is_empty()
	{
		Span<SequentialGuid> destination = [];

		// Span<T> is a ref struct and cannot be captured by Should.NotThrow's lambda (CS8175);
		// calling directly is equivalent — an unhandled exception already fails the test.
		SequentialGuid.Fill(destination);
	}

	[Fact]
	void Should_create_many_matching_the_requested_count()
	{
		var values = SequentialGuid.CreateMany(15);

		values.Length.ShouldBe(15);
	}

	[Fact]
	void Should_return_an_empty_array_when_count_is_zero() =>
		SequentialGuid.CreateMany(0).ShouldBeEmpty();

	[Fact]
	void Should_throw_when_count_is_negative() =>
		Should.Throw<ArgumentOutOfRangeException>(() => SequentialGuid.CreateMany(-1));

	[Fact]
	void Should_throw_when_count_exceeds_the_counter_space() =>
		Should.Throw<ArgumentOutOfRangeException>(() => SequentialGuid.CreateMany(0x400_0001));
}
