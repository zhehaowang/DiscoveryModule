using System;

namespace remap.NDNMOG.DiscoveryModule
{
	public class Constants
	{
		public const UInt32 FNVPrime32 = 16777619;
		public const UInt32 FNVOffset32 = 2166136261;
		// digest's length in bytes
		public const int HashLength = 4;

		public const string XorDebugKey = "debug";

		public const string BroadcastPrefix = "/ndn/broadcast/apps/Matryoshka/";
		public const int octOffset = 4;

		public const int octreeLevel = 8;

		public const byte isWhole = 0x31;
		public const byte isPart = 0x30;
		public const byte padding = 0x00;

		public const int rootIndex = -1;
	}
}