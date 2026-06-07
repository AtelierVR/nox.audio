using Nox.CCK.Microphone;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	/// <summary>
	/// Toggle: mute/unmute the microphone globally.
	/// Path: audio > microphone > mute
	/// </summary>
	public sealed class MuteMicSetting : ToggleHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "mute" };

		protected override GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/toggle.prefab");

		public static bool Value {
			get => Config.Load().Get("settings.voice.mute", false);
			set {
				var config = Config.Load();
				config.Set("settings.voice.mute", value);
				config.Save();
				MicrophoneSettings.Mute = value;
				MicrophoneSettings.OnMuteChanged.Invoke(value);
			}
		}

		public MuteMicSetting() {
			SetValue(Value, notify: false);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
		}

		protected override void OnValueChanged(bool value)
			=> Value = value;
	}
}
