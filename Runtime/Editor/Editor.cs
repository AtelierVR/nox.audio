#if UNITY_EDITOR
using Nox.Microphone.Runtime;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Mods.Panels;

namespace Nox.Microphone.Runtime {
	public class Editor : IEditorModInitializer {
		internal static IEditorModCoreAPI CoreAPI;

		private static IEditorPanel     _listPanel;
		private        MicrophonePanel _list;

		public void OnInitializeEditor(IEditorModCoreAPI api) {
			CoreAPI    = api;
			_list      = new MicrophonePanel();
			_listPanel = api.PanelAPI.AddLocalPanel(_list);
		}

		public void OnDisposeEditor() {
			CoreAPI.PanelAPI.RemoveLocalPanel(_listPanel);
			_listPanel = null;
			_list      = null;
			CoreAPI    = null;
		}
	}
}
#endif