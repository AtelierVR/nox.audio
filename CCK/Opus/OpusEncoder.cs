using System;
using Concentus;
using Concentus.Enums;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Opus audio encoder — managed Concentus wrapper (no native P/Invoke).
    /// Works in IL2CPP, WebGL, and all Unity platforms.
    /// </summary>
    public static class OpusEncoder {
        public class OpusEncoderInstance : IDisposable {

            private readonly IOpusEncoder _encoder;
            private readonly byte[] _buffer;

            /// <summary>Last bitrate the encoder was told to use, kept so callers can read it back.</summary>
            private int _bitrate;

            private bool _disposed;

            public bool IsValid 
				=> !_disposed;

            /// <summary>
            /// Target bitrate in bps. Assignable while encoding — libopus applies it to the following
            /// frames — so a sender can follow a moving datagram budget without rebuilding the encoder.
            /// </summary>
            public int Bitrate {
                get => _bitrate;
                set {
                    _bitrate = OpusConstants.ClampBitrate(value);
                    if (!_disposed)
                        _encoder.Bitrate = _bitrate;
                }
            }

            /// <summary>
            /// Create an Opus encoder instance.
            /// </summary>
            /// <param name="sampleRate">Sample rate (8000, 12000, 16000, 24000 or 48000; 48000 recommended).</param>
            /// <param name="channels">Number of channels (1 = mono).</param>
            /// <param name="bitrate">Target bitrate in bps.</param>
            /// <param name="complexity">Opus complexity (0-10).</param>
            /// <param name="signalType">Signal type hint (auto/voice/music).</param>
            /// <param name="packetLossPercent">Expected packet loss (percent); 0 leaves the encoder default.</param>
            /// <param name="useInbandFec">
            /// Enables in-band FEC so lost frames can be partially recovered from the *next*
            /// packet. Only useful together with a non-zero packetLossPercent, and only helps
            /// if the receiver actually calls TryRecoverWithFec on the decoder side.
            /// </param>
            public OpusEncoderInstance(
                int sampleRate,
                int channels,
                int bitrate,
                int complexity,
                OpusSignalType signalType,
                int packetLossPercent = OpusConstants.DefaultPacketLossPercent,
                bool useInbandFec = true) {

                OpusConstants.ValidateSampleRate(sampleRate);
                OpusConstants.ValidateChannels(channels);

                // Voice-tuned encoder path, for a 20 ms mono stream over unreliable datagrams.
                _encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
                Bitrate = bitrate;
                _encoder.Complexity = OpusConstants.ClampComplexity(complexity);
                _encoder.SignalType = signalType.ToOpusSignal();

                if (packetLossPercent > 0) {
                    _encoder.PacketLossPercent = Math.Clamp(packetLossPercent, 0, 100);
                    // FEC only has an effect when the encoder is told loss is expected;
                    // enabling it with 0% expected loss would just waste bitrate.
                    _encoder.UseInbandFEC = useInbandFec;
                }

                _buffer = new byte[OpusConstants.MaxPacketSize];
            }

            /// <summary>
            /// Encode PCM float samples to Opus bytes. Allocates a new array each call;
            /// use <see cref="Encode(float[], int, byte[], int)"/> to encode into a reusable buffer.
            /// </summary>
            /// <param name="pcmData">Float PCM samples [-1..1].</param>
            /// <param name="frameSize">Samples per channel per frame (e.g. 960 for 20ms @ 48kHz).</param>
            /// <param name="maxDataBytes">Max encoded bytes to produce (clamped to <see cref="MaxPacketSize"/>).</param>
            /// <returns>Opus-encoded byte array, or null on failure.</returns>
            public byte[] Encode(float[] pcmData, int frameSize, int maxDataBytes = OpusConstants.MaxPacketSize) {
                int max = Math.Min(maxDataBytes <= 0 ? OpusConstants.MaxPacketSize : maxDataBytes, _buffer.Length);
                int bytesEncoded = Encode(pcmData, frameSize, _buffer, max);
                if (bytesEncoded <= 0) return null;

                byte[] result = new byte[bytesEncoded];
                Array.Copy(_buffer, result, bytesEncoded);
                return result;
            }

            /// <summary>
            /// Encode PCM float samples directly into a caller-provided buffer (no allocation).
            /// </summary>
            /// <param name="pcmData">Float PCM samples [-1..1].</param>
            /// <param name="frameSize">Samples per channel per frame.</param>
            /// <param name="destination">Buffer to write encoded bytes into.</param>
            /// <param name="maxDataBytes">Max bytes to write into destination.</param>
            /// <returns>Number of bytes written, or -1 on failure.</returns>
            public int Encode(float[] pcmData, int frameSize, byte[] destination, int maxDataBytes) {
                if (_disposed) throw new ObjectDisposedException(nameof(OpusEncoderInstance));
                if (destination == null) throw new ArgumentNullException(nameof(destination));

                int max = Math.Min(maxDataBytes, destination.Length);
                if (max <= 0) max = destination.Length;

                return _encoder.Encode(pcmData, frameSize, destination, max);
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