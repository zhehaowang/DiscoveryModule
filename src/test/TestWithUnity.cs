using System;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule.Test
{
	public class TestWithUnity
	{
		public TestWithUnity ()
		{
		}

		public static void main (string characterName = "default")
		{
			Vector3 location = new Vector3 (6750, 3550, 4800);

			string name = characterName;

			Instance instance = new Instance(CommonUtility.getOctantIndicesFromVector3(location), name, location, null, null); 

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

