using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using net.named_data.jndn;
using net.named_data.jndn.util;
using net.named_data.jndn.transport;
using net.named_data.jndn.security;
using net.named_data.jndn.security.identity;
using net.named_data.jndn.security.policy;
using net.named_data.jndn.tests;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Data interface: for processing the incoming data
	/// </summary>


	/// <summary>
	/// Network interface: for periodic broadcast of sync style message.
	/// Accesses all the octants and their digest fields to form the broadcast interest names
	/// Calls the corresponding functions in InterestInterface and DataInterface
	/// </summary>
	public class Instance
	{
		/// <summary>
		/// The root of the octree which stores all the names a player knows
		/// </summary>
		private Octant root_;
		private string name_;

		// The list of prefix filter strings (octant Indices)
		private Hashtable trackingPrefixes_;

		// The list of octants to express interest towards
		private List<Octant> interestExpressionOctants_;
		//private List<string> interestExpressionOctants_;

		// The thread that handles broadcast interest expression
		private Thread tInterestExpression_;
		// The thread that handles posititon interest expression
		private Thread tPositionInterestExpression_;

		// Keychain for face localhost and default certificate name, instantiated along with instance class
		private KeyChain keyChain_;
		private Name certificateName_;

		// Face for broadcast discovery interest
		// Always using default face_ localhost
		private Face face_;

		// Face for position and action update: is this the right pattern? Need to have another KeyChain as well...
		private Face positionFace_;
		private KeyChain positionKeyChain_;

		// The list that stores other game entities than self
		private List<GameEntity> gameEntities_;

		// The storage of self(game entity)
		private GameEntity selfEntity_;

		/// <summary>
		/// Instance class is supposed to be generated from an initial position of the player,
		/// which is a list of integers; and a string name, which is the name of the player(instance).
		/// </summary>
		/// <param name="index">Index</param>
		/// <param name="name">The name of the player (this instance).</param>
		public Instance (List<int> index, string name, Vector3 location)
		{
			selfEntity_ = new GameEntity (name, EntityType.Player, location);

			gameEntities_ = new List<GameEntity> ();

			// Instantiate location and assign name
			trackingPrefixes_ = new Hashtable ();
			interestExpressionOctants_ = new List<Octant> ();

			if (index.Count != Constants.octreeLevel) {
				Console.WriteLine ("Cannot instantiate from non-leaf location.");
				return;
			}

			// Let's imagine all the octants are derived from a octant index '-1'?
			root_ = new Octant (Constants.rootIndex, false);
			// creates the tree structure using the index as input, 
			// and put the name of self to the nameDataset of leaf.
			int i = 0;
			Octant temp = root_;

			// since it's the instantiation process, all childs are null. So we don't call addOctant to add initial octant according to index
			for (i = 0; i < Constants.octreeLevel - 1; i++) {
				Octant insert = new Octant (index[i], false);
				temp.addChild (insert);
				temp = insert;
			}
			Octant temp1 = new Octant(index[i], true);
			temp1.addName (name);
			temp.addChild (temp1);

			name_ = name;

			// Instantiate the face_, keyChain_ and certificateName_
			face_ = new Face ("localhost");

			MemoryIdentityStorage identityStorage = new MemoryIdentityStorage ();
			MemoryPrivateKeyStorage privateKeyStorage = new MemoryPrivateKeyStorage ();
			keyChain_ = new KeyChain (new IdentityManager (identityStorage, privateKeyStorage), 
				new SelfVerifyPolicyManager (identityStorage));
			keyChain_.setFace (face_);

			// Initiate the postionFace_, positionKeyChain_
			positionFace_ = new Face ("localhost");

			positionKeyChain_ = new KeyChain (new IdentityManager (identityStorage, privateKeyStorage), 
				new SelfVerifyPolicyManager (identityStorage));
			positionKeyChain_.setFace (positionFace_);

			// Initialize the storage.
			Name keyName = new Name ("/testname/DSK-123");
			certificateName_ = keyName.getSubName (0, keyName.size () - 1).append ("KEY").append (keyName.get (-1)).append ("ID-CERT").append ("0");
			identityStorage.addKey (keyName, KeyType.RSA, new Blob (TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, false));

			privateKeyStorage.setKeyPairForKeyName (keyName, TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, TestPublishAsyncNdnx.DEFAULT_PRIVATE_KEY_DER);

			// Register prefix for position related interest of self
			Name positionPrefix = new Name (Constants.AlephPrefix + Constants.PositionPrefix + name);
			PositionInterestInterface positionInterestInterface = new PositionInterestInterface (positionKeyChain_, certificateName_, this);
			positionFace_.registerPrefix (positionPrefix, positionInterestInterface, positionInterestInterface);
		}

		/// <summary>
		/// Add another octant to the octree structure of this instance identified by root.
		/// </summary>
		/// <returns>true, if octant with given indices is added; false, if octant with given indices already exists.</returns>
		/// <param name="index">The octant index list of the octant to be added</param>
		public bool addOctant(List<int> index)
		{
			int i = 0;
			Octant temp = root_;
			bool isLeaf = (index.Count == Constants.octreeLevel);
			for (i = 0; i < index.Count - 1; i++) {
				Octant insert = temp.getChildByIndex (index [i]);
				if (insert == null) {
					insert = new Octant (index [i], false);
					temp.addChild (insert);
					temp = insert;
				} else {
					temp = insert;
				}
			}
			Octant temp1 = temp.getChildByIndex (index [i]);
			if (temp1 == null) {
				temp1 = new Octant (index [i], isLeaf);
				temp.addChild (temp1);
				return true;
			} else {
				return false;
			}
		}

		/// <summary>
		/// Debugs the octree structure of this instance.
		/// </summary>
		public void debugTree()
		{
			// Built-in BFS tree traversal
			// Simple BFS testing passed
			Queue q = new Queue ();
			q.Enqueue (root_);
			Octant temp;

			while (q.Count != 0) {
				temp = (Octant)q.Dequeue ();
				Console.Write (temp.getIndex() + "\t");

				if (!temp.isLeaf ()) {
					temp = temp.leftChild ();
					while (temp != null) {
						q.Enqueue (temp);
						temp = temp.rightSibling ();
					}
				}
			}
			Console.WriteLine ();
			return;
		}

		/// <summary>
		/// Gets child octant of an input octant by input index.
		/// </summary>
		/// <returns>The octant matching the index, or null if there are none that matches.</returns>
		/// <param name="index">Input single index</param> 
		/// <param name="parent">The parent octant from which the octant matching the index is selected.</param>
		public Octant getOctantByIndex(Octant parent, int index)
		{
			Octant temp = parent.leftChild ();
			while (temp != null) {
				if (temp.getIndex () == index) {
					return temp;
				}
				temp = temp.rightSibling ();
			}
			return null;
		}

		/// <summary>
		/// Gets octant by index list.
		/// </summary>
		/// <returns>The octant matching the index, or null if there are none that matches.</returns>
		/// <param name="index">Input array of index</param> 
		public Octant getOctantByIndex(List<int> index)
		{
			int i = 0;
			// index allows the first node to be root(stub) or actual node 
			if (index [0] == -1) {
				i = 1;
			}
			Octant temp = root_;
			for (; i < index.Count; i++) {
				temp = getOctantByIndex (temp, index [i]);
				if (temp == null) {
					return null;
				}
			}
			return temp;
		}

		public Interest constructBdcastInterest(string prefix, Octant oct)
		{
			//ASCII '1' stands for isWholeSet; 0 is appended so that base64 for UInt32 does not need padding
			byte[] isWhole = { Constants.isWhole };
			byte[] padding = { Constants.padding };

			byte[] fullBytes = new byte[Constants.HashLength + isWhole.Length + padding.Length];
			System.Buffer.BlockCopy (isWhole, 0, fullBytes, 0, isWhole.Length);

			if (oct != null) {
				oct.setDigestComponent ();
				byte[] digestBytes = oct.getDigestComponent ().getDigestAsByteArray ();

				System.Buffer.BlockCopy (digestBytes, 0, fullBytes, isWhole.Length, digestBytes.Length);
				System.Buffer.BlockCopy (padding, 0, fullBytes, isWhole.Length + digestBytes.Length, padding.Length);
			} else {
				int i = isWhole.Length;
				for (; i < fullBytes.Length; i++) {
					System.Buffer.BlockCopy (padding, 0, fullBytes, i, padding.Length);
				}
			}
			//All base64 characters are legal for url, but in order for the digest field to be url-safe, the following replace is done
			//an easier but (slightly) longer way to express the digest component is below.
			//char isWhole = "1";
			//string digestStr = isWhole + digest.ToString ("D8");
			string digestStr = Convert.ToBase64String (fullBytes);
			digestStr = digestStr.Replace ('/', '_');
			string indexStr = oct.getListIndexAsString();

			Name name = new Name (prefix + indexStr + digestStr);
			Interest interest = new Interest (name);

			return interest;
		}

		/// <summary>
		/// Constructs the bdcast interest for a certain octant. 
		/// The method first checks the local digest of the octant, append it after "isWhole" byte,
		/// And use Base64 to encode the bytes and get a string to append as the last name component.
		/// </summary>
		/// <returns>Interest constructed</returns>
		/// <param name="oct">The octant indices to express interest towards</param>
		public Interest constructBdcastInterest(string prefix, List<int> index)
		{
			Octant oct = getOctantByIndex (index);
			return constructBdcastInterest (prefix, oct);
		}

		/// <summary>
		/// Constructs the bdcast interest for some childs of a given octant.
		/// The method first checks the local digest of each of the given octants, append them after "isPart" byte.
		/// The format should look like (isPart ChildIndex1 00 Digest1 00 ChildIndex2 00 ChildIndex2 00)
		/// And use Base64 to encode the bytes and get a string to append as the last name component.
		/// </summary>
		/// <returns>The bdcast interest.</returns>
		/// <param name="index">The index of parent octant.</param> 
		/// <param name="childs">The relative 2D index array of childs to the parent octant.</param> 
		public Interest constructBdcastInterest(string prefix, List<int> index, List<int>[] childs)
		{
			// Since neither the padding constant nor the digest length is likely to change, doing it in the way below is ridiculous
			int childNum = childs.Length;
			int i = 0;

			byte[] isWhole = { Constants.isPart };
			byte[] padding = { Constants.padding };

			int childTotalLength = 0;
			for (i = 0; i < childNum; i++) {
				childTotalLength += childs [i].Count;
			}

			int fullBytesLength = isWhole.Length + (int)Constants.HashLength * childNum + (int)2 * padding.Length * childNum + childTotalLength;
			int paddingLength = 0;
			while (((fullBytesLength + paddingLength) * 8) % 6 != 0) {
				paddingLength++;
			}
			fullBytesLength += paddingLength;

			byte[] fullBytes = new byte[fullBytesLength];

			byte[] digestBytes = new byte[Constants.HashLength];
			byte[] childBytes = new byte[childTotalLength];
			Octant oct = new Octant ();

			int j = 0;

			System.Buffer.BlockCopy (isWhole, 0, fullBytes, 0, isWhole.Length);
			int loc = isWhole.Length;

			// Construct the byte array as (isPart ChildIndex1 00 Digest1 00 ChildIndex2 00 ChildIndex2 00)
			for (i = 0; i < childNum; i++) {
				for (j = 0; j < childs [i].Count; j++) {
					childBytes [j] = (byte)childs [i] [j];
				}
				System.Buffer.BlockCopy (childBytes, 0, fullBytes, loc, j);
				loc += j;

				System.Buffer.BlockCopy (padding, 0, fullBytes, loc, padding.Length);
				loc += padding.Length;

				List<int> tempIndex = new List<int>(index);
				tempIndex.AddRange (childs [i]);

				oct = getOctantByIndex (tempIndex);
				if (oct != null) {
					oct.setDigestComponent ();
					digestBytes = oct.getDigestComponent ().getDigestAsByteArray ();
					System.Buffer.BlockCopy (digestBytes, 0, fullBytes, loc, digestBytes.Length);

					loc += digestBytes.Length;
				} else {
					for (j = loc; j < loc + Constants.HashLength; j++) {
						System.Buffer.BlockCopy (padding, 0, fullBytes, j, padding.Length);
					}
					loc += (int)Constants.HashLength;
				}

				System.Buffer.BlockCopy (padding, 0, fullBytes, loc, padding.Length);
				loc += padding.Length;
			}
			// Copy the ending paddings, don't have to do it like this
			for (i = 0; i < paddingLength; i++) {
				System.Buffer.BlockCopy (padding, 0, fullBytes, loc, padding.Length);
				loc++;
			}

			string digestStr = Convert.ToBase64String (fullBytes);
			digestStr = digestStr.Replace ('/', '_');

			string indexStr = CommonUtility.getStringFromList (index);
			Name name = new Name (prefix + indexStr + digestStr);
			Interest interest = new Interest (name);
			return interest;
		}

		/// <summary>
		/// Start tracking an octant if it was not tracked before.
		/// (Expressing interest towards it, and register prefix for receiving interest about its parent indicated by valid level of octants)
		/// </summary>
		/// <returns>The octant.</returns>
		public void trackOctant(Octant oct)
		{
			// TODO: test this method

			List<int> index = oct.getListIndex ();
			Octant temp = oct;
			int i = Constants.octreeLevel - index.Count + 1;
			if (i < 1) {
				Console.WriteLine ("Given octant's indices doesn't fit in valid octree level");
				return;
			}
			for (; i<Constants.validOctreeLevel; i++)
			{
				temp = temp.parent ();
			}

			// register prefix for its parent
			string filterPrefixStr = temp.getListIndexAsString ();
			if (!trackingPrefixes_.Contains (filterPrefixStr)) {
				DiscoveryInterestInterface interestHandle = new DiscoveryInterestInterface (keyChain_, certificateName_, this);

				Name prefix = new Name(Constants.AlephPrefix + filterPrefixStr);
				long id = face_.registerPrefix (prefix, interestHandle, interestHandle);

				trackingPrefixes_.Add(filterPrefixStr, id);
			}

			// add itself to the list of strings to express interest towards;
			// TODO: change to detection of whether aggregation is needed to minimize the amount of interest sent
			// TODO: Test if oct equals(contains) method works
			if (!interestExpressionOctants_.Contains (oct)) {
				interestExpressionOctants_.Add (oct);
			}

			oct.startTracking ();
		}

		/// <summary>
		/// Stop tracking an octant if it's being tracked now.
		/// (Stop expressing interest towards it. Stop registering prefix for its parent, if its parent does not contain any other tracking children)
		/// </summary>
		public void untrackOctant(Octant oct)
		{
			// TODO: test this method
			oct.stopTracking ();

			// stop registering prefix for its parent if this octant's the last tracking children of that parent
			List<int> index = oct.getListIndex ();
			Octant temp = oct;
			int i = Constants.octreeLevel - index.Count + 1;
			if (i < 1) {
				Console.WriteLine ("Given octant's indices doesn't fit in valid octree level");
				return;
			}
			for (; i<Constants.validOctreeLevel; i++)
			{
				temp = temp.parent ();
			}

			string filterPrefixStr = temp.getListIndexAsString ();
			bool containsPrefix = trackingPrefixes_.ContainsKey (filterPrefixStr);
			if (containsPrefix) {
				if (!temp.hasTrackingChildren ()) {
					trackingPrefixes_.Remove (filterPrefixStr);
					face_.removeRegisteredPrefix ((long)trackingPrefixes_[filterPrefixStr]);
				}
			}

			// stop expressing interest for itself
			// TODO: change to detection of whether aggregation is needed to minimize the amount of interest sent
			// TODO: Test if oct equals(contains) method works
			int idx = interestExpressionOctants_.IndexOf (oct);
			if (idx != -1) {
				interestExpressionOctants_.RemoveAt(idx);
			}
		}

		/// <summary>
		/// The function executed by thread. It broadcasts discovery interest periodically.
		/// </summary>
		public void discoveryExpressInterest()
		{
			int i = 0;
			Interest interest = new Interest();
			int sleepSeconds = 0; 

			// Data interface does not need keyChain_ or certificateName_, yet
			DiscoveryDataInterface dataHandle = new DiscoveryDataInterface (this);
			while (true) {
				for (i = 0; i<interestExpressionOctants_.Count; i++)
				{
					// the judgment of isNull may seem unnecessary, but since this is carried out in another thread,
					// and List<Octant>.count's change is usually before data is inserted into List, which means
					// Add method may be half way through (is not atomic), and the list's count is incremented, while content is not inserted.
					// Which will cause a NullReference exception.

					if (interestExpressionOctants_ [i] != null) {
						interest = constructBdcastInterest (Constants.AlephPrefix, interestExpressionOctants_ [i]);

						interest.setMustBeFresh (true);
						interest.setInterestLifetimeMilliseconds (Constants.BroadcastTimeoutMilliSeconds);

						// interesting notes: the override with name as first component times out, the override with default constructed interest as first component does not time out
						long pid = face_.expressInterest (interest, dataHandle, dataHandle);

						Console.WriteLine ("Interest PIT ID: " + pid + " expressed : " + interest.toUri ());

						while (dataHandle.callbackCount_ < 1) {
							//while (true){
							face_.processEvents ();
							System.Threading.Thread.Sleep (10);
							sleepSeconds += 10;
						}
						dataHandle.callbackCount_ = 0;

						// give the peer some time(3s) for it to process the unique names received, 
						// and confirm with those unique names whether they are in my vicinity or not.
						// Or the interest with out-of-date digest gets sent again, and immediately gets the same response
						int interval = Constants.BroadcastIntervalMilliSeconds - sleepSeconds;
						sleepSeconds = 0;
						if (interval > 0) {
							Thread.Sleep (interval);
						}
					}
				}
			}
		}

		/// <summary>
		/// Discovery sends broadcast discovery interest periodically.
		/// </summary>
		public void discovery ()
		{
			try {
				// TODO: Implement which octant to express interest towards; and how to express interest towards it:

				// Thread for expressing interest.
				tInterestExpression_ = new Thread(this.discoveryExpressInterest);
				tInterestExpression_.Start();

				tPositionInterestExpression_ = new Thread(this.positionExpressInterest);
				tPositionInterestExpression_.Start();

				// The main event loop.  
				// Wait to receive one interest for the prefix.

				//while (interestHandle.responseCount_ < 1) {

				//while (true){
					//face_.processEvents ();
				//System.Threading.Thread.Sleep (5000);
				//}
			} catch (Exception e) {
				Console.WriteLine ("exception: " + e.Message + "\nStack trace: " + e.StackTrace);
			}
		}

		public GameEntity getSelfGameEntity()
		{
			return selfEntity_;
		}

		public List<GameEntity> getOtherGameEntities()
		{
			return gameEntities_;
		}

		public GameEntity getGameEntityByName(string name)
		{
			for (int i = 0; i < gameEntities_.Count; i++) {
				if (gameEntities_ [i].getName () == name) {
					return gameEntities_ [i];
				}
			}
			return null;
		}

		/// <summary>
		/// Express position interest with positionFace_ towards given name
		/// </summary>
		/// <param name="name">Name.</param>
		public void positionExpressInterest()
		{
			int count = gameEntities_.Count;
			PositionDataInterface positionDataInterface = new PositionDataInterface (this);

			while (true) {
				count = gameEntities_.Count;
				positionDataInterface.callbackCount_ = 0;
				// count is for the cross-thread reference of gameEntities does not go wrong...
				for (int i = 0; i < count; i++) {
					// It's unnatural that we must do this; should lock gameEntites in Add for cross thread reference
					if (gameEntities_ [i] != null) {
						// Position interest name is assumed to be only the Prefix + EntityName for now
						Name interestName = new Name (Constants.AlephPrefix + Constants.PositionPrefix + gameEntities_ [i].getName ());
						Interest interest = new Interest (interestName);

						// Assuming the lifetime for interest is also the same amount as position data freshness period
						interest.setInterestLifetimeMilliseconds (Constants.PosititonDataFreshnessMilliSeconds);
						interest.setMustBeFresh (true);

						positionFace_.expressInterest (interest, positionDataInterface, positionDataInterface);
					} else {
						// We will be expecting one less data response since interest is not expressed.
						count--;
					}
				}

				while (positionDataInterface.callbackCount_ < count) {
					//while (true){
					positionFace_.processEvents ();
					System.Threading.Thread.Sleep (10);
				}
			}

		}
	}
}