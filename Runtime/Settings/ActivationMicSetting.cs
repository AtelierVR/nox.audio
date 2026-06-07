using Nox.CCK.Microphone;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Microphone.Runtime {
	public class ActivationMicComponent : MonoBehaviour {
		public Image  visualFill;
		public Slider visual;

		private void Update() {
			var current = Main.Instance.Manager.CurrentMicrophone;
			if (current == null) {
				visual.value = 0f;
				return;
			}

			visual.value = current.GetLoudness();
		}
	}

	public sealed class ActivationMicSetting : RangeHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "activation" };

		public static float Value {
			get => Config.Load().Get("settings.voice.activation", 0.1f);
			set {
				var config = Config.Load();
				config.Set("settings.voice.activation", value);
				config.Save();
				MicrophoneSettings.ActivationThreshold = value;
				MicrophoneSettings.OnActivationThresholdChanged.Invoke(value);
			}
		}

		public ActivationMicSetting() {
			SetRange(0f, 1f);
			SetStep(0.001f);
			SetValue(Value, notify: false);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
			SetValueKey("settings.range.value.percent");
		}

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("prefabs/activation.prefab");


		public override GameObject GetContent(RectTransform transform, IMenu menu) {
			var go        = base.GetContent(transform, menu);
			var reference = go.GetOrAddComponent<ActivationMicComponent>();
			reference.visualFill = Reference.GetComponent<Image>("visual_fill", go);
			reference.visual     = Reference.GetComponent<Slider>("visual", go);
			return go;
		}

		override protected void OnValueChanged(float value)
			=> Value = value;
	}
}