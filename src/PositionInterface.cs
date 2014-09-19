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
		public static bool isSenderFallingBehind(long seq1, long seq2)
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

		public void onInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			++callbackCount_;
			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnInterest: " + interest.toUri());

			if (NamespaceUtils.getCmdFromName(interest.getName()) == Constants.PositionPrefix)
			{
				onPositionInterest(prefix, interest, transport, registeredPrefixId);
			}
			else if (NamespaceUtils.getCmdFromName(interest.getName()) == Constants.InfoPrefix)
			{
				onInfoInterest(prefix, interest, transport, registeredPrefixId);
			}
		}

		public void onInfoInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{
			Name lengthName = new Name (Constants.AlephPrefix);
			// the thing that comes directly after hubPrefix should be "players" + entityName + "info/position"
			string cmdName = interest.getName().get (lengthName.size() + 3).toEscapedString ();
			// processing render info interest
			if (cmdName == Constants.RenderInfoPrefix) {
				Data data = new Data (interest.getName());

				data.setContent (new Blob (Encoding.UTF8.GetBytes(instance_.getSelfGameEntityInfo().getRenderString())));
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
			return;
		}

		/// <summary>
		/// Send position of self back to the interest's issuer, with freshness defined in constants class.
		/// </summary>
		/// <param name="prefix">Prefix.</param>
		/// <param name="interest">Interest.</param>
		/// <param name="transport">Transport.</param>
		/// <param name="registeredPrefixId">Registered prefix identifier.</param>
		public void onPositionInterest (Name prefix, Interest interest, Transport transport, long registeredPrefixId)
		{

			//Vector3 location = instance_.getSelfGameEntity ().getLocation ();
			bool canReturn = true;
			string returnContent = "";

			Data data = new Data (interest.getName());

			Name newName = new Name (Constants.AlephPrefix);

			// should print the latency between generating and actually receiving interest and answering
			if (interest.getName ().size () == (newName.size () + 3)) {
				long sequenceNumber = instance_.getSelfGameEntity ().getQuerySequenceNumber ();
				data.getName ().append (Name.Component.fromNumber(sequenceNumber));
				returnContent = instance_.getSelfGameEntity ().locationArray_ [sequenceNumber].ToString();
			} else {
				long sequenceNumber = NamespaceUtils.getSequenceFromName(interest.getName());

				long currentSequence = instance_.getSelfGameEntity ().getQuerySequenceNumber ();
				if (isSenderAhead (currentSequence, sequenceNumber)) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnInterest: Requested sequence(" + sequenceNumber + ") is ahead of current(" + currentSequence + "), wait and reply.");

					// when the requested data is not yet generated, fire this onInterest again after a delay
					// we don't have the data yet, don't return anything
					canReturn = false;
					System.Threading.Timer timer = new System.Threading.Timer (new TimerCallback(timerCallback), new CallbackParam(prefix, interest, transport, registeredPrefixId), (sequenceNumber - currentSequence) * Constants.PositionIntervalMilliSeconds, Timeout.Infinite);
				} else if (isSenderFallingBehind (currentSequence, sequenceNumber)) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnInterest: Requested sequence(" + sequenceNumber + ") has fallen behind current(" + currentSequence + "), replying with reset.");
					// For such situations, receiver should tell sender to send an interest without sequence number
					// This case is ignored, for now
					returnContent = "reset:" + currentSequence + ":behind";
				} else {
					loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnInterest: Replying to sequence: " + sequenceNumber + "; Current sequence: " + currentSequence);
					returnContent = instance_.getSelfGameEntity ().locationArray_ [sequenceNumber].ToString ();
				}
			}

			if (canReturn) {
				data.setContent (new Blob (Encoding.UTF8.GetBytes (returnContent)));
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
		}

		class CallbackParam
		{
			public Transport transport_;
			public Interest interest_;
			public Name prefix_;
			public long registeredPrefixId_;

			public CallbackParam(Name prefix, Interest interest, Transport transport, long registeredPrefixId)
			{
				prefix_ = prefix;
				transport_ = transport;
				interest_ = interest;
				registeredPrefixId_ = registeredPrefixId;
			}
		}

		public void timerCallback(object param)
		{
			CallbackParam callbackParam = (CallbackParam)param;
			loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnInterest: timer refiring.");
			onPositionInterest (callbackParam.prefix_, callbackParam.interest_, callbackParam.transport_, callbackParam.registeredPrefixId_);
		}

		/// <summary>
		/// Called when register position prefix fails
		/// </summary>
		/// <param name="prefix">The failed prefix.</param>
		public void onRegisterFailed (Name prefix)
		{
			++callbackCount_;
			loggingCallback_ ("ERROR", "Position: Register failed for prefix " + prefix.toUri ());
		}

		KeyChain keyChain_;
		Name certificateName_;
		Instance instance_;
		LoggingCallback loggingCallback_;

		public int callbackCount_ = 0;
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
		/// Judge sequence tells if the latter sequence should be accepted as the new sequence number.
		/// </summary>
		/// <returns><c>true</c>, if seq1 is smaller than seq2, and seq2 should be taken as the new sequence, <c>false</c> otherwise.</returns>
		/// <param name="seq1">Seq1.</param>
		/// <param name="seq2">Seq2.</param>
		public bool judgeSequence(long seq1, long seq2)
		{
			if (seq1 < seq2 || seq1 == Constants.MaxSequenceNumber - 1 || seq1 == Constants.DefaultSequenceNumber) {
				return true;
			} else {
				loggingCallback_("WARNING", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnData judgeSequence: received: " + seq2 + " sequence not taken; current " + seq1);
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
				// And content.get gives me array index out of range...interesting.
			content.get (contentBytes);
			string contentStr = Encoding.UTF8.GetString (contentBytes);

			loggingCallback_ 
			  ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnData: " + data.getName().toUri() + " received: " + contentStr);

			string entityName = NamespaceUtils.getEntityNameFromName (data.getName ());
			long sequenceNumber = NamespaceUtils.getSequenceFromName (data.getName ());

			GameEntity gameEntity = instance_.getGameEntityByName (entityName);

			if (gameEntity != null && judgeSequence(gameEntity.getRenderSequenceNumber(), sequenceNumber)) {
				if (contentStr.Contains("reset") == false) {
					if (gameEntity.getQuerySequenceNumber() == Constants.DefaultSequenceNumber)
					{
						gameEntity.setExpectedSequenceNumber (sequenceNumber);
					}
					gameEntity.setRenderSequenceNumber (sequenceNumber);

					string[] locationStr = contentStr.Split (',');
					Vector3 location = new Vector3 (locationStr);

					Vector3 prevLocation = gameEntity.getLocation ();

					gameEntity.setLocation (location, Constants.InvokeSetPosCallback);
					gameEntity.resetTimeOut ();

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

								// Test this part
								instance_.removeGameEntityByName (entityName);
								gameEntity.setLocation (new Vector3 (Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity), Constants.InvokeSetPosCallback);
							} else {
								// This entity has left previous octant
								List<int> prevIndices = CommonUtility.getOctantIndicesFromVector3 (prevLocation);
								Octant prevOct = instance_.getOctantByIndex (prevIndices);
								// NameDataset class should need MutexLock for its names
								prevOct.removeName (entityName);
								prevOct.setDigestComponent ();

								instance_.removeGameEntityByName (entityName);
								gameEntity.setLocation (new Vector3 (Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity, Constants.DefaultLocationDropEntity), Constants.InvokeSetPosCallback);
							}
						} else {
							// only x_ should be enough for telling if prevLocation does not exist, need to work with the actual boundary of the game though
							if (prevLocation.x_ == Constants.DefaultLocationNewEntity || prevLocation.x_ == Constants.DefaultLocationDropEntity) {
								// This entity is newly discovered, and it is cared about
								// so it should be added to the list of names

								oct.addName (entityName);
								oct.setDigestComponent ();

								// express interest for rendering info
								if (Constants.FetchAdditionalInfoOnDiscovery) {
									instance_.renderExpressInterest (entityName);
								}
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
					string []split = contentStr.Split(':');
					long catchupSequenceNumber = 0;

					// TODO: whenever "reset" is received, local instance update the sequence number, which could cause thrashing if local's too fast?
					if (long.TryParse (split [1], out catchupSequenceNumber)) {
						loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnData: Received name (" + entityName + ") asks for a reset, sequence reset to " + catchupSequenceNumber);
						gameEntity.setExpectedSequenceNumber (catchupSequenceNumber);
						gameEntity.setRenderSequenceNumber (Constants.DefaultSequenceNumber);
					} else {
						loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnData: Received name (\" + entityName + \") asks for a reset, but sequence number processing failed.");
						gameEntity.setExpectedSequenceNumber (Constants.DefaultSequenceNumber);
						gameEntity.setRenderSequenceNumber (Constants.DefaultSequenceNumber);
					}
				}
			} else {
				// Don't expect this to happen
				if (gameEntity == null) {
					loggingCallback_ ("WARNING", DateTime.Now.ToString ("h:mm:ss tt") + "\t-\tPosition OnData: Received name (" + entityName + ") does not have a gameEntity stored locally");
				} else {
					loggingCallback_ ("WARNING", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnData: Sequence rejected. Local: " + gameEntity.getRenderSequenceNumber() + "; Received: " + sequenceNumber);
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
			callbackCount_++;

			loggingCallback_ ("WARNING",  DateTime.Now.ToString("h:mm:ss tt") + "\t-\tPosition OnTimeout: Time out for interest " + interest.getName ().toUri ());
			string entityName = NamespaceUtils.getEntityNameFromName (interest.getName ());

			GameEntity gameEntity = instance_.getGameEntityByName (entityName);

			if (gameEntity != null) {
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
				}
			}
		}

		private Instance instance_;
		private LoggingCallback loggingCallback_;
		public int callbackCount_ = 0;
	}

	public class InfoDataInterface : OnData, OnTimeout
	{
		public InfoDataInterface (Instance instance, InfoCallback infoCallback, LoggingCallback loggingCallback)
		{
			instance_ = instance;
			infoCallback_ = infoCallback;
			loggingCallback_ = loggingCallback;
		}

		public void onData(Interest interest, Data data)
		{
			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tRender data received, name: " + interest.getName().toUri());

			string entityName = NamespaceUtils.getEntityNameFromName (interest.getName());

			ByteBuffer content = data.getContent ().buf ();
			byte[] contentBytes = new byte[content.remaining()];
			content.get (contentBytes);
			string contentStr = Encoding.UTF8.GetString (contentBytes);

			loggingCallback_ ("INFO", DateTime.Now.ToString("h:mm:ss tt") + "\t-\tRendering: " + entityName + " " + contentStr);
			if (infoCallback_ != null) {
				infoCallback_ (entityName, contentStr);
			}
		}

		public void onTimeout(Interest interest)
		{
			string entityName = NamespaceUtils.getEntityNameFromName (interest.getName());
			instance_.renderExpressInterest (entityName);
		}

		private Instance instance_;
		private InfoCallback infoCallback_;
		private LoggingCallback loggingCallback_;
	}
}

