namespace Nox.Microphone {
	public interface IMicrophoneAPI {
		public IMicrophone   GetDefault();
		
		public IMicrophone[] GetAll();
		
		public IMicrophone   GetCurrent();

		public IMicrophone Get(string name);
	}
}