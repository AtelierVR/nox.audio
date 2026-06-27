using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Audio.Runtime {
	public sealed class ActivationSetting : RangeHandler, IAudioSetting {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "activation" };

		public override int GetOrder()
			=> 1;

		public ActivationSetting() {
			SetRange(0f, 1f);
			SetStep(0.001f);

			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
			SetValueKey("settings.range.value.percent");

			Main.MicrophoneManager.OnCurrentChanged.AddListener(UpdateCurrent);
			UpdateCurrent(Main.MicrophoneManager.Current);
		}

		public void Dispose()
			=> Main.MicrophoneManager.OnCurrentChanged.RemoveListener(UpdateCurrent);

		private void UpdateCurrent(Microphone.Microphone arg0)
			=> SetValue(Main.MicrophoneManager.Current.Activation, notify: false);

		override protected void OnValueChanged(float value)
			=> Main.MicrophoneManager.Current.Activation = value;
		
		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("prefabs/activation.prefab");
		
		public override GameObject GetContent(RectTransform transform, IMenu menu) {
			var go        = base.GetContent(transform, menu);
			var reference = go.GetOrAddComponent<ActivationMicComponent>();
			reference.fill   = Reference.GetComponent<Image>("visual_fill", go);
			reference.visual = Reference.GetComponent<Slider>("visual", go);
			return go;
		}
	}
	
	public class ActivationMicComponent : MonoBehaviour {
		public Image fill;
		public Slider visual;

		public void Update() {
			var current = Main.MicrophoneManager.Current;

			if (current == null) {
				visual.value = 0f;
				return;
			}

			visual.value = current.Loudness;
		}
	}
}