using System;
using Concentus;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Opus audio decoder — managed Concentus wrapper (no native P/Invoke).
	/// Works in IL2CPP, WebGL, and all Unity platforms.
	/// </summary>
	public static class OpusDecoder {
		/// <summary>
		/// A single Opus decoder instance. Create one per remote player audio stream.
		/// </summary>
		public class OpusDecoderInstance : IDisposable {
			private readonly IOpusDecoder _decoder;
			private readonly int _channels;
			private bool _disposed;

			public bool IsValid => !_disposed;

			/// <summary>
			/// Create a new Opus decoder.
			/// </summary>
			/// <param name="sampleRate">Sample rate in Hz (e.g. 48000).</param>
			/// <param name="channels">Number of channels (1 = mono).</param>
			public OpusDecoderInstance(int sampleRate, int channels) {
				_decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
				_channels = channels;
			}

			/// <summary>
			/// Decode an Opus packet into PCM float samples.
			/// </summary>
			/// <param name="opusData">The Opus-encoded byte packet.</param>
			/// <param name="frameSize">Expected samples per channel in output.</param>
			/// <returns>Decoded PCM float samples, or empty array on failure.</returns>
			public float[] Decode(byte[] opusData, int frameSize) {
				if (_disposed) throw new ObjectDisposedException(nameof(OpusDecoderInstance));

				if (opusData == null || opusData.Length == 0)
					return DecodeLost(frameSize);

				float[] pcm = new float[frameSize * _channels];
				int samplesDecoded;
				try {
					samplesDecoded = _decoder.Decode(opusData, pcm, frameSize, false);
				} catch (OpusException) {
					// Concentus throws on corrupted/unparseable packets.
					// Fall back to packet loss concealment.
					return DecodeLost(frameSize);
				}

				if (samplesDecoded < 0)
					return DecodeLost(frameSize);
				if (samplesDecoded == 0)
					return Array.Empty<float>();

				int totalSamples = samplesDecoded * _channels;
				if (totalSamples < pcm.Length) {
					float[] trimmed = new float[totalSamples];
					Array.Copy(pcm, trimmed, totalSamples);
					return trimmed;
				}

				return pcm;
			}

			/// <summary>
			/// Decode with packet loss concealment (PLC) to fill a missing frame.
			/// </summary>
			/// <param name="frameSize">Expected output sample count.</param>
			/// <returns>Concealed PCM samples, or silence on failure.</returns>
			public float[] DecodeLost(int frameSize) {
				if (_disposed) return Array.Empty<float>();

				float[] pcm = new float[frameSize * _channels];
				int samplesDecoded = _decoder.Decode(System.ReadOnlySpan<byte>.Empty, pcm, frameSize, true);

				if (samplesDecoded < 0)
					return new float[frameSize * _channels]; // silence

				return pcm;
			}

			public void Dispose() {
				if (!_disposed) {
					_decoder?.Dispose();
					_disposed = true;
				}
			}
		}
	}
}
