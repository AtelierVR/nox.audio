#if UNITY_EDITOR
using Nox.Audio.Runtime;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;

namespace Nox.Audio.Runtime {
	public class Editor : IEditorModInitializer {
		internal static IEditorModCoreAPI CoreAPI;

		public void OnInitializeEditor(IEditorModCoreAPI api) => CoreAPI = api;
		public void OnDisposeEditor()                         => CoreAPI = null;
	}
}
#endif