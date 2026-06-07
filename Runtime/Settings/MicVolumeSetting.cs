using Nox.CCK.Microphone;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	public sealed class MicVolumeSetting : RangeHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "volume" };

		public static float Value {
			get => Config.Load().Get("settings.voice.volume", 1f);
			set {
				var config = Config.Load();
				config.Set("settings.voice.volume", value);
				config.Save();
				MicrophoneSettings.Volume = value;
				MicrophoneSettings.OnVolumeChanged.Invoke(value);
			}
		}

		public MicVolumeSetting() {
			SetRange(0f, 2f);
			SetStep(0.001f);
			SetValue(Value, notify: false);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
			SetValueKey("settings.range.value.percent");
		}

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/range.prefab");

		override protected void OnValueChanged(float value)
			=> Value = value;
	}
}