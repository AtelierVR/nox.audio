using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Nox.CCK.Utils;

namespace Nox.Audio.Runtime.Channels {
	/// <summary>
	/// Runtime implementation of <see cref="IChannelAudio"/>.
	/// Managed by <see cref="ChannelManager"/>.
	/// </summary>
	public sealed class ChannelAudio : IChannelAudio {
		private readonly ChannelManager _manager;

		public string Id { get; }
		public string[] Depends { get; internal set; }

		internal int Priority = -1;

		public ChannelAudio(ChannelManager manager, string id, string[] depends) {
			_manager = manager;
			Id       = id;
			Depends  = depends;
		}

		private string[] GetSetting(string sub)
			=> new[] {
				"settings",
				"channels",
				Id,
				sub
			};

		public readonly UnityEvent<float> OnVolume = new();

		/// <summary>
		/// Raw volume [0, 1]. Persisted via Config. Setting this notifies the manager to propagate to dependents.
		/// </summary>
		public float Volume {
			get => Config.Load().Get(GetSetting("volume"), 1f);
			set {
				var old = Volume;
				var val = Mathf.Clamp01(value);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(GetSetting("volume"), val);
				config.Save();
				OnVolumeChanged();
			}
		}

		public readonly UnityEvent<bool> OnMute = new();

		/// <summary>
		/// Explicit mute. Persisted via Config. A channel is effectively muted if it is muted OR any parent is muted.
		/// </summary>
		public bool IsMuted {
			get => Config.Load().Get(GetSetting("mute"), false);
			set {
				var old = IsMuted;
				if (old == value)
					return;
				var config = Config.Load();
				config.Set(GetSetting("mute"), value);
				config.Save();
				OnMuteChanged();
			}
		}

		/// <summary>
		/// Effective mute state, considering parent chain.
		/// </summary>
		public bool IsEffectivelyMuted {
			get {
				if (IsMuted)
					return true;

				foreach (var id in Depends) {
					var depend = _manager.Get(id);
					if (depend is { IsEffectivelyMuted: true })
						return true;
				}

				return false;
			}
		}

		/// <summary>
		/// Effective volume, clamped by the minimum of all parent volumes.
		/// </summary>
		public float EffectiveVolume {
			get {
				var effective = Volume;

				foreach (var id in Depends) {
					var depend = _manager.Get(id);
					effective = Mathf.Min(effective, depend.EffectiveVolume);
				}

				return effective;
			}
		}

		/// <summary>
		/// Called when a dependency's volume changed.
		/// Because our EffectiveVolume depends on parents, we notify our own dependents.
		/// </summary>
		internal void OnVolumeChanged() {
			OnVolume.Invoke(Volume);
			Main.CoreAPI.EventAPI.Emit("audio.channel.volume_changed", this, Volume, EffectiveVolume);
			NotifyDependentsVolumeChanged();
		}

		/// <summary>
		/// Called when a dependency's mute state changed.
		/// Because our IsEffectivelyMuted depends on parents, we notify our own dependents.
		/// </summary>
		internal void OnMuteChanged() {
			OnMute.Invoke(IsMuted);
			Main.CoreAPI.EventAPI.Emit("audio.channel.mute_changed", this, IsMuted, IsEffectivelyMuted);
			NotifyDependentsMuteChanged();
		}

		private void NotifyDependentsVolumeChanged() {
			foreach (var kvp in _manager.Channels) {
				if (kvp.Value.Item1 == this)
					continue;
				if (kvp.Value.Item1.Depends.Contains(Id))
					kvp.Value.Item1.OnVolumeChanged();
			}
		}

		private void NotifyDependentsMuteChanged() {
			foreach (var kvp in _manager.Channels) {
				if (kvp.Value.Item1 == this)
					continue;
				if (kvp.Value.Item1.Depends.Contains(Id))
					kvp.Value.Item1.OnMuteChanged();
			}
		}
	}
}