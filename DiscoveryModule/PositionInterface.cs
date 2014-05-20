using System;

using net.named_data.jndn;
using net.named_data.jndn.util;
using net.named_data.jndn.transport;
using net.named_data.jndn.security;
using net.named_data.jndn.security.identity;
using net.named_data.jndn.security.policy;
using net.named_data.jndn.tests;

// discovery module is written in CSharp so it follows the CSharp namespace naming convention:
// <Company>.(<Product>|<Technology>)[.<Feature>][.<Subnamespace>]
using System.Text;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule
{
	public class PositionInterestInterface : OnInterest, OnRegisterFailed
	{
		public PositionInterestInterface (KeyChain keyChain, Name certificateName, Instance instance)
		{
			keyChain_ = keyChain;      
			certificateName_ = certificateName;
			instance_ = instance;
		}

		/// <summary>
		/// Send position info of self back to the interest's issuer, with 200ms of data freshness
		/// </summary>
		/// <param name="prefix">Prefix.</param>
		/// <param name="interest">Interest.</param>
		/// <param name="transport">Transport.</param>
		/// <param name="registeredPrefixId">Registered prefix identifier.</param>
		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			Console.WriteLine ("Interest received: " + interest.toUri());
			++responseCount_;

			Vector3 location = instance_.getSelfGameEntity ().getLocation ();

			string returnContent = location.ToString();

			Data data = new Data (prefix);

			data.setContent (new Blob (Encoding.UTF8.GetBytes(returnContent)));
			data.getMetaInfo ().setFreshnessSeconds (Constants.PosititonDataFreshnessSeconds);

			try {
				keyChain_.sign (data, certificateName_);
			} catch (SecurityException exception) {
				Console.WriteLine ("SecurityException in sign: " + exception.Message);
			}

			Blob encodedData = data.wireEncode ();
			try {
				transport.send (encodedData.buf ());
			} catch (Exception ex) {
				Console.WriteLine ("Exception in sending data " + ex.Message);
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

	public class PositionDataInterface : OnData, OnTimeout
	{
		public PositionDataInterface(Instance instance)
		{
			instance_ = instance;
		}

		public string getEntityNameFromURI(string nameURI)
		{
			string[] splitString = nameURI.Split ('/');
			string entityName = splitString[splitString.Length - 1];
			return entityName;
		}

		/// <summary>
		/// OnData assumes the name of the entity is the last component of data name, which may not hold true for later designs
		/// </summary>
		/// <param name="interest">Interest.</param>
		/// <param name="data">Data.</param>
		public void onData (Interest interest, Data data)
		{
			ByteBuffer content = data.getContent ().buf ();

			byte[] contentBytes = new byte[content.remaining()];
			content.get (contentBytes);

			string contentStr = Encoding.UTF8.GetString (contentBytes);

			++callbackCount_;
			Console.WriteLine ("Data received: " + contentStr + " Freshness period: " + data.getMetaInfo().getFreshnessPeriod());

			string entityName = getEntityNameFromURI (data.getName().toUri());

			// since it's pointer reference, do I need to extend the mutex lock here?
			GameEntity gameEntity = instance_.getGameEntityByName (entityName);
			string[] locationStr = contentStr.Split (',');

			Vector3 location = new Vector3 (locationStr);

			if (gameEntity != null) {
				Vector3 prevLocation = gameEntity.getLocation ();

				gameEntity.setLocation (location, Constants.InvokeSetPosCallback);
				gameEntity.resetTimeOut ();

				// TODO: implement the following stub methods, decide whether put them in here or encapsulated as a function in instance class
				// Cross thread reference without mutex is still a problem here.
				List<int> octantIndices = CommonUtility.getOctantIndicesFromVector3 (location);

				if (octantIndices != null) {
					Octant oct = instance_.getOctantByIndex (octantIndices);
					if (oct == null || (!oct.isTracking ())) {
						// this instance does not even care about this octant for now, so this game entity is no longer cared about as well
						// it should be removed from the list of gameEntities, and no more position interest should be issued towards it.
					} else {
						// Need to make sure that digestComponent if updated correctly: whether digestComponent is always generated dynamically? 
						// or its change is triggered by events such as add or remove?
						List<int> prevIndices = CommonUtility.getOctantIndicesFromVector3 (prevLocation);
						// And need to make sure equals method works
						if (!octantIndices.Equals (prevIndices)) {
							// Game entity moved from one octant to another, and both are cared about by this instance
							oct.addName (entityName);
							Octant prevOct = instance_.getOctantByIndex (prevIndices);
							// NameDataset class should need MutexLock for its names
							prevOct.removeName (entityName);
						}
					}
				}

			} else {
				// Don't expect this to happen
				Console.WriteLine("Received name (" + entityName + ") does not have a gameEntity stored locally.");
			}
		}

		public void onTimeout (Interest interest)
		{
			++callbackCount_;
			System.Console.Out.WriteLine ("Time out for interest " + interest.getName ().toUri ());
			string entityName = getEntityNameFromURI (interest.getName ().toUri ());
			if (instance_.getGameEntityByName (entityName).incrementTimeOut ()) {
				Console.WriteLine (entityName + " could have dropped.");
			}
		}

		public int callbackCount_;
		private Instance instance_;
	}
}

