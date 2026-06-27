using System;
using System.Collections.Generic;
using System.Linq;
using Nox.Settings;

namespace Nox.Audio.Runtime.Channels {
	/// <summary>
	/// Manages all registered <see cref="IChannelAudio"/> channels.
	/// Handles dependency chains and propagates volume/mute changes.
	/// Dynamically generates volume/mute settings for each registered channel.
	/// </summary>
	public class ChannelManager : IDisposable {
		readonly internal Dictionary<string, (ChannelAudio, IHandler)> Channels = new();

		// ── Predefined channel descriptors ──────────────────────

		/// <summary>
		/// Register a channel by id. Uses predefined depends if the id is known,
		/// otherwise registers with no dependencies.
		/// Generates dynamic volume + mute settings and registers them with the settings API.
		/// </summary>
		public IChannelAudio Register(string id, string[] depends = null) {
			depends ??= Array.Empty<string>();

			if (Channels.TryGetValue(id, out var e)) {
				var existing = e.Item1;
				
				if (depends.Length <= 0)
					return existing;

				var merged = existing.Depends
					.Union(depends)
					.ToArray();
				
				if (merged.Length <= existing.Depends.Length)
					return existing;

				existing.Depends = merged;
				UpdatePriority();

				return existing;
			}

			var setting = Main.SettingAPI;
			var channel = new ChannelAudio(this, id, depends);
			Channels[id] = (
				channel,
				setting.Add(new ChannelSetting(channel))
			);

			UpdatePriority();

			return channel;
		}

		public void UpdatePriority() {
			// Build reverse dependency graph: parent → list of children
			var children = new Dictionary<string, List<string>>();
			var inDegree = new Dictionary<string, int>();

			foreach (var kvp in Channels) {
				var id      = kvp.Key;
				var channel = kvp.Value.Item1;
				children[id] = new List<string>();
				inDegree[id] = channel.Depends.Length;
			}

			// Populate reverse edges
			foreach (var kvp in Channels) {
				var id      = kvp.Key;
				var channel = kvp.Value.Item1;
				foreach (var depId in channel.Depends)
					if (children.ContainsKey(depId))
						children[depId].Add(id);
			}

			// Reset all priorities
			foreach (var kvp in Channels)
				kvp.Value.Item1.Priority = -1;

			// Kahn topological sort: start from roots (no dependencies)
			var queue = new Queue<string>();
			foreach (var kvp in Channels) {
				var id      = kvp.Key;
				var channel = kvp.Value.Item1;
				if (channel.Depends.Length == 0) {
					channel.Priority = 0;
					queue.Enqueue(id);
				}
			}

			while (queue.Count > 0) {
				var currentId = queue.Dequeue();
				var current   = Channels[currentId].Item1;

				if (!children.TryGetValue(currentId, out var childList))
					continue;

				foreach (var childId in childList) {
					var child = Channels[childId].Item1;
					if (current.Priority + 1 > child.Priority)
						child.Priority = current.Priority + 1;

					inDegree[childId]--;
					if (inDegree[childId] == 0)
						queue.Enqueue(childId);
				}
			}

			// Fallback for cycles or unreachable nodes
			foreach (var kvp in Channels)
				if (kvp.Value.Item1.Priority < 0)
					kvp.Value.Item1.Priority = 0;
		}

		public IChannelAudio Get(string id)
			=> Channels.TryGetValue(id, out var existing)
				? existing.Item1
				: null;

		/// <summary>
		/// Request to unregister a channel. Emits <see cref="OnRequestUnregister"/>
		/// so listeners can cancel. If not cancelled, removes the channel and its settings.
		/// </summary>
		public void UnRegister(string id, bool force = false) {
			if (!Channels.TryGetValue(id, out var channel))
				return;

			// Send can_load event to all mods
			var cancel = false;

			if (!force)
				Main.CoreAPI.EventAPI.Emit("audio.channel.remove_requested", channel, new Action<object[]>(Action));

			if (cancel)
				return;

			// Remove dynamic settings
			var settings = Main.SettingAPI;
			settings.Remove(channel.Item2.GetPath());

			Channels.Remove(id);

			UpdatePriority();

			return;

			void Action(object[] obj) {
				if (obj.Length > 0 && obj[0] is false) {
					Main.CoreAPI.LoggerAPI.LogWarning($"A mod blocked removing the channel '{id}'.");
					cancel = true;
				} else
					Main.CoreAPI.LoggerAPI.LogWarning($"A mod allowed removing the channel '{id}'. But is ignored.");
			}
		}


		public void Dispose() {
			foreach (var id in Channels.Keys)
				UnRegister(id, true);
		}
	}
}