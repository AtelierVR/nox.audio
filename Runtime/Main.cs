using System;
using System.Linq;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Settings;
using Nox.UI;
using System.Collections.Generic;
using Nox.Audio.Runtime.Microphone;
using Nox.Audio.Runtime.Channels;

namespace Nox.Audio.Runtime {
	public class Main : IMainModInitializer, IMicrophoneAPI, IAudioAPI {
		static internal IMainModCoreAPI CoreAPI;
		static internal MicrophoneManager MicrophoneManager;
		static internal ChannelManager ChannelManager;
		private IAudioSetting[] Settings = Array.Empty<IAudioSetting>();
		private LanguagePack _lang;

		public static ISettingAPI SettingAPI
			=> CoreAPI.ModAPI
				.GetMod("settings")
				.GetInstance<ISettingAPI>();

		public static IUiAPI UiAPI
			=> CoreAPI.ModAPI
				.GetMod("ui")
				.GetInstance<IUiAPI>();

		public void OnInitializeMain(IMainModCoreAPI api) {
			CoreAPI           = api;
			MicrophoneManager = new MicrophoneManager();
			ChannelManager    = new ChannelManager();

			// Preload the native opus library (required by OpusEncoder / OpusDecoder)
			api.LibAPI.Load("opus");

			_lang = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);

			CoreAPI.LoggerAPI.Log("Microphone API initialized.");
			var @default = MicrophoneManager.Default;
			CoreAPI.LoggerAPI.Log($"Default microphone: {@default?.Name ?? "null"}");

			// Register volume channels (generates dynamic volume/mute settings)
			Register("general");

			// Microphone settings
			Settings = new IAudioSetting[] {
				new CurrentSetting(),
				new VolumeSetting(),
				new ActivationSetting(),
				new NoiseSuppressionSetting()
			};

			foreach (var setting in Settings)
				SettingAPI.Add(setting);

			MicrophoneManager.Refresh();
		}

		private DateTime _lastUpdate = DateTime.MinValue;

		public void OnUpdateMain() {
			if ((DateTime.Now - _lastUpdate).TotalSeconds < 5)
				return;
			_lastUpdate = DateTime.Now;
			MicrophoneManager.Refresh();
		}


		public void OnDisposeMain() {
			LanguageManager.RemovePack(_lang);

			foreach (var setting in Settings)
				SettingAPI.Remove(setting.GetPath());
			Settings = Array.Empty<IAudioSetting>();

			ChannelManager.Dispose();
			MicrophoneManager.Dispose();

			CoreAPI.LibAPI.Unload("opus");

			ChannelManager    = null;
			MicrophoneManager = null;
			CoreAPI           = null;
		}

		// ── IMicrophoneAPI ────────────────────────────

		public IMicrophone Default
			=> MicrophoneManager.Default;

		public IEnumerable<IMicrophone> All
			=> MicrophoneManager.Microphones
				.Cast<IMicrophone>()
				.ToArray();

		public IMicrophone Current
			=> MicrophoneManager.Current;

		public IMicrophone Get(string name)
			=> MicrophoneManager.Microphones
				.FirstOrDefault(m => m.Name == name);

		// ── IAudioAPI ─────────────────────────────────

		public IChannelAudio Register(string id, string[] dependencies = null)
			=> ChannelManager.Register(id, dependencies);

		public void UnRegister(string id)
			=> ChannelManager.UnRegister(id);
	}
}