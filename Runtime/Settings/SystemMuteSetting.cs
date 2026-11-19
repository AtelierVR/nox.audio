using Nox.Audio.Runtime.Channels;
using Nox.CCK.Settings;
using Nox.Settings;
using UnityEngine;

namespace Nox.Audio.Runtime {
	public sealed class SystemMuteSetting : TextHandler, IAudioSetting {
		public override string[] Path
			=> new[] { "audio", "channel", "system_mute" };

		public override int Order
			=> -1;

		public override bool IsActive
			=> SystemAudio.IsSilent;

		public SystemMuteSetting() {
			SetText(Key(SystemAudio.Flags));
			SystemAudio.OnMuteChanged.AddListener(OnMuteChanged);
		}

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/text.prefab");

		private void OnMuteChanged(MuteFlags flags) {
			SetText(Key(flags));
			SettingsNotifier.NotifyUpdated(null);
		}

		private static string Key(MuteFlags flags) {
			if ((flags & MuteFlags.Unity) != 0)
				return "settings.entry.audio.channel.system_mute.unity";
			if ((flags & MuteFlags.System) != 0)
				return "settings.entry.audio.channel.system_mute.system";
			return "settings.entry.audio.channel.system_mute";
		}

		public void Dispose()
			=> SystemAudio.OnMuteChanged.RemoveListener(OnMuteChanged);
	}
}
