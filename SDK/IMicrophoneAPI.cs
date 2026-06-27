using System.Collections.Generic;

namespace Nox.Audio {
	public interface IMicrophoneAPI {
		public IMicrophone Default { get; }

		public IEnumerable<IMicrophone> All { get; }

		public IMicrophone Current { get; }

		public IMicrophone Get(string name);
	}
}