namespace Nox.Microphone.Players {
	/// <summary>
	/// Voice activity / loudness level flags for a player.
	/// Levels mirror the three <c>VoiceMode</c> ranges and are used to
	/// drive UI indicators (speaking ring, level bars, etc.).
	/// </summary>
	[System.Flags]
	public enum LevelFlags : byte {
		/// <summary>Below voice-activity threshold — silent.</summary>
		None      = 0,

		/// <summary>Above VAD threshold — the player is actively producing voice.</summary>
		Speaking  = 1 << 0,

		/// <summary>Whisper level — short range, quiet voice.</summary>
		Whisper   = 1 << 1,

		/// <summary>Normal level — medium range, regular voice.</summary>
		Normal    = 1 << 2,

		/// <summary>Broadcast level — global range, loud or amplified voice.</summary>
		Broadcast = 1 << 3,
	}
}
