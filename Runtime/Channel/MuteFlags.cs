using System;

namespace Nox.Audio.Runtime.Channels {
	[Flags]
	public enum MuteFlags {
		None   = 0,
		Unity  = 1 << 0,
		System = 1 << 1,
	}
}
