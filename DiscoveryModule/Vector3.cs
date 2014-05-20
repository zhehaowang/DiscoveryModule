using System;

namespace remap.NDNMOG.DiscoveryModule
{
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

		public static Vector3 operator + (Vector3 v1, Vector3 v2)
		{
			Vector3 result = new Vector3 (v1.x_ + v2.x_, v1.y_ + v2.y_, v1.z_ + v2.z_);
			return result;
		}
	}
}

