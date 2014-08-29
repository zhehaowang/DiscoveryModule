using System;

namespace remap.NDNMOG.DiscoveryModule
{
	public class Constants
	{
		// string names digest related variables
		public const UInt32 FNVPrime32 = 16777619;
		public const UInt32 FNVOffset32 = 2166136261;

		public const string XorDebugKey = "debug";

		// digest's length in bytes
		public const int HashLength = 4;

		// BroadcastPrefix is not referenced for now.
		public const string BroadcastPrefix = "/ndn/broadcast/apps/Matryoshka/";

		// use this prefix for this app when peers are all conneceted via aleph.ndn.ucla.edu;
		// so that the data can be directed back.
		public const string AlephPrefix = "/ndn/edu/ucla/remap/apps/Matryoshka/";

		// use this component when expressing interest towards specific player
		public const string PlayersPrefix = "players/";

		// Branch for position update
		public const string PositionPrefix = "/position/";

		// The minimum starting location of octant indices name component in a name, any numbers that can be tryParsed will be interpretted as octant indices
		public const int octOffset = 4;

		// The maximum level of octree
		public const int octreeLevel = 7;
		// interest can only be expressed towards the lowest 2 levels of the octant.
		public const int validOctreeLevel = 2;

		// Several constants for padding in digest component for discovery interest. (Could use string.pad method instead)
		public const byte isWhole = 0x31;
		public const byte isPart = 0x30;
		public const byte padding = 0x00;

		// Time out value for broadcast discovery interest
		public const int BroadcastTimeoutMilliSeconds = 10000;
		// Interval for broadcast discovery interests if interest brought back unique names
		public const int BroadcastIntervalMilliSeconds = 3000;

		// Time out value for position update interest
		public const int PositionTimeoutMilliSeconds = 800;
		// Interval for position update if it brought back position data
		public const int PositionIntervalMilliSeconds = 200;

		// Freshness period for broadcast digest data
		public const int DigestDataFreshnessSeconds = 20;
		// Freshness period for each position update. According to the documentation on ndnd-tlv, any freshness period less than a second is not supported
		public const int PosititonDataFreshnessSeconds = 1;

		// Number of position update (published if queried) per second, please make sure (mod (1000 * PositionDataFreshnessSeconds, PositionIntervalMilliSeconds) == 0)
		public const int PositionUpdatesPerSecond = PosititonDataFreshnessSeconds * 1000 / PositionIntervalMilliSeconds;

		// The default timeout for mutex locks used in instance class
		public const int MutexLockTimeoutMilliSeconds = 1000;

		// The assumed index of root octant in instance's octree structure
		public const int rootIndex = -1;

		// After receiving 10 timeouts in a row, the peer will be considered as dropped and therefore removed from GameEntities list and octant's name list (if exists)
		public const int DropTimeoutCount = 20;

		// Whether setPoCallback in gameEntity class is enabed in that class's setPosition function
		public const bool InvokeSetPosCallback = true;

		// Default location for newly initiated entities
		public const float DefaultLocationNewEntity = -1;

		// Drop code for dropped entities
		public const float DefaultLocationDropEntity = -2;

		// Entity name component's offset from the end of position interest name
		public const int EntityNameOffsetFromEnd = 3;

		// Data version component's offset from the end of data name
		public const int DataVersionOffsetFromEnd = 1;

		// Exclusion filter clear period
		public const int ExclusionClearPeriod = 2000 * PosititonDataFreshnessSeconds;

		public const int MaxSequenceNumber = 256;
		public const int DefaultSequenceNumber = -1;

		// When local sequence number is ahead of received sequence number by at least 30, the remote sequence number should be reset
		public const int MaxSequenceThreshold = 30;
		// When received sequence number is ahead of local sequence number by at most 3, local should reply with most recent published position
		// Expect this to happen once when startup.
		public const int MinSequenceThreshold = 3;
	}
}