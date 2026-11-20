using Nox.Audio.Players;
using UnityEngine.Events;

namespace Nox.CCK.Audio {
	public static class VoiceModeSettings {
		public const string SPEAK_MODE_KEY = "voice.speak_mode";
		public const string ALLOWED_MODES_KEY = "voice.speak_modes";
		public const string FORCE_MUTE_KEY = "voice.force_mute";

		public const int ALL_SPEAK_MODES =
			(1 << (int)SpeakMode.Whisper)
			| (1 << (int)SpeakMode.Normal)
			| (1 << (int)SpeakMode.Loud)
			| (1 << (int)SpeakMode.Broadcast);

		public static SpeakMode? ChangeOnJoin { get; private set; }

		public static readonly UnityEvent<SpeakMode?> OnChangeOnJoinChanged = new();

		public static void SetChangeOnJoin(SpeakMode? mode) {
			if (ChangeOnJoin == mode)
				return;

			ChangeOnJoin = mode;
			OnChangeOnJoinChanged.Invoke(mode);
		}

		public static bool IsAllowed(SpeakMode mode, int allowedModes)
			=> mode is >= SpeakMode.Whisper and <= SpeakMode.Broadcast
			   && (allowedModes & (1 << (int)mode)) != 0;

		public static SpeakMode? ParseMode(object value)
			=> value switch {
				SpeakMode mode => mode,
				int mode => (SpeakMode)mode,
				byte mode => (SpeakMode)mode,
				string text when int.TryParse(text, out var mode) => (SpeakMode)mode,
				_ => null,
			};

		public static SpeakMode Resolve(SpeakMode? sessionMode, SpeakMode? preference, int allowedModes) {
			var mode = sessionMode ?? preference ?? SpeakMode.Normal;

			if (mode == SpeakMode.Muted)
				return SpeakMode.Muted;

			if (mode is < SpeakMode.Whisper or > SpeakMode.Broadcast)
				mode = SpeakMode.Normal;

			return IsAllowed(mode, allowedModes)
				? mode
				: SpeakMode.Normal;
		}
	}
}
