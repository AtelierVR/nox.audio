using System;
using System.Collections.Generic;
using Nox.Audio.Players;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Decodes incoming Opus packets and writes the resulting PCM into a circular
    /// <see cref="AudioClip"/>, mirroring how Unity's Microphone exposes captured
    /// audio. Loop an <see cref="AudioSource"/> on <see cref="Clip"/> to play it back.
    /// </summary>
    /// <remarks>
    /// Datagrams are reordered, duplicated and lost in transit, so frames are handed over with their
    /// sender frame index (<see cref="ReceiveFrame"/>) and the playout asks for the next slot
    /// (<see cref="PopFrame"/>, once per frame duration): the buffer restores the sender's order, a
    /// retransmission is ignored, a frame that never arrives is concealed in its own slot instead of
    /// delaying every frame after it, and a stream that stops is padded with silence. Its depth is
    /// bounded (<see cref="Settings.MaxBufferedFrames"/>), beyond which the playout resynchronizes on
    /// the newest frame rather than growing the latency.
    /// </remarks>
    public class OpusAudioReceiver : IDisposable, ICapturedAudio {
        public class Settings {
            public int Channels = 1;
            public float FrameDurationMs = OpusConstants.DefaultFrameDurationMs;

            /// <summary>Length of the circular playback buffer, in seconds.</summary>
            public float RingBufferSeconds = 1f;

            /// <summary>
            /// Frames the reorder buffer accepts ahead of the playout, so a burst cannot grow the
            /// latency; a frame further ahead resynchronizes the timeline instead.
            /// </summary>
            public int MaxBufferedFrames = 25;

            /// <summary>
            /// Beyond this long without a frame, a hole comes from the sender's voice gate: write real
            /// silence and wait for the stream to lock a new timeline.
            /// </summary>
            public float ConcealMaxSilenceMs = 150f;

            /// <summary>
            /// Missing slots in a row concealed with PLC. Beyond that a hole is padded with silence: a
            /// long PLC tail drones, and the frames in between are lost anyway.
            /// </summary>
            public int MaxConcealedFrames = 3;

            /// <summary>
            /// Gain applied to the RMS when computing <see cref="Level"/>. The default normalizes a
            /// full-scale sine (RMS = 1/√2) to 1, so the level follows the amplitude instead of
            /// saturating as soon as the input is loud.
            /// </summary>
            public float LevelGain = 1.41421f;
        }

        public AudioClip Clip { get; private set; }
        public int Position => _writePosition;

        /// <summary>Frames received but not played yet: the jitter cushion currently held.</summary>
        public int BufferedFrames => _pending.Count;

        /// <summary>
        /// Level of the frame currently being played by the audio output, in the 0-1 range (silence to
        /// full scale). Set <see cref="ReadPosition"/> to the output read head
        /// (<c>AudioSource.timeSamples</c>) so UI indicators follow what is heard instead of what was
        /// just written.
        /// </summary>
        public float Level {
            get {
                if (_frameLevels.Length == 0)
                    return 0f;

                var position = _readPosition < 0 ? _writePosition : _readPosition;
                return _frameLevels[position / _frameSize % _frameLevels.Length];
            }
        }

        /// <summary>
        /// Sample position being played by the output, in <see cref="Clip"/> coordinates. Negative
        /// means "not playing": <see cref="Level"/> then reports the frame just written.
        /// </summary>
        public int ReadPosition {
            get => _readPosition;
            set => _readPosition = value;
        }

        private readonly Settings _settings;
        private readonly OpusDecoder.OpusDecoderInstance _decoder;
        private readonly int _channels;
        private readonly int _frameSize;       // samples per channel per frame
        private readonly int _maxFrameSize;    // samples per channel of the largest decodable packet (120 ms)
        private readonly float[] _frameBuffer; // _maxFrameSize * channels
        private readonly float[] _frameLevels; // one level per frame slot of Clip, aligned with it
        private float[] _writeBuffer;          // scratch holding exactly the samples being written

        private int _writePosition;
        private int _readPosition = -1;
        private bool _disposed;

        /// <summary>
        /// Frames received but not popped yet, keyed by the sender's frame index. A <c>null</c> value is a
        /// slot the sender announced as silence (empty payload).
        /// </summary>
        private readonly Dictionary<int, byte[]> _pending = new();

        /// <summary>Frame index the playout expects next, or -1 while no stream is locked onto.</summary>
        private int _nextIndex = -1;

        /// <summary>Slots concealed in a row since the last real frame.</summary>
        private int _concealed;

        /// <summary>Playout slots elapsed since the last received frame.</summary>
        private int _slotsSinceFrame;

        public OpusAudioReceiver(int sampleRate, Settings settings = null, string clipName = "OpusAudioReceiver") {
            _settings = settings ?? new Settings();

            OpusConstants.ValidateSampleRate(sampleRate);
            OpusConstants.ValidateChannels(_settings.Channels);
            OpusConstants.ValidateFrameDuration(_settings.FrameDurationMs);

            _channels = _settings.Channels;
            _frameSize = OpusConstants.FrameSize(sampleRate, _settings.FrameDurationMs);
            // A single Opus packet may carry up to 120 ms (several frames): size the decode buffer for
            // that maximum so such a packet is decoded instead of being rejected and concealed.
            _maxFrameSize = sampleRate * 120 / 1000;

            int ringBufferSamples = Mathf.Max(_frameSize, Mathf.CeilToInt(sampleRate * _settings.RingBufferSeconds));
            // Round up to a whole number of frames, purely for tidiness (SetData/GetData wrap fine either way).
            ringBufferSamples = (ringBufferSamples + _frameSize - 1) / _frameSize * _frameSize;

            Clip = AudioClip.Create(clipName, ringBufferSamples, _channels, sampleRate, false);
            _frameLevels = new float[ringBufferSamples / _frameSize];

            _decoder = new OpusDecoder.OpusDecoderInstance(sampleRate, _channels);
            _frameBuffer = new float[_maxFrameSize * _channels];
        }

        // ── Reorder buffer ────────────────────────────────────────────────────

        /// <summary>
        /// Hands a frame received from the network to the playout buffer. Frames are ordered by
        /// <paramref name="frameIndex"/> — datagrams arrive out of order, and the same index twice (a
        /// retransmission, or a broadcast to several listeners) is ignored — and an empty payload marks
        /// the slot as silence, so the index stays continuous and a silence never looks like a loss.
        /// <see cref="PopFrame"/> then plays them in the sender's order.
        /// </summary>
        public void ReceiveFrame(int frameIndex, byte[] opusData) {
            if (_disposed) return;

            // An empty payload is a frame the sender announced as silence: it still holds its slot.
            var sample = opusData is { Length: > 0 } ? opusData : null;

            _slotsSinceFrame = 0;

            if (_nextIndex < 0) {
                // First frame of a stream, or of a stream that resumed after a stall: lock onto it.
                _pending.Clear();
                _nextIndex = frameIndex;
            } else if (frameIndex < _nextIndex) {
                // Its slot is already behind the playout: a duplicate, or a packet that arrived too late.
                return;
            } else if (frameIndex - _nextIndex >= _settings.MaxBufferedFrames) {
                // Too far ahead to keep waiting for the frames in between: resync onto this one.
                Logger.LogDebug(
                    $"{nameof(OpusAudioReceiver)} jumped {frameIndex - _nextIndex} frames, resyncing.",
                    tag: nameof(OpusAudioReceiver)
                );
                _pending.Clear();
                _nextIndex = frameIndex;
            }

            _pending[frameIndex] = sample;
        }

        /// <summary>
        /// Writes the frame the sender's timeline expects next into the ring: the buffered one, a concealed
        /// (PLC) frame while the stream is merely late, or real silence when the slot is lost beyond repair
        /// or the stream stopped (so nothing stale keeps looping). Call it once per frame duration.
        /// </summary>
        public void PopFrame() {
            if (_disposed) return;

            // No stream locked (nothing received yet, or it stopped): keep the ring fed with silence.
            if (_nextIndex < 0) {
                ReceiveSilence();
                return;
            }

            _slotsSinceFrame++;

            if (_pending.Remove(_nextIndex, out var packet)) {
                if (packet != null)
                    ReceivePacket(packet);
                else
                    ReceiveSilence();

                _nextIndex++;
                _concealed = 0;
                return;
            }

            if (_slotsSinceFrame * _settings.FrameDurationMs > _settings.ConcealMaxSilenceMs) {
                // The stream stopped: write silence and wait for it to lock onto a new timeline.
                ReceiveSilence();
                _nextIndex = -1;
                _concealed = 0;
                return;
            }

            if (_concealed < _settings.MaxConcealedFrames) {
                // The frame is late or lost: conceal its slot and move on. A packet arriving afterwards is
                // dropped as already played, which keeps the rest of the stream in order.
                ReceiveConcealedFrame();
                _nextIndex++;
                _concealed++;
                return;
            }

            // Hole too deep to conceal: pad it with silence so the playout keeps its pace.
            ReceiveSilence();
            _nextIndex++;
        }

        /// <summary>
        /// Forgets the timeline and everything buffered for it: the next received frame locks a new one.
        /// </summary>
        public void ResetTimeline() {
            _pending.Clear();
            _nextIndex       = -1;
            _concealed       = 0;
            _slotsSinceFrame = 0;
        }

        // ── Ring buffer ───────────────────────────────────────────────────────

        /// <summary>
        /// Decode one Opus packet and write it into the ring buffer, in arrival order. Prefer
        /// <see cref="ReceiveFrame"/> when the sender numbers its frames: this one cannot reorder.
        /// </summary>
        public void ReceivePacket(byte[] opusData) {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusAudioReceiver));
            if (opusData == null || opusData.Length == 0) return;

            int samplesDecoded = _decoder.Decode(opusData, _maxFrameSize, _frameBuffer);
            if (samplesDecoded <= 0) return;

            WriteFrame(samplesDecoded);
        }

        /// <summary>
        /// Writes one concealed (PLC) frame. Use when a frame is missing, so the playout cadence is
        /// kept without leaving stale audio looping.
        /// </summary>
        public void ReceiveConcealedFrame() {
            if (_disposed) return;

            int concealed = _decoder.DecodeLost(_frameSize, _frameBuffer);
            WriteFrame(concealed > 0 ? concealed : _frameSize);
        }

        /// <summary>Writes one frame of silence (the stream is idle: nothing worth concealing).</summary>
        public void ReceiveSilence() {
            if (_disposed) return;

            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            WriteFrame(_frameSize);
        }

        /// <summary>
        /// Writes the first <paramref name="samplesDecoded"/> samples of <see cref="_frameBuffer"/> into
        /// the ring and moves the write head by that same amount. Writing (and advancing by) the
        /// configured frame size instead would duplicate the stale tail of any shorter frame.
        /// </summary>
        private void WriteFrame(int samplesDecoded) {
            if (samplesDecoded <= 0)
                return;

            int sampleCount = samplesDecoded * _channels;
            if (_writeBuffer == null || _writeBuffer.Length != sampleCount)
                _writeBuffer = new float[sampleCount];

            Array.Copy(_frameBuffer, _writeBuffer, sampleCount);
            Clip.SetData(_writeBuffer, _writePosition);

            // Record the level per frame slot so it can be read back at the output's read position.
            _frameLevels[_writePosition / _frameSize % _frameLevels.Length] = ComputeLevel(sampleCount);

            _writePosition = (_writePosition + samplesDecoded) % Clip.samples;
        }

        private float ComputeLevel(int sampleCount) {
            if (sampleCount <= 0)
                return 0f;

            float sumSquares = 0f;
            for (int i = 0; i < sampleCount; i++) {
                float s = _frameBuffer[i];
                sumSquares += s * s;
            }
            float rms = Mathf.Sqrt(sumSquares / sampleCount);
            return Mathf.Clamp01(rms * _settings.LevelGain);
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _decoder?.Dispose();
            _pending.Clear();

            if (Clip != null) {
                UnityEngine.Object.Destroy(Clip);
                Clip = null;
            }
        }
    }
}
