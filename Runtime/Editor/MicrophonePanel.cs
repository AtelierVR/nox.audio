#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Nox.Audio.Runtime.Microphone;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Editor.Panel;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Nox.Audio.Runtime {
	public class MicrophonePanel : IEditorModInitializer, Nox.Editor.Panel.IPanel {
		internal IEditorModCoreAPI        API;
		internal MicrophonePanelInstance  Instance;

		public void OnInitializeEditor(IEditorModCoreAPI api) => API = api;
		public void OnDisposeEditor() { Instance?.OnDestroy(); API = null; }

		public string[] GetPath()  => new[] { "audio", "microphone" };
		public string   GetLabel() => "Audio/Microphone";

		public IInstance[] GetInstances()
			=> Instance != null ? new IInstance[] { Instance } : Array.Empty<IInstance>();

		public IInstance Instantiate(IWindow window, Dictionary<string, object> data)
			=> Instance = new MicrophonePanelInstance(this, window);
	}

	public class MicrophonePanelInstance : IInstance {
		private readonly MicrophonePanel   _panel;
		private readonly IWindow           _window;
		private          VisualElement     _root;
		private          MicrophoneManager _manager;
		private          ScrollView        _list;
		private          VisualElement     _empty;
		private          float             _lastUpdateTime;
		private const    float             UpdateInterval = 0.1f;

		public MicrophonePanelInstance(MicrophonePanel panel, IWindow window) {
			_panel  = panel;
			_window = window;
		}

		public Nox.Editor.Panel.IPanel GetPanel()  => _panel;
		public IWindow                 GetWindow() => _window;
		public string                  GetTitle()  => "Microphone";

		public IToolOption[] GetOptions() => new IToolOption[] {
			new DefaultToolOption("Refresh", RefreshMicrophones)
		};

		public void OnDestroy() {
			EditorApplication.update -= UpdateMicrophoneInfo;
			_panel.Instance = null;
		}

		public VisualElement GetContent() {
			if (_root != null) return _root;
			_root = _panel.API.AssetAPI.GetAsset<VisualTreeAsset>("microphone-panel.uxml").CloneTree();
			_root.AddToClassList("flex-fill");

			_list  = _root.Q<ScrollView>("list");
			_empty = _root.Q<VisualElement>("empty");

			_manager = Main.MicrophoneManager;

			RefreshMicrophones();
			EditorApplication.update += UpdateMicrophoneInfo;
			return _root;
		}

		private void RefreshMicrophones() {
			_list.Clear();

			var hasMics = _manager != null && _manager.Microphones.Count > 0;
			_empty?.EnableInClassList("hidden", hasMics);
			_list.EnableInClassList("hidden", !hasMics);

			if (!hasMics) return;

			foreach (var mic in _manager.Microphones)
				_list.Add(CreateMicrophoneItem(mic));
		}

		private VisualElement CreateMicrophoneItem(Microphone.Microphone microphone) {
			var container = new GroupBox();
			container.AddToClassList("p-8");
			container.AddToClassList("m-0");
			container.AddToClassList("border-b");

			// Header row: name + badges
			var header = new VisualElement();
			header.AddToClassList("flex-row");
			header.AddToClassList("align-center");
			header.AddToClassList("mb-4");

			var nameLabel = new Label(microphone.Name);
			nameLabel.AddToClassList("text-sm");
			nameLabel.AddToClassList("flex-grow");
			nameLabel.EnableInClassList("text-bold", microphone.IsDefault);

			header.Add(nameLabel);

			if (microphone.IsDefault) {
				var badge = new Label("DEFAULT");
				badge.AddToClassList("badge");
				badge.AddToClassList("badge-warning");
				header.Add(badge);
			}

			if (microphone.IsCurrent) {
				var badge = new Label("CURRENT");
				badge.AddToClassList("badge");
				badge.AddToClassList("badge-success");
				header.Add(badge);
			}

			container.Add(header);

			// Recording status row
			var recordingRow = new VisualElement();
			recordingRow.AddToClassList("flex-row");
			recordingRow.AddToClassList("align-center");
			recordingRow.AddToClassList("mb-4");

			var recLabel = new Label("Status");
			recLabel.AddToClassList("key-label");
			recLabel.AddToClassList("opacity-75");

			var recStatus = new Label();
			recStatus.name = $"recording-{microphone.GetHashCode()}";
			recStatus.AddToClassList("flex-grow");
			UpdateRecordingStatus(recStatus, microphone);

			recordingRow.Add(recLabel);
			recordingRow.Add(recStatus);
			container.Add(recordingRow);

			// Volume row
			var volRow = new VisualElement();
			volRow.AddToClassList("flex-row");
			volRow.AddToClassList("align-center");
			volRow.AddToClassList("mb-4");

			var volLabel = new Label("Volume");
			volLabel.AddToClassList("key-label");
			volLabel.AddToClassList("opacity-75");

			var loudnessBar = new ProgressBar();
			loudnessBar.name = $"loudness-{microphone.GetHashCode()}";
			loudnessBar.AddToClassList("flex-grow");
			UpdateLoudnessBar(loudnessBar, microphone);

			volRow.Add(volLabel);
			volRow.Add(loudnessBar);
			container.Add(volRow);

			// Used-by row
			var usedByRow = new VisualElement();
			usedByRow.AddToClassList("flex-row");
			usedByRow.AddToClassList("align-center");

			var usedByKeyLabel = new Label("Used by");
			usedByKeyLabel.AddToClassList("key-label");
			usedByKeyLabel.AddToClassList("opacity-75");

			var usedByLabel = new Label();
			usedByLabel.name = $"usedby-{microphone.GetHashCode()}";
			usedByLabel.AddToClassList("flex-grow");
			UpdateUsedByLabel(usedByLabel, microphone);

			usedByRow.Add(usedByKeyLabel);
			usedByRow.Add(usedByLabel);
			container.Add(usedByRow);

			// Technical info
			var freqs = microphone.Frequencies;
			var techRow = new VisualElement();
			techRow.AddToClassList("mt-4");
			var techLabel = new Label($"{freqs.x}Hz – {freqs.y}Hz");
			techLabel.AddToClassList("text-xs");
			techLabel.AddToClassList("opacity-50");
			techRow.Add(techLabel);
			container.Add(techRow);

			return container;
		}

		private void UpdateMicrophoneInfo() {
			if (Time.realtimeSinceStartup - _lastUpdateTime < UpdateInterval) return;
			_lastUpdateTime = Time.realtimeSinceStartup;
			if (_manager == null || _list == null) return;

			foreach (var microphone in _manager.Microphones) {
				var recEl = _list.Q<Label>($"recording-{microphone.GetHashCode()}");
				var louEl = _list.Q<ProgressBar>($"loudness-{microphone.GetHashCode()}");
				var useEl = _list.Q<Label>($"usedby-{microphone.GetHashCode()}");
				if (recEl != null)  UpdateRecordingStatus(recEl, microphone);
				if (louEl != null)  UpdateLoudnessBar(louEl, microphone);
				if (useEl != null)  UpdateUsedByLabel(useEl, microphone);
			}
		}

		private static void UpdateRecordingStatus(Label label, Microphone.Microphone mic) {
			var isRecording = mic.IsRecording;
			label.text = isRecording ? "Recording" : "Idle";
			label.EnableInClassList("text-danger", isRecording);
		}

		private static void UpdateLoudnessBar(ProgressBar bar, Microphone.Microphone mic) {
			var v    = mic.Loudness;
			bar.value = v * 100f;
			bar.title = $"{v:P0}";
		}

		private static void UpdateUsedByLabel(Label label, Microphone.Microphone mic) {
			var usedBy = mic.UsedBy;
			if (usedBy == null || usedBy.Length == 0) {
				label.text        = "None";
				label.style.color = StyleKeyword.Null;
			} else {
				label.text        = string.Join(", ", usedBy);
				label.style.color = StyleKeyword.Null;
			}
		}
	}
}
#endif