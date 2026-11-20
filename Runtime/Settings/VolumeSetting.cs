using System;
using Nox.CCK.Audio;
using Nox.CCK.Mods.Events;
using Nox.CCK.Settings;
using Nox.CCK.Utils;
using Nox.UI;
using UnityEngine;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Combined volume slider + mute toggle for the current microphone.
	/// Uses the volume.prefab which contains both a range slider and a toggle.
	/// </summary>
	public sealed class VolumeSetting : RangeHandler, IAudioSetting {
		private ToggleField _field;
		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		public override string[] Path
			=> new[] { "audio", "microphone", "volume" };

		public override int Order
			=> 40002;

		override protected GameObject GetPrefab()
			=> Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/volume.prefab");

		public VolumeSetting() {
			SetRange(0f, 2f);
			SetStep(0.001f);

			SetLabelKey($"settings.entry.{string.Join(".", Path)}.label");
			SetValueKey("settings.range.value.percent");

			Main.MicrophoneManager.OnCurrentChanged.AddListener(UpdateCurrent);
			UpdateCurrent(Main.MicrophoneManager.Current);
		}

		public override GameObject GetContent(RectTransform transform, IMenu menu) {
			var go = base.GetContent(transform, menu);

			_field = go.GetOrAddComponent<ToggleField>();

			_field.prefab = Main.CoreAPI.AssetAPI.GetAsset<GameObject>("settings:prefabs/toggle.prefab");
			_field.keepLabel = false;
			_field.Build();
			_field.OnValueChanged.AddListener(OnToggleChanged);

			_events = new[] {
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.mute_changed", OnMicrophoneChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.volume_changed", OnMicrophoneChanged)
			};

			Refresh();

			return go;
		}

		private void OnToggleChanged(bool value) {
			Main.MicrophoneManager.Current.IsMuted = value;
			UpdateMuteType();
		}

		private void OnMicrophoneChanged(EventData context)
			=> Refresh();

		/// <summary>Re-reads the device state into the row (silent: the toggle is not a user input).</summary>
		private void Refresh() {
			_field?.SetValue(Main.MicrophoneManager.Current.IsMuted);
			UpdateMuteType();
		}

		/// <summary>
		/// A muted device shows "muted", and so does a device at volume 0: it captures nothing, which
		/// is what a mute does (<see cref="MicrophoneExtensions.IsSilent"/>).
		/// </summary>
		private void UpdateMuteType() {
			SetTypeKey(Main.MicrophoneManager.Current.IsSilent()
				? "mute.active"
				: "settings.range.type.empty");
		}

		public void Dispose()
			=> Main.MicrophoneManager.OnCurrentChanged.RemoveListener(UpdateCurrent);

		private void UpdateCurrent(Microphone.Microphone arg0) {
			SetValue(Main.MicrophoneManager.Current.Volume, notify: false);
			Refresh();
		}

		override protected void OnValueChanged(float value)
			=> Main.MicrophoneManager.Current.Volume = value;

		override protected void OnDestroy() {
			base.OnDestroy();

			foreach (var subscription in _events)
				Main.CoreAPI?.EventAPI.Unsubscribe(subscription);
			_events = Array.Empty<EventSubscription>();

			_field?.OnValueChanged.RemoveListener(OnToggleChanged);
			_field = null;
		}
	}
}
