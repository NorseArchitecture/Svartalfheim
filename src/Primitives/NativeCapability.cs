using System.Diagnostics.CodeAnalysis;

namespace Norse.Primitives;

/// <summary>
/// Gates whether a parser/generator routes to its native (HyperUuid/HyperCast) execution path
/// or the managed fallback. Two layers: an <see cref="OperatingSystem"/> platform check the
/// trimmer/NativeAOT constant-folds per publish target, and a cached one-time native probe for
/// RID-family gaps the platform check can't see (e.g. glibc vs. musl Linux -- both report
/// <see cref="OperatingSystem.IsLinux"/> <see langword="true"/>, but only glibc ships a native
/// asset today).
/// </summary>
static class NativeCapability
{
	[ThreadStatic]
	static bool _forcedManagedOnly;

	static readonly Lazy<bool> _probe = new(Probe, LazyThreadSafetyMode.PublicationOnly);

	/// <summary>
	/// <see langword="true"/> when this call should route to the native engine: the platform
	/// family is one HyperUuid/HyperCast ship for, the cached native probe succeeded, and no
	/// test has forced the managed path via <see cref="ForManagedOnly"/> on this thread.
	/// </summary>
	internal static bool Available =>
		!_forcedManagedOnly && PlatformCovered && _probe.Value;

	// HyperUuid/HyperCast ship linux-x64/arm64, osx-x64/arm64, win-x64/arm64, and browser-wasm
	// today -- no ios/android RID exists yet (tracked upstream, see the design's §9). The mobile
	// checks below don't fire today (no MAUI head exists on this platform yet), but they're the
	// trimmer-foldable half of the gate regardless of what runs today, so a future head gets this
	// for free. There is no browser/WASM check here yet at all -- a known, tracked gap (design
	// doc §9), not an oversight; add one when a WASM head lands.
	static bool PlatformCovered =>
		!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() && !OperatingSystem.IsTvOS();

	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Deliberate, narrow exception (probe's entire job is \"detect that native didn't load\"): DllNotFoundException/EntryPointNotFoundException are the expected shapes, but a wrong-architecture native asset (BadImageFormatException), PlatformNotSupportedException, or a static-initializer failure must also fall back to managed rather than propagate. Lazy<bool>'s default caching mode would otherwise cache any uncaught exception here and re-throw it forever, permanently poisoning Available for the rest of the process; the managed fallback exists precisely to cover this.")]
	static bool Probe()
	{
		try
		{
			// A trivial, side-effect-free native call -- proves the P/Invoke library actually
			// resolved and loaded for this exact RID, not just that the platform family matches.
			HyperUuid.UuidGenerator.TryNewV4(out _);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Test-only: forces <see cref="Available"/> to <see langword="false"/> for the duration of
	/// <paramref name="test"/>, so the managed fallback is exercised deterministically
	/// regardless of the host platform's own native capability. Restores the prior state even
	/// if <paramref name="test"/> throws. Thread-local, not process-global, so parallel test
	/// runs on other threads are unaffected -- callers still isolate via
	/// <c>DisableParallelization</c> on their own collection to avoid two overrides racing on
	/// the *same* thread's reentrant call.
	/// </summary>
	internal static void ForManagedOnly(Action test)
	{
		var previous = _forcedManagedOnly;
		_forcedManagedOnly = true;
		try
		{
			test();
		}
		finally
		{
			_forcedManagedOnly = previous;
		}
	}
}
