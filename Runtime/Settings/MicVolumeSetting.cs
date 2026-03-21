using Nox.CCK.Settings;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	public sealed class MicVolumeSetting : RangeHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "volume" };

		public MicVolumeSetting() {
			SetRange(0f, 2f);
			SetStep(0.001f);
			SetValue(Main.Instance.Manager.ConfigVolume);
			SetLabelKey($"settings.entry.{string.Join(".", GetPath())}.label");
			SetValueKey("settings.range.value.percent");
		}

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/range.prefab");

		override protected void OnValueChanged(float value)
			=> Main.Instance.Manager.ConfigVolume = value;
	}
}