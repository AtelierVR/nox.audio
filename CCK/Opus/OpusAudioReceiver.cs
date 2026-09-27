using System;
using Nox.Audio.Players;
using UnityEngine;

namespace Nox.CCK.Audio.Opus {
    /// <summary>
    /// Decodes incoming Opus packets and writes the resulting PCM into a circular
    /// <see cref="AudioClip"/>, mirroring how Unity's Microphone exposes captured
    /// audio. Loop an <see cref="AudioSource"/> on <see cref="Clip"/> to play it back.
    /// Call <see cref="ReceivePacket"/> whenever a packet arrives from the network,
    /// and <see cref="Tick"/> once per frame to conceal stalls (dropped packets).
    /// </summary>
    public class OpusAudioReceiver : IDisposable, ICapturedAudio {
        public class Settings {
            public int Channels = 1;
            public float FrameDurationMs = OpusConstants.DefaultFrameDurationMs;

            /// <summary>Length of the circular playback buffer, in seconds.</summary>
            public float RingBufferSeconds = 1f;

            /// <summary>Write PLC (concealed) frames when packets stop arriving, instead of letting stale audio loop.</summary>
            public bool ConcealMissingFrames = true;

            /// <summary>Stop concealment (go fully silent) after this long without a real packet.</summary>
            public float SilenceTimeoutMs = 500f;

            /// <summary>
            /// Gain applied to the RMS when computing <see cref="Level"/>. The default normalizes a
            /// full-scale sine (RMS = 1/√2) to 1, so the level follows the amplitude instead of
            /// saturating as soon as the input is loud.
            /// </summary>
            public float LevelGain = 1.41421f;
        }

        public AudioClip Clip { get; private set; }
        public int Position => _writePosition;

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
        private readonly float _frameDurationSeconds;
        private readonly float[] _frameBuffer; // _maxFrameSize * channels
        private readonly float[] _frameLevels; // one level per frame slot of Clip, aligned with it
        private float[] _writeBuffer;          // scratch holding exactly the samples being written

        private int _writePosition;
        private int _readPosition = -1;
        private float _timeSinceLastPacket;
        private bool _hasReceivedAnyPacket;
        private bool _disposed;

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
            _frameDurationSeconds = _settings.FrameDurationMs / 1000f;

            int ringBufferSamples = Mathf.Max(_frameSize, Mathf.CeilToInt(sampleRate * _settings.RingBufferSeconds));
            // Round up to a whole number of frames, purely for tidiness (SetData/GetData wrap fine either way).
            ringBufferSamples = (ringBufferSamples + _frameSize - 1) / _frameSize * _frameSize;

            Clip = AudioClip.Create(clipName, ringBufferSamples, _channels, sampleRate, false);
            _frameLevels = new float[ringBufferSamples / _frameSize];

            _decoder = new OpusDecoder.OpusDecoderInstance(sampleRate, _channels);
            _frameBuffer = new float[_maxFrameSize * _channels];
        }

        /// <summary>
        /// Decode one received Opus packet and write it into the ring buffer.
        /// Call this from your network receive callback.
        /// </summary>
        public void ReceivePacket(byte[] opusData) {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusAudioReceiver));
            if (opusData == null || opusData.Length == 0) return;

            int samplesDecoded = _decoder.Decode(opusData, _maxFrameSize, _frameBuffer);
            if (samplesDecoded <= 0) return;

            WriteFrame(samplesDecoded);
            _hasReceivedAnyPacket = true;
            _timeSinceLastPacket = 0f;
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
        /// Call once per frame (e.g. from MonoBehaviour.Update) so a stalled stream
        /// gets concealed (PLC) instead of the AudioClip looping stale audio forever.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds since the last Tick.</param>
        public void Tick(float deltaTime) {
            if (_disposed || !_hasReceivedAnyPacket) return;

            _timeSinceLastPacket += deltaTime;

            if (_timeSinceLastPacket * 1000f > _settings.SilenceTimeoutMs) return; // fully idle, stop concealing
            if (!_settings.ConcealMissingFrames) return;
            if (_timeSinceLastPacket < _frameDurationSeconds) return; // not due for a frame yet

            int concealed = _decoder.DecodeLost(_frameSize, _frameBuffer);
            if (concealed <= 0) return;

            WriteFrame(concealed);
            _timeSinceLastPacket -= _frameDurationSeconds;
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

            if (Clip != null) {
                UnityEngine.Object.Destroy(Clip);
                Clip = null;
            }
        }
    }
}