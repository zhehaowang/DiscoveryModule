using System;
using net.named_data.jndn;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Callback for a set location prototype function, which should be implemented in Unity
	/// </summary>
	public delegate bool SetPosCallback(string name, Vector3 location);

	public enum EntityType
	{
		Player,
		NPC
	};

	public class GameEntity
	{
		private string name_;
		private Vector3 location_;

		private EntityType entityType_;
		private int timeoutCount_;

		private long previousRespondTime_;

		public Vector3[] locationArray_;
		private long sequenceNumber_;

		private SetPosCallback setPosCallback_;

		public GameEntity (string name, EntityType entityType)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (0, 0, 0);
			timeoutCount_ = 0;
			setPosCallback_ = null;
			previousRespondTime_ = 0;

			sequenceNumber_ = Constants.DefaultSequenceNumber;
			locationArray_ = new Vector3[Constants.MaxSequenceNumber];
			for (int i = 0; i < Constants.MaxSequenceNumber; i++) {
				locationArray_ [i] = new Vector3 (0, 0, 0);
			}
		}

		public GameEntity (string name, EntityType entityType, Vector3 location)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (location);
			timeoutCount_ = 0;
			setPosCallback_ = null;
			previousRespondTime_ = 0;

			sequenceNumber_ = Constants.DefaultSequenceNumber;
			locationArray_ = new Vector3[Constants.MaxSequenceNumber];
			for (int i = 0; i < Constants.MaxSequenceNumber; i++) {
				locationArray_ [i] = new Vector3 (0, 0, 0);
			}
		}

		public GameEntity (string name, EntityType entityType, Vector3 location, SetPosCallback setPosCallback)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (location);
			timeoutCount_ = 0;
			setPosCallback_ = setPosCallback;
			previousRespondTime_ = 0;

			sequenceNumber_ = Constants.DefaultSequenceNumber;
			locationArray_ = new Vector3[Constants.MaxSequenceNumber];
			for (int i = 0; i < Constants.MaxSequenceNumber; i++) {
				locationArray_ [i] = new Vector3 (0, 0, 0);
			}
		}

		public GameEntity (string name, EntityType entityType, float x, float y, float z)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (x, y, z);
			timeoutCount_ = 0;
			setPosCallback_ = null;
			previousRespondTime_ = 0;

			sequenceNumber_ = Constants.DefaultSequenceNumber;
			locationArray_ = new Vector3[Constants.MaxSequenceNumber];
			for (int i = 0; i < Constants.MaxSequenceNumber; i++) {
				locationArray_ [i] = new Vector3 (0, 0, 0);
			}
		}

		public string getName()
		{
			return name_;
		}

		public Vector3 getLocation()
		{
			return location_;
		}

		public void setPreviousRespondTime(long respondTime)
		{
			previousRespondTime_ = respondTime;
		}

		public long getPreviousRespondTime()
		{
			return previousRespondTime_;
		}

		/// <summary>
		/// Set the location of this game entity, and call Callback passed from Unity depending on the input boolean
		/// </summary>
		/// <param name="location">Location.</param>
		/// <param name="invokeCallback">If set to <c>true</c> invoke callback.</param>
		public void setLocation(Vector3 location, bool invokeCallback)
		{
			location_ = location;
			if (invokeCallback) {
				if (setPosCallback_ == null) {
					Console.WriteLine ("setPosCallback_ for setLocation function is null.");
				} else {
					setPosCallback_ (name_, location_);
				}
			}
		}

		public void setLocation(float x, float y, float z)
		{
			location_.x_ = x;
			location_.y_ = y;
			location_.z_ = z;
			return;
		}

		public EntityType getType()
		{
			return entityType_;
		}

		public long getSequenceNumber()
		{
			return sequenceNumber_;
		}

		public void setSequenceNumber(long sequenceNumber)
		{
			sequenceNumber_ = sequenceNumber;
			return;
		}

		/// <summary>
		/// Increments the timeout count.
		/// </summary>
		/// <returns><c>true</c>True, if timeout received in a row reached the cap value.<c>false</c> otherwise.</returns>
		public bool incrementTimeOut()
		{
			timeoutCount_++;
			if (timeoutCount_ > Constants.DropTimeoutCount) {
				return true;
			} else {
				return false;
			}
		}


		public void resetTimeOut()
		{
			timeoutCount_ = 0;
		}
	}
}

