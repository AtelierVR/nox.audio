using System;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Shared constants and validation helpers for Opus encoding/decoding.
    /// </summary>
    public static class OpusConstants {
        /// <summary>Valid Opus sample rates (Hz). Any other value is rejected by libopus.</summary>
        public static readonly int[] ValidSampleRates = { 8000, 12000, 16000, 24000, 48000 };

        /// <summary>Recommended sample rate for full-band audio (music, high quality voice).</summary>
        public const int SampleRate48k = 48000;

        /// <summary>Common sample rate for voice-only use cases (lower bandwidth/CPU).</summary>
        public const int SampleRate24k = 24000;

        /// <summary>Narrowband sample rate, telephone-quality voice.</summary>
        public const int SampleRate8k = 8000;

        /// <summary>Minimum channel count (mono).</summary>
        public const int MinChannels = 1;

        /// <summary>Maximum channel count supported by Opus (stereo).</summary>
        public const int MaxChannels = 2;

        /// <summary>Maximum encoded size (bytes) of a single Opus packet.</summary>
        public const int MaxPacketSize = 1275;

        /// <summary>Maximum Opus bitrate (bps). libopus caps at ~510 kbps.</summary>
        public const int MaxBitrate = 510_000;

        /// <summary>Minimum sane Opus bitrate (bps) below which quality degrades severely.</summary>
        public const int MinBitrate = 6_000;

        /// <summary>A reasonable default bitrate (bps) for voice chat (VOIP).</summary>
        public const int DefaultVoiceBitrate = 24_000;

        /// <summary>
        /// Default ceiling (bps) for the automatic bitrate. The datagram budget of a 1500-byte MTU
        /// allows ~510 kbps at 20 ms frames, but mono at 48 kHz is already transparent well below
        /// that — this is the point past which extra bits only cost bandwidth and packet loss.
        /// </summary>
        public const int DefaultVoiceBitrateCeiling = 96_000;

        /// <summary>Minimum Opus complexity (fastest, lowest quality).</summary>
        public const int MinComplexity = 0;

        /// <summary>Maximum Opus complexity (slowest, highest quality).</summary>
        public const int MaxComplexity = 10;

        /// <summary>Default complexity: good quality/CPU trade-off for real-time voice.</summary>
        public const int DefaultComplexity = 5;

        /// <summary>Default expected packet loss (percent) declared to the encoder.</summary>
        public const int DefaultPacketLossPercent = 10;

        /// <summary>Minimum valid frame duration (ms) accepted by Opus.</summary>
        public const float MinFrameDurationMs = 2.5f;

        /// <summary>Maximum valid frame duration (ms) accepted by Opus.</summary>
        public const float MaxFrameDurationMs = 60f;

        /// <summary>Standard frame duration (ms) used for real-time voice chat.</summary>
        public const int DefaultFrameDurationMs = 20;

        /// <summary>Valid Opus frame durations (ms). Any other value is rejected by libopus.</summary>
        public static readonly float[] ValidFrameDurationsMs = { 2.5f, 5f, 10f, 20f, 40f, 60f };

        /// <summary>
        /// Computes samples-per-channel for a given sample rate and frame duration.
        /// e.g. FrameSize(48000, 20) = 960.
        /// </summary>
        public static int FrameSize(int sampleRate, float frameDurationMs) {
            return (int)(sampleRate * frameDurationMs / 1000f);
        }

        /// <summary>Throws if sampleRate is not a value Opus accepts.</summary>
        public static void ValidateSampleRate(int sampleRate) {
            if (Array.IndexOf(ValidSampleRates, sampleRate) < 0) {
                throw new ArgumentException(
                    $"Invalid Opus sample rate: {sampleRate}. Must be one of {string.Join(", ", ValidSampleRates)}.",
                    nameof(sampleRate));
            }
        }

        /// <summary>Throws if channels is not 1 or 2.</summary>
        public static void ValidateChannels(int channels) {
            if (channels < MinChannels || channels > MaxChannels) {
                throw new ArgumentException(
                    $"Invalid channel count: {channels}. Opus supports {MinChannels} (mono) or {MaxChannels} (stereo).",
                    nameof(channels));
            }
        }

        /// <summary>Throws if frameDurationMs is not one of the durations Opus accepts.</summary>
        public static void ValidateFrameDuration(float frameDurationMs) {
            if (Array.IndexOf(ValidFrameDurationsMs, frameDurationMs) < 0) {
                throw new ArgumentException(
                    $"Invalid Opus frame duration: {frameDurationMs}ms. Must be one of {string.Join(", ", ValidFrameDurationsMs)}.",
                    nameof(frameDurationMs));
            }
        }

        /// <summary>Clamps a bitrate value to Opus' valid range.</summary>
        public static int ClampBitrate(int bitrate) {
            return Math.Clamp(bitrate, MinBitrate, MaxBitrate);
        }

        /// <summary>Clamps a complexity value to Opus' valid range.</summary>
        public static int ClampComplexity(int complexity) {
            return Math.Clamp(complexity, MinComplexity, MaxComplexity);
        }
    }
}