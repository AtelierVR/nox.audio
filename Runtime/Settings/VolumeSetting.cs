using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Combined volume slider + mute toggle for the current microphone.
	/// Uses the volume.prefab which contains both a range slider and a toggle.
	/// </summary>
	public sealed class VolumeSetting : RangeHandler, IAudioSetting {
		private Toggle _toggle;

		public override string[] GetPath()
			=> new[] { "audio", "microphone", "volume" };

		public override int GetOrder()
			=> 1;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/volume.prefab");

		public VolumeSetting() {
			SetRange(0f, 2f);
			SetStep(0.001f);

			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
			SetValueKey("settings.range.value.percent");

			Main.MicrophoneManager.OnCurrentChanged.AddListener(UpdateCurrent);
			UpdateCurrent(Main.MicrophoneManager.Current);
		}

		public override GameObject GetContent(RectTransform transform, IMenu menu) {
			var go = base.GetContent(transform, menu);

			// Set up mute toggle from the combined prefab
			_toggle = Reference.GetComponent<Toggle>("toggle", go);
			_toggle.onValueChanged.AddListener(OnToggleChanged);
			_toggle.SetIsOnWithoutNotify(Main.MicrophoneManager.Current.IsMuted);
			UpdateMuteType();

			return go;
		}

		private void OnToggleChanged(bool value) {
			Main.MicrophoneManager.Current.IsMuted = value;
			UpdateMuteType();
		}

		private void UpdateMuteType() {
			SetTypeKey(Main.MicrophoneManager.Current.IsMuted
				? "mute.active"
				: "settings.range.type.empty");
		}

		public void Dispose()
			=> Main.MicrophoneManager.OnCurrentChanged.RemoveListener(UpdateCurrent);

		private void UpdateCurrent(Microphone.Microphone arg0) {
			SetValue(Main.MicrophoneManager.Current.Volume, notify: false);
			_toggle?.SetIsOnWithoutNotify(Main.MicrophoneManager.Current.IsMuted);
			UpdateMuteType();
		}

		override protected void OnValueChanged(float value)
			=> Main.MicrophoneManager.Current.Volume = value;

		override protected void OnDestroy() {
			base.OnDestroy();
			_toggle.onValueChanged.RemoveListener(OnToggleChanged);
			_toggle = null;
		}
	}
}