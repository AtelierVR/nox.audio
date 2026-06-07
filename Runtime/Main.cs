using System;
using System.Linq;
using Nox.CCK.Language;
using Nox.CCK.Microphone;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Microphone;
using Nox.Settings;
using Nox.UI;

namespace Nox.Microphone.Runtime {
	public class Main : IMainModInitializer, IMicrophoneAPI {
		internal static Main              Instance;
		internal static IMainModCoreAPI    CoreAPI;
		internal        MicrophoneManager Manager;
		internal        IHandler[]        Settings;
		private         LanguagePack      _lang;

		public static IMicrophoneAPI MicrophoneAPI => Instance;

		public static ISettingAPI SettingAPI
			=> CoreAPI.ModAPI
				.GetMod("settings")
				.GetInstance<ISettingAPI>();

		public static IUiAPI UiAPI
			=> CoreAPI.ModAPI
				.GetMod("ui")
				.GetInstance<IUiAPI>();

		public void OnInitializeMain(IMainModCoreAPI api) {
			CoreAPI  = api;
			Instance = this;
			Manager  = new MicrophoneManager();

			// Pre-load the native opus library (required by OpusEncoder / OpusDecoder)
			api.LibAPI.Load("opus");

			_lang    = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);

			MicrophoneManager.OnAdded.AddListener(OnMicrophoneAdded);
			MicrophoneManager.OnRemoved.AddListener(OnMicrophoneRemoved);
			MicrophoneManager.OnDefaultChanged.AddListener(OnMicrophoneDefaultChanged);
			MicrophoneManager.OnIndexChanged.AddListener(OnMicrophoneIndexChanged);
			MicrophoneManager.OnCurrentChanged.AddListener(OnMicrophoneCurrentChanged);

			MicrophoneSettings.OnActivationThresholdChanged.AddListener(OnActivationThresholdChanged);
			MicrophoneSettings.OnNoiseSuppressionChanged.AddListener(OnNoiseSuppressionChanged);
			MicrophoneSettings.OnMuteChanged.AddListener(OnMuteChanged);
			MicrophoneSettings.OnCurrentMicrophoneChanged.AddListener(OnCurrentMicrophoneSettingChanged);
			MicrophoneSettings.OnVolumeChanged.AddListener(OnVolumeChanged);

			CoreAPI.LoggerAPI.Log("Microphone API initialized.");
			var defaultMic = Manager.DefaultMicrophone;
			CoreAPI.LoggerAPI.Log($"Default microphone: {defaultMic?.GetName() ?? "null"}");

			Settings = new IHandler[] {
				new CurrentMicSetting(),
				new MicVolumeSetting(),
				new ActivationMicSetting(),
				new NoiseSuppressionSetting(),
				new MuteMicSetting()
			};

			foreach (var setting in Settings)
				SettingAPI.Add(setting);

			// Initialize MicrophoneSettings from persisted config (no events on startup)
			MicrophoneSettings.ActivationThreshold = ActivationMicSetting.Value;
			MicrophoneSettings.NoiseSuppression    = NoiseSuppressionSetting.Value;
			MicrophoneSettings.Mute                = MuteMicSetting.Value;
			MicrophoneSettings.Volume              = MicVolumeSetting.Value;
			MicrophoneSettings.CurrentMicrophone   = Manager.CurrentMicrophone?.GetName();

			Manager.Refresh();
		}

		private DateTime _lastUpdate = DateTime.MinValue;

		public void OnUpdateMain() {
			if ((DateTime.Now - _lastUpdate).TotalSeconds < 5) return;
			_lastUpdate = DateTime.Now;
			Manager.Refresh();
		}

		private void OnMicrophoneIndexChanged(MicrophoneManager arg0, Microphone arg1, int arg2, int arg3) {
			CoreAPI.EventAPI.Emit("microphone_index_changed", arg1, arg2, arg3);
			CoreAPI.LoggerAPI.Log($"Microphone index changed: {arg1.GetName()} ({arg2} -> {arg3})");
		}

		private void OnMicrophoneDefaultChanged(MicrophoneManager arg0, Microphone arg1, Microphone arg2) {
			CoreAPI.EventAPI.Emit("microphone_default_changed", arg1, arg2);
			CoreAPI.LoggerAPI.Log($"Microphone default changed: {arg1?.GetName() ?? "null"} -> {arg2?.GetName() ?? "null"}");
		}

		private void OnMicrophoneRemoved(MicrophoneManager arg0, Microphone arg1) {
			CoreAPI.EventAPI.Emit("microphone_removed", arg1);
			CoreAPI.LoggerAPI.Log($"Microphone removed: {arg1.GetName()}");
		}

		private void OnMicrophoneAdded(MicrophoneManager manager, Microphone mic) {
			CoreAPI.EventAPI.Emit("microphone_added", mic);
			CoreAPI.LoggerAPI.Log($"Microphone added: {mic.GetName()} (Index: {mic.GetIndex()}, Frequencies: {mic.GetFrequencies().x}-{mic.GetFrequencies().y})");
		}

		private void OnMicrophoneCurrentChanged(MicrophoneManager arg0, Microphone arg1, Microphone arg2) {
			arg1?.Stop("current");
			arg2?.Start("current");
			var prevName = MicrophoneSettings.CurrentMicrophone;
			MicrophoneSettings.CurrentMicrophone = arg2?.GetName();
			MicrophoneSettings.OnCurrentMicrophoneChanged.Invoke(prevName, arg2?.GetName());
			CoreAPI.EventAPI.Emit("microphone_current_changed", arg1, arg2);
			CoreAPI.LoggerAPI.Log($"Microphone current changed: {arg1?.GetName() ?? "null"} -> {arg2?.GetName() ?? "null"}");
		}

		private void OnActivationThresholdChanged(float value) {
			CoreAPI.EventAPI.Emit("microphone_activation_threshold_changed", value);
		}

		private void OnNoiseSuppressionChanged(bool value) {
			CoreAPI.EventAPI.Emit("microphone_noise_suppression_changed", value);
		}

		private void OnMuteChanged(bool value) {
			CoreAPI.EventAPI.Emit("microphone_mute_changed", value);
		}

		private void OnVolumeChanged(float value) {
			CoreAPI.EventAPI.Emit("microphone_volume_changed", value);
		}

		private void OnCurrentMicrophoneSettingChanged(string previous, string current) {
			CoreAPI.EventAPI.Emit("microphone_setting_current_changed", previous, current);
		}


		public void OnDisposeMain() {
			LanguageManager.RemovePack(_lang);

			foreach (var setting in Settings)
				SettingAPI.Remove(setting.GetPath());
			Settings = Array.Empty<IHandler>();
			MicrophoneManager.OnAdded.RemoveListener(OnMicrophoneAdded);
			MicrophoneManager.OnRemoved.RemoveListener(OnMicrophoneRemoved);
			MicrophoneManager.OnDefaultChanged.RemoveListener(OnMicrophoneDefaultChanged);
			MicrophoneManager.OnIndexChanged.RemoveListener(OnMicrophoneIndexChanged);
			MicrophoneManager.OnCurrentChanged.RemoveListener(OnMicrophoneCurrentChanged);
			MicrophoneSettings.OnActivationThresholdChanged.RemoveListener(OnActivationThresholdChanged);
			MicrophoneSettings.OnNoiseSuppressionChanged.RemoveListener(OnNoiseSuppressionChanged);
			MicrophoneSettings.OnMuteChanged.RemoveListener(OnMuteChanged);
			MicrophoneSettings.OnCurrentMicrophoneChanged.RemoveListener(OnCurrentMicrophoneSettingChanged);
			MicrophoneSettings.OnVolumeChanged.RemoveListener(OnVolumeChanged);
			Manager.Dispose();

			// Release the native opus library (after settings/managers are cleaned)
			CoreAPI?.LibAPI?.Unload("opus");

			Manager  = null;
			Instance = null;
			CoreAPI  = null;
		}

		public IMicrophone GetDefault()
			=> Manager.DefaultMicrophone;

		public IMicrophone[] GetAll()
			=> Manager.Microphones.Cast<IMicrophone>().ToArray();

		public IMicrophone GetCurrent()
			=> Manager.CurrentMicrophone;

		public IMicrophone Get(string name)
			=> Manager.Microphones.FirstOrDefault(m => m.GetName() == name);
	}
}