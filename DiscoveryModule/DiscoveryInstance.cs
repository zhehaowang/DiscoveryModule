using System;
using System.Text;
using net.named_data.jndn;
using net.named_data.jndn.util;
using net.named_data.jndn.transport;
using net.named_data.jndn.security;
using net.named_data.jndn.security.identity;
using net.named_data.jndn.security.policy;
using net.named_data.jndn.tests;
using System.Collections.Generic;

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
		public InterestInterface (KeyChain keyChain, Name certificateName)
		{ 
			keyChain_ = keyChain;      
			certificateName_ = certificateName;
		}

		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			++responseCount_;

			// Make and sign a Data packet.
			Data data = new Data (interest.getName ());
			String content = "Echo " + interest.getName ().toUri ();
			data.setContent (new Blob (Encoding.UTF8.GetBytes (content)));

			// setTimestampMilliseconds is needed for BinaryXml compatibility.
			data.getMetaInfo ().setTimestampMilliseconds (Common.getNowMilliseconds ());

			try {
				keyChain_.sign (data, certificateName_);
			} catch (SecurityException exception) {
				// Don't expect this to happen.
				Console.WriteLine ("SecurityException in sign: " + exception.Message);
			}
			Blob encodedData = data.wireEncode ();

			Console.WriteLine ("Sent content " + content);
			try {
				transport.send (encodedData.buf ());
			} catch (Exception ex) {
				Console.WriteLine ("Echo: IOException in sending data " + ex.Message);
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
		/// NetworkInterface class is supposed to be generated from an initial position of the player,
		/// which is a list of integers
		/// </summary>
		/// <param name="index">Index</param>
		public Instance (List<int> index, string name)
		{
			if (index.Count != Constants.octreeLevel) {
				Console.WriteLine ("Cannot instantiate from non-leaf location.");
				return;
			}

			// Let's imagine all the octants are derived from a octant index '0'?
			root_ = new Octant (-1, false);
			// creates the tree structure using the index as input, 
			// and put the name of self to the nameDataset of leaf.
			int i = 0;
			Octant temp = root_;

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
		/// </summary>
		/// <returns>Interest constructed</returns>
		/// <param name="oct">Oct.</param>
		public Interest constructBdcastInterest(List<int> index)
		{
			// TODO: implement bdcast interest towards a specific octant
			return new Interest ();
		}

		/// <summary>
		/// Constructs the bdcast interest for some certain childs of a octant.
		/// </summary>
		/// <returns>The bdcast interest.</returns>
		public Interest constructBdcastInterest(List<int> index, List<int> childs)
		{
			// TODO: implement bdcast interest towards some childs of a specific octant
			// What's the best way to pass the childs as parameters?
			return new Interest ();
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

				InterestInterface echo = new InterestInterface (keyChain, certificateName);
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