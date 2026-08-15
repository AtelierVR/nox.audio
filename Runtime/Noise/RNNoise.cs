using System;
using System.Runtime.InteropServices;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Managed wrapper around the native RNNoise denoiser.
	/// Processes fixed 480-sample (10ms @ 48kHz) float frames in-place.
	/// The native library must be loaded first (see Main → LibAPI.Load("rnnoise")).
	/// </summary>
	public sealed class RNNoise : IDisposable {
		public const int FrameSize = 480;

		// rnnoise expects float samples in int16 range [-32768, 32767].
		private const float SignalScale = 32767f;
		private const float SignalScaleInv = 1f / 32767f;

		private IntPtr _state;
		private bool _createAttempted;

		public bool IsValid
			=> _state != IntPtr.Zero;

		/// <summary>
		/// Denoise a 480-sample frame in-place. No-op if the frame size is not 480.
		/// </summary>
		public void Process(float[] samples) {
			if (samples == null || samples.Length == 0 || samples.Length % FrameSize != 0)
				return;

			EnsureCreated();
			if (_state == IntPtr.Zero)
				return;

			// Scale to int16 range.
			for (int i = 0; i < samples.Length; i++)
				samples[i] *= SignalScale;

			int frames = samples.Length / FrameSize;
			var handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
			try {
				IntPtr basePtr = handle.AddrOfPinnedObject();
				for (int f = 0; f < frames; f++) {
					IntPtr framePtr = IntPtr.Add(basePtr, f * FrameSize * sizeof(float));
					Native.rnnoise_process_frame(_state, framePtr, framePtr);
				}
			} finally {
				handle.Free();
			}

			// Scale back to [-1, 1].
			for (int i = 0; i < samples.Length; i++)
				samples[i] *= SignalScaleInv;
		}

		/// <summary>
		/// Lazily create the native denoise state. Deferred until the native library
		/// has been loaded (LibAPI.Load runs after MicrophoneManager is built).
		/// </summary>
		private void EnsureCreated() {
			if (_state != IntPtr.Zero || _createAttempted)
				return;
			_createAttempted = true;
			try {
				_state = Native.rnnoise_create(IntPtr.Zero);
			} catch (Exception e) {
				Logger.LogWarning($"Failed to initialize native denoiser: {e.Message}", tag: nameof(RNNoise));
				_state = IntPtr.Zero;
			}
		}

		public void Dispose() {
			if (_state != IntPtr.Zero) {
				Native.rnnoise_destroy(_state);
				_state = IntPtr.Zero;
			}
		}

		private static class Native {
			private const string Lib = "rnnoise";

			[DllImport(Lib)]
			public static extern IntPtr rnnoise_create(IntPtr model);

			[DllImport(Lib)]
			public static extern void rnnoise_destroy(IntPtr state);

			[DllImport(Lib)]
			public static extern float rnnoise_process_frame(IntPtr state, IntPtr dataOut, IntPtr dataIn);
		}
	}
}
