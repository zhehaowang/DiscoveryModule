using System;
using remap.NDNMOG.DiscoveryModule;

using System.Text;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule.Test
{
	public class DiscoveryTest
	{
		public static void Main ()
		{
			List<string> nameList1 = new List<string>()
			{
				"zhehao",
				"zening"
			};

			/*
			List<string> nameList2 = new List<string>()
			{
				"zhehao",
				"zening",
				"mytest"
			};
			*/

			List<int> startingLoc = new List<int> () {
				0, 1, 2, 3,
				4, 5, 6, 7
			};

			// NameDataset ndTest1 = new NameDataset (nameList1);

			// A new player by the name of mytest joins the game at starting loc.
			Instance instance = new Instance (startingLoc, "mytest");
			Octant temp = instance.getOctantByIndex (startingLoc);
			temp.getNameDataset ().debugList ();
		}
	}
}
