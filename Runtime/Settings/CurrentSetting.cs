using System.Collections.Generic;
using Nox.CCK.Settings;
using Nox.UI;
using Nox.UI.modals;
using UnityEngine;

namespace Nox.Audio.Runtime {
	public sealed class CurrentSetting : DropdownHandler, IAudioSetting {
		public override string[] GetPath()
			=> new[] { "audio", "microphone", "current" };

		public override int GetOrder()
			=> 1;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/dropdown.prefab");

		override protected IModalBuilder GetModalBuilder(IMenu menu)
			=> Main.UiAPI.MakeModal(menu);

		public CurrentSetting() {
			SetLabel($"settings.entry.{string.Join(".", GetPath())}.label");
			SetOptions(GetOptions());
			SetValue(Main.MicrophoneManager.CurrentName, false);
		}

		private static Dictionary<string, string[]> GetOptions() {
			var devices = Main.MicrophoneManager.Microphones;
			var options = new Dictionary<string, string[]>();

			if (devices.Count == 0)
				return options;

			var defaultMic = Main.MicrophoneManager.Default;
			options.Add("default", new[] { "audio.microphone.default", defaultMic.Name });

			foreach (var t in devices) 
				options.Add(t.Name, new[] { "value", t.Name });
			
			return options;
		}

		override protected void OnValueChanged(string value)
			=> Main.MicrophoneManager.CurrentName = value;

		public void Dispose() { }
	}
}