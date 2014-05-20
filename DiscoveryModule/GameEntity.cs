using System;

namespace remap.NDNMOG.DiscoveryModule
{
	public enum EntityType
	{
		Player,
		NPC
	};

	public class Vector3
	{
		public float x_;
		public float y_;
		public float z_;

		public Vector3(float x, float y, float z)
		{
			x_ = x;
			y_ = y;
			z_ = z;
			return;
		}

		/// <summary>
		/// Constructor by copying.
		/// </summary>
		/// <param name="vector">Vector.</param>
		public Vector3(Vector3 vector)
		{
			x_ = vector.x_;
			y_ = vector.y_;
			z_ = vector.z_;
			return;
		}

		public Vector3(string [] vectorStr)
		{
			if (vectorStr.Length != 3) {
				Console.WriteLine ("Length of vectorStr is not correct.");
				return;
			}
			float x = 0;
			float y = 0;
			float z = 0;
			if (!(float.TryParse(vectorStr[0], out x) && float.TryParse(vectorStr[1], out y) && float.TryParse(vectorStr[2], out z)))
			{
				Console.WriteLine ("Error parsing float from vectorStr");
				return;
			}
		}

		public override string ToString()
		{
			return String.Format ("{0},{1},{2}", x_, y_, z_);
		}
	}

	public class GameEntity
	{
		private string name_;
		private Vector3 location_;

		private EntityType entityType_;
		private int timeoutCount_;

		public GameEntity (string name, EntityType entityType)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (0, 0, 0);
			timeoutCount_ = 0;
		}

		public GameEntity (string name, EntityType entityType, Vector3 location)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (location);
			timeoutCount_ = 0;
		}

		public GameEntity (string name, EntityType entityType, float x, float y, float z)
		{
			name_ = name;
			entityType_ = entityType;
			location_ = new Vector3 (x, y, z);
			timeoutCount_ = 0;
		}

		public string getName()
		{
			return name_;
		}

		public Vector3 getLocation()
		{
			return location_;
		}

		public void setLocation(Vector3 location)
		{
			location_ = location;
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

