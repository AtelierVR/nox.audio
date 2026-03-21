using System;
using System.Runtime.InteropServices;

namespace Nox.Microphone.Runtime {
	public static class OpusEncoder {
		private const int OpusOk                = 0;
		private const int OpusApplicationVoip   = 2048;
		private const int OpusSetBitrateRequest = 4002;

		[DllImport("opus")]
		private static extern IntPtr opus_encoder_create(int fs, int channels, int application, out int error);

		[DllImport("opus")]
		private static extern void opus_encoder_destroy(IntPtr encoder);

		[DllImport("opus")]
		private static extern int opus_encode(IntPtr encoder, IntPtr pcm, int frameSize, IntPtr data, int maxDataBytes);

		[DllImport("opus")]
		private static extern int opus_encoder_ctl(IntPtr encoder, int request, int value);

		public class OpusEncoderInstance : IDisposable {
			private IntPtr _encoder;
			private bool   _disposed;

			public bool IsValid
				=> _encoder != IntPtr.Zero;

			public OpusEncoderInstance(int sampleRate, int channels, int bitrate) {
				int error;
				_encoder = opus_encoder_create(sampleRate, channels, OpusApplicationVoip, out error);

				if (error != OpusOk || _encoder == IntPtr.Zero) {
					throw new Exception($"Failed to create Opus encoder: {error}");
				}

				// Set bitrate
				opus_encoder_ctl(_encoder, OpusSetBitrateRequest, bitrate);
			}

			public byte[] Encode(float[] pcmData, int frameSize) {
				if (_disposed || _encoder == IntPtr.Zero) {
					throw new ObjectDisposedException("OpusEncoderInstance");
				}

				// Convert float to short
				short[] shortData = new short[pcmData.Length];
				for (int i = 0; i < pcmData.Length; i++) {
					shortData[i] = (short)(pcmData[i] * 32767f);
				}

				byte[] outputBuffer = new byte[4000]; // Max Opus packet size

				GCHandle pcmHandle    = GCHandle.Alloc(shortData, GCHandleType.Pinned);
				GCHandle outputHandle = GCHandle.Alloc(outputBuffer, GCHandleType.Pinned);

				try {
					int encodedBytes = opus_encode(_encoder, pcmHandle.AddrOfPinnedObject(), frameSize, outputHandle.AddrOfPinnedObject(), outputBuffer.Length);

					if (encodedBytes < 0) {
						throw new Exception($"Opus encoding failed: {encodedBytes}");
					}

					byte[] result = new byte[encodedBytes];
					Array.Copy(outputBuffer, result, encodedBytes);
					return result;
				} finally {
					pcmHandle.Free();
					outputHandle.Free();
				}
			}

			public void Dispose() {
				if (!_disposed && _encoder != IntPtr.Zero) {
					opus_encoder_destroy(_encoder);
					_encoder  = IntPtr.Zero;
					_disposed = true;
				}
			}
		}
	}
}