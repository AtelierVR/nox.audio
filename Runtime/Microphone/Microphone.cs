using Nox.Audio;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityMicrophone = UnityEngine.Microphone;
using Nox.CCK;
using Nox.CCK.Utils;
using UnityEngine.Events;

namespace Nox.Audio.Runtime.Microphone {
	public class Microphone : IMicrophone, IComparable<IMicrophone> {
		private readonly MicrophoneManager _manager;
		private readonly Vector2 _frequencies;
		private int _index;
		private readonly List<string> _usedBy;

		public Microphone(MicrophoneManager manager, string name, int index, int minFrequency, int maxFrequency) {
			_manager     = manager;
			Name         = name;
			_frequencies = new Vector2(minFrequency, maxFrequency);
			_usedBy      = new List<string>();
		}

		public string[] UsedBy
			=> _usedBy?.ToArray();

		public bool IsRecording
			=> Clip;

		public bool IsDefault
			=> _index == 0;

		public bool IsCurrent
			=> _manager.CurrentName == Name;

		public AudioClip Start(string by) {
			if (!_usedBy.Contains(by))
				_usedBy.Add(by);
			if (IsRecording)
				return Clip;
			Clip = UnityMicrophone.Start(Name, true, 10, 48000);
			if (!Clip)
				_usedBy.Remove(by);
			return Clip;
		}

		public void Stop(string by) {
			_usedBy.Remove(by);
			if (!IsRecording)
				return;
			if (_usedBy.Count > 0)
				return;
			UnityMicrophone.End(Name);
			Clip = null;
		}

		public void ForceStop() {
			_usedBy.Clear();
			if (!IsRecording)
				return;
			UnityMicrophone.End(Name);
			Clip = null;
		}

		public string Name { get; }

		public int Position
			=> UnityMicrophone.GetPosition(Name);

		public AudioClip Clip { get; private set; }

		public float Loudness {
			get {
				if (!IsRecording || !Clip)
					return 0f;

				var position = Position;
				if (position <= 0)
					return 0f;

				var window  = Mathf.Min(1024, position);
				var samples = new float[ window ];
				var start   = Mathf.Max(0, position - window);
				Clip.GetData(samples, start);
				var sum = samples.Sum(t => t * t);
				var rms = Mathf.Sqrt(sum / samples.Length);
				return Mathf.Clamp01(rms * 10f);
			}
		}

		public Vector2 Frequencies
			=> _frequencies;

		private string[] GetSetting(string sub)
			=> new[] {
				"settings",
				"microphones",
				Name,
				sub
			};


		public readonly UnityEvent<float> OnVolumeChanged = new();

		public float Volume {
			get => Config.Load().Get(GetSetting("volume"), 1f);
			set {
				var old = Volume;
				var val = Mathf.Clamp(value, 0f, 2f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(GetSetting("volume"), val);
				config.Save();
				OnVolumeChanged.Invoke(val);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.volume_changed", this, val, old);
			}
		}

		public readonly UnityEvent<bool> OnMuteChanged = new();

		public bool IsMuted {
			get => Config.Load().Get(GetSetting("mute"), false);
			set {
				var old = IsMuted;
				if (old == value)
					return;
				var config = Config.Load();
				config.Set(GetSetting("mute"), value);
				config.Save();
				OnMuteChanged.Invoke(value);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.mute_changed", this, value);
			}
		}

		public readonly UnityEvent<float> OnActivationChanged = new();

		public float Activation {
			get => Config.Load().Get(GetSetting("activation"), .02f);
			set {
				var old = Activation;
				var val = Mathf.Clamp(value, 0f, 1f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(GetSetting("activation"), val);
				config.Save();
				OnActivationChanged.Invoke(val);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.activation_changed", this, val, old);
			}
		}

		public readonly UnityEvent<float> OnNoiseSuppressionChanged = new();

		public float NoiseSuppression {
			get => Config.Load().Get(GetSetting("noise_suppression"), .6f);
			set {
				var old = NoiseSuppression;
				var val = Mathf.Clamp(value, 0f, 1f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(GetSetting("noise_suppression"), val);
				config.Save();
				OnNoiseSuppressionChanged.Invoke(val);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.noise_suppression_changed", this, val, old);
			}
		}

		public int CompareTo(IMicrophone other)
			=> string.Compare(other.Name, Name, StringComparison.Ordinal);

		public override int GetHashCode()
			=> Name.GetHashCode();
	}
}