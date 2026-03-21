using UnityEngine;

namespace Nox.Microphone {
	public interface IMicrophone {
		public string GetName();

		public int GetPosition();

		public float GetLoudness();

		public AudioClip Start(string by);

		public void Stop(string by);

		public Vector2 GetFrequencies();
	}
}