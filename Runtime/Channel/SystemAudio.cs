using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nox.Audio.Runtime.Channels {
	internal static class SystemAudio {
		private const double PollInterval = 1.0;

		private static bool _hasValue;
		private static bool _silent;
		private static double _nextPoll;

		public static bool IsSilent {
			get {
				if (!_hasValue)
					Refresh();
				return _silent;
			}
		}

		public static bool Update() {
			var now = Time.realtimeSinceStartupAsDouble;
			if (_hasValue && now < _nextPoll)
				return false;

			return Read(now);
		}

		public static bool Refresh()
			=> Read(Time.realtimeSinceStartupAsDouble);

		private static bool Read(double now) {
			_nextPoll = now + PollInterval;

			var value = Probe();
			var changed = _hasValue && value != _silent;

			_hasValue = true;
			_silent = value;

			return changed;
		}

		private static bool Probe() {
#if UNITY_EDITOR
			if (UnityEditor.EditorUtility.audioMasterMute)
				return true;
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			return Windows.IsSilent;
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			return Linux.IsSilent;
#else
			return false;
#endif
		}

		public static void Shutdown() {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			Windows.Release();
#endif
			_hasValue = false;
			_silent = false;
			_nextPoll = 0;
		}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		private static class Windows {
			private static IAudioEndpointVolume _endpoint;
			private static bool _unavailable;

			public static bool IsSilent {
				get {
					var endpoint = Endpoint();
					if (endpoint == null)
						return false;

					try {
						if (endpoint.GetMute(out var mute) >= 0 && mute)
							return true;

						return endpoint.GetMasterVolumeLevelScalar(out var level) >= 0 && level <= 0f;
					} catch (Exception) {
						Release();
						return false;
					}
				}
			}

			public static void Release() {
				var endpoint = _endpoint;
				_endpoint = null;

				if (endpoint == null)
					return;

				try {
					if (Marshal.IsComObject(endpoint))
						Marshal.ReleaseComObject(endpoint);
				} catch (Exception) { }
			}

			private static IAudioEndpointVolume Endpoint() {
				if (_endpoint != null)
					return _endpoint;

				if (_unavailable)
					return null;

				object enumeratorObject = null;
				object deviceObject = null;

				try {
					enumeratorObject = new MmDeviceEnumeratorComObject();

					var enumerator = (IMMDeviceEnumerator)enumeratorObject;
					if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) < 0)
						throw new InvalidOperationException();

					deviceObject = device;

					var iid = typeof(IAudioEndpointVolume).GUID;
					if (device.Activate(ref iid, 1, IntPtr.Zero, out var activated) < 0 || activated is not IAudioEndpointVolume endpoint)
						throw new InvalidOperationException();

					_endpoint = endpoint;

					return _endpoint;
				} catch (Exception) {
					_unavailable = true;
					return null;
				} finally {
					if (deviceObject != null)
						Marshal.ReleaseComObject(deviceObject);
					if (enumeratorObject != null)
						Marshal.ReleaseComObject(enumeratorObject);
				}
			}

			private enum EDataFlow {
				Render = 0,
				Capture = 1,
				All = 2,
			}

			private enum ERole {
				Console = 0,
				Multimedia = 1,
				Communications = 2,
			}

			[ComImport]
			[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
			private class MmDeviceEnumeratorComObject { }

			[ComImport]
			[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
			[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			private interface IMMDeviceEnumerator {
				int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
				int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
			}

			[ComImport]
			[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
			[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			private interface IMMDevice {
				int Activate(ref Guid iid, int context, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
			}

			[ComImport]
			[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
			[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			private interface IAudioEndpointVolume {
				int RegisterControlChangeNotify(IntPtr notify);
				int UnregisterControlChangeNotify(IntPtr notify);
				int GetChannelCount(out uint count);
				int SetMasterVolumeLevel(float level, ref Guid context);
				int SetMasterVolumeLevelScalar(float level, ref Guid context);
				int GetMasterVolumeLevel(out float level);
				int GetMasterVolumeLevelScalar(out float level);
				int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
				int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
				int GetChannelVolumeLevel(uint channel, out float level);
				int GetChannelVolumeLevelScalar(uint channel, out float level);
				int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
				int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
			}
		}
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
		private static class Linux {
			private const string DefaultSink = "@DEFAULT_SINK@";

			private static readonly Regex Percent = new(@"(\d{1,3})%", RegexOptions.Compiled);

			private static int _tool;

			public static bool IsSilent {
				get {
					if (_tool <= 0 && !Detect())
						return false;

					if (_tool == 1) {
						var mute = Run("pactl", $"get-sink-mute {DefaultSink}");
						if (mute == null)
							return false;

						if (mute.IndexOf("yes", StringComparison.OrdinalIgnoreCase) >= 0)
							return true;

						var volume = Run("pactl", $"get-sink-volume {DefaultSink}");
						return volume != null && ParseVolume(volume) <= 0f;
					}

					var master = Run("amixer", "get Master");
					if (master == null)
						return false;

					if (master.IndexOf("[off]", StringComparison.OrdinalIgnoreCase) >= 0)
						return true;

					return ParseVolume(master) <= 0f;
				}
			}

			private static bool Detect() {
				if (Run("pactl", "info") != null) {
					_tool = 1;
					return true;
				}

				if (Run("amixer", "get Master") != null) {
					_tool = 2;
					return true;
				}

				_tool = -1;
				return false;
			}

			private static float ParseVolume(string output) {
				var match = Percent.Match(output);
				if (!match.Success)
					return 1f;

				return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
					? percent / 100f
					: 1f;
			}

			private static string Run(string file, string arguments) {
				try {
					var info = new ProcessStartInfo(file, arguments) {
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					};

					using var process = Process.Start(info);
					if (process == null)
						return null;

					var output = process.StandardOutput.ReadToEndAsync();

					if (!process.WaitForExit(500)) {
						try {
							process.Kill();
						} catch (Exception) { }

						return null;
					}

					return process.ExitCode == 0
						? output.Result
						: null;
				} catch (Exception) {
					return null;
				}
			}
		}
#endif
	}
}
