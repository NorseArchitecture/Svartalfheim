using System.Diagnostics.CodeAnalysis;

namespace Norse.Primitives.Tests;

// Runs in its own collection: NativeCapability.ForManagedOnly mutates thread-local state and
// must not race against another test on the same thread reentrantly overriding
// NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class NativeCapabilityTests
{
	[Fact]
	void Available_reflects_a_real_probe_on_this_platform()
	{
		// This dev/CI box is native-capable (linux glibc) -- Available should be true here
		// without any override in play.
		NativeCapability.Available.ShouldBeTrue();
	}

	[Fact]
	void ForManagedOnly_forces_the_managed_path_for_the_duration_of_the_callback()
	{
		var observedInsideOverride = true;

		NativeCapability.ForManagedOnly(() =>
			observedInsideOverride = NativeCapability.Available);

		observedInsideOverride.ShouldBeFalse();
	}

	[Fact]
	void ForManagedOnly_restores_the_prior_state_after_the_callback_returns()
	{
		var before = NativeCapability.Available;

		NativeCapability.ForManagedOnly(() => { });

		NativeCapability.Available.ShouldBe(before);
	}

	[Fact]
	void ForManagedOnly_restores_the_prior_state_even_when_the_callback_throws()
	{
		var before = NativeCapability.Available;

		Should.Throw<InvalidOperationException>(() =>
			NativeCapability.ForManagedOnly(() => throw new InvalidOperationException()));

		NativeCapability.Available.ShouldBe(before);
	}
}

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
	Justification =
		"xUnit v3 collection-definition convention: the marker type is named for use as nameof(XCollection) inside [Collection(...)], not as a public API surface -- the 'Collection' suffix is the idiom, not a naming mistake.")]
[CollectionDefinition(nameof(NativeCapabilityCollection), DisableParallelization = true)]
public sealed class NativeCapabilityCollection;
