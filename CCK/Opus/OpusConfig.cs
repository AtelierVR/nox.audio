using Nox.CCK.Utils;
using Concentus.Enums;

namespace Nox.CCK.Audio.Opus {
	/// <summary>
	/// Opus codec configuration — static accessor over the shared config store
	/// (<see cref="Config.Load()"/>). Values are read live from config.json with
	/// sensible defaults, so no ScriptableObject asset is required.
	/// </summary>
	public static class OpusConfig {
		// ── Constants (matching Opus specs) ──
		public const int BitsPerSample = 16;
		public const int SamplesPerSecond = 48_000;
		public const int ClipLoopSeconds = 1;
		public const int SamplesPerClip = SamplesPerSecond * ClipLoopSeconds;

		private const string Prefix = "settings.opus";

		private static T Get<T>(string key, T fallback)
			=> Config.Load().Get($"{Prefix}.{key}", fallback);

		private static void Set<T>(string key, T value) {
			var config = Config.Load();
			config.Set($"{Prefix}.{key}", value);
			config.Save();
		}

		// ── Codec settings ──
		public static int Complexity {
			get => Get("complexity", 10);
			set => Set("complexity", value);
		}
		public static OpusSignalType SignalType {
			get => Get("signal_type", "auto").ToOpusSignalType();
			set => Set("signal_type", value.ToString());
		}
		public static int FrameSize {
			get => Get("frame_size", 20);  // frame duration in ms
			set => Set("frame_size", value);
		}
		public static int Bitrate {
			get => Get("bitrate", 0);  // 0 = auto (adapt to MTU)
			set => Set("bitrate", value);
		}

		// ── Derived values ──
		public static int FramePeriodMs => FrameSize;
		public static int FramesPerSecond => 1000 / FramePeriodMs;
		public static float SecondsPerFrame => FramePeriodMs / 1000f;
		public static int SamplesPerFrame => SamplesPerSecond / FramesPerSecond;
		public static int FramesPerClip => FramesPerSecond * ClipLoopSeconds;
	}
}
