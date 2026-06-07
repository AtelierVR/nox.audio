using Nox.CCK.Microphone;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	/// <summary>
	/// Toggle: enable/disable noise suppression (noise gate) on microphone input.
	/// Path: audio > microphone > noise_suppression
	/// </summary>
	public sealed class NoiseSuppressionSetting : ToggleHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "noise_suppression" };

		protected override GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/toggle.prefab");

		public static bool Value {
			get => Config.Load().Get("settings.voice.noise_suppression", false);
			set {
				var config = Config.Load();
				config.Set("settings.voice.noise_suppression", value);
				config.Save();
				MicrophoneSettings.NoiseSuppression = value;
				MicrophoneSettings.OnNoiseSuppressionChanged.Invoke(value);
			}
		}

		public NoiseSuppressionSetting() {
			SetValue(Value, notify: false);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
		}

		protected override void OnValueChanged(bool value)
			=> Value = value;
	}
}
