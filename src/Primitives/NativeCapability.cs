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

	static readonly Lazy<bool> _probe = new(Probe);

	/// <summary>
	/// <see langword="true"/> when this call should route to the native engine: the platform
	/// family is one HyperUuid/HyperCast ship for, the cached native probe succeeded, and no
	/// test has forced the managed path via <see cref="ForManagedOnly"/> on this thread.
	/// </summary>
	internal static bool Available =>
		!_forcedManagedOnly && PlatformCovered && _probe.Value;

	// HyperUuid/HyperCast ship linux-x64/arm64, osx-x64/arm64, win-x64/arm64, and browser-wasm
	// today -- no ios/android RID exists yet (tracked upstream, see the design's §9). Neither
	// the mobile checks nor the browser check below fires today (no MAUI/WASM head exists on
	// this platform yet), but they're the trimmer-foldable half of the gate regardless of what
	// runs today, so a future head gets this for free.
	static bool PlatformCovered =>
		!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() && !OperatingSystem.IsTvOS();

	static bool Probe()
	{
		try
		{
			// A trivial, side-effect-free native call -- proves the P/Invoke library actually
			// resolved and loaded for this exact RID, not just that the platform family matches.
			HyperUuid.UuidGenerator.TryNewV4(out _);
			return true;
		}
		catch (DllNotFoundException)
		{
			return false;
		}
		catch (EntryPointNotFoundException)
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
