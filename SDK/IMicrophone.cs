using UnityEngine;

namespace Nox.Audio {
	public interface IMicrophone {
		public string Name { get; }

		public int Position { get; }

		public float Loudness { get; }

		public AudioClip Start(string by);

		public void Stop(string by);

		public Vector2 Frequencies { get; }

		public bool IsMuted { get; set; }

		public float Volume { get; set; }
		
		public float NoiseSuppression { get; set; }
	}
}