using System;
using Concentus;

namespace Nox.CCK.Audio.Opus {
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

            public bool IsValid 
				=> !_disposed;

            /// <summary>
            /// Create a new Opus decoder.
            /// </summary>
            /// <param name="sampleRate">Sample rate in Hz (8000, 12000, 16000, 24000 or 48000).</param>
            /// <param name="channels">Number of channels (1 = mono).</param>
            public OpusDecoderInstance(int sampleRate, int channels) {
                OpusConstants.ValidateSampleRate(sampleRate);
                OpusConstants.ValidateChannels(channels);

                _decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
                _channels = channels;
            }

            /// <summary>
            /// Decode an Opus packet into PCM float samples. Allocates a new array each call;
            /// use <see cref="Decode(byte[], int, float[])"/> to decode into a reusable buffer.
            /// </summary>
            /// <param name="opusData">The Opus-encoded byte packet.</param>
            /// <param name="frameSize">Expected samples per channel in output.</param>
            /// <returns>Decoded PCM float samples, or empty array on failure.</returns>
            public float[] Decode(byte[] opusData, int frameSize) {
                float[] pcm = new float[frameSize * _channels];
                int samplesDecoded = Decode(opusData, frameSize, pcm);

                if (samplesDecoded <= 0)
                    return samplesDecoded == 0 ? Array.Empty<float>() : new float[frameSize * _channels];

                int totalSamples = samplesDecoded * _channels;
                if (totalSamples < pcm.Length) {
                    float[] trimmed = new float[totalSamples];
                    Array.Copy(pcm, trimmed, totalSamples);
                    return trimmed;
                }

                return pcm;
            }

            /// <summary>
            /// Decode an Opus packet into a caller-provided buffer (no allocation on the happy path).
            /// </summary>
            /// <param name="opusData">The Opus-encoded byte packet. Null/empty triggers PLC.</param>
            /// <param name="frameSize">Expected samples per channel in output.</param>
            /// <param name="destination">Buffer to decode into; must be at least frameSize * channels.</param>
            /// <returns>Number of samples decoded per channel, or -1 on failure.</returns>
            public int Decode(byte[] opusData, int frameSize, float[] destination) {
                if (_disposed) throw new ObjectDisposedException(nameof(OpusDecoderInstance));
                if (destination == null || destination.Length < frameSize * _channels)
                    throw new ArgumentException("Destination buffer is too small.", nameof(destination));

                if (opusData == null || opusData.Length == 0)
                    return DecodeLost(frameSize, destination);

                int samplesDecoded;
                try {
                    samplesDecoded = _decoder.Decode(opusData, destination, frameSize, false);
                } catch (OpusException) {
                    // Concentus throws on corrupted/unparseable packets.
                    // Fall back to packet loss concealment.
                    return DecodeLost(frameSize, destination);
                }

                return samplesDecoded < 0 ? DecodeLost(frameSize, destination) : samplesDecoded;
            }

            /// <summary>
            /// Decode with packet loss concealment (PLC) to fill a missing frame.
            /// </summary>
            /// <param name="frameSize">Expected output sample count.</param>
            /// <returns>Concealed PCM samples, or silence on failure.</returns>
            public float[] DecodeLost(int frameSize) {
                if (_disposed) return Array.Empty<float>();

                float[] pcm = new float[frameSize * _channels];
                DecodeLost(frameSize, pcm);
                return pcm;
            }

            /// <summary>
            /// Decode with packet loss concealment into a caller-provided buffer.
            /// </summary>
            public int DecodeLost(int frameSize, float[] destination) {
                if (_disposed) return -1;

                // No actual packet available here, so this is plain PLC, not FEC:
                // decode_fec must be false. FEC only applies when you pass the *next*
                // received packet's data (it carries recovery info for the previous one).
                int samplesDecoded = _decoder.Decode(null, destination, frameSize, false);

                if (samplesDecoded < 0) {
                    Array.Clear(destination, 0, frameSize * _channels); // fall back to silence
                    return frameSize;
                }

                return samplesDecoded;
            }

            /// <summary>
            /// Attempt FEC recovery of a lost frame using the next received packet,
            /// then decode that next packet normally. Call this instead of DecodeLost
            /// when you already have the following packet in hand.
            /// </summary>
            /// <param name="nextPacket">The next Opus packet actually received (not the lost one).</param>
            /// <param name="lostFrameSize">Samples per channel to recover for the lost frame.</param>
            /// <param name="recoveredLostFrame">Buffer to receive the recovered (previous) frame.</param>
            /// <returns>True if recovery via FEC succeeded.</returns>
            public bool TryRecoverWithFec(byte[] nextPacket, int lostFrameSize, float[] recoveredLostFrame) {
                if (_disposed || nextPacket == null || nextPacket.Length == 0) return false;
                if (recoveredLostFrame == null || recoveredLostFrame.Length < lostFrameSize * _channels) return false;

                try {
                    int samples = _decoder.Decode(nextPacket, recoveredLostFrame, lostFrameSize, true);
                    return samples > 0;
                } catch (OpusException) {
                    return false;
                }
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