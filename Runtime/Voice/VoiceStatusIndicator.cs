using System;
using Nox.Audio.Players;
using Nox.CCK.Audio;
using Nox.CCK.Mods.Events;
using UnityEngine;
using UnityEngine.Events;

namespace Nox.Audio.Runtime {
	/// <summary>
	/// Ready-to-wire local voice HUD: self-mute, deafen and "muted by someone" indicators.
	/// <para>
	/// The prefab owns the visuals (desktop overlay, XR panel, wrist UI, ...); this component only
	/// toggles the referenced objects from the audio events, so the same component works for every
	/// controller. It also exposes <see cref="ToggleMute"/> and <see cref="ToggleDeafen"/> for UI
	/// buttons.
	/// </para>
	/// </summary>
	public sealed class VoiceStatusIndicator : MonoBehaviour {
		[Header("Self indicators")]
		[Tooltip("Shown while the local microphone is muted.")]
		public GameObject MuteIndicator;

		[Tooltip("Shown while the local user is deafened.")]
		public GameObject DeafenIndicator;

		[Header("Voice mode indicators")]
		[Tooltip("Shown while whispering.")]
		public GameObject WhisperIndicator;

		[Tooltip("Shown while speaking loud.")]
		public GameObject LoudIndicator;

		[Tooltip("Shown while broadcasting.")]
		public GameObject BroadcastIndicator;

		[Header("Muted-by indicator")]
		[Tooltip("Shown while at least one player muted the local user (private relation).")]
		public GameObject MutedByIndicator;

		[Tooltip("Invoked with the number of players muting us (0 when none).")]
		public UnityEvent<int> OnMutedByChanged;

		/// <summary>Number of players currently muting the local user.</summary>
		public int MutedByCount { get; private set; }

		[Header("Input")]
		[Tooltip("Event emitted to toggle the microphone (already bound to the mute key by nox.desktop).")]
		public string MuteEventName = "mute";

		/// <summary>Id of the voice channel (same as the relay register).</summary>
		private const string VoiceChannelId = "voice";

		private EventSubscription[] _subscriptions = Array.Empty<EventSubscription>();

		private static IAudioAPI Audio
			=> Main.CoreAPI?.ModAPI
				.GetMod("audio")
				?.GetInstance<IAudioAPI>();

		private void OnEnable()
			=> TrySubscribe();

		private void OnDisable()
			=> Unsubscribe();

		private void Update() {
			// The audio mod may initialize after this component (scene order, mod load order).
			if (_subscriptions.Length == 0)
				TrySubscribe();
		}

		/// <summary>Toggles the current microphone mute state.</summary>
		public void ToggleMute()
			=> Main.CoreAPI?.EventAPI.Emit(MuteEventName);

		/// <summary>Toggles the voice channel deafen state.</summary>
		public void ToggleDeafen()
			=> SetDeafened(!IsDeafened);

		/// <summary>Sets the voice channel mute (deafen).</summary>
		public void SetDeafened(bool value) {
			var channel = VoiceChannel;
			if (channel != null)
				channel.IsMuted = value;
		}

		/// <summary>
		/// The voice channel is silent: muted, muted by a parent, or at volume 0 (which is audibly the
		/// same, so the deafen indicator follows the volume too).
		/// </summary>
		private static bool IsDeafened
			=> VoiceChannel?.IsSilent() ?? false;

		private static IChannelAudio VoiceChannel
			=> Audio?.GetChannel(VoiceChannelId);

		private void TrySubscribe() {
			var core = Main.CoreAPI;
			if (core == null || _subscriptions.Length > 0)
				return;

			_subscriptions = new[] {
				core.EventAPI.Subscribe("audio.microphone.mute_changed", OnMicrophoneMuteChanged),
				core.EventAPI.Subscribe("audio.microphone.volume_changed", OnMicrophoneVolumeChanged),
				core.EventAPI.Subscribe("audio.channel.mute_changed", OnChannelMuteChanged),
				core.EventAPI.Subscribe("audio.channel.volume_changed", OnChannelVolumeChanged),
				core.EventAPI.Subscribe("voice.status_changed", HandleStatusChanged),
				core.EventAPI.Subscribe("voice.muted_by_changed", HandleMutedByChanged)
			};

			RefreshMute();
			RefreshDeafen();
		}

		private void Unsubscribe() {
			if (_subscriptions.Length == 0)
				return;

			var core = Main.CoreAPI;
			if (core != null)
				foreach (var subscription in _subscriptions)
					core.EventAPI.Unsubscribe(subscription);

			_subscriptions = Array.Empty<EventSubscription>();
		}

		private void OnMicrophoneMuteChanged(EventData context)
			=> RefreshMute();

		/// <summary>The microphone reached volume 0 (or left it): that is audibly a mute.</summary>
		private void OnMicrophoneVolumeChanged(EventData context)
			=> RefreshMute();

		private void OnChannelMuteChanged(EventData context) {
			if (context.TryGet<IChannelAudio>(0, out var channel) && channel?.Id == VoiceChannelId)
				RefreshDeafen();
		}

		/// <summary>A channel reached volume 0 (or left it): that is audibly a deafen.</summary>
		private void OnChannelVolumeChanged(EventData context)
			=> RefreshDeafen();

		private void HandleStatusChanged(EventData context) {
			if (context.TryGet<bool>(1, out var muted))
				SetActive(MuteIndicator, muted);
			if (context.TryGet<bool>(2, out var deafened))
				SetActive(DeafenIndicator, deafened);
			if (context.TryGet<int>(3, out var speak))
				RefreshSpeakMode((SpeakMode)speak);
		}

		private void HandleMutedByChanged(EventData context) {
			if (!context.TryGet<int>(2, out var count))
				return;

			MutedByCount = count;

			if (MutedByIndicator != null)
				MutedByIndicator.SetActive(count > 0);

			OnMutedByChanged?.Invoke(count);
		}

		private void RefreshMute() {
			var muted = Main.MicrophoneManager?.Current?.IsSilent() ?? false;
			SetActive(MuteIndicator, muted);
		}

		private void RefreshDeafen() {
			var deafened = IsDeafened;
			SetActive(DeafenIndicator, deafened);
		}

		private void RefreshSpeakMode(SpeakMode mode) {
			SetActive(WhisperIndicator, mode == SpeakMode.Whisper);
			SetActive(LoudIndicator, mode == SpeakMode.Loud);
			SetActive(BroadcastIndicator, mode == SpeakMode.Broadcast);
		}

		private static void SetActive(GameObject target, bool active) {
			if (target != null && target.activeSelf != active)
				target.SetActive(active);
		}
	}
}
