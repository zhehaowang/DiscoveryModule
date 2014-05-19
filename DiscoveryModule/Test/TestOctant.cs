using System;
using remap.NDNMOG.DiscoveryModule;
using net.named_data.jndn;

using System.Text;
using System.Collections.Generic;
using net.named_data.jndn.security.identity;
using net.named_data.jndn.security;
using net.named_data.jndn.security.policy;
using net.named_data.jndn.util;
using net.named_data.jndn.tests;

namespace remap.NDNMOG.DiscoveryModule.Test
{
	public class DiscoveryTest
	{
		public static void main ()
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
				4, 5, 6, 3
			};

			List<int> parentLoc = new List<int> () {
				3, 1, 4, 3,
				4, 5, 6
			};

			List<int> anotherLoc = new List<int> () {
				3, 1, 4, 3,
				4, 5, 2, 1
			};

			List<int>[] childLoc = new List<int>[2];

			childLoc [0] = new List<int> () {
				4
			};

			childLoc [1] = new List<int> () {
				1
			};

			//NameDataset ndTest1 = new NameDataset (nameList1);

			// A new player by the name of mytest joins the game at starting loc.
			// test for construct bdcast interest for certain octants

			// gemerate an instance with the name of "mytest", at startingLoc
			Vector3 location = new Vector3 (20, 30, 40);
			Instance instance = new Instance (startingLoc, "ramsay", location);
			Octant oct = instance.getOctantByIndex (startingLoc);
		
			// assume that this instance also knows about two more names in the startingLoc
			oct.addName (nameList1[0]);
			oct.addName (nameList1[1]);

			// assume that this instance also knows about anotherLoc
			bool added = instance.addOctant (anotherLoc);
			Console.WriteLine (added);

			// And let's see what the tree looks like after adding anotherLoc and startingLoc
			// instance.debugTree ();

			// test if addOctant by Index works as it should
			/*
			added = instance.addOctant (parentLoc);
			Console.WriteLine (added);
			*/

			Console.WriteLine ("The names in the starting location of instance: ");
			oct.getNameDataset ().debugList ();

			// try constructing a broadcast interest from the info of startingLoc, which should contain 3 names
			Console.WriteLine ("Constructing interest from starting location: ");
			Interest interest = instance.constructBdcastInterest (Constants.AlephPrefix, startingLoc);
			Console.WriteLine (interest.toUri ());

			// try constructing a broadcast interest from startingLoc and childLoc list, which does not contain any names
			Console.WriteLine ("Constructing interest from starting location and another location: ");
			Interest interest1 = instance.constructBdcastInterest (Constants.AlephPrefix, parentLoc, childLoc);
			Console.WriteLine (interest1.toUri ());

			// assumes that another instance receives the above constructed interest, and tries to decode and decide what to return.
			Instance instance1 = new Instance (startingLoc, "anotherinstance", location);
			// test for constructing data packet for given interest.
			Face face = new Face ("localhost");

			// and 'another instance' is interested in childLoc[1] as well, and 'another instance' thinks there is 'something' in the childLoc[1]
			parentLoc.AddRange (childLoc [1]);
			instance1.addOctant (parentLoc);
			instance1.getOctantByIndex (parentLoc).addName ("something");

			MemoryIdentityStorage identityStorage = new MemoryIdentityStorage ();
			MemoryPrivateKeyStorage privateKeyStorage = new MemoryPrivateKeyStorage ();
			KeyChain keyChain = new KeyChain (new IdentityManager (identityStorage, privateKeyStorage), 
				new SelfVerifyPolicyManager (identityStorage));
			keyChain.setFace (face);

			// Initialize the storage.
			Name keyName = new Name ("/testname/DSK-123");
			Name certificateName = keyName.getSubName (0, keyName.size () - 1).append ("KEY").append (keyName.get (-1)).append ("ID-CERT").append ("0");
			identityStorage.addKey (keyName, KeyType.RSA, new Blob (TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, false));

			privateKeyStorage.setKeyPairForKeyName (keyName, TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, TestPublishAsyncNdnx.DEFAULT_PRIVATE_KEY_DER);

			DiscoveryInterestInterface ii = new DiscoveryInterestInterface (keyChain, certificateName, instance1);

			List<Octant> octants = ii.parseDigest (interest1);
			if (octants.Count != 0) {
				// for debugging and not actual network tranmission, using new Interest() in generateData/parseData won't cause problems
				Data data = ii.constructData (new Interest(), octants);
				DiscoveryDataInterface di = new DiscoveryDataInterface (instance);

				// in this test, because instance is not tracking childLoc[1], though data belonging to the oct is returned, 
				// it does not get shown in debug or used for constructing position interest in later processes.
				di.parseContent (new Interest(), data);
			}

			// Let's initiate discovery, this method starts a new thread which deals with interest expression and event loop
			// Another problem: there is not always an event loop going on, need modification for discovery main loop strategy
			instance.discovery ();

			// instance is interested in its starting location
			instance.trackOctant (instance.getOctantByIndex(startingLoc));

			while (true) {
				System.Threading.Thread.Sleep (5000);
			}
			//InterestInterface.parseDigest (interest1);
		}
	}
}
