namespace Nox.Audio.Players {
	/// <summary>
	/// Extends <see cref="IPlayerVoice"/> for the local player.
	/// The local player owns this capture: it binds the audio mod's current microphone itself, and no
	/// controller pushes it (the microphone lifecycle belongs to the player, not the controller).
	/// </summary>
	public interface ILocalPlayerVoice : IPlayerVoice {
		/// <summary>
		/// Get or set the live <see cref="ICapturedAudio"/> for this local player.
		/// <para>
		/// The relay local player assigns a <see cref="MicrophoneAudio"/> built from
		/// <c>IMicrophone.Start()</c> when it enters a room, swaps it when the audio mod switches device,
		/// and clears it when it leaves. The relay voice sender reads the clip and position from it.
		/// </para>
		/// </summary>
		new ICapturedAudio Audio { get; set; }
	}
}