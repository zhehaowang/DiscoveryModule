﻿using System;

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

			// Publish position data according to the current 'block' of milliseconds we are at.

			// if received /position, then there is no problem

			// if received /position/<number>, then it is indicated that new data for that number should be published,
			// but that number is dependent upon the other side's increment, which makes it irrelevant with local timestamp of milliseconds(block)
			// It seems that 

			string returnContent = location.ToString();

			// The prefix input does not seem to contain the last name component
			//Console.WriteLine (prefix.toUri());

			// changed to using interest.getname for the name of return data
			Data data = new Data (interest.getName());

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

		/// <summary>
		/// Get the entityName from getName().toUri(), here we are assuming that the entity name is the last two name component
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

