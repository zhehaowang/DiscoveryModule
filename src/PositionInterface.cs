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
using System.Threading;

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
		public PositionInterestInterface (KeyChain keyChain, Name certificateName, Instance instance, LoggingCallback loggingCallback)
		{
			keyChain_ = keyChain;
			certificateName_ = certificateName;
			instance_ = instance;
			loggingCallback_ = loggingCallback;
		}

		/// <summary>
		/// isSenderFallingBehind tells if the received sequence is expected.
		/// </summary>
		/// <returns><c>true</c>, if the received sequence is within a reasonable range, <c>false</c> otherwise.</returns>
		/// <param name="seq1">Current sequence number of local instance.</param>
		/// <param name="seq2">Received sequence number in interest.</param>
		public static Boolean isSenderFallingBehind(long seq1, long seq2)
		{
			if (seq1 - seq2 > Constants.MaxSequenceThreshold || (seq1 < seq2 && seq1 > Constants.MaxSequenceThreshold)) {
				return true;
			} else {
				return false;
			}
		}

		/// <summary>
		/// isSenderFallingBehind tells if the received sequence is expected.
		/// </summary>
		/// <returns><c>true</c>, if the received sequence is within a reasonable range, <c>false</c> otherwise.</returns>
		/// <param name="seq1">Current sequence number of local instance.</param>
		/// <param name="seq2">Received sequence number in interest.</param>
		public static Boolean isSenderAhead(long seq1, long seq2)
		{
			if (seq2 > seq1 && seq2 - seq1 < Constants.MinSequenceThreshold || (seq1 > (Constants.MaxSequenceNumber - Constants.MinSequenceThreshold) && seq2 < Constants.MinSequenceThreshold)) {
				return true;
			} else {
				return false;
			}
		}

		// To publish content to memory content cache would be ideal.

		/// <summary>
		/// Send position of self back to the interest's issuer, with freshness defined in constants class.
		/// </summary>
		/// <param name="prefix">Prefix.</param>
		/// <param name="interest">Interest.</param>
		/// <param name="transport">Transport.</param>
		/// <param name="registeredPrefixId">Registered prefix identifier.</param>
		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnInterest: " + interest.toUri());

			//Vector3 location = instance_.getSelfGameEntity ().getLocation ();

			string returnContent = "";

			Data data = new Data (interest.getName());

			Name newName = new Name (Constants.AlephPrefix);

			// should print the latency between generating and actually receiving interest and answering
			if (interest.getName ().size () == (newName.size () + 3)) {
				long sequenceNumber = instance_.getSelfGameEntity ().getSequenceNumber ();
				data.getName ().append (Name.Component.fromNumber(sequenceNumber));
				returnContent = instance_.getSelfGameEntity ().locationArray_ [sequenceNumber].ToString();
			} else {
				long sequenceNumber = PositionDataInterface.getSequenceFromName(interest.getName());

				long currentSequence = instance_.getSelfGameEntity ().getSequenceNumber ();
				if (isSenderAhead (currentSequence, sequenceNumber)) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnInterest: Requested sequence(" + sequenceNumber + ") is ahead of current(" + currentSequence + "), replying with most recent data");

					returnContent = instance_.getSelfGameEntity ().locationArray_ [currentSequence].ToString ();
				} else if (isSenderFallingBehind (currentSequence, sequenceNumber)) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnInterest: Requested sequence(" + sequenceNumber + ") has fallen behind current(" + currentSequence + "), replying with catchup right now");
					// For such situations, receiver should tell sender to send an interest without sequence number
					// This case is ignored, for now
					returnContent = "catch up";
				} else {
					loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnInterest: Replying to sequence: " + sequenceNumber + "; Current sequence: " + currentSequence);
					returnContent = instance_.getSelfGameEntity ().locationArray_ [sequenceNumber].ToString ();
				}
			}

			data.setContent (new Blob (Encoding.UTF8.GetBytes(returnContent)));
			data.getMetaInfo ().setFreshnessSeconds (Constants.PosititonDataFreshnessSeconds);

			try {
				keyChain_.sign (data, certificateName_);
			} catch (SecurityException exception) {
				loggingCallback_ ("ERROR", "Position OnInterest: SecurityException in sign: " + exception.Message);
			}

			Blob encodedData = data.wireEncode ();
			try {
				transport.send (encodedData.buf ());
			} catch (Exception ex) {
				loggingCallback_ ("ERROR", "Position OnInterest: Exception in sending data " + ex.Message);
			}
		}

		/// <summary>
		/// Called when register position prefix fails
		/// </summary>
		/// <param name="prefix">The failed prefix.</param>
		public void onRegisterFailed (Name prefix)
		{
			loggingCallback_ ("ERROR", "Position: Register failed for prefix " + prefix.toUri ());
		}

		KeyChain keyChain_;
		Name certificateName_;
		Instance instance_;
		LoggingCallback loggingCallback_;
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
		public PositionDataInterface(Instance instance, LoggingCallback loggingCallback)
		{
			instance_ = instance;
			loggingCallback_ = loggingCallback;

			//onDataLock_ = new Mutex ();
		}

		/// <summary>
		/// Get the entityName from getName().toUri(), entity name locator (position from the end of an interest name) is defined in constants
		/// </summary>
		/// <returns>The entity name from URI.</returns>
		/// <param name="name">Name.</param>
		public static string getEntityNameFromName(Name name)
		{
			Name lengthName = new Name (Constants.AlephPrefix);
			// the thing that comes directly after hubPrefix should be players + entityName
			string entityName = name.get (lengthName.size() + 1).toEscapedString ();
			return entityName;
		}

		/// <summary>
		/// Gets the sequence number from name.
		/// </summary>
		/// <returns>The sequence from name.</returns>
		public static long getSequenceFromName(Name name)
		{
			Name lengthName = new Name (Constants.AlephPrefix);

			// hubPrefix + players + entityName + position + seq
			if (lengthName.size () + 4 == name.size ()) {
				long sequenceNumber = name.get (-1).toNumber ();
				return sequenceNumber;
			} else {
				return -1;
			}
		}

		/// <summary>
		/// Express position interest with positionFace_ towards given gameEntity,
		/// This method may block thread execution for at most Constants.PositionIntervalMilliSeconds milliseconds
		/// It should always be called at the end of an onData or on onTimeout function
		/// </summary>
		/// <param name="name">Name.</param>
		public void positionExpressInterest(GameEntity gameEntity)
		{
			long milliseconds = gameEntity.getMilliseconds () + Constants.PositionIntervalMilliSeconds - 
				DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			if (milliseconds > 0) {
				Thread.Sleep (Convert.ToInt32(milliseconds));
			}

			Name interestName = new Name (Constants.AlephPrefix + Constants.PlayersPrefix + gameEntity.getName () + Constants.PositionPrefix);
			if (gameEntity.getSequenceNumber () != Constants.DefaultSequenceNumber) {
				interestName.append (Name.Component.fromNumber ((gameEntity.getSequenceNumber () + 1) % Constants.MaxSequenceNumber));
				Interest interest = new Interest (interestName);

				interest.setInterestLifetimeMilliseconds (Constants.PositionTimeoutMilliSeconds);
				interest.setMustBeFresh (true);

				instance_.getPositionFace().expressInterest (interest, this, this);

				gameEntity.setMilliseconds ();
				loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition ExpressInterest: " + interest.toUri ());
			} else {
				Interest interest = new Interest (interestName);

				interest.setInterestLifetimeMilliseconds (Constants.PositionTimeoutMilliSeconds);
				interest.setMustBeFresh (true);
				// with fetching mode, fetching the rightmost child, if it's the first interest for the game entity, should be ok.
				interest.setChildSelector (1);

				instance_.getPositionFace().expressInterest (interest, this, this);	

				gameEntity.setMilliseconds ();
				loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition ExpressInterest: " + interest.toUri ());
			}
		}

		/// <summary>
		/// Judge sequence tells if the latter sequence should be accepted as the new sequence number.
		/// </summary>
		/// <returns><c>true</c>, if seq1 is smaller than seq2, and seq2 should be taken as the new sequence, <c>false</c> otherwise.</returns>
		/// <param name="seq1">Seq1.</param>
		/// <param name="seq2">Seq2.</param>
		public Boolean judgeSequence(long seq1, long seq2)
		{
			if (seq1 < seq2 || seq1 == Constants.MaxSequenceNumber - 1 || seq1 == Constants.DefaultSequenceNumber) {
				return true;
			} else {
				return false;
			}
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

			loggingCallback_ 
			("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnData: " + data.getName().toUri() + " received: " + contentStr);

			string entityName = getEntityNameFromName (data.getName ());
			long sequenceNumber = getSequenceFromName (data.getName ());

			// TODO: Remove the dependency on Instance_....or lock

			GameEntity gameEntity = instance_.getGameEntityByName (entityName);

			if (gameEntity != null && judgeSequence(gameEntity.getSequenceNumber(), sequenceNumber)) {
				if (contentStr != "catch up") {
					gameEntity.setSequenceNumber (sequenceNumber);

					string[] locationStr = contentStr.Split (',');
					Vector3 location = new Vector3 (locationStr);

					Vector3 prevLocation = gameEntity.getLocation ();

					gameEntity.setLocation (location, Constants.InvokeSetPosCallback);
					gameEntity.resetTimeOut ();

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

								positionExpressInterest (gameEntity);
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

									positionExpressInterest (gameEntity);
								}
							}
						}
					}
				} else {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnData: Received name (" + entityName + ") asks for a catch up, sequence reset");

					gameEntity.setSequenceNumber (Constants.DefaultSequenceNumber);
					positionExpressInterest (gameEntity);
				}
			} else {
				// Don't expect this to happen
				if (gameEntity == null) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnData: Received name (" + entityName + ") does not have a gameEntity stored locally");
				} else {
					loggingCallback_ ("WARNING", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnData: Sequence rejected. Local: " + gameEntity.getSequenceNumber() + "; Received: " + sequenceNumber);
				}
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
			loggingCallback_ ("INFO",  DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnTimeout: Time out for interest " + interest.getName ().toUri ());
			string entityName = getEntityNameFromName (interest.getName ());

			GameEntity gameEntity = instance_.getGameEntityByName (entityName);
			if (gameEntity.incrementTimeOut ()) {
				loggingCallback_ ("INFO", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnTimeout: " + entityName + " could have dropped.");
				// For those could have dropped, remove them from the rendered objects of Unity (if it is rendered), and remove them from the gameEntitiesList
				Vector3 prevLocation = gameEntity.getLocation ();
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
				gameEntity.setLocation (new Vector3 (Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity), Constants.InvokeSetPosCallback);
			} else {

				gameEntity.setSequenceNumber (Constants.DefaultSequenceNumber);
				positionExpressInterest (gameEntity);
			}
		}

		private Instance instance_;
		private LoggingCallback loggingCallback_;
		//private Mutex onDataLock_;
	}
}

