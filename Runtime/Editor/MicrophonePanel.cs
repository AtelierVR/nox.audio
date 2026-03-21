#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Nox.CCK.Mods.Panels;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Nox.Microphone.Runtime {
	public class MicrophonePanel : IEditorPanelBuilder, IDisposable {
		public string GetId()
			=> "microphone";

		public string GetName()
			=> "Audio/Microphone";

		public bool IsHidden()
			=> false;

		private readonly VisualElement     _root = new();
		private          MicrophoneManager _microphoneManager;
		private          ScrollView        _microphoneList;
		private          Label             _noMicrophonesLabel;
		private          float             _lastUpdateTime;
		private const    float             UpdateInterval = 0.1f; // Mise à jour toutes les 100ms

		public VisualElement Make(Dictionary<string, object> data) {
			_root.Clear();
			var root = new VisualElement();

			// Style du panneau principal
			root.style.paddingTop = root.style.paddingBottom =
				root.style.paddingLeft = root.style.paddingRight = 10;
			root.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f));

			// Titre du panneau
			var title = new Label("Liste des Microphones");
			title.style.fontSize                = 16;
			title.style.unityFontStyleAndWeight = FontStyle.Bold;
			title.style.marginBottom            = 10;
			title.style.color                   = Color.white;
			root.Add(title);

			// Bouton de rafraîchissement
			var refreshButton = new Button(RefreshMicrophones) { text = "Rafraîchir" };
			refreshButton.style.marginBottom = 10;
			root.Add(refreshButton);

			// Zone de défilement pour la liste des microphones
			_microphoneList                 = new ScrollView();
			_microphoneList.style.flexGrow  = 1;
			_microphoneList.style.maxHeight = 400;
			root.Add(_microphoneList);

			// Label pour "aucun microphone"
			_noMicrophonesLabel               = new Label("Aucun microphone détecté");
			_noMicrophonesLabel.style.color   = Color.gray;
			_noMicrophonesLabel.style.display = DisplayStyle.None;
			root.Add(_noMicrophonesLabel);

			// Initialisation
			InitializeMicrophoneManager();
			RefreshMicrophones();

			// Mise à jour périodique
			EditorApplication.update += UpdateMicrophoneInfo;

			_root.Add(root);
			return _root;
		}

		private void InitializeMicrophoneManager() {
			try {
				// Récupérer le MicrophoneManager depuis l'instance principale
				if (Main.Instance != null) {
					_microphoneManager = Main.Instance.Manager;
				}
			} catch (Exception ex) {
				Debug.LogError($"Erreur lors de l'initialisation du MicrophoneManager: {ex.Message}");
			}
		}

		private void RefreshMicrophones() {
			_microphoneList.Clear();

			if (_microphoneManager == null || _microphoneManager.Microphones.Count == 0) {
				_noMicrophonesLabel.style.display = DisplayStyle.Flex;
				return;
			}

			_noMicrophonesLabel.style.display = DisplayStyle.None;

			foreach (var microphone in _microphoneManager.Microphones) {
				CreateMicrophoneItem(microphone);
			}
		}

		private void CreateMicrophoneItem(Microphone microphone) {
			var container = new VisualElement();
			container.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f));
			container.style.borderTopWidth = container.style.borderBottomWidth =
				container.style.borderLeftWidth = container.style.borderRightWidth = 1;
			container.style.borderTopColor = container.style.borderBottomColor =
				container.style.borderLeftColor = container.style.borderRightColor = Color.gray;
			container.style.paddingTop = container.style.paddingBottom =
				container.style.paddingLeft = container.style.paddingRight = 10;
			container.style.marginBottom = 5;

			// Ligne 1: Nom et statut par défaut
			var headerRow = new VisualElement();
			headerRow.style.flexDirection  = FlexDirection.Row;
			headerRow.style.justifyContent = Justify.SpaceBetween;
			headerRow.style.alignItems     = Align.Center;

			var nameLabel = new Label(microphone.GetName());
			nameLabel.style.fontSize                = 14;
			nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			nameLabel.style.color                   = microphone.IsDefault() ? Color.yellow : Color.white;

			var statusContainer = new VisualElement();
			statusContainer.style.flexDirection = FlexDirection.Row;

			if (microphone.IsDefault()) {
				var defaultBadge = new Label("DÉFAUT");
				defaultBadge.style.backgroundColor = Color.yellow;
				defaultBadge.style.color           = Color.black;
				defaultBadge.style.paddingTop = defaultBadge.style.paddingBottom =
					defaultBadge.style.paddingLeft = defaultBadge.style.paddingRight = 2;
				defaultBadge.style.fontSize    = 10;
				defaultBadge.style.marginRight = 5;
				statusContainer.Add(defaultBadge);
			}

			if (_microphoneManager.CurrentMicrophone == microphone) {
				var currentBadge = new Label("ACTUEL");
				currentBadge.style.backgroundColor = Color.green;
				currentBadge.style.color           = Color.white;
				currentBadge.style.paddingTop = currentBadge.style.paddingBottom =
					currentBadge.style.paddingLeft = currentBadge.style.paddingRight = 2;
				currentBadge.style.fontSize = 10;
				statusContainer.Add(currentBadge);
			}

			headerRow.Add(nameLabel);
			headerRow.Add(statusContainer);
			container.Add(headerRow);

			// Ligne 2: Statut d'enregistrement et volume
			var infoRow = new VisualElement();
			infoRow.style.flexDirection  = FlexDirection.Row;
			infoRow.style.justifyContent = Justify.SpaceBetween;
			infoRow.style.marginTop      = 5;

			var recordingStatus = new Label();
			recordingStatus.name = $"recording-{microphone.GetIndex()}";
			UpdateRecordingStatus(recordingStatus, microphone);

			var loudnessContainer = new VisualElement();
			loudnessContainer.style.flexDirection = FlexDirection.Row;
			loudnessContainer.style.alignItems    = Align.Center;

			var loudnessLabel = new Label("Volume:");
			loudnessLabel.style.marginRight = 5;
			loudnessLabel.style.color       = Color.white;

			var loudnessBar = new ProgressBar();
			loudnessBar.name         = $"loudness-{microphone.GetIndex()}";
			loudnessBar.style.width  = 100;
			loudnessBar.style.height = 15;
			UpdateLoudnessBar(loudnessBar, microphone);

			loudnessContainer.Add(loudnessLabel);
			loudnessContainer.Add(loudnessBar);

			infoRow.Add(recordingStatus);
			infoRow.Add(loudnessContainer);
			container.Add(infoRow);

			// Ligne 3: Utilisé par
			var usedByRow = new VisualElement();
			usedByRow.style.marginTop = 5;

			var usedByLabel = new Label();
			usedByLabel.name = $"usedby-{microphone.GetIndex()}";
			UpdateUsedByLabel(usedByLabel, microphone);

			usedByRow.Add(usedByLabel);
			container.Add(usedByRow);

			// Ligne 4: Informations techniques
			var techInfoRow = new VisualElement();
			techInfoRow.style.marginTop = 5;
			techInfoRow.style.fontSize  = 11;
			techInfoRow.style.color     = Color.gray;

			var frequencies = microphone.GetFrequencies();
			var techInfo    = new Label($"Index: {microphone.GetIndex()} | Fréquences: {frequencies.x}Hz - {frequencies.y}Hz");
			techInfoRow.Add(techInfo);
			container.Add(techInfoRow);

			_microphoneList.Add(container);
		}

		private void UpdateMicrophoneInfo() {
			if (Time.realtimeSinceStartup - _lastUpdateTime < UpdateInterval) return;
			_lastUpdateTime = Time.realtimeSinceStartup;

			if (_microphoneManager == null) return;

			foreach (var microphone in _microphoneManager.Microphones) {
				var recordingStatusElement = _microphoneList.Q<Label>($"recording-{microphone.GetIndex()}");
				if (recordingStatusElement != null) {
					UpdateRecordingStatus(recordingStatusElement, microphone);
				}

				var loudnessBarElement = _microphoneList.Q<ProgressBar>($"loudness-{microphone.GetIndex()}");
				if (loudnessBarElement != null) {
					UpdateLoudnessBar(loudnessBarElement, microphone);
				}

				var usedByElement = _microphoneList.Q<Label>($"usedby-{microphone.GetIndex()}");
				if (usedByElement != null) {
					UpdateUsedByLabel(usedByElement, microphone);
				}
			}
		}

		private void UpdateRecordingStatus(Label label, Microphone microphone) {
			if (microphone.IsRecording()) {
				label.text        = "🔴 ENREGISTREMENT";
				label.style.color = Color.red;
			} else {
				label.text        = "⚫ ARRÊTÉ";
				label.style.color = Color.gray;
			}
		}

		private void UpdateLoudnessBar(ProgressBar bar, Microphone microphone) {
			var loudness = microphone.GetLoudness();
			bar.value = loudness * 100f;
			bar.title = $"{loudness:P0}";

			// Couleur de la barre basée sur le niveau
			if (loudness > 0.7f) {
				bar.style.backgroundColor = Color.red;
			} else if (loudness > 0.4f) {
				bar.style.backgroundColor = Color.yellow;
			} else {
				bar.style.backgroundColor = Color.green;
			}
		}

		private void UpdateUsedByLabel(Label label, Microphone microphone) {
			var usedBy = microphone.UsedBy();
			if (usedBy == null || usedBy.Length == 0) {
				label.text        = "Utilisé par: Aucun";
				label.style.color = Color.gray;
			} else {
				label.text        = $"Utilisé par: {string.Join(", ", usedBy)}";
				label.style.color = Color.cyan;
			}
		}

		public void Dispose() {
			EditorApplication.update -= UpdateMicrophoneInfo;
			_root?.Clear();
		}
	}
}
#endif