using UnityEngine;
using System;
using System.Collections.Generic;
using UnityMicrophone = UnityEngine.Microphone;
using Nox.CCK.Utils;
using UnityEngine.Events;

namespace Nox.Audio.Runtime.Microphone {
	public class Microphone : IMicrophone, IComparable<IMicrophone> {
		private readonly MicrophoneManager _manager;
		private readonly Vector2 _frequencies;
		private int _index;
		private readonly List<string> _usedBy;
		private ClipProcessor _processor;

		public Microphone(MicrophoneManager manager, string name, int index, int minFrequency, int maxFrequency) {
			_manager = manager;
			Name = name;
			_frequencies = new Vector2(minFrequency, maxFrequency);
			_usedBy = new List<string>();
			_settingPath = new[] { "settings", "microphones", name, null };
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
			else
				EnsureClipProcessor();
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
			DisposeClipProcessor();
		}

		public void ForceStop() {
			_usedBy.Clear();
			if (!IsRecording)
				return;
			UnityMicrophone.End(Name);
			Clip = null;
			DisposeClipProcessor();
		}

		/// <summary>Starts the background processor that keeps the recorded clip DSP-processed.</summary>
		private void EnsureClipProcessor() {
			if (_processor != null)
				return;
			_processor = new ClipProcessor(this);
		}

		/// <summary>Stops and releases the background processor.</summary>
		private void DisposeClipProcessor()
			=> _processor = null;

		/// <summary>Processes any newly recorded samples (called every frame by the manager).</summary>
		internal void Update()
			=> _processor?.ProcessAvailable();

		public string Name { get; }

		public int Position
			=> UnityMicrophone.GetPosition(Name);

		public AudioClip Clip { get; private set; }

		/// <summary>Loudness of the last processed frame (post volume + noise suppression, pre-gate).</summary>
		public float Loudness
			=> _processor?.Loudness ?? 0f;

		/// <inheritdoc />
		public bool TryDequeueProcessedFrame(out float[] samples) {
			if (_processor == null) {
				samples = null;
				return false;
			}

			return _processor.TryDequeue(out samples);
		}

		/// <inheritdoc />
		public void DiscardPendingFrames()
			=> _processor?.DiscardPending();

		public Vector2 Frequencies
			=> _frequencies;

		private const string VolumeKey = "volume";
		private const string MuteKey = "mute";
		private const string ActivationKey = "activation";
		private const string NoiseSuppressionKey = "noise_suppression";

		/// <summary>
		/// Config path of this microphone, <c>settings.microphones.&lt;name&gt;.&lt;key&gt;</c>, allocated once in the
		/// constructor. Only the last segment changes, so the getters (read every frame by the DSP) never
		/// allocate: a fresh <c>string[]</c> per read was the bulk of their allocations.
		/// </summary>
		private readonly string[] _settingPath;

		private string[] SettingPath(string key) {
			_settingPath[3] = key;
			return _settingPath;
		}

		public readonly UnityEvent<float> OnVolumeChanged = new();

		public float Volume {
			get => Config.Load().Get(SettingPath(VolumeKey), 1f);
			set {
				var old = Volume;
				var val = Mathf.Clamp(value, 0f, 2f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(SettingPath(VolumeKey), val);
				config.Save();
				OnVolumeChanged.Invoke(val);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.volume_changed", this, val, old);
			}
		}

		public readonly UnityEvent<bool> OnMuteChanged = new();

		public bool IsMuted {
			get => Config.Load().Get(SettingPath(MuteKey), false);
			set {
				var old = IsMuted;
				if (old == value)
					return;
				var config = Config.Load();
				config.Set(SettingPath(MuteKey), value);
				config.Save();
				OnMuteChanged.Invoke(value);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.mute_changed", this, value);
			}
		}

		public readonly UnityEvent<float> OnActivationChanged = new();

		public float Activation {
			get => Config.Load().Get(SettingPath(ActivationKey), .05f);
			set {
				var old = Activation;
				var val = Mathf.Clamp(value, 0f, 1f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(SettingPath(ActivationKey), val);
				config.Save();
				OnActivationChanged.Invoke(val);
				Main.CoreAPI.EventAPI.Emit("audio.microphone.activation_changed", this, val, old);
			}
		}

		public readonly UnityEvent<float> OnNoiseSuppressionChanged = new();

		public float NoiseSuppression {
			get => Config.Load().Get(SettingPath(NoiseSuppressionKey), .6f);
			set {
				var old = NoiseSuppression;
				var val = Mathf.Clamp(value, 0f, 1f);
				if (Mathf.Approximately(old, val))
					return;
				var config = Config.Load();
				config.Set(SettingPath(NoiseSuppressionKey), val);
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
