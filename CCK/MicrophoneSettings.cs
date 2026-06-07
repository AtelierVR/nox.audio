using Nox.CCK.Events;

namespace Nox.CCK.Microphone {
	/// <summary>
	/// Global microphone settings. Pure static data store — no logic, no setters.
	/// Values are written and events are invoked by the Runtime assembly.
	/// Follows the MirrorSettings CCK pattern.
	/// </summary>
	public static class MicrophoneSettings {
		// ── Activation Threshold ──────────────────────

		public const float MinimalActivationThreshold = 0.001f;
		public const float MaximalActivationThreshold = 0.5f;
		public static float ActivationThreshold = 0.01f;
		public static readonly NoxEvent<float> OnActivationThresholdChanged = new();

		// ── Volume ────────────────────────────────────

		public const float MinimalVolume = 0f;
		public const float MaximalVolume = 2f;
		public static float Volume = 1f;
		public static readonly NoxEvent<float> OnVolumeChanged = new();

		// ── Noise Suppression ─────────────────────────

		public static bool NoiseSuppression = true;
		public static readonly NoxEvent<bool> OnNoiseSuppressionChanged = new();

		// ── Mute ──────────────────────────────────────

		public static bool Mute;
		public static readonly NoxEvent<bool> OnMuteChanged = new();

		// ── Current Microphone ────────────────────────

		public static string CurrentMicrophone;
		public static readonly NoxEvent<string, string> OnCurrentMicrophoneChanged = new();
	}
}
