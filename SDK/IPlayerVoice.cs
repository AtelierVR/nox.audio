namespace Nox.Audio.Players {
	/// <summary>
	/// Voice info exposed by every player (local and remote).
	/// Placed in <c>Nox.Audio</c> similarly to how <c>IPlayerAvatar</c>
	/// is placed in <c>nox.avatars</c>.
	/// <para>
	/// Consumers (UI, desktop, XR) can query voice state, the live
	/// <see cref="ICapturedAudio"/>, and the current activity <see cref="LevelFlags"/>
	/// without depending on the relay runtime.
	/// </para>
	/// </summary>
	public interface IPlayerVoice {
		/// <summary>
		/// Hearing / reception mode.
		/// Server-authoritative — clients may read but not set.
		/// </summary>
		ListenMode Listen { get; }

		/// <summary>
		/// Speaking / emission mode.
		/// <list type="bullet">
		///   <item><see cref="SpeakMode.Muted"/>     — emits nothing.</item>
		///   <item><see cref="SpeakMode.Whisper"/>   — chuchotement, short range.</item>
		///   <item><see cref="SpeakMode.Normal"/>    — standard range.</item>
		///   <item><see cref="SpeakMode.Loud"/>      — fort, larger zone.</item>
		///   <item><see cref="SpeakMode.Broadcast"/> — global range.</item>
		/// </list>
		/// </summary>
		SpeakMode Speak { get; set; }

		/// <summary>
		/// The <see cref="ICapturedAudio"/> currently associated with this player's voice.
		/// For a remote player this is <c>null</c> (playback is handled internally).
		/// For the local player this is the live microphone source (see <see cref="ILocalPlayerVoice"/>).
		/// Returns <c>null</c> when no voice is active.
		/// </summary>
		ICapturedAudio Audio { get; }

		/// <summary>
		/// Current voice activity level, updated each frame.
		/// Combine with <see cref="LevelFlags.Speaking"/> to detect active voice.
		/// </summary>
		LevelFlags Level { get; }

		/// <summary>
		/// Per-player voice volume [0, 2], persisted locally in <c>.nox/</c>.
		/// The effective volume heard by listeners is <see cref="EffectiveVolume"/>,
		/// which also accounts for the <c>voice</c> and <c>general</c> channels.
		/// </summary>
		float Volume { get; set; }

		/// <summary>
		/// Whether this player is locally muted.
		/// A player is effectively muted if locally muted OR any parent channel is muted.
		/// </summary>
		bool IsMuted { get; set; }

		/// <summary>
		/// Effective volume, clamped by the <c>voice</c> and <c>general</c> channel hierarchy.
		/// </summary>
		float EffectiveVolume { get; }

		/// <summary>
		/// Effective mute state, considering the channel hierarchy.
		/// </summary>
		bool IsEffectivelyMuted { get; }
	}
}