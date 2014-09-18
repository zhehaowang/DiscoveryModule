using System;
using net.named_data.jndn;

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
		public const string BroadcastPrefix = "ndn/broadcast/apps/Matryoshka";

		// use this prefix for this app when peers are all conneceted via aleph.ndn.ucla.edu;
		// so that the data can be directed back.
		public const string AlephPrefix = "ndn/edu/ucla/remap/apps/Matryoshka";

		// use this component when expressing interest towards specific player
		public const string PlayersPrefix = "players";

		// Branch for position update
		public const string PositionPrefix = "position";
		// Branch for infos
		public const string InfoPrefix = "info";
		// Name for rendering info under 'info'
		public const string RenderInfoPrefix = "render";

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
		public const int BroadcastTimeoutMilliSeconds = 2000;
		// Interval for broadcast discovery interests if interest brought back unique names
		public const int BroadcastIntervalMilliSeconds = 2000;

		// Time out value for position update interest
		public const int PositionTimeoutMilliSeconds = 800;
		// Interval for position update if it brought back position data
		public const int PositionIntervalMilliSeconds = 40;

		// Freshness period for broadcast digest data
		public const int DigestDataFreshnessSeconds = 10;
		// Freshness period for each position update. According to the spec, any freshness period less than a second is not supported
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

		// Data version component's offset from the end of data name
		public const int DataVersionOffsetFromEnd = 1;

		// Exclusion filter clear period
		public const int ExclusionClearPeriod = 2000 * PosititonDataFreshnessSeconds;

		public const int MaxSequenceNumber = 1024;
		public const int DefaultSequenceNumber = -1;

		// When local sequence number is ahead of received sequence number by at least 20, the remote sequence number should be reset
		public const int MaxSequenceThreshold = 20;
		// When received sequence number is ahead of local sequence number by at most 3, local should reply with most recent published position
		// Expect this to happen once when startup.
		public const int MinSequenceThreshold = 3;

		// This decides whether additional information should be fetched after an entity is discovered initially
		public const bool FetchAdditionalInfoOnDiscovery = true;
		// The string for default rendering skin
		public const string DefaultRenderString = "yellow";
	}

	public class NamespaceUtils
	{
		/// <summary>
		/// Get the entityName from getName().toUri()
		/// </summary>
		/// <returns>The entity name from URI.</returns>
		/// <param name="name">Name.</param>
		public static string getEntityNameFromName(Name name)
		{
			Name lengthName = new Name (Constants.AlephPrefix);
			// the thing that comes directly after hubPrefix should be "players" + entityName
			string entityName = name.get (lengthName.size() + 1).toEscapedString ();
			return entityName;
		}

		/// <summary>
		/// Gets the sequence number from name.
		/// </summary>
		/// <returns>The sequence from name.</returns>
		public static long getSequenceFromName(Name name)
		{
			Name lengthName = new Name (Constants.AlephPrefix);

			// hubPrefix + players + entityName + position + seq
			if (lengthName.size () + 4 == name.size ()) {
				long sequenceNumber = name.get (-1).toNumber ();
				return sequenceNumber;
			} else {
				return -1;
			}
		}

		/// <summary>
		/// Get the command name from getName().toUri()
		/// </summary>
		/// <returns>The entity name from URI.</returns>
		/// <param name="name">Name.</param>
		public static string getCmdFromName(Name name)
		{
			Name lengthName = new Name (Constants.AlephPrefix);
			// the thing that comes directly after hubPrefix should be "players" + entityName + "info/position"
			string cmdName = name.get (lengthName.size() + 2).toEscapedString ();
			return cmdName;
		}
	}
}