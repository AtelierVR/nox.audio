using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.Audio.Players;
using Nox.CCK.Audio;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.UI;
using UnityEngine;

namespace Nox.Audio.Runtime {
	public class Client : IClientModInitializer {
		private const string PART_KEY = "specials";
		private const string MUTE_KEY = "mute";
		private const string NEXT_SPEAK_EVENT = "voice.speak_mode.next";

		private static readonly Dictionary<SpeakMode, Color> Tints = new() {
			{ SpeakMode.Whisper, new Color(0.250f, 0.550f, 0.850f, 1f) },
			{ SpeakMode.Loud, new Color(0.950f, 0.520f, 0.100f, 1f) },
			{ SpeakMode.Broadcast, new Color(0.850f, 0.200f, 0.250f, 1f) },
		};

		private IClientModCoreAPI _core;
		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		private readonly Dictionary<int, IElement> _elements = new();
		private readonly HashSet<int> _ready = new();

		private SpeakMode _mode = SpeakMode.Normal;
		private bool _muted;

		private IUiAPI Ui
			=> _core?.ModAPI?.GetMod("ui")?.GetInstance<IUiAPI>();

		private IMicrophoneAPI Microphones
			=> _core?.ModAPI?.GetMod("audio")?.GetInstance<IMicrophoneAPI>();

		public void OnInitializeClient(IClientModCoreAPI api) {
			_core = api;

			_events = new[] {
				api.EventAPI.Subscribe("menu_goto", OnMenuGoto),
				api.EventAPI.Subscribe("menu_removed", OnMenuRemoved),
				api.EventAPI.Subscribe("voice.status_changed", OnVoiceStatus),
				api.EventAPI.Subscribe("voice.speak_mode_changed", OnSpeakModeChanged),
				api.EventAPI.Subscribe("audio.microphone.current_changed", OnMicrophoneChanged),
				api.EventAPI.Subscribe("audio.microphone.mute_changed", OnMicrophoneChanged),
				api.EventAPI.Subscribe("audio.microphone.volume_changed", OnMicrophoneChanged),
			};

			RefreshMuted();
		}

		public void OnDisposeClient() {
			if (_core != null)
				foreach (var subscription in _events)
					_core.EventAPI.Unsubscribe(subscription);

			_events = Array.Empty<EventSubscription>();
			_elements.Clear();
			_ready.Clear();
			_core = null;
		}

		private void OnMenuGoto(EventData context) {
			if (!context.TryGet(0, out int menuId))
				return;

			var menu = Ui?.Get<IMenu>(menuId);
			if (menu == null || !_ready.Add(menuId))
				return;

			EnsureMuteAsync(menu).Forget();
		}

		private void OnMenuRemoved(EventData context) {
			if (!context.TryGet(0, out IMenu menu) || menu == null)
				return;

			_elements.Remove(menu.Id);
			_ready.Remove(menu.Id);
		}

		private async UniTaskVoid EnsureMuteAsync(IMenu menu) {
			var part = menu.GetPart(PART_KEY);
			if (part == null)
				return;

			await part.AddElement(new NavigationData {
				Key                    = MUTE_KEY,
				Action                 = MUTE_KEY,
				DoubleAction           = NEXT_SPEAK_EVENT,
				Flags                  = NavigationFlags.Button,
				ExecutionArguments     = Array.Empty<object>(),
				DoubleExecutionArguments = Array.Empty<object>(),
				executionType          = NavigationExecution.Event,
				doubleExecutionType    = NavigationExecution.Event,
			});

			var element = part.GetElement(MUTE_KEY);
			if (element == null)
				return;

			_elements[menu.Id] = element;
			Refresh(element);
		}

		private void OnVoiceStatus(EventData context) {
			if (context.TryGet(1, out bool muted))
				_muted = muted;

			if (context.TryGet(3, out int speak) && speak != (int)SpeakMode.Muted)
				_mode = (SpeakMode)speak;

			RefreshAll();
		}

		private void OnSpeakModeChanged(EventData context) {
			if (!context.TryGet(1, out SpeakMode mode) && !context.TryGet(0, out mode))
				return;

			_mode = mode;
			RefreshAll();
		}

		private void OnMicrophoneChanged(EventData context) {
			RefreshMuted();
			RefreshAll();
		}

		private void RefreshMuted() {
			var microphone = Microphones?.Current ?? Microphones?.Default;
			if (microphone != null)
				_muted = microphone.IsSilent();
		}

		private void RefreshAll() {
			foreach (var element in _elements.Values)
				Refresh(element);
		}

		private void Refresh(IElement element) {
			var muted = _muted || _mode == SpeakMode.Muted;
			var icon  = (ResourceIdentifier)$"ui:icons/audio/{IconOf(_mode, muted)}.png";

			if (element.GetData() is { } data)
				data.Icon = icon;

			element.SetIcon(icon);

			if (Tints.TryGetValue(_mode, out var tint))
				element.SetTint(tint);
			else
				element.ClearTint();
		}

		private static string IconOf(SpeakMode mode, bool muted)
			=> muted
				? "mute"
				: mode is >= SpeakMode.Whisper and <= SpeakMode.Broadcast && mode != SpeakMode.Normal
					? mode.ToString().ToSnakeCase()
					: "normal";
	}
}
