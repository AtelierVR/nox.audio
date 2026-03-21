using System;
using System.Linq;
using Nox.Microphone.Runtime;
using Nox.CCK.Language;
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
			_lang    = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);

			MicrophoneManager.OnAdded.AddListener(OnMicrophoneAdded);
			MicrophoneManager.OnRemoved.AddListener(OnMicrophoneRemoved);
			MicrophoneManager.OnDefaultChanged.AddListener(OnMicrophoneDefaultChanged);
			MicrophoneManager.OnIndexChanged.AddListener(OnMicrophoneIndexChanged);
			MicrophoneManager.OnCurrentChanged.AddListener(OnMicrophoneCurrentChanged);

			CoreAPI.LoggerAPI.Log("Microphone API initialized.");
			var defaultMic = Manager.DefaultMicrophone;
			CoreAPI.LoggerAPI.Log($"Default microphone: {defaultMic?.GetName() ?? "null"}");

			Settings = new IHandler[] {
				new CurrentMicSetting(),
				new MicVolumeSetting(),
				new ActivationMicSetting()
			};
			
			foreach (var setting in Settings)
				SettingAPI.Add(setting);

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
			CoreAPI.EventAPI.Emit("microphone_current_changed", arg1, arg2);
			CoreAPI.LoggerAPI.Log($"Microphone current changed: {arg1?.GetName() ?? "null"} -> {arg2?.GetName() ?? "null"}");
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
			Manager.Dispose();
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