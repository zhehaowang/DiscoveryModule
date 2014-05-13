using System;
using remap.NDNMOG.DiscoveryModule;
using net.named_data.jndn;

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
				3, 1, 4, 3,
				4, 5, 6, 7
			};

			List<int> parentLoc = new List<int> () {
				3, 1, 4, 3,
				4, 5, 6
			};

			List<int>[] childLoc = new List<int>[2];

			childLoc [0] = new List<int> () {
				1
			};

			childLoc [1] = new List<int> () {
				7
			};

			// NameDataset ndTest1 = new NameDataset (nameList1);
			Console.WriteLine ("");

			// A new player by the name of mytest joins the game at starting loc.
			Instance instance = new Instance (startingLoc, "mytest");
			Octant temp = instance.getOctantByIndex (startingLoc);
			Console.WriteLine ("The names in the starting location of instance: ");
			temp.getNameDataset ().debugList ();

			Console.WriteLine ("Constructing interest from starting location: ");
			Interest interest = instance.constructBdcastInterest (startingLoc);
			Console.WriteLine (interest.toUri ());

			Console.WriteLine ("Constructing interest from starting location and another location: ");
			Interest interest1 = instance.constructBdcastInterest (parentLoc, childLoc);
			Console.WriteLine (interest1.toUri ());

			Console.WriteLine ();

			//InterestInterface.parseDigest (interest1);
		}
	}
}
