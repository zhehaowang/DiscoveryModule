﻿using System;
using net.named_data.jndn;
using System.Text;
using System.Collections.Generic;
using net.named_data.jndn.security;
using net.named_data.jndn.util;
using net.named_data.jndn.transport;

namespace remap.NDNMOG.DiscoveryModule
{
	public class DiscoveryDataInterface : OnData, OnTimeout
	{
		public DiscoveryDataInterface (Instance instance, LoggingCallback loggingCallback)
		{
			this.instance_ = instance;
			this.loggingCallback_ = loggingCallback;
		}

		/// <summary>
		/// Parses the data, adds names that this peer does not have, sending position fetching interest at the same time
		/// </summary>
		/// <param name="Interest">Interest.</param>
		/// <param name="data">Data.</param>
		public void parseContent(Interest Interest, Data data)
		{
			try {
				ByteBuffer content = data.getContent ().buf ();

				byte[] contentBytes = new byte[content.remaining()];
				content.get (contentBytes);

				string contentStr = Encoding.UTF8.GetString (contentBytes);

				string[] octantsStr = contentStr.Split ('\n');
				int i = 0;

				foreach (string str in octantsStr) {
					if (str != "") {
						string[] namesStr = str.Split (' ');

						List<int> index = CommonUtility.getListFromString (namesStr [namesStr.Length - 1]);

						Octant oct = instance_.getOctantByIndex (index);
						if (oct != null && oct.isTracking()) {
							for (i = 0; i < namesStr.Length - 1; i++) {
								string [] infoStrs = namesStr[i].Split (':');

								if (!oct.getNameDataset ().containsName (infoStrs[0], infoStrs[1])) {
									// stop considering self as another unique name; this should block the discovery of self, but not fundamentally
									if (infoStrs[1] != instance_.getSelfGameEntity().getName() || infoStrs[0] != instance_.getSelfGameEntity().getHubPrefix()) {
										loggingCallback_ ("INFO",  DateTime.Now.ToString("h:mm:ss tt") + "\t-\tDiscovery parseContent: Received unique name " + namesStr [i] + " at Octant: " + CommonUtility.getStringFromList (index));
										instance_.addGameEntityByName(infoStrs[0], infoStrs[1]);
									}
								}
							}
						}
					}
				}
			}
			catch {
				loggingCallback_ ("ERROR", "Discovery parseData: Did not receive octant name data");
			}
				
			return;
		}

		public void onData (Interest interest, Data data)
		{
			callbackCount_++;
			ByteBuffer content = data.getContent ().buf ();

			byte[] contentBytes = new byte[content.remaining()];
			content.get (contentBytes);

			string contentStr = Encoding.UTF8.GetString (contentBytes);

			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tDiscovery OnData: Received: " + data.getName().toUri() + "; content: " + contentStr);
			parseContent (interest, data);
		}

		public void onTimeout (Interest interest)
		{
			callbackCount_++;
			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tDiscovery OnTimeout: Time out for interest " + interest.getName ().toUri ());
		}

		private Instance instance_;
		private LoggingCallback loggingCallback_;
		public int callbackCount_ = 0;
	}

	/// <summary>
	/// Interest interface: for processing the incoming broadcast discovery interest
	/// </summary>
	public class DiscoveryInterestInterface : OnInterest, OnRegisterFailed
	{
		public DiscoveryInterestInterface (KeyChain keyChain, Name certificateName, Instance instance, LoggingCallback loggingCallback)
		{ 
			keyChain_ = keyChain;      
			certificateName_ = certificateName;
			instance_ = instance;
			loggingCallback_ = loggingCallback;
		}

		public static List<int> octantIndexFromInterestURI(string[] interestURI)
		{
			int i = 0;

			List<int> index = new List<int> ();
			int res = 0;
			for (i = Constants.octOffset; i < interestURI.Length - 1; i++) {
				if (int.TryParse (interestURI [i],out res)) {
					// legal octant index range is from 0 to 7
					if (res >= 0 && res < 8) {
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
			string interestNameStr = interest.getName().toUri ();
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
				// 1. Being cared about by the instance_ : added tracking_ member to octant class; if an octant is cared about by the instance, it can not be null
				// 2. Having a different digest from the incoming interest
				if (oct != null) {
					if (oct.isTracking () && oct.getDigestComponent ().getDigest () != CommonUtility.getUInt32FromBytes (digestBytes, isWhole.Length)) {
						returnList.Add (oct);
					}
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
						// 1. Being cared about by the instance_ : added tracking_ member to octant class; if an octant is cared about by the instance, it can not be nul
						// 2. Having a different digest from the incoming interest
						i += padding.Length;
						if (oct != null) {
							if (oct.isTracking () && oct.getDigestComponent ().getDigest () != CommonUtility.getUInt32FromBytes (digestBytes, isWhole.Length)) {
								returnList.Add (oct);
							}
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
		public Data constructData(Interest interest, List<Octant> octants)
		{
			// Make and sign a Data packet.
			Data data = new Data (interest.getName ());

			String content = "";

			foreach (Octant oct in octants)
			{
				content += (oct.getNameDataset ().getNamesAsString() + oct.getListIndexAsString() + "\n");
			}

			data.setContent (new Blob (Encoding.UTF8.GetBytes (content)));
			// setTimestampMilliseconds is needed for BinaryXml compatibility.
			data.getMetaInfo ().setTimestampMilliseconds (Common.getNowMilliseconds ());
			data.getMetaInfo ().setFreshnessSeconds (Constants.DigestDataFreshnessSeconds);

			try {
				keyChain_.sign (data, certificateName_);
			} catch (SecurityException exception) {
				loggingCallback_ ("ERROR", "Discovery OnInterest: SecurityException in sign: " + exception.Message);
			}
			return data;
		}

		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			++callbackCount_;
			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tDiscovery OnInterest : Received " + interest.getName().toUri());
			List<Octant> octants = parseDigest (interest);
			if (octants.Count != 0) {
				Data data = constructData (interest, octants);
				Blob encodedData = data.wireEncode ();

				try {
					transport.send (encodedData.buf ());
				} catch (Exception ex) {
					loggingCallback_ ("ERROR", "IOException in sending data " + ex.Message + " " + ex.StackTrace);
				}
			}
		}

		public void onRegisterFailed (Name prefix)
		{
			++callbackCount_;
			loggingCallback_ ("ERROR", "Discovery OnInterest: Register failed for prefix " + prefix.toUri ());
		}

		private KeyChain keyChain_;
		private Name certificateName_;
		private Instance instance_;
		private LoggingCallback loggingCallback_;

		public int callbackCount_ = 0;
	}
}

