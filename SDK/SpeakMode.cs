namespace Nox.Microphone.Players {
	/// <summary>
	/// Speaking / emission mode for a player — controls voice range and whether they are heard at all.
	/// </summary>
	public enum SpeakMode : byte {
		/// <summary>
		/// Muet — the player emits no voice.
		/// </summary>
		Muted     = 0,

		/// <summary>
		/// Chuchotement — very short range, quiet voice.
		/// </summary>
		Whisper   = 1,

		/// <summary>
		/// Normal — standard speaking range and volume.
		/// </summary>
		Normal    = 2,

		/// <summary>
		/// Fort — louder voice, larger emission zone.
		/// </summary>
		Loud      = 3,

		/// <summary>
		/// Broadcast — global range, heard by everyone regardless of distance.
		/// </summary>
		Broadcast = 4,
	}
}
