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

		// BroadcastPrefix is not referenced for now.
		public const string BroadcastPrefix = "/ndn/broadcast/apps/Matryoshka/";
		// use this prefix when peers are all conneceted via aleph.ndn.ucla.edu;
		// so that the data can be directed back.
		public const string AlephPrefix = "/ndn/edu/ucla/remap/apps/Matryoshka/";

		// Branch for position update
		public const string PositionPrefix = "position/";

		public const int octOffset = 4;

		public const int octreeLevel = 8;
		// interest can only be expressed towards the lowest 2 levels of the octant.
		public const int validOctreeLevel = 2;

		public const byte isWhole = 0x31;
		public const byte isPart = 0x30;
		public const byte padding = 0x00;

		// Interval for broadcasting discovery interests if interest times out
		public const int BroadcastTimeoutMilliSeconds = 10000;
		// Interval for broadcasting discovery interests if interest brought back unique names
		public const int BroadcastIntervalMilliSeconds = 3000;

		// Freshness period for broadcast digest data
		public const int DigestDataFreshnessSeconds = 20;
		// Freshness period for postion update data
		public const int PosititonDataFreshnessMilliSeconds = 200;

		public const int rootIndex = -1;
	}
}