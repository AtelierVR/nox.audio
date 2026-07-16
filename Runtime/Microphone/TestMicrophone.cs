using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;
using Nox.CCK.Utils;

namespace Nox.Audio.Runtime.Microphone {
	/// <summary>
	/// Singleton debug component that plays the current microphone through an AudioSource.
	/// Created on demand by <see cref="TestMicrophoneSetting"/> and placed on the
	/// controller's head anchor so the user hears their own voice.
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class TestMicrophone : MonoBehaviour {
		/// <summary>Current active instance, or null if monitoring is off.</summary>
		public static TestMicrophone Instance { get; private set; }

		private AudioSource _source;
		private AudioClip _micClip;
		private int _lastPosition;

		private void Awake() {
			// Only allow one instance
			if (Instance != null) {
				Logger.LogWarning("Another instance already exists, destroying duplicate.", tag: nameof(TestMicrophone));
				gameObject.Destroy();
				return;
			}
			Instance = this;

			_source = GetComponent<AudioSource>();
			_source.loop = true;
			_source.playOnAwake = false;
			_source.spatialBlend = 1f;
		}

		private void Start() {
			var mic = Main.MicrophoneManager.Current;
			if (mic == null) {
				Logger.LogWarning("No current microphone available.", tag: nameof(TestMicrophone));
				gameObject.Destroy();
				return;
			}

			_micClip = mic.Start("debug");
			if (_micClip == null) {
				Logger.LogError($"[TestMicrophone] Failed to start microphone '{mic.Name}' for debug.", tag: nameof(TestMicrophone));
				gameObject.Destroy();
				return;
			}

			_source.clip = _micClip;
			_source.Play();
			_lastPosition = 0;

			Logger.Log($"[TestMicrophone] Monitoring microphone '{mic.Name}' — you should hear yourself.", tag: nameof(TestMicrophone));
		}

		private void Update() {
			if (_micClip == null || _source == null) return;

			int pos = Main.MicrophoneManager.Current?.Position ?? 0;
			if (pos == _lastPosition) return;

			_lastPosition = pos;

			if (!_source.isPlaying)
				_source.Play();
		}

		private void OnDestroy() {
			var mic = Main.MicrophoneManager.Current;
			mic?.Stop("debug");
			if (Instance == this)
				Instance = null;
		}
	}
}
