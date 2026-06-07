using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nox.Microphone.Runtime {
	/// <summary>
	/// Test MonoBehaviour: simulates a streaming Opus encode/decode pipeline.
	/// Each Update tick one frame is encoded (Send) then decoded (Receive) and
	/// pushed into a PCM queue.  Unity's audio engine drains the queue via the
	/// PCMReaderCallback, so playback is truly driven by the audio thread.
	/// </summary>
	public class OpusRoundTripTest : MonoBehaviour {
		[Header("Input")]
		public AudioClip sourceClip;

		[Header("Opus settings")]
		[Tooltip("Bitrate in bits/s")]
		public int bitrate = 64000;

		[Tooltip("Frame size in samples per channel (960 = 20 ms @ 48 kHz)")]
		public int frameSize = 960;

		[Header("Streaming")]
		[Tooltip("Frames to buffer before starting playback")]
		public int playbackBufferFrames = 5;

		[Header("Output")]
		public AudioSource outputSource;

		// ------------------------------------------------------------------ //
		// Runtime state

		private OpusEncoder.OpusEncoderInstance _encoder;
		private OpusDecoder.OpusDecoderInstance _decoder;

		private float[] _sourceData;
		private int     _channels;
		private int     _sampleRate;
		private int     _readOffset;

		private bool      _streaming;
		private Coroutine _streamCoroutine;

		// PCM queue shared between the main thread (producer) and audio thread (consumer)
		private readonly Queue<float> _pcmQueue  = new();
		private readonly object       _queueLock = new();

		// ------------------------------------------------------------------ //

		private void Start() {
			if (outputSource == null)
				outputSource = GetComponent<AudioSource>();
		}

		private void OnDestroy() => StopStream();

		// ------------------------------------------------------------------ //

		[ContextMenu("Start Stream")]
		public void StartStream() {
			StopStream();

			if (sourceClip == null) {
				Debug.LogError("[OpusTest] sourceClip not assigned.");
				return;
			}

			if (outputSource == null) {
				Debug.LogError("[OpusTest] No AudioSource found.");
				return;
			}

			_channels   = sourceClip.channels;
			_sampleRate = sourceClip.frequency;

			_sourceData = new float[sourceClip.samples * _channels];
			sourceClip.GetData(_sourceData, 0);

			_encoder = new OpusEncoder.OpusEncoderInstance(_sampleRate, _channels, bitrate);
			_decoder = new OpusDecoder.OpusDecoderInstance(_sampleRate, _channels);

			lock (_queueLock) _pcmQueue.Clear();

			_readOffset = 0;
			_streaming  = true;

			// Streaming AudioClip: Unity calls OnPCMRead whenever it needs data.
			// Length here is just the virtual window size (we loop); actual content
			// comes from the queue.
			var streamClip = AudioClip.Create(
				$"{sourceClip.name}_stream",
				_sampleRate,        // 1-second window, looped by AudioSource
				_channels,
				_sampleRate,
				true,               // stream = true → PCMReaderCallback active
				OnPCMRead
			);

			outputSource.clip        = streamClip;
			outputSource.loop        = true;
			outputSource.playOnAwake = false;

			int totalFrames = sourceClip.samples / frameSize;
			Debug.Log($"[OpusTest] Stream started — {totalFrames} frames, {_sampleRate} Hz, {_channels} ch");

			_streamCoroutine = StartCoroutine(StreamCoroutine());
		}

		[ContextMenu("Stop Stream")]
		public void StopStream() {
			_streaming = false;

			if (_streamCoroutine != null) {
				StopCoroutine(_streamCoroutine);
				_streamCoroutine = null;
			}

			_encoder?.Dispose(); _encoder = null;
			_decoder?.Dispose(); _decoder = null;
			_sourceData = null;

			lock (_queueLock) _pcmQueue.Clear();

			if (outputSource != null && outputSource.isPlaying)
				outputSource.Stop();
		}

		// ------------------------------------------------------------------ //
		// PCMReaderCallback — called on the audio thread

		private void OnPCMRead(float[] data) {
			int filled = 0;
			lock (_queueLock) {
				while (filled < data.Length && _pcmQueue.Count > 0)
					data[filled++] = _pcmQueue.Dequeue();
			}

			// Silence for any samples not yet available
			for (int i = filled; i < data.Length; i++)
				data[i] = 0f;
		}

		// ------------------------------------------------------------------ //
		// Streaming coroutine — one Opus frame per Update tick

		private IEnumerator StreamCoroutine() {
			int frameCount = 0;

			while (_streaming && _readOffset + frameSize * _channels <= _sourceData.Length) {
				Simulate();
				frameCount++;

				// Start playback once enough frames are buffered
				if (!outputSource.isPlaying && frameCount >= playbackBufferFrames)
					outputSource.Play();

				yield return null;
			}

			// Wait for the queue to drain before stopping
			while (true) {
				lock (_queueLock) {
					if (_pcmQueue.Count == 0) break;
				}
				yield return null;
			}

			Debug.Log($"[OpusTest] Stream finished ({frameCount} frames).");
			outputSource.Stop();
			_streaming = false;
		}

		// ------------------------------------------------------------------ //
		// Core streaming API

		/// <summary>Simulate one network hop: encode then immediately decode.</summary>
		private void Simulate() {
			byte[] packet = Send();
			Receive(packet);
		}

		/// <summary>Read the next frame from the source clip and Opus-encode it.</summary>
		private byte[] Send() {
			int    step  = frameSize * _channels;
			float[] frame = new float[step];
			Array.Copy(_sourceData, _readOffset, frame, 0, step);
			_readOffset += step;
			return _encoder.Encode(frame, frameSize);
		}

		/// <summary>Decode a packet and push the PCM samples into the playback queue.</summary>
		private void Receive(byte[] packet) {
			float[] decoded = _decoder.Decode(packet, frameSize);
			lock (_queueLock) {
				foreach (float s in decoded)
					_pcmQueue.Enqueue(s);
			}
		}
	}
}
