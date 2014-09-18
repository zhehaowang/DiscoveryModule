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
			// Does not log INFO for this test
			if (info != "INFO")
			{
				Console.WriteLine (info + " " + data);
			}
			return true;
		}

		public static void main (string characterName = "default")
		{
			Vector3 location = new Vector3 (6750, 4000, 4800);

			string name = characterName;

			Instance instance = new Instance(CommonUtility.getOctantIndicesFromVector3(location), name, location, null, loggingCallback); 

			instance.discovery ();

			// instance is interested in its starting location
			instance.trackOctant (instance.getOctantByIndex(CommonUtility.getOctantIndicesFromVector3(location)));

			while (true) {
				instance.getSelfGameEntity ().setLocation (location.x_ + (new Random().Next(-5, 5)), location.y_ + (new Random().Next(-5, 5)), location.z_ + (new Random().Next(-5, 5)));
				System.Threading.Thread.Sleep (1000);
			}
			//InterestInterface.parseDigest (interest1);
		}
	}
}

