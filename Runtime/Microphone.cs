using Nox.Microphone;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityMicrophone = UnityEngine.Microphone;

namespace Nox.Microphone.Runtime {
	public class Microphone : IMicrophone {
		private readonly string       _name;
		private readonly Vector2      _frequencies;
		private readonly int          _index;
		private          AudioClip    _audioClip;
		private readonly List<string> _usedBy;

		public Microphone(string name, int index, int minFrequency, int maxFrequency) {
			_name        = name;
			_index       = index;
			_frequencies = new Vector2(minFrequency, maxFrequency);
			_usedBy      = new List<string>();
		}

		public string[] UsedBy()
			=> _usedBy?.ToArray();


		public bool IsRecording()
			=> _audioClip;

		public bool IsDefault()
			=> _index == 0;

		public int GetIndex()
			=> _index;

		public AudioClip Start(string by) {
			if (!_usedBy.Contains(by))
				_usedBy.Add(by);
			if (IsRecording())
				return _audioClip;
			_audioClip = UnityMicrophone.Start(_name, true, 10, 44100);
			if (!_audioClip)
				_usedBy.Remove(by);
			return _audioClip;
		}

		public void Stop(string by) {
			_usedBy.Remove(by);
			if (!IsRecording()) return;
			if (_usedBy.Count > 0) return;
			UnityMicrophone.End(_name);
			_audioClip = null;
		}

		public void ForceStop() {
			_usedBy.Clear();
			if (!IsRecording()) return;
			UnityMicrophone.End(_name);
			_audioClip = null;
		}

		public string GetName()
			=> _name;

		public int GetPosition()
			=> UnityMicrophone.GetPosition(_name);

		public AudioClip GetClip()
			=> _audioClip;

		public float GetLoudness() {
			if (!IsRecording() || !_audioClip)
				return 0f;
			var position = GetPosition();
			if (position <= 0) return 0f;
			var sampleWindow  = Mathf.Min(1024, position);
			var samples       = new float[sampleWindow];
			var startPosition = Mathf.Max(0, position - sampleWindow);
			_audioClip.GetData(samples, startPosition);
			var sum      = samples.Sum(t => t * t);
			var rms      = Mathf.Sqrt(sum    / samples.Length);
			var loudness = Mathf.Clamp01(rms * 10f);
			return loudness;
		}

		public Vector2 GetFrequencies()
			=> _frequencies;
	}
}