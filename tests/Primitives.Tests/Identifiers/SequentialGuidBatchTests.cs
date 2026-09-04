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
	void Should_produce_a_contiguous_increasing_sequence_across_entropy_chunk_boundaries()
	{
		// Entropy is drawn in fixed-size chunks (3072 bytes / 6 = 512 items per draw), not
		// per item -- this batch spans multiple chunks, pinning that the counter and byte
		// layout stay correct across the boundary, not just within one draw.
		Span<SequentialGuid> destination = new SequentialGuid[1500];

		SequentialGuid.Fill(destination);

		SequentialGuid[] array = [.. destination];
		array.Distinct().Count().ShouldBe(1500);
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

	[Fact]
	void Should_fill_destination_with_distinct_well_formed_values_on_the_managed_path()
	{
		var array = new SequentialGuid[10];

		NativeCapability.ForManagedOnly(() =>
		{
			Span<SequentialGuid> destination = array;
			SequentialGuid.Fill(destination);
		});

		array.Distinct().Count().ShouldBe(10);
		foreach (var value in array)
			GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
	}

	[Fact]
	void Should_fill_destination_with_distinct_well_formed_values_on_the_native_path()
	{
		Span<SequentialGuid> destination = new SequentialGuid[10];

		SequentialGuid.Fill(destination);

		SequentialGuid[] array = [.. destination];
		array.Distinct().Count().ShouldBe(10);
		foreach (var value in array)
			GuidVersionBits.HasVersionAndVariant(value.Value, 7).ShouldBeTrue();
	}

	[Fact]
	void Should_not_allocate_an_unbounded_native_buffer_for_a_large_batch()
	{
		// Regression: FillNative used to size its Guid buffer to the whole batch once destination
		// exceeded 256 items (`new Guid[destination.Length]`) -- at the documented max batch size
		// (67,108,864, the 26-bit counter space) that's roughly 1 GB in one shot. It's now chunked
		// in fixed 256-item slices through a single reusable stack buffer, mirroring FillManaged's
		// own entropy-chunking fix. This only proves the native path (Available must be true on
		// this host) since forcing managed-only inside GC measurement would only exercise the
		// already-bounded FillManaged path.
		NativeCapability.Available.ShouldBeTrue();

		const int BatchSize = 100_000;
		var array = new SequentialGuid[BatchSize];

		// Warm up JIT/native library loading before measuring, so first-call overhead doesn't
		// dominate the allocation delta.
		SequentialGuid.Fill(array.AsSpan(0, 16));

		var before = GC.GetAllocatedBytesForCurrentThread();
		SequentialGuid.Fill(array);
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// A single Guid[BatchSize] heap array alone would be BatchSize * 16 bytes (1.6 MB here);
		// bounded chunking keeps total allocation well under that regardless of batch size. The
		// SequentialGuid[] destination itself is stack/caller-owned, not counted here.
		allocated.ShouldBeLessThan(BatchSize * 16L);
	}
}
