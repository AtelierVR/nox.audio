using System;
using Concentus;
using Concentus.Enums;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Opus audio encoder — managed Concentus wrapper (no native P/Invoke).
	/// Works in IL2CPP, WebGL, and all Unity platforms.
	/// </summary>
	public static class OpusEncoder {
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
			public OpusEncoderInstance(int sampleRate, int channels, int bitrate) {
				_encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
				_encoder.Bitrate = bitrate;
				_encoder.Complexity = 10;
				_encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
				_encoder.ForceMode = OpusMode.MODE_SILK_ONLY;
				_encoder.Bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;

				_buffer = new byte[1275]; // Max Opus packet size
			}

			/// <summary>
			/// Encode PCM float samples to Opus bytes.
			/// </summary>
			/// <param name="pcmData">Float PCM samples [-1..1].</param>
			/// <param name="frameSize">Samples per channel per frame (e.g. 960 for 20ms @ 48kHz).</param>
			/// <returns>Opus-encoded byte array, or null on failure.</returns>
			public byte[] Encode(float[] pcmData, int frameSize) {
				if (_disposed) throw new ObjectDisposedException(nameof(OpusEncoderInstance));

				int bytesEncoded = _encoder.Encode(pcmData, frameSize, _buffer, _buffer.Length);
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