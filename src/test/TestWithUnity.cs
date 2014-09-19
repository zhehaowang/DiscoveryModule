using System;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule.Test
{
	public class TestWithUnity
	{
		public TestWithUnity ()
		{
		}

		public static bool loggingCallback(string info, string data)
		{
			if (logVerbose_ || info != "INFO")
			{
				Console.WriteLine (info + " " + data);
			}
			return true;
		}

		// Known problems in three-party test: 
		// 1. One instance dropped out in the other two, the other two instances are still in its game, but their positions are updated: 
		// presumably position interest expression thread has already crashed in Unity.
		// 2. Color string did not get rendered for one of the instances for the same run.
		public static void main (string characterName = "default", bool verbose = false, string hubPrefix = "")
		{
			Vector3 location = new Vector3 (6750, 4000, 4800);

			string name = characterName;
			logVerbose_ = verbose;

			Instance instance = new Instance
				(CommonUtility.getOctantIndicesFromVector3(location), 
					name, location, null, loggingCallback, null, 
					null, null, null, "yellow", hubPrefix); 

			instance.discovery ();

			// instance is interested in its starting location
			instance.trackOctant (instance.getOctantByIndex(CommonUtility.getOctantIndicesFromVector3(location)));

			while (true) {
				instance.getSelfGameEntity ().setLocation (location.x_ + (new Random().Next(-5, 5)), location.y_ + (new Random().Next(-5, 5)), location.z_ + (new Random().Next(-5, 5)));
				System.Threading.Thread.Sleep (1000);
			}
			//InterestInterface.parseDigest (interest1);
		}

		private static bool logVerbose_;
	}
}