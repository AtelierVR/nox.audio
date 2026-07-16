using Nox.Audio.Runtime.Microphone;
using Nox.CCK.Settings;
using Nox.Settings;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;
using Nox.CCK.Utils;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Debug setting that toggles microphone monitoring.
	/// Creates a <see cref="TestMicrophone"/> GameObject attached to the
	/// controller's head anchor so the user hears their own voice.
	/// </summary>
	public sealed class TestMicrophoneSetting : ButtonHandler, IAudioSetting {
		public override string[] GetPath()
			=> new[] { "debug", "audio", "microphone", "test" };

		public override int GetOrder()
			=> 999;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/button.prefab");

		public TestMicrophoneSetting() {
			SetLabel($"settings.entry.{string.Join(".", GetPath())}.label");
			SetButtonText(IsActive() ? "settings.debug.microphone.stop" : "settings.debug.microphone.start");
		}

		public override bool IsActive()
			=> TestMicrophone.Instance != null;

		public override void OnClick(IContext context) {
			if (IsActive()) {
				StopMonitoring();
			} else {
				StartMonitoring();
			}
			SetButtonText(IsActive() ? "settings.debug.microphone.stop" : "settings.debug.microphone.start");
		}

		private void StartMonitoring() {
			var controller = Main.ControllerAPI.Current;
			if (controller == null) {
				Logger.LogWarning("No current controller available.", tag: nameof(TestMicrophoneSetting));
				return;
			}

			// Get the head anchor transform
			var camera = controller.GetCamera();
			if (camera == null) {
				Logger.LogWarning("Controller has no camera parts.", tag: nameof(TestMicrophoneSetting));
				return;
			}

			var go = new GameObject("TestMicrophone");
			go.transform.SetParent(camera.transform, worldPositionStays: false);
			go.transform.localPosition = Vector3.zero;
			go.transform.localRotation = Quaternion.identity;

			var source = go.AddComponent<AudioSource>();
			source.spatialBlend = 1f;
			source.rolloffMode = AudioRolloffMode.Linear;
			source.minDistance = 0.1f;
			source.maxDistance = 1f;

			go.AddComponent<TestMicrophone>();

			Logger.Log($"Started monitoring on controller head.", tag: nameof(TestMicrophoneSetting));
		}

		private void StopMonitoring() {
			var instance = TestMicrophone.Instance;
			if (instance == null) return;
			instance.gameObject.Destroy();
			Logger.Log("Stopped monitoring.", tag: nameof(TestMicrophoneSetting));
		}

		public void Dispose() {
			StopMonitoring();
		}
	}
}
