using UnityEngine;
using Nox.Audio.Players;
using Nox.Audio;

namespace Nox.CCK.Audio {
	/// <summary>
	/// <see cref="IAudio"/> implementation backed by a live <see cref="IMicrophone"/> device.
	/// Built by the local player itself — it calls <c>IMicrophone.Start()</c> when it enters a room and
	/// exposes the result as its <see cref="ILocalPlayerVoice.Audio"/> — so the capture follows the
	/// audio mod's current microphone for the whole time the player is entered.
	/// <para>
	/// The clip is obtained once from <c>IMicrophone.Start()</c> and held for the lifetime
	/// of the session binding; <see cref="Position"/> delegates to the microphone device
	/// so every consumer always reads the freshest write head.
	/// </para>
	/// </summary>
	public sealed class CapturedMicrophone : ICapturedAudio {
		private readonly AudioClip   _clip;
		private readonly IMicrophone _microphone;

		public CapturedMicrophone(AudioClip clip, IMicrophone microphone) {
			_clip = clip;
			_microphone  = microphone;
		}

		/// <inheritdoc/>
		public AudioClip Clip 
			=> _clip;

		/// <inheritdoc/>
		public int Position 
			=> _microphone.Position;

		/// <inheritdoc/>
		public float Level
			=> _microphone?.Loudness ?? 0f;
	}
}
