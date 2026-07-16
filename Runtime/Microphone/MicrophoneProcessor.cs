using UnityEngine;

namespace Nox.Audio.Runtime.Microphone {
	/// <summary>
	/// Processes raw PCM audio from the microphone: noise suppression, activation gate,
	/// mute, and volume. Reads per-microphone settings from <see cref="MicrophoneManager.Current"/>.
	/// <para>This is the core DSP layer — UI-agnostic, no relay dependency.</para>
	/// <para>Use <c>MicrophoneProcessor.Process(samples)</c> to apply all enabled effects.</para>
	/// </summary>
	public class MicrophoneProcessor {
		// ── Noise suppression state ──
		private float[] _noiseProfile;
		private float _noiseProfileTime;
		private bool _noiseProfileReady;

		// ── Activation gate state ──
		private float _activationHoldTimer;

		/// <summary>
		/// Apply mute, volume, noise suppression, and activation gate to a PCM frame.
		/// </summary>
		/// <param name="samples">PCM samples (modified in-place).</param>
		/// <param name="mic">Microphone whose settings to use. If null, processing is skipped.</param>
		public void Process(float[] samples, Microphone mic) {
			if (samples == null || samples.Length == 0 || mic == null) return;

			// ── Mute check: zero out the frame ──
			if (mic.IsMuted) {
				for (int i = 0; i < samples.Length; i++)
					samples[i] = 0f;
				return;
			}

			// ── Volume ──
			float volume = mic.Volume;
			if (!Mathf.Approximately(volume, 1f)) {
				for (int i = 0; i < samples.Length; i++)
					samples[i] *= volume;
			}

			// ── Noise suppression ──
			float noiseSuppression = mic.NoiseSuppression;
			if (noiseSuppression > 0f)
				ApplyNoiseSuppression(samples, noiseSuppression, mic.Activation);

			// ── Activation gate ──
			float activation = mic.Activation;
			if (activation > 0f && !PassesActivationGate(samples, activation)) {
				// Silence the frame instead of dropping it (keeps frame index sync)
				for (int i = 0; i < samples.Length; i++)
					samples[i] = 0f;
			}
		}

		// ── Noise Suppression (Spectral Subtraction) ──

		private void ApplyNoiseSuppression(float[] samples, float strength, float activationThreshold) {
			int frameSize = samples.Length;
			int numBands = frameSize / 2;
			if (numBands < 2) return;

			// Resize noise profile if frame size changed
			if (_noiseProfile == null || _noiseProfile.Length != numBands)
				_noiseProfile = new float[numBands];

			int bandSize = frameSize / numBands;
			if (bandSize < 1) bandSize = 1;

			// Compute per-band energy
			float[] signalBands = new float[numBands];
			float totalEnergy = 0f;
			for (int b = 0; b < numBands; b++) {
				float bandEnergy = 0f;
				int bandStart = b * bandSize;
				int count = 0;
				for (int i = 0; i < bandSize && (bandStart + i) < frameSize; i++) {
					float s = samples[bandStart + i];
					bandEnergy += s * s;
					count++;
				}
				bandEnergy = count > 0 ? bandEnergy / count : 0f;
				signalBands[b] = bandEnergy;
				totalEnergy += bandEnergy;
			}

			float avgEnergy = totalEnergy / numBands;

			// Build/update noise profile from quiet frames
			if (!_noiseProfileReady) {
				for (int b = 0; b < numBands; b++)
					_noiseProfile[b] = (_noiseProfile[b] * _noiseProfileTime + signalBands[b] * Time.deltaTime)
						/ (_noiseProfileTime + Time.deltaTime);
				_noiseProfileTime += Time.deltaTime;
				if (_noiseProfileTime >= 1.0f) // 1 second attack
					_noiseProfileReady = true;
			} else if (avgEnergy < Mathf.Max(activationThreshold * 2f, 0.001f)) {
				// Slowly update noise profile from quiet frames
				float decay = 0.02f;
				for (int b = 0; b < numBands; b++)
					_noiseProfile[b] = _noiseProfile[b] * (1f - decay) + signalBands[b] * decay;
			}

			if (!_noiseProfileReady) return;

			// Apply spectral subtraction per band (Wiener-like gain)
			for (int b = 0; b < numBands; b++) {
				float gain = 1f;
				if (_noiseProfile[b] > 1e-10f) {
					float snr = signalBands[b] / _noiseProfile[b];
					gain = Mathf.Max(0.01f, 1f - strength / Mathf.Max(snr, 0.1f));
				}
				int bandStart = b * bandSize;
				for (int i = 0; i < bandSize && (bandStart + i) < frameSize; i++)
					samples[bandStart + i] *= gain;
			}
		}

		// ── Activation Gate ──

		private bool PassesActivationGate(float[] samples, float threshold) {
			float sumSq = 0f;
			for (int i = 0; i < samples.Length; i++)
				sumSq += samples[i] * samples[i];
			float rms = Mathf.Sqrt(sumSq / samples.Length);

			if (rms >= threshold) {
				_activationHoldTimer = 0.3f;
				return true;
			}

			if (_activationHoldTimer > 0f) {
				_activationHoldTimer -= Time.deltaTime;
				return true;
			}

			return false;
		}
	}
}
