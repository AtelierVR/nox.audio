using Nox.CCK.Settings;
using UnityEngine;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Toggle: enable/disable noise suppression (noise gate) on microphone input.
	/// Path: audio > microphone > noise_suppression
	/// </summary>
	public sealed class NoiseSuppressionSetting : ToggleHandler, IAudioSetting {
		public const float NORMAL = 0.6f;
		public const float DISABLED = 0f;

		public override string[] GetPath()
			=> new[] { "audio", "microphone", "noise_suppression" };

		public override int GetOrder()
			=> 1;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/toggle.prefab");

		public NoiseSuppressionSetting() {
			SetValue(Value, notify: false);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");

			Main.MicrophoneManager.OnCurrentChanged.AddListener(UpdateCurrent);
			UpdateCurrent(Main.MicrophoneManager.Current);
		}

		public void Dispose()
			=> Main.MicrophoneManager.OnCurrentChanged.RemoveListener(UpdateCurrent);

		private void UpdateCurrent(Microphone.Microphone arg0)
			=> SetValue(
				Mathf.Approximately(Main.MicrophoneManager.Current.NoiseSuppression, NORMAL),
				notify: false
			);

		override protected void OnValueChanged(bool value)
			=> Main.MicrophoneManager.Current.NoiseSuppression = value
				? NORMAL
				: DISABLED;

	}
}