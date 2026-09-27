using System;
using Nox.CCK.Utils;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Opus codec configuration — static accessor over the shared config store
    /// (<see cref="Config.Load()"/>). Values are read live from config.json with
    /// sensible defaults, so no ScriptableObject asset is required.
    /// </summary>
    public static class OpusConfig {

        private const string Prefix = "settings.opus";

        private static T Get<T>(string key, T fallback)
            => Config.Load().Get($"{Prefix}.{key}", fallback);

        private static void Set<T>(string key, T value) {
            var config = Config.Load();
            config.Set($"{Prefix}.{key}", value);
            config.Save();
        }

        // ── Codec settings ──
        public static int Complexity {
            get => Get("complexity", OpusConstants.DefaultComplexity);
            set => Set("complexity", OpusConstants.ClampComplexity(value));
        }

        public static OpusSignalType SignalType {
            get => Get("signal_type", "auto").ToOpusSignalType();
            set => Set("signal_type", value.ToString());
        }

        /// <summary>Frame duration in ms (must be one of 2.5/5/10/20/40/60).</summary>
        public static float FrameDurationMs {
            get {
                var value = Get("frame_duration_ms", (float)OpusConstants.DefaultFrameDurationMs);
                // A hand-edited config must not make every encoder creation throw.
                return Array.IndexOf(OpusConstants.ValidFrameDurationsMs, value) < 0
                    ? OpusConstants.DefaultFrameDurationMs
                    : value;
            }
            set {
                OpusConstants.ValidateFrameDuration(value);
                Set("frame_duration_ms", value);
            }
        }

        /// <summary>Target bitrate in bps. 0 = auto (uses <see cref="VoiceBitrateCeiling"/>).</summary>
        public static int Bitrate {
            get => Get("bitrate", 0);
            set => Set("bitrate", value == 0 ? 0 : OpusConstants.ClampBitrate(value));
        }

        /// <summary>
        /// Bitrate ceiling used when <see cref="Bitrate"/> is 0 (auto). The raw datagram budget allows
        /// several hundred kbps, but mono voice is transparent far below that and every extra bit is a
        /// bit more likely to be lost — see <see cref="ResolveBitrate"/>.
        /// </summary>
        public static int VoiceBitrateCeiling {
            get => Get("voice_bitrate_ceiling", OpusConstants.DefaultVoiceBitrateCeiling);
            set => Set("voice_bitrate_ceiling", OpusConstants.ClampBitrate(value));
        }

        // ── Datagram budget ──

        /// <summary>
        /// Largest payload a single Opus packet can hold — the budget to fall back on while the
        /// transport MTU is not known yet.
        /// </summary>
        public static int MaxPayload
            => OpusConstants.MaxPacketSize;

        /// <summary>
        /// Clamps a datagram payload budget to what one Opus packet can carry. Callers compute the raw
        /// budget from the transport (<c>IConnector.Mtu - protocol framing</c>), since only they know how
        /// the packet is framed on the wire.
        /// </summary>
        public static int ClampPayload(int bytes)
            => Math.Clamp(bytes, 128, OpusConstants.MaxPacketSize);

        /// <summary>
        /// Bitrate to encode at for a given payload budget: <see cref="Bitrate"/> when it was set
        /// explicitly, otherwise the largest rate that budget can carry at the configured frame rate,
        /// capped by <see cref="VoiceBitrateCeiling"/> so a loud, complex frame cannot overflow the
        /// datagram.
        /// </summary>
        public static int ResolveBitrate(int maxPayloadBytes) {
            var configured = Bitrate;
            if (configured > 0)
                return configured;

            var allowed = (int)(maxPayloadBytes * 8 * FramesPerSecond);
            return OpusConstants.ClampBitrate(Math.Min(VoiceBitrateCeiling, allowed));
        }

        // ── Derived values ──
        public static int SamplesPerFrame 
			=> OpusConstants.FrameSize(
				OpusConstants.SampleRate48k, 
				FrameDurationMs
			);

        public static float FramesPerSecond 
			=> 1000f / FrameDurationMs;

        public static float SecondsPerFrame 
			=> FrameDurationMs / 1000f;
    }
}