using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;
using UnityMicrophone = UnityEngine.Microphone;

namespace Nox.Microphone.Runtime {
	public class MicrophoneManager : IDisposable {
		public const    string           DefaultName = "default";
		public readonly List<Microphone> Microphones = new();

		public static readonly UnityEvent<MicrophoneManager, Microphone>             OnAdded          = new();
		public static readonly UnityEvent<MicrophoneManager, Microphone>             OnRemoved        = new();
		public static readonly UnityEvent<MicrophoneManager, Microphone, int, int>   OnIndexChanged   = new();
		public static readonly UnityEvent<MicrophoneManager, Microphone, Microphone> OnDefaultChanged = new();
		public static readonly UnityEvent<MicrophoneManager, Microphone, Microphone> OnCurrentChanged = new();

		public Microphone DefaultMicrophone
			=> Microphones.FirstOrDefault(m => m.IsDefault());

		public Microphone CurrentMicrophone
			=> Microphones.FirstOrDefault(m => m.GetName() == CurrentConfigName) ?? DefaultMicrophone;

		public string CurrentConfigName {
			get {
				var cur = Config.Load().Get("settings.voice.current", DefaultName);
				return string.IsNullOrEmpty(cur) ? DefaultName : cur;
			}
			set {
				var previousCurrent = CurrentMicrophone; // Capturer l'état actuel avant le changement

				var v      = value?.Trim();
				var config = Config.Load();
				if (string.IsNullOrEmpty(v))
					v = DefaultName;
				config.Set("settings.voice.current", v);
				config.Save();

				// Forcer la réévaluation du microphone actuel après le changement de configuration
				var newCurrent = CurrentMicrophone;
				if (previousCurrent != newCurrent && (previousCurrent?.GetName() != newCurrent?.GetName() || previousCurrent?.GetIndex() != newCurrent?.GetIndex())) {
					Logger.Log($"Current microphone changed from '{previousCurrent?.GetName()}' to '{newCurrent?.GetName()}'");
					OnCurrentChanged.Invoke(this, previousCurrent, newCurrent);
				}
			}
		}

		public MicrophoneManager()
			=> Refresh();

		public void Refresh() {
			var currentNames = Microphones.ConvertAll(m => m.GetName());
			var deviceNames  = UnityMicrophone.devices.ToList();

			// Sauvegarder l'état précédent pour comparaison
			var previousDefault = DefaultMicrophone;
			var previousCurrent = CurrentMicrophone;

			// Add new devices
			foreach (var name in deviceNames.Where(name => !currentNames.Contains(name))) {
				UnityMicrophone.GetDeviceCaps(name, out var minFreq, out var maxFreq);
				var index = Array.IndexOf(UnityMicrophone.devices, name);
				var mic   = new Microphone(name, index, minFreq, maxFreq);
				Microphones.Add(mic);
				OnAdded.Invoke(this, mic);
			}

			// Remove disconnected devices
			for (var i = Microphones.Count - 1; i >= 0; i--) {
				var mic  = Microphones[i];
				var name = mic.GetName();
				if (deviceNames.Contains(name)) continue;
				Microphones.RemoveAt(i);
				mic.ForceStop();
				OnRemoved.Invoke(this, mic);
			}

			// Recréer les microphones existants avec les nouveaux index
			for (var i = 0; i < Microphones.Count; i++) {
				var oldMic       = Microphones[i];
				var currentIndex = Array.IndexOf(UnityMicrophone.devices, oldMic.GetName());

				if (currentIndex == oldMic.GetIndex()) continue;

				// L'index a changé, recréer le microphone
				UnityMicrophone.GetDeviceCaps(oldMic.GetName(), out var minFreq, out var maxFreq);
				var newMic = new Microphone(oldMic.GetName(), currentIndex, minFreq, maxFreq);

				Microphones[i] = newMic;

				Logger.Log($"Microphone '{oldMic.GetName()}' index changed from {oldMic.GetIndex()} to {currentIndex}");
				OnIndexChanged.Invoke(this, newMic, oldMic.GetIndex(), currentIndex);
			}

			// Vérifier si le microphone par défaut a changé
			var currentDefault = DefaultMicrophone;
			if (previousDefault != currentDefault && (previousDefault?.GetName() != currentDefault?.GetName() || previousDefault?.IsDefault() != currentDefault?.IsDefault())) {
				Logger.Log($"Default microphone changed from '{previousDefault?.GetName()}' to '{currentDefault?.GetName()}'");
				OnDefaultChanged.Invoke(this, previousDefault, currentDefault);
			}

			// Vérifier si le microphone actuel a changé suite au refresh
			var newCurrent = CurrentMicrophone;
			if (previousCurrent != newCurrent && (previousCurrent?.GetName() != newCurrent?.GetName() || previousCurrent?.GetIndex() != newCurrent?.GetIndex())) {
				Logger.Log($"Current microphone changed after refresh from '{previousCurrent?.GetName()}' to '{newCurrent?.GetName()}'");
				OnCurrentChanged.Invoke(this, previousCurrent, newCurrent);
			}
		}

		public void Dispose()
			=> Microphones.Clear();
	}
}