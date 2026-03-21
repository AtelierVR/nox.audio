using System.Collections.Generic;
using Nox.CCK.Settings;
using Nox.UI;
using Nox.UI.modals;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	public sealed class CurrentMicSetting : DropdownHandler {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "current" };

		protected override GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/dropdown.prefab");

		protected override IModalBuilder GetModalBuilder(IMenu menu)
			=> Main.UiAPI.MakeModal(menu);

		public CurrentMicSetting() {
			SetLabel($"settings.entry.{string.Join(".", GetPath())}.label");
			SetOptions(GetOptions());
			SetValue(Main.Instance.Manager.CurrentConfigName, false);
		}

		private static Dictionary<string, string[]> GetOptions() {
			var devices = Main.Instance.Manager.Microphones;
			var options = new Dictionary<string, string[]>();

			if (devices.Count == 0)
				return options;

			var defaultMic = Main.Instance.Manager.DefaultMicrophone;
			options.Add("default", new[] { "audio.microphone.default", defaultMic.GetName() });

			foreach (var t in devices) {
				if (t == defaultMic) continue;
				options.Add(t.GetName(), new[] { "value", t.GetName() });
			}

			return options;
		}

		protected override void OnValueChanged(string value)
			=> Main.Instance.Manager.CurrentConfigName = value;
	}
}