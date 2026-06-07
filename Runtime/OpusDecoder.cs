using System;
using System.Runtime.InteropServices;

namespace Nox.Microphone.Runtime {
	/// <summary>
	/// Opus audio decoder — P/Invoke wrapper around native libopus.
	/// Depends on <see cref="OpusNative.Init()"/> having been called first.
	/// </summary>
	public static class OpusDecoder {
		private const int OpusOk = 0;

		[DllImport("opus")]
		private static extern IntPtr opus_decoder_create(int fs, int channels, out int error);

		[DllImport("opus")]
		private static extern void opus_decoder_destroy(IntPtr decoder);

		[DllImport("opus")]
		private static extern int opus_decode_float(
			IntPtr decoder,
			byte[] data,
			int len,
			[Out] float[] pcm,
			int frameSize,
			int decodeFec
		);

		/// <summary>
		/// A single Opus decoder instance. Create one per remote player audio stream.
		/// </summary>
		public class OpusDecoderInstance : IDisposable {
			private IntPtr _decoder;
			private bool   _disposed;
			private int    _channels;

			/// <summary>
			/// Whether the native decoder was created successfully.
			/// </summary>
			public bool IsValid
				=> _decoder != IntPtr.Zero;

			/// <summary>
			/// Create a new Opus decoder for the given sample rate and channel count.
			/// </summary>
			/// <param name="sampleRate">Sample rate in Hz (e.g. 44100, 48000)</param>
			/// <param name="channels">Number of channels (1 = mono)</param>
			public OpusDecoderInstance(int sampleRate, int channels) {
				int error;
				_decoder  = opus_decoder_create(sampleRate, channels, out error);
				_channels = channels;

				if (error != OpusOk || _decoder == IntPtr.Zero) {
					throw new Exception($"Failed to create Opus decoder: {error}");
				}
			}

			/// <summary>
			/// Decode an Opus packet into PCM float samples.
			/// </summary>
			/// <param name="opusData">The Opus-encoded byte packet.</param>
			/// <param name="frameSize">
			/// Number of samples per channel expected in the output.
			/// Must match the frame size used during encoding.
			/// </param>
			/// <returns>
			/// Decoded PCM samples (interleaved if stereo).
			/// Returns an empty array on decode failure or silent frame.
			/// </returns>
			public float[] Decode(byte[] opusData, int frameSize) {
				if (_disposed || _decoder == IntPtr.Zero) {
					throw new ObjectDisposedException(nameof(OpusDecoderInstance));
				}

				if (opusData == null || opusData.Length == 0) {
					// No data → decode with PLC (packet loss concealment)
					return DecodeLost(frameSize);
				}

				// Opus writes frameSize * channels interleaved samples into pcm
				float[] pcm = new float[frameSize * _channels];
				int samplesDecoded = opus_decode_float(
					_decoder,
					opusData,
					opusData.Length,
					pcm,
					frameSize,
					0 // decodeFec = 0 (normal decode)
				);

				if (samplesDecoded < 0) {
					throw new Exception($"Opus decoding failed: {samplesDecoded}");
				}

				if (samplesDecoded == 0) {
					return Array.Empty<float>();
				}

				int totalSamples = samplesDecoded * _channels;
				if (totalSamples < pcm.Length) {
					// Trim to actual decoded size
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
			/// <returns>Concealed PCM samples, or empty on failure.</returns>
			public float[] DecodeLost(int frameSize) {
				if (_disposed || _decoder == IntPtr.Zero) {
					return Array.Empty<float>();
				}

				float[] pcm = new float[frameSize * _channels];
				int samplesDecoded = opus_decode_float(
					_decoder,
					null,
					0,
					pcm,
					frameSize,
					0
				);

				if (samplesDecoded < 0) {
					// PLC failed — return silence
					return new float[frameSize * _channels];
				}

				return pcm;
			}

			public void Dispose() {
				if (!_disposed && _decoder != IntPtr.Zero) {
					opus_decoder_destroy(_decoder);
					_decoder  = IntPtr.Zero;
					_disposed = true;
				}
			}
		}
	}
}
