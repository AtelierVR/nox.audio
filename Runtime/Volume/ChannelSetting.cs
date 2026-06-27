using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.Settings;
using Nox.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Audio.Runtime.Channels {
	/// <summary>
	/// Combined volume slider + mute toggle for a <see cref="ChannelAudio"/> channel.
	/// Generated automatically by <see cref="ChannelManager"/> on Register.
	/// Uses the volume.prefab which contains both a range slider and a toggle.
	/// </summary>
	public sealed class ChannelSetting : RangeHandler, IAudioSetting {
		private readonly ChannelAudio _channel;
		private Toggle _toggle;

		public override string[] GetPath()
			=> new[] { "audio", "channel", _channel.Id };

		public override int GetOrder()
			=> _channel.Priority;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/volume.prefab");

		public ChannelSetting(ChannelAudio channel) {
			_channel = channel;
			SetRange(0f, 1f);
			SetStep(0.01f);
			SetLabelKey($"settings.entry.audio.channel.{channel.Id}");
			SetValueKey("settings.range.value.percent");
			SetValue(channel.Volume, notify: false);
		}

		public override GameObject GetContent(RectTransform transform, IMenu menu) {
			var go = base.GetContent(transform, menu);

			// Set up mute toggle from the combined prefab
			_toggle = Reference.GetComponent<Toggle>("toggle", go);
			_toggle.onValueChanged.AddListener(OnToggleChanged);
			_toggle.SetIsOnWithoutNotify(_channel.IsMuted);
			UpdateMuteType();

			return go;
		}

		private void OnToggleChanged(bool value) {
			_channel.IsMuted = value;
			UpdateMuteType();
		}

		private void UpdateMuteType() {
			SetTypeKey(_channel.IsMuted
				? "mute.active"
				: "settings.range.type.empty");
		}

		override protected void OnValueChanged(float value)
			=> _channel.Volume = value;

		override protected void OnDestroy() {
			base.OnDestroy();
			_toggle.onValueChanged.RemoveListener(OnToggleChanged);
			_toggle = null;
		}

		public void Dispose() { }
	}
}