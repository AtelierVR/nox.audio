namespace Nox.Audio {
	/// <summary>
	/// Represents a volume-controlled audio channel (e.g., "general", "world", "ui", "player").
	/// Channels can depend on each other via <see cref="Depends"/>, forming a hierarchy
	/// where a child's effective volume is clamped by its parent(s).
	/// </summary>
	public interface IChannelAudio {
		/// <summary>
		/// Unique identifier for this volume channel.
		/// Typical values: "general", "world", "ui", "player".
		/// </summary>
		string Id { get; }

		/// <summary>
		/// Identifiers of parent <see cref="IChannelAudio"/> channels this one depends on.
		/// A child's effective volume is clamped by the minimum volume of its parents.
		/// Example: "world" depends on "general".
		/// </summary>
		string[] Depends { get; }

		/// <summary>
		/// Raw volume level [0, 1] for this channel.
		/// The effective volume may be lower if a parent is muted or has lower volume.
		/// </summary>
		float Volume { get; set; }

		/// <summary>
		/// Whether this channel is explicitly muted.
		/// A channel is effectively muted if it is explicitly muted OR any of its parents is muted.
		/// </summary>
		bool IsMuted { get; set; }
		
		bool IsEffectivelyMuted  { get; }
		
		float EffectiveVolume { get; }
	}
}
