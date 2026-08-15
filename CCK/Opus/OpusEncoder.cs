using System;
using Concentus;
using Concentus.Enums;

namespace Nox.CCK.Audio.Opus {
	/// <summary>
	/// Opus audio encoder — managed Concentus wrapper (no native P/Invoke).
	/// Works in IL2CPP, WebGL, and all Unity platforms.
	/// </summary>
	public static class OpusEncoder {
		/// <summary>Maximum encoded size (bytes) of a single Opus packet.</summary>
		public const int MaxPacketSize = 1275;

		/// <summary>Maximum Opus bitrate (bps). libopus caps at ~510 kbps.</summary>
		public const int MaxBitrate = 510000;

		public class OpusEncoderInstance : IDisposable {
			private readonly IOpusEncoder _encoder;
			private readonly byte[] _buffer;
			private bool _disposed;

			public bool IsValid => !_disposed;

			/// <summary>
			/// Create an Opus encoder instance.
			/// </summary>
			/// <param name="sampleRate">Sample rate (48000 recommended).</param>
			/// <param name="channels">Number of channels (1 = mono).</param>
			/// <param name="bitrate">Target bitrate in bps.</param>
			/// <param name="complexity">Opus complexity (0-10).</param>
			/// <param name="signalType">Signal type hint (auto/voice/music).</param>
			public OpusEncoderInstance(int sampleRate, int channels, int bitrate, int complexity, OpusSignalType signalType) {
				_encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO);
				_encoder.Bitrate = bitrate;
				_encoder.Complexity = complexity;
				_encoder.SignalType = signalType.ToOpusSignal();
				_buffer = new byte[MaxPacketSize];
			}

			/// <summary>
			/// Encode PCM float samples to Opus bytes.
			/// </summary>
			/// <param name="pcmData">Float PCM samples [-1..1].</param>
			/// <param name="frameSize">Samples per channel per frame (e.g. 960 for 20ms @ 48kHz).</param>
			/// <param name="maxDataBytes">Max encoded bytes to produce (clamped to <see cref="MaxPacketSize"/>).</param>
			/// <returns>Opus-encoded byte array, or null on failure.</returns>
			public byte[] Encode(float[] pcmData, int frameSize, int maxDataBytes = MaxPacketSize) {
				if (_disposed) throw new ObjectDisposedException(nameof(OpusEncoderInstance));

				int max = Math.Min(maxDataBytes, _buffer.Length);
				if (max <= 0) max = _buffer.Length;

				int bytesEncoded = _encoder.Encode(pcmData, frameSize, _buffer, max);
				if (bytesEncoded <= 0) return null;

				byte[] result = new byte[bytesEncoded];
				Array.Copy(_buffer, result, bytesEncoded);
				return result;
			}

			public void Dispose() {
				if (!_disposed) {
					_encoder?.Dispose();
					_disposed = true;
				}
			}
		}
	}
}