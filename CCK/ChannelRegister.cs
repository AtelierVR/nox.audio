using System;
using Nox.Audio;
using Nox.CCK.Events;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Utils;

namespace Nox.CCK.Audio {
	/// <summary>
	/// Registers an audio channel and protects it from removal.
	/// Subscribes to <c>audio.channel.remove_requested</c> and blocks
	/// removal until <see cref="Dispose"/> is called.
	/// <para>
	/// Usage in <c>nox.relay</c>:
	/// <code>
	/// _voiceChannel = new ChannelRegister("voice", new[]{"general"}, CoreAPI);
	/// </code>
	/// </para>
	/// </summary>
	public sealed class ChannelRegister : IDisposable {
		private readonly string _id;
		private readonly IModCoreAPI _coreAPI;
		private EventSubscription[] _subscriptions = Array.Empty<EventSubscription>();

		public ChannelRegister(string id, string[] depends, IModCoreAPI coreAPI) {
			_id      = id;
			_coreAPI = coreAPI;

			var api = coreAPI.ModAPI
				.GetMod("audio")
				?.GetInstance<IAudioAPI>();

			if (api == null)
				throw new InvalidOperationException($"ChannelRegister: Audio API not available, cannot register channel '{id}'.");

            Channel = api.Register(id, depends);
			_subscriptions = new[] {
				coreAPI.EventAPI.Subscribe("audio.channel.remove_requested", OnRemoveRequested),
				coreAPI.EventAPI.Subscribe("audio.channel.volume_changed", OnVolumeChanged),
				coreAPI.EventAPI.Subscribe("audio.channel.mute_changed", OnMuteChanged)
			};
		}


        public IChannelAudio Channel { get; }

		public readonly NoxEvent<float, float> OnVolume = new();

		public readonly NoxEvent<bool, bool> OnMute = new();

        private void OnRemoveRequested(EventData context) {
			if (!context.TryGet(0, out (IChannelAudio, object) tuple))
				return;
			if (tuple.Item1?.Id != _id)
				return;
			if (!context.TryGet(1, out Action<object[]> callback))
				return;

			callback(new object[] { false });
		}

        private void OnMuteChanged(EventData context) {
			if (!context.TryGet(0, out IChannelAudio c) || c.Id != _id)
				return;
			OnMute.Invoke(Channel.IsMuted, Channel.IsEffectivelyMuted);
        }

        private void OnVolumeChanged(EventData context) {
			if (!context.TryGet(0, out IChannelAudio c) || c.Id != _id)
				return;
			OnVolume.Invoke(Channel.Volume, Channel.EffectiveVolume);
        }

		public void Dispose() {
			foreach (var subscription in _subscriptions)
				_coreAPI.EventAPI.Unsubscribe(subscription);
			_subscriptions = Array.Empty<EventSubscription>();
			_coreAPI.ModAPI
				.GetMod("audio")
				?.GetInstance<IAudioAPI>()
				?.UnRegister(_id);
		}
	}
}
