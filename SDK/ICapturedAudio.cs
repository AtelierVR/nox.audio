using UnityEngine;

namespace Nox.Audio.Players {
	/// <summary>
	/// Abstracts a live audio source used by the voice pipeline.
	/// Carries both the raw <see cref="AudioClip"/> buffer and the current
	/// recording position, so that <c>VoiceSender</c>, <c>VoiceAvatarModule</c>
	/// and the avatar AudioSource all share a single authoritative source.
	/// <para>
	/// Use <see cref="MicrophoneAudio"/> to wrap a microphone device.
	/// </para>
	/// </summary>
	public interface ICapturedAudio {
		/// <summary>The underlying <see cref="AudioClip"/> circular buffer.</summary>
		AudioClip Clip { get; }

		/// <summary>
		/// Current write position in samples within <see cref="Clip"/>.
		/// Matches Unity's <c>Microphone.GetPosition()</c> when backed by a microphone.
		/// </summary>
		int Position { get; }
	}
}
