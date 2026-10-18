using System.Collections.Generic;

namespace Nox.Audio {
	public interface IMicrophoneAPI {
		public IMicrophone Default { get; }

		public IEnumerable<IMicrophone> Microphones { get; }

		public IMicrophone Current { get; }

		public IMicrophone GetMicrophone(string name);
	}
}