namespace Nox.Audio {
	/// <summary>
	/// Public API for managing volume audio channels.
	/// Implemented by the nox.audio runtime.
	/// </summary>
	public interface IAudioAPI {
		/// <summary>
		/// Register a new volume audio channel.
		/// Returns the created <see cref="IChannelAudio"/> instance.
		/// </summary>
		IChannelAudio Register(string id, string[] dependencies = null);

		/// <summary>
		/// Request to unregister a volume audio channel.
		/// by setting Event audio.channel.remove_requested to <c>false</c>.
		/// If no listener cancels the request, the channel is removed.
		/// </summary>
		void UnRegister(string id);
	}
}