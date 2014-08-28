using System;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Vector3 is an implementation similar with UnityEngine's vector3. Conversion from one to another is done in Unity.
	/// </summary>
	public class Vector3
	{
		public float x_;
		public float y_;
		public float z_;

		/// <summary>
		/// Standard constructor from three floats
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="z">The z coordinate.</param>
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
			if (!(float.TryParse(vectorStr[0], out x_) && float.TryParse(vectorStr[1], out y_) && float.TryParse(vectorStr[2], out z_)))
			{
				Console.WriteLine ("Error parsing float from vectorStr");
				return;
			}
		}

		public override string ToString()
		{
			return String.Format ("{0},{1},{2}", x_, y_, z_);
		}

		public override bool Equals (object obj)
		{
			Vector3 vector3 = (Vector3)obj;
			if (x_ == vector3.x_ && y_ == vector3.y_ && z_ == vector3.z_)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public static Vector3 operator + (Vector3 v1, Vector3 v2)
		{
			Vector3 result = new Vector3 (v1.x_ + v2.x_, v1.y_ + v2.y_, v1.z_ + v2.z_);
			return result;
		}
	}
}

