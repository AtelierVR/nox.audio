using System;
using System.Collections.Generic;
using Nox.Audio.Players;
using Nox.CCK.Audio;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.UI;
using Nox.UI.modals;
using UnityEngine;

namespace Nox.Audio.Runtime {
	public sealed class ChangeSessionSetting : DropdownHandler, IAudioSetting {
		public override string[] Path
			=> new[] { "audio", "microphone", "change_session" };

		public override int Order
			=> 40001;

		private static string[] ConfigPath
			=> new[] { "settings", "audio", "voice", "change_session" };

		public ChangeSessionSetting() {
			SetLabel($"settings.entry.{string.Join(".", Path)}.label");
			SetOptions(GetOptions());

			var value = Value;
			SetValue(Key(value), notify: false);
			VoiceModeSettings.SetChangeOnJoin(value);
		}

		private Dictionary<string, string[]> GetOptions() {
			var options = new Dictionary<string, string[]>();

			Add(null);

			foreach (SpeakMode mode in Enum.GetValues(typeof(SpeakMode)))
				Add(mode);

			return options;

			void Add(SpeakMode? mode) {
				var key = Key(mode);
				options[key] = new[] { $"settings.entry.{string.Join(".", Path)}.option.{key}" };
			}
		}

		protected override GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/dropdown.prefab");

		protected override IModalBuilder GetModalBuilder(IMenu menu)
			=> Main.UiAPI.MakeModal(menu);

		protected override void OnValueChanged(string value)
			=> Value = Parse(value);

		public static string Key(SpeakMode? mode)
			=> mode == null
				? "none"
				: mode.Value.ToString().ToSnakeCase();

		public static SpeakMode? Parse(string key)
			=> Enum.TryParse(key, true, out SpeakMode mode)
				? mode
				: null;

		public static new SpeakMode? Value {
			get => Parse(Config.Load().Get(ConfigPath, "none"));
			set {
				var config = Config.Load();
				config.Set(ConfigPath, Key(value));
				VoiceModeSettings.SetChangeOnJoin(value);
				config.Save();
			}
		}

		public void Dispose() { }
	}
}
