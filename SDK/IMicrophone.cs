using UnityEngine;

namespace Nox.Audio {
	public interface IMicrophone {
		public string Name { get; }

		public int Position { get; }

		public float Loudness { get; }

		public AudioClip Start(string by);

		public void Stop(string by);

		/// <summary>
		/// Take the next DSP-processed frame on the DSP's own frame grid, or <c>false</c> when none is
		/// pending. Consumers streaming the microphone must use this rather than reading the
		/// recording clip, whose in-place rewrite is not aligned with their own reads.
		/// </summary>
		public bool TryDequeueProcessedFrame(out float[] samples);

		/// <summary>Drops the frames processed before the caller started consuming.</summary>
		public void DiscardPendingFrames();

		public Vector2 Frequencies { get; }

		public bool IsMuted { get; set; }

		public float Volume { get; set; }
		
		public float NoiseSuppression { get; set; }
	}
}