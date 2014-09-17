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
	public delegate bool LoggingCallback(string type, string info);

	/// <summary>
	/// Instance: for periodic broadcast of sync style message.
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

		// This list can be modified by trackOctant thread, and referenced by discovery interest expression thread, so we need a mutex lock for it
		//private Mutex interestExpressionOctantsLock_;

		// The thread that handles broadcast interest expression
		private Thread tInterestExpression_;
		// The thread that handles posititon interest expression
		private Thread tPositionInterestExpression_;
		// The thread that publishes the location of the local game entity.
		private Thread publisherThread_;

		// Keychain for face localhost and default certificate name, instantiated along with instance class
		private KeyChain keyChain_;
		private Name certificateName_;

		// Face for broadcast discovery interest
		// Always using default face_ localhost
		private Face positionFace_;
		private Face broadcastFace_;

		// The list that stores other game entities than self
		private List<GameEntity> gameEntities_;

		// This list can be modified by onDiscoveryData callback thread, and referenced by position interest expression thread, so we need a mutex lock for it
		//private Mutex gameEntitiesLock_;

		// The storage of self(game entity)
		private GameEntity selfEntity_;
		// The storage of the info of self entity, which is only used for responding to info interests such as rendering
		private GameEntityInfo selfEntityInfo_;

		// The callback given by Unity to set position in Unity
		private SetPosCallback setPosCallback_;
		// The callback given by Unity to log into specific files
		private LoggingCallback loggingCallback_;
		// The callback given by Unity to set info of entity such as rendering info
		private InfoCallback infoCallback_;

		// selfOctant is the octant this instance's avatar's belonging to.
		private List<int> selfOctant_;

		/// <summary>
		/// Instance class is supposed to be generated from an initial position of the player,
		/// which is a list of integers; and a string name, which is the name of the player(instance).
		/// </summary>
		/// <param name="index">Index</param>
		/// <param name="name">The name of the player (this instance).</param>
		public Instance 
		(List<int> index, string name, Vector3 location, SetPosCallback setPosCallback, LoggingCallback loggingCallback, InfoCallback infoCallback = null, Face face = null, KeyChain keyChain = null, Name certificateName = null, string renderString = "")
		{
			selfEntity_ = new GameEntity (name, EntityType.Player, location);
			if (renderString == "") {
				renderString = Constants.DefaultRenderString;
			}
			selfEntityInfo_ = new GameEntityInfo (name, renderString);

			setPosCallback_ = setPosCallback;
			loggingCallback_ = loggingCallback;
			infoCallback_ = infoCallback;

			gameEntities_ = new List<GameEntity> ();

			// Instantiate location and assign name
			trackingPrefixes_ = new Hashtable ();
			interestExpressionOctants_ = new List<Octant> ();

			if (index.Count != Constants.octreeLevel) {
				loggingCallback_ ("ERROR", "Initialization: Cannot instantiate from non-leaf location.");
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

			selfOctant_ = CommonUtility.getOctantIndicesFromVector3(location);

			name_ = name;

			// Instantiate the face_, keyChain_ and certificateName_

			positionFace_ = new Face ("localhost");

			MemoryIdentityStorage identityStorage = new MemoryIdentityStorage ();
			MemoryPrivateKeyStorage privateKeyStorage = new MemoryPrivateKeyStorage ();
			keyChain_ = new KeyChain (new IdentityManager (identityStorage, privateKeyStorage), 
				new SelfVerifyPolicyManager (identityStorage));
			keyChain_.setFace (positionFace_);

			// Initialize the storage.
			Name keyName = new Name ("/testname/DSK-123");
			certificateName_ = keyName.getSubName (0, keyName.size () - 1).append ("KEY").append (keyName.get (-1)).append ("ID-CERT").append ("0");
			identityStorage.addKey (keyName, KeyType.RSA, new Blob (TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, false));

			privateKeyStorage.setKeyPairForKeyName (keyName, TestPublishAsyncNdnx.DEFAULT_PUBLIC_KEY_DER, TestPublishAsyncNdnx.DEFAULT_PRIVATE_KEY_DER);

			// Allow prefix registration for nfd
			positionFace_.setCommandSigningInfo (keyChain_, certificateName_);

			Name localPrefix = new Name (Constants.AlephPrefix).append(Constants.PlayersPrefix).append(name);
			PositionInterestInterface positionInterestInterface = new PositionInterestInterface (keyChain_, certificateName_, this, loggingCallback_);
			positionFace_.registerPrefix (localPrefix, positionInterestInterface, positionInterestInterface);

			broadcastFace_ = new Face ("localhost");
			broadcastFace_.setCommandSigningInfo (keyChain_, certificateName_);

			//interestExpressionOctantsLock_ = new Mutex ();
			//gameEntitiesLock_ = new Mutex ();
		}

		/// <summary>
		/// Add another octant to the octree structure of this instance identified by root.
		/// </summary>
		/// <returns>true, if octant with given indices is added; false, if octant with given indices already exists.</returns>
		/// <param name="index">The octant index list of the octant to be added</param>
		public Octant addOctant(List<int> index)
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
				return temp1;
			} else {
				return null;
			}
		}

		/// <summary>
		/// Debugs the octree structure of this instance using BFS traversal
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

		/// <summary>
		/// Constructs the broadcast discovery interest for a certain octant indicated by given octant. 
		/// </summary>
		/// <returns>The bdcast interest.</returns>
		/// <param name="prefix">The prefix the octant indices are appended to.</param>
		/// <param name="oct">The octant to construct broadcast interest for.</param>
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

			Name name = new Name (prefix + indexStr).append(digestStr);
			Interest interest = new Interest (name);

			return interest;
		}

		/// <summary>
		/// Constructs the broadcast discovery interest for a certain octant indicated by given indices. 
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
			List<int> index = oct.getListIndex ();
			Octant temp = oct;
			int i = Constants.octreeLevel - index.Count + 1;
			if (i < 1) {
				loggingCallback_ ("ERROR", "trackOctant: Given octant's indices doesn't fit in valid octree level");
				return;
			}
			for (; i<Constants.validOctreeLevel; i++)
			{
				temp = temp.parent ();
			}

			// register prefix for its parent
			string filterPrefixStr = temp.getListIndexAsString ();
			if (!trackingPrefixes_.Contains (filterPrefixStr)) {
				DiscoveryInterestInterface interestHandle = new DiscoveryInterestInterface (keyChain_, certificateName_, this, loggingCallback_);

				Name prefix = new Name(Constants.BroadcastPrefix + filterPrefixStr);
				long id = broadcastFace_.registerPrefix (prefix, interestHandle, interestHandle);

				trackingPrefixes_.Add(filterPrefixStr, id);
			}

			// add itself to the list of strings to express interest towards;
			// TODO: change to detection of whether aggregation is needed to minimize the amount of interest sent
			if (!interestExpressionOctants_.Contains (oct)) {
				//interestExpressionOctantsLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
				interestExpressionOctants_.Add (oct);
				//interestExpressionOctantsLock_.ReleaseMutex ();
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
				loggingCallback_ ("ERROR", "untrackOctant: Given octant's indices doesn't fit in valid octree level");
				return;
			}
			for (; i < Constants.validOctreeLevel; i++) {
				temp = temp.parent ();
			}

			string filterPrefixStr = temp.getListIndexAsString ();
			bool containsPrefix = trackingPrefixes_.ContainsKey (filterPrefixStr);
			if (containsPrefix) {
				if (!temp.hasTrackingChildren ()) {
					trackingPrefixes_.Remove (filterPrefixStr);
					broadcastFace_.removeRegisteredPrefix ((long)trackingPrefixes_[filterPrefixStr]);
				}
			}

			// stop expressing broadcast interest for that octant
			// TODO: change to detection of whether aggregation is needed to minimize the amount of interest sent
			int idx = interestExpressionOctants_.IndexOf (oct);
			if (idx != -1) {
				//interestExpressionOctantsLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
				interestExpressionOctants_.RemoveAt(idx);
				//interestExpressionOctantsLock_.ReleaseMutex ();
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
			int count = 0;

			int responseCount = 0;
			// Data interface does not need keyChain_ or certificateName_, yet
			DiscoveryDataInterface dataHandle = new DiscoveryDataInterface (this, loggingCallback_);

			try 
			{
				while (true) {
					// TODO: Implement which octant to express interest towards: Could be done in another thread which constants receives the location from Unity instance
					// 		using MutexLock when modifying interestExpressionOctants_. Could be more closely coupled with Unity instance.
					// TODO: Check if there's moments main event loop will not be running or fail to get out?

					// It might be a better idea to copy the octant list and act based on that copy, so that we don't have to lock up the whole for loop
					//interestExpressionOctantsLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
					List<Octant> copyOctantsList = new List<Octant> (interestExpressionOctants_);
					//interestExpressionOctantsLock_.ReleaseMutex();

					//loggingCallback_ ("INFO", "working");

					count = copyOctantsList.Count;
					responseCount = count;

					for (i = 0; i<count; i++)
					{
						// the judgment of isNull may seem unnecessary, but since this is carried out in another thread,
						// and List<Octant>.count's change is usually before data is inserted into List, which means
						// Add method may be half way through (is not atomic), and the list's count is incremented, while content is not inserted.
						// Which will cause a NullReference exception.

						if (copyOctantsList [i] != null) {
							interest = constructBdcastInterest (Constants.BroadcastPrefix, copyOctantsList [i]);

							interest.setMustBeFresh (true);
							interest.setInterestLifetimeMilliseconds (Constants.BroadcastTimeoutMilliSeconds);

							// interesting notes: the override with name as first component times out, the override with default constructed interest as first component does not time out
							broadcastFace_.expressInterest (interest, dataHandle, dataHandle);

							loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tDiscovery ExpressInterest: " + interest.toUri ());
						} else {
							responseCount--;
						}
					}

					while (dataHandle.callbackCount_ < responseCount) {
						broadcastFace_.processEvents ();
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

					// give the peer some time(3s) for it to process the unique names received, 
					// and confirm with those unique names whether they are in my vicinity or not.
					// Or the interest with out-of-date digest gets sent again, and immediately gets the same response
					//Thread.Sleep (Constants.BroadcastIntervalMilliSeconds);
				}
			}
			catch (Exception e) {
				loggingCallback_ ("ERROR", DateTime.Now.ToString("h:mm:ss tt") + "\t-\t" + " discoveryThread: " + e.Message);
			}
		}

		/// <summary>
		/// Stop discovery kills the broadcast interest expression thread and position interest expression thread.
		/// It is called in onApplicationExit in Unity, both threads will keep running after application exits if it's not called.
		/// </summary>
		public void stopDiscovery()
		{
			if (tInterestExpression_.IsAlive) {
				tInterestExpression_.Abort ();
			} else {
				loggingCallback_("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tstopDiscovery:discovery expression thread is not alive");
			}
			if (tPositionInterestExpression_.IsAlive) {
				tPositionInterestExpression_.Abort ();
			} else {
				loggingCallback_("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tstopDiscovery:position interest thread is not alive");
			}
			if (publisherThread_.IsAlive) {
				publisherThread_.Abort ();
			} else {
				loggingCallback_("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tstopDiscovery:self position update thread is not alive");
			}
			//face_.stopProcessing ();
		}

		/// <summary>
		/// Discovery starts sending broadcast discovery interest and position udpate interest periodically.
		/// </summary>
		public void discovery ()
		{
			try {
				// Thread for expressing interest.
				tInterestExpression_ = new Thread(this.discoveryExpressInterest);
				tInterestExpression_.Start();

				tPositionInterestExpression_ = new Thread(this.positionExpressInterest);
				tPositionInterestExpression_.Start();

				publisherThread_ = new Thread(this.publishLocation);
				publisherThread_.Start();

			} catch (Exception e) {
				loggingCallback_ ("ERROR", "Discovery Exception: " + e.Message + "\nStack trace: " + e.StackTrace);
			}
		}

		// should use this method to publish to memory content cache
		public void publishLocation()
		{
			while (true) {
				// should lock it here
				selfEntity_.setSequenceNumber ((selfEntity_.getSequenceNumber() + 1) % Constants.MaxSequenceNumber);
				selfEntity_.locationArray_ [selfEntity_.getSequenceNumber ()] = selfEntity_.getLocation ();

				List<int> octantList = CommonUtility.getOctantIndicesFromVector3 (selfEntity_.getLocation ());
				if (octantList != selfOctant_) {
					Octant toRemove = getOctantByIndex (selfOctant_);
					if (toRemove != null) {
						toRemove.removeName (selfEntity_.getName());
						toRemove.setDigestComponent ();
					} else {
						loggingCallback_ ("ERROR", "Publish location error:" + " Previous octant does not exist");
					}

					Octant toAdd = getOctantByIndex (octantList);
					if (toAdd != null) {
						toAdd.addName (selfEntity_.getName());
						toAdd.setDigestComponent ();
					} else {
						// Don't expect this to happen
						loggingCallback_ ("WARNING", "Publish location:" + " New octant was not cared about previously");
						addOctant (octantList);

						toAdd.addName (selfEntity_.getName());
						toAdd.setDigestComponent ();
						trackOctant (toAdd);
					}
					selfOctant_ = octantList;
				}

				Thread.Sleep(Constants.PositionIntervalMilliSeconds);
			}
		}

		public GameEntityInfo getSelfGameEntityInfo()
		{
			return selfEntityInfo_;
		}

		public GameEntity getSelfGameEntity()
		{
			return selfEntity_;
		}

		/// <summary>
		/// Adds a new game entity to the list gameEntities_ by name if there is not an existing one.
		/// </summary>
		/// <returns><c>true</c>, if game entity by name does not exist before, and was added; <c>false</c> otherwise.</returns>
		/// <param name="name">Name.</param>
		public bool addGameEntityByName(string name)
		{
			if (getGameEntityByName (name) == null) {
				GameEntity gameEntity = new GameEntity (name, EntityType.Player, new Vector3(Constants.DefaultLocationNewEntity, Constants.DefaultLocationNewEntity, Constants.DefaultLocationNewEntity), setPosCallback_);
				//gameEntitiesLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
				gameEntities_.Add (gameEntity);
				//gameEntitiesLock_.ReleaseMutex ();
				return true;
			} else {
				return false;
			}
		}

		/// <summary>
		/// Removes an existing game entity from the list gameEntities_ by name if there is an existing one.
		/// </summary>
		/// <returns><c>true</c>, if game entity does exist and was removed, <c>false</c> otherwise.</returns>
		/// <param name="name">Name.</param>
		public bool removeGameEntityByName(string name)
		{
			// TODO: test this method
			GameEntity gameEntity = getGameEntityByName (name);
			if (gameEntity == null) {
				return false;
			} else {
				//gameEntitiesLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
				gameEntities_.RemoveAt (gameEntities_.IndexOf (gameEntity));
				//gameEntitiesLock_.ReleaseMutex ();
				return true;
			}
		}

		/// <summary>
		/// Don't expect this method to be used, other classes should always call functions of this class to modify gameEntities_ list, and those functions are locked by mutex locks
		/// </summary>
		/// <returns>The other game entities.</returns>
		public List<GameEntity> getOtherGameEntities()
		{
			return gameEntities_;
		}

		public GameEntity getGameEntityByName(string name)
		{
			//gameEntitiesLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
			for (int i = 0; i < gameEntities_.Count; i++) {
				if (gameEntities_ [i].getName () == name) {
					//gameEntitiesLock_.ReleaseMutex ();
					return gameEntities_ [i];
				}
			}
			// Make sure lock is released in both cases
			//gameEntitiesLock_.ReleaseMutex ();
			return null;
		}

		public void renderExpressInterest(string entityName)
		{
			InfoDataInterface infoDataInterface = new InfoDataInterface(this, infoCallback_, loggingCallback_);
			Name renderName = new Name (Constants.AlephPrefix).append(Constants.PlayersPrefix).append(entityName).append(Constants.InfoPrefix).append(Constants.RenderInfoPrefix);

			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tRender interest expressed " + renderName.toUri());

			Interest renderInterest = new Interest (renderName);
			positionFace_.expressInterest (renderInterest, infoDataInterface, infoDataInterface);

			return;
		}

		/// <summary>
		/// Express position interest with positionFace_ towards given name
		/// </summary>
		/// <param name="name">Name.</param>
		public void positionExpressInterest()
		{
			int count = 0;
			int responseCount = 0;
			int sleepSeconds = 0; 

			// TODO: There is always only one position data interface, which could cause potential problems...
			// How do I test/verify?
			PositionDataInterface positionDataInterface = new PositionDataInterface (this, loggingCallback_);

			try
			{
				while (true) {
					// It might be a better idea to copy the octant list and act based on that copy, so that we don't have to lock up the whole for loop
					//gameEntitiesLock_.WaitOne (Constants.MutexLockTimeoutMilliSeconds);
					List<GameEntity> copyGameEntities = new List<GameEntity> (gameEntities_);
					//gameEntitiesLock_.ReleaseMutex ();
					count = copyGameEntities.Count;
					responseCount = count;

					// count is for the cross-thread reference of gameEntities does not go wrong...after adding mutex lock, it shouldn't go wrong, but is still preserved for safety
					for (int i = 0; i < count; i++) {
						// It's unnatural that we must do this; should lock gameEntites in Add for cross thread reference
						if (copyGameEntities [i] != null) {
							// Position interest name is assumed to be only the Prefix + EntityName for now
							Name interestName = new Name (Constants.AlephPrefix).append(Constants.PlayersPrefix).append(copyGameEntities [i].getName ()).append(Constants.PositionPrefix);
							if (copyGameEntities [i].getSequenceNumber () != Constants.DefaultSequenceNumber) {
								interestName.append (Name.Component.fromNumber ((copyGameEntities [i].getSequenceNumber () + 1) % Constants.MaxSequenceNumber));
								Interest interest = new Interest (interestName);

								interest.setInterestLifetimeMilliseconds (Constants.PositionTimeoutMilliSeconds);
								interest.setMustBeFresh (true);

								positionFace_.expressInterest (interest, positionDataInterface, positionDataInterface);
								loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition ExpressInterest: " + interest.toUri ());
							} else {
								Interest interest = new Interest (interestName);

								interest.setInterestLifetimeMilliseconds (Constants.PositionTimeoutMilliSeconds);
								interest.setMustBeFresh (true);
								// with fetching mode, fetching the rightmost child, if it's the first interest for the game entity, should be ok.
								interest.setChildSelector (1);
								interest.setAnswerOriginKind (0);

								positionFace_.expressInterest (interest, positionDataInterface, positionDataInterface);	
								loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition ExpressInterest: " + interest.toUri ());
							}
						} else {
							responseCount--;
						}
					}

					while (positionDataInterface.callbackCount_ < responseCount) {
						positionFace_.processEvents ();
						System.Threading.Thread.Sleep (10);
						sleepSeconds += 10;
					}

					positionDataInterface.callbackCount_ = 0;

					int interval = Constants.PositionIntervalMilliSeconds - sleepSeconds;
					sleepSeconds = 0;
					if (interval > 0) {
						Thread.Sleep (interval);
					}
					// give the peer some time(3s) for it to process the unique names received, 
					// and confirm with those unique names whether they are in my vicinity or not.
					// Or the interest with out-of-date digest gets sent again, and immediately gets the same response
					//Thread.Sleep (Constants.PositionIntervalMilliSeconds);
				}
			}
			catch (Exception e) {
				loggingCallback_ ("ERROR", DateTime.Now.ToString("h:mm:ss tt") + "\t-\t" + "positionThread: " + e.Message);
			}
		}
	}
}