using System.Collections.Generic;
using UnityEngine;
using Nox.CCK.Audio.Opus;
using System;

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
		private int _lastPosition;
		private readonly MicrophoneProcessor _processor = new();

		/// <summary>Processed frames waiting for a consumer, on the DSP's own frame grid.</summary>
		private readonly Queue<float[]> _frames = new();

		/// <summary>Length of the DSP frames, cached so the pool only recycles matching buffers.</summary>
		private int _length;

		/// <summary>
		/// Free frame buffers. A frame dropped from an overfull queue is recycled instead of being
		/// thrown away, so a running microphone stops allocating after the queue has filled once.
		/// Buffers handed out by <see cref="TryDequeue"/> are owned by the caller and never pooled.
		/// </summary>
		private readonly Stack<float[]> _free = new();

		/// <summary>Frames kept when nobody consumes them (2 s at 50 fps).</summary>
		private const int MaxQueuedFrames = 100;

		/// <summary>
		/// Upper bound on the frames drained in a single update. After a stall (load hitch, domain
		/// reload, ...) the clip can hold seconds of backlog; draining it in one frame used to turn a
		/// small hitch into a huge one — one profiler capture showed ~580 frames of noise
		/// suppression inside a single 342 ms frame. The excess is dropped instead of queued: a short
		/// gap in the microphone stream is far cheaper than a multi-frame stall.
		/// </summary>
		private const int MaxFramesPerUpdate = 8;

		/// <summary>Loudness of the last processed frame (post volume + noise suppression, pre-gate).</summary>
		public float Loudness
			=> _processor.Loudness;

		public ClipProcessor(Microphone mic) {
			_mic = mic;
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
			_length = frame;
			var processed = 0;
			while (samplesAvailable >= frame) {
				int start = _lastPosition % clip.samples;
				var buf = RentFrame(frame);
				ReadFrame(clip, start, buf);
				_processor.Process(buf, _mic); // applies mute/volume/noise-suppression/activation gate in place
				WriteFrame(clip, start, buf);

				// Publish the frame on the DSP's grid (see TryDequeue).
				_frames.Enqueue(buf);
				while (_frames.Count > MaxQueuedFrames)
					ReturnFrame(_frames.Dequeue());

				_lastPosition = (start + frame) % clip.samples;
				samplesAvailable -= frame;

				if (++processed >= MaxFramesPerUpdate) {
					// Resynchronise on the frame grid at the record head and drop the rest of the
					// backlog, so the work per update stays bounded instead of growing every frame.
					_lastPosition = pos - pos % frame;
					break;
				}
			}
		}

		/// <summary>Takes the next DSP-processed frame, or <c>false</c> when none is pending.</summary>
		public bool TryDequeue(out float[] samples) {
			if (_frames.Count == 0) {
				samples = null;
				return false;
			}

			samples = _frames.Dequeue();
			return true;
		}

		/// <summary>Drops the frames processed before the caller started consuming.</summary>
		public void DiscardPending() {
			while (_frames.Count > 0)
				ReturnFrame(_frames.Dequeue());
		}

		/// <summary>Takes a frame buffer of <paramref name="length"/> samples, recycled when possible.</summary>
		private float[] RentFrame(int length) {
			while (_free.Count > 0) {
				var pooled = _free.Pop();
				if (pooled.Length == length)
					return pooled;
			}

			return new float[length];
		}

		/// <summary>Recycles a buffer that no consumer holds any more.</summary>
		private void ReturnFrame(float[] buffer) {
			if (buffer != null && buffer.Length == _length)
				_free.Push(buffer);
		}


		private void ReadFrame(AudioClip clip, int start, float[] buf) {
			if (start + buf.Length <= clip.samples) {
				clip.GetData(buf, start);
				return;
			}

			// The frame straddles the end of the recording ring: read the two halves. AudioClip
			// requires arrays of the exact length it reads, hence the temporary pair (rare).
			int first = clip.samples - start;
			int second = buf.Length - first;
			var head = new float[first];
			var tail = new float[second];
			clip.GetData(head, start);
			clip.GetData(tail, 0);
			Array.Copy(head, 0, buf, 0, first);
			Array.Copy(tail, 0, buf, first, second);
		}

		private static void WriteFrame(AudioClip clip, int start, float[] buf) {
			if (start + buf.Length <= clip.samples) {
				clip.SetData(buf, start);
				return;
			}

			// Same ring wrap as the read: AudioClip only writes an array of the exact length.
			int first = clip.samples - start;
			int second = buf.Length - first;
			var head = new float[first];
			var tail = new float[second];
			Array.Copy(buf, 0, head, 0, first);
			Array.Copy(buf, first, tail, 0, second);
			clip.SetData(head, start);
			clip.SetData(tail, 0);
		}
	}
}
