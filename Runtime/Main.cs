using System;
using System.Linq;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.Controllers;
using Nox.Settings;
using Nox.UI;
using System.Collections.Generic;
using Nox.Audio.Runtime.Microphone;
using Nox.Audio.Runtime.Channels;

namespace Nox.Audio.Runtime {
	public class Main : IMainModInitializer, IMicrophoneAPI, IAudioAPI {
		static internal IMainModCoreAPI CoreAPI;
		static public MicrophoneManager MicrophoneManager;
		static internal ChannelManager ChannelManager;
		private IAudioSetting[] Settings = Array.Empty<IAudioSetting>();
		private LanguagePack _lang;
		private EventSubscription[] _events = Array.Empty<EventSubscription>();

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

			// Preload the native RNNoise denoiser (optional — mic still works without it).
			try {
				api.LibAPI.Load("rnnoise");
			} catch (DllNotFoundException) {
				CoreAPI.LoggerAPI.Log("RNNoise native library not found; noise suppression disabled.");
			}

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

			// Subscribe to events.
			_events = new[] {
				CoreAPI.EventAPI.Subscribe("mute", OnMuteEvent),
			};
		}

		private DateTime _lastUpdate = DateTime.MinValue;

		public void OnUpdateMain() {
			// Per-frame: drive the microphone DSP (activation gate, etc.).
			MicrophoneManager?.Update();

			// Throttled: refresh the device list (hot-plug detection).
			if ((DateTime.Now - _lastUpdate).TotalSeconds < 5)
				return;
			_lastUpdate = DateTime.Now;
			MicrophoneManager.Refresh();
		}


		public void OnDisposeMain() {
			foreach (var subscription in _events)
				CoreAPI.EventAPI.Unsubscribe(subscription);
			_events = Array.Empty<EventSubscription>();

			foreach (var setting in Settings)
				SettingAPI.Remove(setting.GetPath());
			Settings = Array.Empty<IAudioSetting>();

			LanguageManager.RemovePack(_lang);

			ChannelManager.Dispose();
			MicrophoneManager.Dispose();

			CoreAPI.LibAPI.Unload("opus");
			CoreAPI.LibAPI.Unload("rnnoise");

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

		// ── Mute event ────────────────────────────────

		/// <summary>
		/// Handles the "mute" event by toggling the current microphone's mute state.
		/// </summary>
		private void OnMuteEvent(EventData data) {
			var mic = MicrophoneManager.Current;
			if (mic == null)
				return;
			mic.IsMuted = !mic.IsMuted;
		}

		// ── ControllerAPI ────────────────────────────

		internal static IControllerAPI ControllerAPI
			=> CoreAPI.ModAPI
				.GetMod("controllers")
				.GetInstance<IControllerAPI>();
	}
}