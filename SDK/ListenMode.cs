namespace Nox.Audio.Players {
	/// <summary>
	/// Listening / hearing mode for a player — controls what range of voices they can receive.
	/// Server-authoritative: clients read this value but cannot set it directly.
	/// </summary>
	public enum ListenMode : byte {
		/// <summary>
		/// Sourd — the player is deafened and hears no one.
		/// </summary>
		Deafen  = 0,

		/// <summary>
		/// Zone limitée — only nearby / proximity voices are audible.
		/// </summary>
		Limited = 1,

		/// <summary>
		/// Normal — standard hearing range.
		/// </summary>
		Normal  = 2,

		/// <summary>
		/// Grande zone — extended hearing range, receives voices from further away.
		/// </summary>
		Extended = 3,
	}
}
