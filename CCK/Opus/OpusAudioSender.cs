using System;
using Nox.Audio.Players;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Reads newly captured samples from an <see cref="ICapturedAudio"/> circular buffer
    /// (e.g. a Microphone-backed AudioClip), encodes them to Opus frame by frame,
    /// and forwards each packet to a send callback. Call <see cref="Tick"/> every frame
    /// (e.g. from MonoBehaviour.Update) to drain newly available audio.
    /// </summary>
    public class OpusAudioSender : IDisposable {
        public class Settings {
            public int Channels = 1;
            public float FrameDurationMs = OpusConstants.DefaultFrameDurationMs;
            public int Bitrate = OpusConstants.DefaultVoiceBitrate;
            public int Complexity = OpusConstants.DefaultComplexity;
            public OpusSignalType SignalType = OpusSignalType.Voice;
            public int PacketLossPercent = OpusConstants.DefaultPacketLossPercent;
            public bool UseInbandFec = true;

            /// <summary>Max encoded payload size in bytes for a single packet (e.g. transport MTU budget).</summary>
            public int MaxPayloadSize = OpusConstants.MaxPacketSize;

            /// <summary>
            /// Settings resolved from <see cref="OpusConfig"/> for a datagram payload budget (the
            /// transport MTU minus the protocol framing, see <c>StreamRequest.FramingOverhead</c>): the
            /// payload is capped to what a datagram can carry, and the bitrate fills that budget (up to
            /// the configured ceiling) instead of staying at a fixed voice-chat rate.
            /// <para>
            /// The frame duration is the configured one, which is also the grid the microphone DSP
            /// runs on, so both stay phase-aligned.
            /// </para>
            /// </summary>
            public static Settings FromBudget(int maxPayloadBytes) {
                var budget = OpusConfig.ClampPayload(maxPayloadBytes);

                return new Settings {
                    Channels        = 1,
                    FrameDurationMs = OpusConfig.FrameDurationMs,
                    Bitrate         = OpusConfig.ResolveBitrate(budget),
                    Complexity      = OpusConfig.Complexity,
                    SignalType      = OpusConfig.SignalType,
                    MaxPayloadSize  = budget,
                };
            }
        }

        private readonly ICapturedAudio _source;
        private readonly Action<byte[]> _send;
        private readonly Settings _settings;
        private readonly OpusEncoder.OpusEncoderInstance _encoder;

        private readonly int _sampleRate;
        private readonly int _frameSize;      // samples per channel per frame
        private readonly float[] _frameBuffer; // frameSize * channels
        private byte[] _encodeBuffer;

        private int _maxPayloadSize;

        private int _lastReadPosition = -1;
        private bool _disposed;

        /// <summary>False while paused; Tick() becomes a no-op.</summary>
        public bool IsRunning { get; private set; } = true;

        /// <summary>
        /// Largest encoded payload a single datagram may carry: the transport's usable payload minus the
        /// protocol framing. Assignable at any time — the bitrate budget and the encoder's maximum packet
        /// size follow immediately, without interrupting the stream — so a transport whose MTU moves
        /// (QUIC path MTU discovery) can simply push the new value.
        /// </summary>
        public int MaxPayloadSize {
            get => _maxPayloadSize;
            set {
                var budget = OpusConfig.ClampPayload(value);
                if (budget == _maxPayloadSize)
                    return;

                _maxPayloadSize  = budget;
                _encodeBuffer    = new byte[budget];
                Bitrate          = OpusConstants.ClampBitrate(OpusConfig.ResolveBitrate(budget));
                _encoder.Bitrate = Bitrate;

                Logger.LogDebug(
                    $"Payload budget {budget} B: encoding at {Bitrate / 1000} kbps.",
                    tag: nameof(OpusAudioSender)
                );
            }
        }

        /// <summary>Bitrate the encoder targets, in bps; follows <see cref="MaxPayloadSize"/>.</summary>
        public int Bitrate { get; private set; }

        public OpusAudioSender(ICapturedAudio source, Action<byte[]> send, Settings settings = null) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _settings = settings ?? new Settings();

            if (_source.Clip == null)
                throw new ArgumentException("ICapturedAudio.Clip is null; cannot determine sample rate/channels yet.", nameof(source));

            _sampleRate = _source.Clip.frequency;

            OpusConstants.ValidateSampleRate(_sampleRate);
            OpusConstants.ValidateChannels(_settings.Channels);
            OpusConstants.ValidateFrameDuration(_settings.FrameDurationMs);

            if (_source.Clip.channels != _settings.Channels) 
                Logger.LogWarning($"Clip channel count ({_source.Clip.channels}) does not match configured channels ({_settings.Channels}); using configured value.", tag: nameof(OpusAudioSender));

            _frameSize = OpusConstants.FrameSize(_sampleRate, _settings.FrameDurationMs);

            _maxPayloadSize = OpusConfig.ClampPayload(_settings.MaxPayloadSize);
            Bitrate         = OpusConstants.ClampBitrate(_settings.Bitrate);

            _encoder = new OpusEncoder.OpusEncoderInstance(
                _sampleRate,
                _settings.Channels,
                Bitrate,
                _settings.Complexity,
                _settings.SignalType,
                _settings.PacketLossPercent,
                _settings.UseInbandFec);

            _frameBuffer  = new float[_frameSize * _settings.Channels];
            _encodeBuffer = new byte[_maxPayloadSize];
        }

        /// <summary>
        /// Pulls newly available samples since the last call, encodes every complete
        /// frame, and sends it. Call this once per frame/tick from the outside.
        /// </summary>
        public void Tick() {
            if (_disposed || !IsRunning) return;
            if (_source.Clip == null) return;

            int clipLength = _source.Clip.samples; // per channel, i.e. ring buffer length
            int writePos = _source.Position;

            if (_lastReadPosition < 0) {
                // First tick: start tracking from "now" instead of flushing
                // whatever backlog happens to already be in the buffer.
                _lastReadPosition = writePos;
                return;
            }

            int available = writePos - _lastReadPosition;
            if (available < 0) available += clipLength; // wrapped around

            if (available <= 0) return;

            if (available > clipLength) {
                // We fell behind further than the buffer can hold: data was
                // overwritten before we read it. Resync instead of reading garbage.
                Logger.LogWarning("Capture buffer overrun; some audio was dropped.", tag: nameof(OpusAudioSender));
                _lastReadPosition = writePos;
                return;
            }

            while (available >= _frameSize) {
                // AudioClip.GetData reads cyclically from offsetSamples, wrapping
                // past the end of the clip automatically (this is what makes reading
                // a Microphone's looping AudioClip work) — no manual split needed.
                _source.Clip.GetData(_frameBuffer, _lastReadPosition);

                EncodeAndSend();

                _lastReadPosition = (_lastReadPosition + _frameSize) % clipLength;
                available -= _frameSize;
            }
        }

        private void EncodeAndSend() {
            int bytesEncoded = _encoder.Encode(_frameBuffer, _frameSize, _encodeBuffer, _encodeBuffer.Length);
            if (bytesEncoded <= 0) return;

            byte[] packet = new byte[bytesEncoded];
            Array.Copy(_encodeBuffer, packet, bytesEncoded);
            _send(packet);
        }

        /// <summary>Stops Tick() from doing anything until Resume() is called.</summary>
        public void Pause() => IsRunning = false;

        /// <summary>
        /// Resumes ticking. Resets tracking to the current write position so no
        /// backlog accumulated while paused gets flushed all at once. Calling it
        /// while already running is a no-op, so callers may resume unconditionally.
        /// </summary>
        public void Resume() {
            if (IsRunning) return;

            _lastReadPosition = -1;
            IsRunning         = true;
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            IsRunning = false;
            _encoder?.Dispose();
        }
    }
}