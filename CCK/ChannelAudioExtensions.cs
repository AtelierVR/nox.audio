using Nox.Audio;

namespace Nox.CCK.Audio {
	/// <summary>Helpers for <see cref="IChannelAudio"/> consumers.</summary>
	public static class ChannelAudioExtensions {
		/// <summary>
		/// True when the channel is silent: it is muted (itself or through a parent), or its effective
		/// volume is 0 — which is audibly the same as a mute, so an indicator must treat it as one.
		/// </summary>
		public static bool IsSilent(this IChannelAudio channel)
			=> channel != null && (channel.IsEffectivelyMuted || channel.EffectiveVolume <= 0f);
	}
}
