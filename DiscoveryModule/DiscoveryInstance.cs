﻿using System;
using System.Text;
using net.named_data.jndn;
using net.named_data.jndn.util;
using net.named_data.jndn.transport;
using net.named_data.jndn.security;
using net.named_data.jndn.security.identity;
using net.named_data.jndn.security.policy;
using net.named_data.jndn.tests;
using System.Collections.Generic;

//Overall TODO: Register Prefix, enable remote peers, Actual sending/receiving, PrivateKey from parsing DER

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Data interface: for processing the incoming data
	/// </summary>
	public class DataInterface : OnData, OnTimeout
	{
		public DataInterface ()
		{
			this.callbackCount_ = 0;
		}

		/// <summary>
		/// Parses the data, adds names that this peer does not have, sending position fetching interest at the same time
		/// </summary>
		/// <param name="Interest">Interest.</param>
		/// <param name="data">Data.</param>
		public void parseData(Interest Interest, Data data)
		{
			// TODO: After the implementation of processDigest, process the data which is the return value of that function
			return;
		}

		public virtual void onData (Interest interest, Data data)
		{
			++callbackCount_;
			System.Console.Out.WriteLine ("Got data packet with name "
			+ data.getName ().toUri ());
			ByteBuffer content = data.getContent ().buf ();
			for (int i = content.position (); i < content.limit (); ++i)
				System.Console.Out.Write ((char)content.get (i));
			System.Console.Out.WriteLine ("");
		}

		public int callbackCount_;

		public virtual void onTimeout (Interest interest)
		{
			++callbackCount_;
			System.Console.Out.WriteLine ("Time out for interest "
			+ interest.getName ().toUri ());
		}
	}

	/// <summary>
	/// Interest interface: for processing the incoming broadcast discovery interest
	/// </summary>
	public class InterestInterface : OnInterest, OnRegisterFailed
	{
		public InterestInterface (KeyChain keyChain, Name certificateName, Instance instance)
		{ 
			keyChain_ = keyChain;      
			certificateName_ = certificateName;
			instance_ = instance;
		}

		public static List<int> octantIndexFromInterestURI(string[] interestURI)
		{
			int i = 0;

			List<int> index = new List<int> ();
			int res = 0;
			for (i = Constants.octOffset; i < interestURI.Length - 1; i++) {
				if (int.TryParse (interestURI [i],out res)) {
					// legal octant index range is from 1 to 8
					if (res > 0 && res < 9) {
						index.Add (res);
					}
				}
			}
			return index;
		}

		/// <summary>
		/// Original design (static) : Parses the digest field that comes as the last component of the interest name.
		/// Returns a list of octants with no actual names but digest field set.
		/// Current design (non-static) : Parses the digest field that comes as the last component of the interest name.
		/// Returns a list of octants whose local digest differ from that of the received digest.
		/// Both cases in 'parseDigest' not yet tested.
		/// </summary>
		/// <returns>The list of octants, for each of which a different digest is found.</returns>
		/// <param name="interest">The incoming interest that contains the digest field in question.</param> 
		public List<Octant> parseDigest(Interest interest)
		{
			// TODO: debug this method
			string interestNameStr = interest.toUri ();
			string[] nameComponentStr = interestNameStr.Split ('/');

			string lastComponent = nameComponentStr [nameComponentStr.Length - 1];
			// because of url-safe replacement, before decoding base64, replace '_' back with '/'
			lastComponent = lastComponent.Replace ('_', '/');

			byte[] digestBytes = Convert.FromBase64String (lastComponent);

			byte[] isWhole = { Constants.isWhole };
			byte[] padding = { Constants.padding };

			List<int> index = octantIndexFromInterestURI (nameComponentStr);

			List<Octant> returnList= new List<Octant> ();
			if (digestBytes [0] == Constants.isWhole) {
				Octant oct = instance_.getOctantByIndex (index);
				// notice here, all interests matching the registered prefix can trigger this. But registered octant != octant the peer actually care about
				// there are no data structures for the latter yet.
				// The ideal returning octant should match
				// 1. Being cared about by the instance_ (TODO, implement. Whether oct's null doesn't matter)
				// 2. Having a different digest from the incoming interest
				if (oct == null || oct.getDigestComponent().getDigest() != CommonUtility.getUInt32FromBytes(digestBytes, isWhole.Length)) {
					returnList.Add (oct);
				}
			} else {
				int i = isWhole.Length;
				int j = isWhole.Length;
				List<int> tempIndex = new List<int> (index);

				while (i < digestBytes.Length) {
					while (digestBytes [i] != 0x00) {
						tempIndex.Add((int)digestBytes[i]);
						i++;
					}
					if (!tempIndex.Equals (index)) {
						Octant oct = instance_.getOctantByIndex (tempIndex);
						// notice here, all interests matching the registered prefix can trigger this. But registered octant != octant the peer actually care about
						// there are no data structures for the latter yet.
						// The ideal returning octant should match
						// 1. Being cared about by the instance_ (TODO, implement. Whether it's null doesn't matter)
						// 2. Having a different digest from the incoming interest
						i += padding.Length;
						if (oct == null || oct.getDigestComponent ().getDigest () != CommonUtility.getUInt32FromBytes (digestBytes, i)) {
							returnList.Add (oct);
						}
						tempIndex = new List<int> (index);
					}
					i += (Constants.HashLength + padding.Length);
				}
			}

			return returnList;
		}

		/// <summary>
		/// Generate Data class to be used as response to the given interest, according to given list of octant.
		/// </summary>
		/// <returns>The digest.</returns>
		/// <param name="octants">A list of octants belonging to the current instance.</param>
		/// <param name="interest">Given interest to which the generated data responds.</param> 
		public Data generateData(Interest interest, List<Octant> octants)
		{
			// Make and sign a Data packet.
			Data data = new Data (interest.getName ());

			String content = "";
			// TODO: Test this feature

			foreach (Octant oct in octants)
			{
				if (oct == null) {
					Console.WriteLine (" Null octant detected");
				}
				content += (oct.getNameDataset ().getNamesAsString() + " " + oct.getListIndexAsString() + " ");
			}

			Console.WriteLine (content);

			data.setContent (new Blob (Encoding.UTF8.GetBytes (content)));
			// setTimestampMilliseconds is needed for BinaryXml compatibility.
			data.getMetaInfo ().setTimestampMilliseconds (Common.getNowMilliseconds ());

			try {
				keyChain_.sign (data, certificateName_);
			} catch (SecurityException exception) {
				Console.WriteLine ("SecurityException in sign: " + exception.Message);
			}
			return data;
		}

		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			++responseCount_;
			List<Octant> octants = parseDigest (interest);
			if (octants.Count != 0) {
				Data data = generateData (interest, octants);
				Blob encodedData = data.wireEncode ();

				try {
					transport.send (encodedData.buf ());
				} catch (Exception ex) {
					Console.WriteLine ("Echo: IOException in sending data " + ex.Message);
				}
			}
		}

		public void onRegisterFailed (Name prefix)
		{
			++responseCount_;
			Console.WriteLine ("Register failed for prefix " + prefix.toUri ());
		}

		KeyChain keyChain_;
		Name certificateName_;
		public int responseCount_ = 0;
		Instance instance_;
	}

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
		Octant root_;
		string name_;

		/// <summary>
		/// Instance class is supposed to be generated from an initial position of the player,
		/// which is a list of integers; and a string name, which is the name of the player(instance).
		/// </summary>
		/// <param name="index">Index</param>
		/// <param name="name">The name of the player (this instance).</param>
		public Instance (List<int> index, string name)
		{
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
		/// Constructs the bdcast interest for a certain octant. 
		/// The method first checks the local digest of the octant, append it after "isWhole" byte,
		/// And use Base64 to encode the bytes and get a string to append as the last name component.
		/// </summary>
		/// <returns>Interest constructed</returns>
		/// <param name="oct">The octant indices to express interest towards</param>
		public Interest constructBdcastInterest(List<int> index)
		{
			Octant oct = getOctantByIndex (index);

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
			string indexStr = CommonUtility.getStringFromList (index);

			Name name = new Name (Constants.BroadcastPrefix + indexStr + digestStr);
			Interest interest = new Interest (name);

			return interest;
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
		public Interest constructBdcastInterest(List<int> index, List<int>[] childs)
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
			Name name = new Name (Constants.BroadcastPrefix + indexStr + digestStr);
			Interest interest = new Interest (name);
			return interest;
		}

		/// <summary>
		/// discovery is copied from TestPublishAsyncNdnx directly.
		/// </summary>
		public void discovery ()
		{
			try {
				Face face = new Face ("localhost");

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

				InterestInterface echo = new InterestInterface (keyChain, certificateName, this);
				Name prefix = new Name ("/unitytest");
				Console.WriteLine ("Register prefix  " + prefix.toUri ());
				face.registerPrefix (prefix, echo, echo);

				// The main event loop.  
				// Wait to receive one interest for the prefix.
				while (echo.responseCount_ < 1) {
					face.processEvents ();

					// We need to sleep for a few milliseconds so we don't use 100% of 
					//   the CPU.
					System.Threading.Thread.Sleep (5);
				}
			} catch (Exception e) {
				Console.WriteLine ("exception: " + e.Message);
			}
		}
	}
}