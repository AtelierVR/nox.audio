using Concentus.Enums;

namespace Nox.CCK.Audio.Opus {
	public enum OpusSignalType {
		/// <summary>Automatic signal type detection (default).</summary>
		Auto = OpusSignal.OPUS_SIGNAL_AUTO,

		/// <summary>Voice signal type (human speech).</summary>
		Voice = OpusSignal.OPUS_SIGNAL_VOICE,

		/// <summary>Music signal type (non-speech audio).</summary>
		Music = OpusSignal.OPUS_SIGNAL_MUSIC
	}

	/// <summary>Maps <see cref="OpusSignalType"/> to/from the Concentus <see cref="OpusSignal"/> enum and config strings.</summary>
	public static class OpusSignalExtension {

		/// <summary>Converts an <see cref="OpusSignalType"/> to its config string representation.</summary>
		public static string ToString(this OpusSignalType signalType) => signalType switch {
			OpusSignalType.Voice => "voice",
			OpusSignalType.Music => "music",
			_ => "auto"
		};

		/// <summary>Converts a config string to an <see cref="OpusSignalType"/>.</summary>
		public static OpusSignalType ToOpusSignalType(this string signalType) => signalType switch {
			"voice" => OpusSignalType.Voice,
			"music" => OpusSignalType.Music,
			_ => OpusSignalType.Auto
		};

		/// <summary>Converts an <see cref="OpusSignalType"/> to the Concentus <see cref="OpusSignal"/> enum.</summary>
		public static OpusSignal ToOpusSignal(this OpusSignalType signalType) => signalType switch {
			OpusSignalType.Voice => OpusSignal.OPUS_SIGNAL_VOICE,
			OpusSignalType.Music => OpusSignal.OPUS_SIGNAL_MUSIC,
			_ => OpusSignal.OPUS_SIGNAL_AUTO
		};
	}
}
