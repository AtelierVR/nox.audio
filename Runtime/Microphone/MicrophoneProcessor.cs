using UnityEngine;

namespace Nox.Audio.Runtime.Microphone {
	/// <summary>
	/// Processes raw PCM audio from the microphone: noise suppression, activation gate,
	/// mute, and volume. Reads per-microphone settings from <see cref="MicrophoneManager.Current"/>.
	/// <para>This is the core DSP layer — UI-agnostic, no relay dependency.</para>
	/// <para>Use <c>MicrophoneProcessor.Process(samples)</c> to apply all enabled effects.</para>
	/// </summary>
	public class MicrophoneProcessor {
		// ── RNNoise denoiser (native) ──
		private readonly RNNoise _denoiser = new();

		// ── Activation gate state ──
		private float _activationHoldTimer;

		/// <summary>Loudness of the last processed frame (post volume + noise suppression, pre-gate).</summary>
		public float Loudness { get; private set; }

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
				Loudness = 0f;
				return;
			}

			// ── Volume ──
			float volume = mic.Volume;
			if (!Mathf.Approximately(volume, 1f)) {
				for (int i = 0; i < samples.Length; i++)
					samples[i] *= volume;
			}

			// ── Noise suppression (RNNoise) ──
			if (mic.NoiseSuppression > 0f)
				_denoiser.Process(samples);

			// ── Loudness (post volume + noise suppression, pre-gate) ──
			float sumSq = 0f;
			for (int i = 0; i < samples.Length; i++)
				sumSq += samples[i] * samples[i];
			float rms = Mathf.Sqrt(sumSq / samples.Length);
			Loudness = Mathf.Clamp01(rms * 10f);

			// ── Activation gate ──
			float activation = mic.Activation;
			if (activation > 0f && !PassesActivationGate(activation)) {
				// Silence the frame instead of dropping it (keeps frame index sync)
				for (int i = 0; i < samples.Length; i++)
					samples[i] = 0f;
			}
		}

		// ── Activation Gate ──

		private bool PassesActivationGate(float threshold) {
			if (Loudness >= threshold) {
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
