using UnityEngine;
using Nox.CCK.Audio.Opus;

namespace Nox.Audio.Runtime.Microphone {
	/// <summary>
	/// Drains raw PCM from a <see cref="Microphone"/>'s recording clip, applies the
	/// microphone's DSP and writes the processed samples back into the clip. This
	/// guarantees mute, volume, noise suppression and the activation gate always run
	/// inside nox.audio, regardless of which consumer later reads the stream.
	/// <para>This is a plain object (no <see cref="MonoBehaviour"/>): it is driven by
	/// <see cref="MicrophoneManager.Update"/>, which is itself called every frame from
	/// <c>OnUpdateMain</c>.</para>
	/// </summary>
	internal sealed class ClipProcessor {
		private readonly Microphone _mic;
		private int                 _lastPosition;
		private readonly MicrophoneProcessor _processor = new();

		/// <summary>Loudness of the last processed frame (post volume + noise suppression, pre-gate).</summary>
		public float Loudness
			=> _processor.Loudness;

		public ClipProcessor(Microphone mic) {
			_mic          = mic;
			_lastPosition = mic.Position;
		}

		/// <summary>
		/// Applies the owned DSP (mute, volume, noise suppression, activation gate) to a
		/// raw PCM frame. Intended for external consumers that read the processed clip.
		/// </summary>
		public void Process(float[] samples)
			=> _processor.Process(samples, _mic);

		/// <summary>Processes all samples recorded since the last call.</summary>
		public void ProcessAvailable() {
			if (_mic == null || !_mic.IsRecording)
				return;

			var clip = _mic.Clip;
			if (clip == null || clip.samples <= 0)
				return;

			int pos = _mic.Position;
			if (pos < 0)
				return;
			pos %= clip.samples;
			if (pos == _lastPosition)
				return;

			int samplesAvailable = pos > _lastPosition
				? pos - _lastPosition
				: (clip.samples - _lastPosition) + pos;

			// Process in frames sized to the configured frame period, scaled to the
			// actual recording rate, to keep the activation gate responsive.
			int frame = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * OpusConfig.SecondsPerFrame));
			while (samplesAvailable >= frame) {
				int start = _lastPosition % clip.samples;
				var buf  = ReadFrame(clip, start, frame);
				_processor.Process(buf, _mic); // applies mute/volume/noise-suppression/activation gate in place
				WriteFrame(clip, start, buf);

				_lastPosition     = (start + frame) % clip.samples;
				samplesAvailable -= frame;
			}
		}

		private static float[] ReadFrame(AudioClip clip, int start, int length) {
			var buf = new float[length];
			if (start + length <= clip.samples) {
				clip.GetData(buf, start);
				return buf;
			}

			int first  = clip.samples - start;
			int second = length - first;
			var head   = new float[first];
			var tail   = new float[second];
			clip.GetData(head, start);
			clip.GetData(tail, 0);
			System.Array.Copy(head, 0, buf, 0, first);
			System.Array.Copy(tail, 0, buf, first, second);
			return buf;
		}

		private static void WriteFrame(AudioClip clip, int start, float[] buf) {
			int remaining = buf.Length;
			int offset    = 0;
			int w         = start;

			while (remaining > 0) {
				int n = Mathf.Min(remaining, clip.samples - w);
				var chunk = new float[n];
				System.Array.Copy(buf, offset, chunk, 0, n);
				clip.SetData(chunk, w);

				remaining -= n;
				offset    += n;
				w          = (w + n) % clip.samples;
			}
		}
	}
}
