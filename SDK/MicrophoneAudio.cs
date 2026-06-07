using UnityEngine;

namespace Nox.Microphone.Players {
	/// <summary>
	/// <see cref="IAudio"/> implementation backed by a live <see cref="IMicrophone"/> device.
	/// Created by the controller's <c>MicrophoneConnector</c> and assigned to the local
	/// player's <see cref="ILocalPlayerVoice.Audio"/>.
	/// <para>
	/// The clip is obtained once from <c>IMicrophone.Start()</c> and held for the lifetime
	/// of the session binding; <see cref="GetPosition"/> delegates to the microphone device
	/// so every consumer always reads the freshest write head.
	/// </para>
	/// </summary>
	public sealed class MicrophoneAudio : IAudio {
		private readonly AudioClip   _clip;
		private readonly IMicrophone _mic;

		public MicrophoneAudio(AudioClip clip, IMicrophone mic) {
			_clip = clip;
			_mic  = mic;
		}

		/// <inheritdoc/>
		public AudioClip Clip => _clip;

		/// <inheritdoc/>
		public int GetPosition() => _mic.GetPosition();
	}
}
