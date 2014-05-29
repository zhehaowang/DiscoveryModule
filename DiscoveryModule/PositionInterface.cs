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
	/// <summary>
	/// PositionInterestInterface handles the registration failure for position interest, or responds to incoming position interest. 
	/// This class inherits onInterest and onRegisterFailed.
	/// Reference to this class is passed as parameter for prefix registration
	/// </summary>
	public class PositionInterestInterface : OnInterest, OnRegisterFailed
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="remap.NDNMOG.DiscoveryModule.PositionInterestInterface"/> class.
		/// KeyChain, CertificateName are used for creating signedData when interest is received.
		/// A reference to an instance of Instance class is passed, so that this class can access the NameDataset of Octants belonging to a certain instance
		/// </summary>
		/// <param name="keyChain">Key chain.</param>
		/// <param name="certificateName">Certificate name.</param>
		/// <param name="instance">The local game instance.</param>
		public PositionInterestInterface (KeyChain keyChain, Name certificateName, Instance instance)
		{
			keyChain_ = keyChain;      
			certificateName_ = certificateName;
			instance_ = instance;
		}

		/// <summary>
		/// Send position of self back to the interest's issuer, with freshness defined in constants class.
		/// </summary>
		/// <param name="prefix">Prefix.</param>
		/// <param name="interest">Interest.</param>
		/// <param name="transport">Transport.</param>
		/// <param name="registeredPrefixId">Registered prefix identifier.</param>
		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			// TODO: Debug version usage
			Console.WriteLine ("Interest received: " + interest.toUri());
			++responseCount_;

			Vector3 location = instance_.getSelfGameEntity ().getLocation ();

			string returnContent = location.ToString();

			// Publish position data with a specific version
			long version = (long)Common.getNowMilliseconds ();
			Console.WriteLine ("Version is : " + version);

			// changed to using interest.getname + version based on the milliseconds of now, for the name of return data
			Data data = new Data (interest.getName().appendVersion(version));

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

		/// <summary>
		/// Called when register position prefix fails
		/// </summary>
		/// <param name="prefix">The failed prefix.</param>
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
	/// PositionDataInterface handles the response to this instance's position interest. It processes the incoming position data, or interest timeout.
	/// This class inherits onData and onTimeout.
	/// Reference to this class is passed as parameter for interest expression.
	/// </summary>
	public class PositionDataInterface : OnData, OnTimeout
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="remap.NDNMOG.DiscoveryModule.PositionDataInterface"/> class.
		/// A reference to instance is passed here so that position info belonging to that instance can be accessed.
		/// </summary>
		/// <param name="instance">Instance.</param>
		public PositionDataInterface(Instance instance)
		{
			instance_ = instance;
		}

		/// <summary>
		/// Get the entityName from getName().toUri(), entity name locator (position from the end of an interest name) is defined in constants
		/// </summary>
		/// <returns>The entity name from URI.</returns>
		/// <param name="nameURI">Name URI.</param>
		public string getEntityNameFromURI(string nameURI)
		{
			string[] splitString = nameURI.Split ('/');
			string entityName = splitString[splitString.Length - Constants.EntityNameOffsetFromEnd];
			return entityName;
		}

		/// <summary>
		/// On data is called when data is received for an expressed position interest.
		/// Position of corresponding entity in Instance need to be updated accordingly.
		/// If the containing octant of the entity is not cared about, and the entity does not belong to a previously cared about octant: Stop expressing position interest towards that entity
		/// If the containing octant of the entity is not cared about, but the entity used to belong to a cared about octant: The entity has left, therefore no more position interest should be expressed towards it, and we remove it from its previous octant
		/// If the containing octant of the entity is cared about, and the entity does not belong to a previously cared about octant: The entity has entered, and should be added to the NameDataset of the corresponding octant
		/// If the containing octant of the entity is cared about, and the entity used to belong to a cared about octant: The entity has left one octant for another, and should be added to the NameDataset of the new Octant, and removed from that of the old octant.
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

			string entityName = getEntityNameFromURI (interest.getName().toUri());

			// since it's pointer reference, do I need to extend the mutex lock here?
			GameEntity gameEntity = instance_.getGameEntityByName (entityName);
			string[] locationStr = contentStr.Split (',');

			Vector3 location = new Vector3 (locationStr);

			long version = data.getName ().get (data.getName().size() - Constants.DataVersionOffsetFromEnd).toVersion();

			if (gameEntity != null && version > gameEntity.getPreviousRespondTime()) {
				Vector3 prevLocation = gameEntity.getLocation ();

				gameEntity.setLocation (location, Constants.InvokeSetPosCallback);
				gameEntity.resetTimeOut ();

				Console.WriteLine ("Version is : " + version);
				// adding or clearing up the exclude field
				if (gameEntity.getExclude ().size () >= Constants.PositionUpdatesPerSecond || version - gameEntity.getPreviousRespondTime() > Constants.ExclusionClearPeriod) {
					gameEntity.resetExclude ();
				} else {
					gameEntity.addExclude (version);
				}
				gameEntity.setPreviousRespondTime(version);

				// TODO: Test following logic
				// Cross thread reference without mutex is still a problem here.
				List<int> octantIndices = CommonUtility.getOctantIndicesFromVector3 (location);

				if (octantIndices != null) {
					Octant oct = instance_.getOctantByIndex (octantIndices);
					if (oct == null || (!oct.isTracking ())) {
						// this instance does not care about this octant for now: this game entity is no longer cared about as well
						// it should be removed from the list of gameEntities, and no more position interest should be issued towards it.
						if (prevLocation.x_ == Constants.DefaultLocationNewEntity || prevLocation.x_ == Constants.DefaultLocationDropEntity) {
							// The info we received is about an entity who is not being cared about now and was not cared about before
							// We don't have to do anything about it, except removing it from our list of names to express interest towards
							instance_.removeGameEntityByName (entityName);
						} else {
							// This entity has left previous octant
							List<int> prevIndices = CommonUtility.getOctantIndicesFromVector3 (prevLocation);
							Octant prevOct = instance_.getOctantByIndex (prevIndices);
							// NameDataset class should need MutexLock for its names
							prevOct.removeName (entityName);
							prevOct.setDigestComponent ();

							instance_.removeGameEntityByName (entityName);
						}
					} else {
						// only x_ should be enough for telling if prevLocation does not exist, need to work with the actual boundary of the game though
						if (prevLocation.x_ == Constants.DefaultLocationNewEntity || prevLocation.x_ == Constants.DefaultLocationDropEntity) {
							// This entity is newly discovered, and it is cared about
							// so it should be added to the list of names

							oct.addName (entityName);
							oct.setDigestComponent ();
						} else {
							// This entity is not newly discovered, and it may be moving out from one cared-about octant to another
							List<int> prevIndices = CommonUtility.getOctantIndicesFromVector3 (prevLocation);
							// And need to make sure equals method works
							if (!octantIndices.Equals (prevIndices)) {
								// Game entity moved from one octant to another, and both are cared about by this instance
								oct.addName (entityName);
								Octant prevOct = instance_.getOctantByIndex (prevIndices);
								// NameDataset class should need MutexLock for its names
								prevOct.removeName (entityName);

								oct.setDigestComponent ();
								prevOct.setDigestComponent ();
							}
						}
					}
				}

			} else {
				// Don't expect this to happen
				Console.WriteLine("Received name (" + entityName + ") does not have a gameEntity stored locally.");
			}
		}

		/// <summary>
		/// Position interest times out. Receiving a number of timeouts in a row signifies an entity has dropped, 
		/// and should be removed from its octant, and position interest should no longer be expressed towards it.
		/// The number is defined in constants.
		/// </summary>
		/// <param name="interest">Interest.</param>
		public void onTimeout (Interest interest)
		{
			++callbackCount_;
			System.Console.Out.WriteLine ("Time out for interest " + interest.getName ().toUri ());
			string entityName = getEntityNameFromURI (interest.getName ().toUri ());
			GameEntity ent = instance_.getGameEntityByName (entityName);
			if (ent.incrementTimeOut ()) {
				Console.WriteLine (entityName + " could have dropped.");
				// For those could have dropped, remove them from the rendered objects of Unity (if it is rendered), and remove them from the gameEntitiesList
				Vector3 prevLocation = ent.getLocation ();
				if (prevLocation.x_ == Constants.DefaultLocationNewEntity || prevLocation.x_ == Constants.DefaultLocationDropEntity) {
					// we don't have info about the dropped entity previously, so we just remove it from the list we express position interest towards
					instance_.removeGameEntityByName (entityName);
				} else {
					// a previously known entity has dropped, so we remove it from the octant it belongs to, and the list we express position interest towards
					List<int> prevIndices = CommonUtility.getOctantIndicesFromVector3 (prevLocation);
					Octant prevOct = instance_.getOctantByIndex (prevIndices);
					prevOct.removeName (entityName);

					prevOct.setDigestComponent ();
					instance_.removeGameEntityByName (entityName);
				}
				ent.setLocation (new Vector3 (Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity), Constants.InvokeSetPosCallback);
			}
		}

		public int callbackCount_;
		private Instance instance_;
	}
}

