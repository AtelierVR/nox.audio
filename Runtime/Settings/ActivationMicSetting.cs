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

		public ActivationMicSetting() {
			SetRange(0f, 1f);
			SetStep(0.001f);
			SetValue(Main.Instance.Manager.ConfigActivation);
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
			=> Main.Instance.Manager.ConfigActivation = value;
	}
}