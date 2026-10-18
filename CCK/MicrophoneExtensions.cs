using Nox.Audio;

namespace Nox.CCK.Audio {
	/// <summary>Helpers for <see cref="IMicrophone"/> consumers.</summary>
	public static class MicrophoneExtensions {
		/// <summary>
		/// True when the device is silent: it is muted, or its volume is 0 — which captures nothing
		/// audible, so an indicator must treat it as a mute.
		/// </summary>
		public static bool IsSilent(this IMicrophone microphone)
			=> microphone != null && (microphone.IsMuted || microphone.Volume <= 0f);
	}
}
