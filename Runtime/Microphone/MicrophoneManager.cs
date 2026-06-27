using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;
using UnityMicrophone = UnityEngine.Microphone;

namespace Nox.Audio.Runtime.Microphone {
	public class MicrophoneManager : IDisposable {
		public const string DefaultName = "default";
		public readonly List<Microphone> Microphones = new();

		public Microphone Default
			=> Microphones.FirstOrDefault(m => m.IsDefault);

		public Microphone Current
			=> Microphones.FirstOrDefault(m => m.Name == CurrentName)
				?? Default;

		public string CurrentName {
			get {
				var cur = Config.Load().Get("settings.voice.current", DefaultName);
				return string.IsNullOrEmpty(cur) ? DefaultName : cur;
			}
			set {
				var old = Current;

				var v      = value?.Trim();
				var config = Config.Load();
				if (string.IsNullOrEmpty(v))
					v = DefaultName;
				config.Set("settings.voice.current", v);
				config.Save();

				var @new = Current;
				if (old == @new || old?.Name == @new?.Name)
					return;

				OnCurrentChangedHandler(@new, old);
			}
		}

		public MicrophoneManager()
			=> Refresh();

		public void Refresh() {
			var currentNames = Microphones.ConvertAll(m => m.Name);
			var deviceNames  = UnityMicrophone.devices.ToList();

			// Sauvegarder l'état précédent pour comparaison
			var oldDefault = Default;
			var oldCurrent = Current;

			// Add new devices
			foreach (var name in deviceNames.Where(name => !currentNames.Contains(name))) {
				UnityMicrophone.GetDeviceCaps(name, out var minFreq, out var maxFreq);
				var index = Array.IndexOf(UnityMicrophone.devices, name);

				var microphone = new Microphone(this, name, index, minFreq, maxFreq);
				Microphones.Add(microphone);

				Main.CoreAPI.EventAPI.Emit("audio.microphone.added", microphone);
				Main.CoreAPI.LoggerAPI.Log($"Microphone added: {microphone.Name} (Frequencies: {microphone.Frequencies.x}-{microphone.Frequencies.y})");
			}

			// Remove disconnected devices
			for (var i = Microphones.Count - 1; i >= 0; i--) {
				var microphone = Microphones[i];

				if (deviceNames.Contains(microphone.Name))
					continue;

				Microphones.RemoveAt(i);
				microphone.ForceStop();

				Main.CoreAPI.EventAPI.Emit("audio.microphone.removed", microphone);
				Main.CoreAPI.LoggerAPI.Log($"Microphone removed: {microphone.Name}");
			}

			// Vérifier si le microphone par défaut a changé
			var newDefault = Default;
			if (oldDefault != newDefault && (oldDefault?.Name != newDefault?.Name || oldDefault?.IsDefault != newDefault?.IsDefault))
				OnDefaultChangedHandler(newDefault, oldDefault);

			// Vérifier si le microphone actuel a changé suite au refresh
			var newCurrent = Current;
			if (oldCurrent != newCurrent && oldCurrent?.Name != newCurrent?.Name)
				OnCurrentChangedHandler(newCurrent, oldCurrent);
		}

		public readonly UnityEvent<Microphone> OnCurrentChanged = new();

		private void OnCurrentChangedHandler(Microphone @new, Microphone old) {
			Main.CoreAPI.LoggerAPI.Log($"Current microphone changed from '{old?.Name}' to '{@new?.Name}'");
			old?.Stop("current");
			@new?.Start("current");
			OnCurrentChanged.Invoke(@new);
			Main.CoreAPI.EventAPI.Emit("audio.microphone.current_changed", @new, old);
		}

		public readonly UnityEvent<Microphone> OnDefaultChanged = new();

		private void OnDefaultChangedHandler(Microphone @new, Microphone old) {
			Main.CoreAPI.LoggerAPI.Log($"Default microphone changed from '{old?.Name}' to '{@new?.Name}'");
			OnDefaultChanged.Invoke(@new);
			Main.CoreAPI.EventAPI.Emit("audio.microphone.default_changed", @new, old);
		}


		public void Dispose()
			=> Microphones.Clear();
	}
}