namespace Nox.Audio.Players {
	/// <summary>
	/// Extends <see cref="IPlayerVoice"/> for the local player.
	/// Allows a controller (Desktop, XR, …) to push a <see cref="MicrophoneAudio"/>
	/// the same way controllers push an avatar via <c>IPlayerAvatar.SetAvatar</c>.
	/// </summary>
	public interface ILocalPlayerVoice : IPlayerVoice {
		/// <summary>
		/// Get or set the live <see cref="ICapturedAudio"/> for this local player.
		/// <para>
		/// The controller assigns a <see cref="MicrophoneAudio"/> built from
		/// <c>IMicrophone.Start()</c>. The relay <c>VoiceSender</c> reads the clip
		/// and position from it; <c>LocalPlayer.RouteClipToAvatar</c> syncs the
		/// avatar AudioSource to the live write head.
		/// </para>
		/// </summary>
		new ICapturedAudio Audio { get; set; }
	}
}