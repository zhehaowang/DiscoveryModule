using System;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule
{
	public class CommonUtility
	{
		public CommonUtility ()
		{
		}

		/// <summary>
		/// Get bytes from input unsigned integer. This method exists so that little/big endian won't be interpretted differently
		/// </summary>
		/// <returns>The bytes from Uint32.</returns>
		/// <param name="input">Input.</param>
		public static byte[] getBytesFromUInt32(UInt32 input)
		{
			//under testing
			byte[] resultBytes = new byte[4];
			resultBytes [0] = (byte)(input & 0x000000FF);
			resultBytes [1] = (byte)((input & 0x0000FF00) >> 8);
			resultBytes [2] = (byte)((input & 0x00FF0000) >> 16);
			resultBytes [3] = (byte)((input & 0xFF000000) >> 24);
			return resultBytes;
		}

		/// <summary>
		/// Get unsigned integer from input byte array. This method exists so that little/big endian won't be interpretted differently
		/// </summary>
		/// <returns>Uint32 integer.</returns>
		/// <param name="input">Input.</param>
		public static UInt32 getUInt32FromBytes(byte [] byteArray, int offset)
		{
			UInt32 u = 0;
			u += (UInt32)byteArray [offset];
			u += ((UInt32)byteArray [1 + offset]) << 8;
			u += ((UInt32)byteArray [2 + offset]) << 16;
			u += ((UInt32)byteArray [3 + offset]) << 24;
			return u;
		}

		/// <summary>
		/// Print the input list of integers as string, each element in the list is separated by '/'.
		/// Head is ignored if it's -1.
		/// </summary>
		/// <returns>A string that looks like "1/2/3/"</returns>
		/// <param name="index">List of integer indices.</param>
		public static string getStringFromList(List<int> index)
		{
			string str = "";
			int i = 0;
			// index allows the first node to be root(stub) or actual node 
			if (index [0] == -1) {
				i = 1;
			}
			for (; i < index.Count; i++) {
				str += (index [i] + "/");
			}
			return str;
		}

		/// <summary>
		/// Get a list of indices from an input string separated by '/'
		/// </summary>
		/// <returns>The list from string.</returns>
		/// <param name="inputStr">Input string, which should look like "(/)1/2/3(/)".</param>
		// TODO: whether the trailing / and leading / works should be further tested
		public static List<int> getListFromString(string inputStr)
		{
			string[] strs = inputStr.Split ('/');

			List<int> result = new List<int> ();
			int temp = 0;
			foreach(string str in strs)
			{
				if (str != "") {
					if (int.TryParse (str, out temp)) {
						result.Add (temp);
					}
				}
			}
			return result;
		}

		public static List<int> getOctantIndicesFromVector3(Vector3 inputVector)
		{
			string indexStr = GetLabel (inputVector);
			return getListFromString (indexStr);
		}

		public static string GetLabel(Vector3 position)
		{
			// decimal points in x,y,z
			// will not be used in this funciton

			// check if the point is in the game world
			if(InWorld(position) == false)
			{
				return null;
			}

			// get binaries
			string xbits = Convert.ToString((int)position.x_, 2).PadLeft(16,'0');
			string ybits = Convert.ToString((int)position.y_, 2).PadLeft(16,'0');
			string zbits = Convert.ToString((int)position.z_, 2).PadLeft(16,'0');

			// reorganize
			string L1bits = ""+xbits[0] + ybits[0] + zbits[0]; 
			string L2bits = ""+xbits[1] + ybits[1] + zbits[1];
			string L3bits = ""+xbits[2] + ybits[2] + zbits[2];
			string L4bits = ""+xbits[3] + ybits[3] + zbits[3];
			string L5bits = ""+xbits[4] + ybits[4] + zbits[4];
			string L6bits = ""+xbits[5] + ybits[5] + zbits[5];
			string L7bits = ""+xbits[6] + ybits[6] + zbits[6];

			int temp1 = Convert.ToInt32(L1bits, 2); 
			int temp2 = Convert.ToInt32(L2bits, 2); 
			int temp3 = Convert.ToInt32(L3bits, 2); 
			int temp4 = Convert.ToInt32(L4bits, 2); 
			int temp5 = Convert.ToInt32(L5bits, 2); 
			int temp6 = Convert.ToInt32(L6bits, 2); 
			int temp7 = Convert.ToInt32(L7bits, 2); 

			string L1 = Convert.ToString(temp1, 8);
			string L2 = Convert.ToString(temp2, 8);
			string L3 = Convert.ToString(temp3, 8);
			string L4 = Convert.ToString(temp4, 8);
			string L5 = Convert.ToString(temp5, 8);
			string L6 = Convert.ToString(temp6, 8);
			string L7 = Convert.ToString(temp7, 8);

			string labels = ""+L1 + "/" + L2 + "/" + L3 + "/" + L4 + "/" + L5 + "/" + L6 + "/" + L7;

			return labels;
		}

		public static Vector3 GetGameCoordinates(string str_lati, string str_longi)
		{
			// convert from latitude and longitude to game coordinates

			double radius = 30000;

			float latitude = Convert.ToSingle( str_lati );
			float longitude = Convert.ToSingle( str_longi ) ;
			float pi = 3.14159265359f;
			float theta = (float)(pi * (1.0 / 2.0 + latitude / 180));
			float fi = (float)(pi*(longitude/180));
			double x = radius * 0.78 * Math.Cos (theta / 4) * Math.Sin (theta) * Math.Sin (fi);
			double y = radius * 1.0 * Math.Cos (theta);
			double z = radius * 0.78 * Math.Cos (theta / 4) * Math.Sin (theta) * Math.Cos (fi);

			// rotate the egg
			double xx = x;
			double yy = Math.Cos (pi / 4) * y - Math.Sin (pi / 4) * z;
			double zz = Math.Sin (pi / 4) * y + Math.Cos (pi / 4) * z;

			// translate the egg
			double xxx = xx + radius;
			double yyy = yy + radius;
			double zzz = zz + radius;

			Vector3 pos = new Vector3 ((float)xxx, (float)yyy, (float)zzz);

			return pos;
		}

		public static void GetBoundaries(string labels, ref int xmin, ref int ymin, ref int zmin)
		{
			string[] split = labels.Split (new char [] { '/' }, StringSplitOptions.RemoveEmptyEntries);

			int L1oct = Convert.ToInt32(split[0],8);
			int L2oct = Convert.ToInt32(split[1],8);
			int L3oct = Convert.ToInt32(split[2],8);
			int L4oct = Convert.ToInt32(split[3],8);
			int L5oct = Convert.ToInt32(split[4],8);
			int L6oct = Convert.ToInt32(split[5],8);
			int L7oct = Convert.ToInt32(split[6],8);

			string L1bits = Convert.ToString (L1oct,2).PadLeft(3,'0');
			string L2bits = Convert.ToString (L2oct,2).PadLeft(3,'0');
			string L3bits = Convert.ToString (L3oct,2).PadLeft(3,'0');
			string L4bits = Convert.ToString (L4oct,2).PadLeft(3,'0');
			string L5bits = Convert.ToString (L5oct,2).PadLeft(3,'0');
			string L6bits = Convert.ToString (L6oct,2).PadLeft(3,'0');
			string L7bits = Convert.ToString (L7oct,2).PadLeft(3,'0');

			string xbits = "" + L1bits[0] + L2bits[0] + L3bits[0] + L4bits[0] + L5bits[0] + L6bits[0] + L7bits[0];
			string ybits = "" + L1bits[1] + L2bits[1] + L3bits[1] + L4bits[1] + L5bits[1] + L6bits[1] + L7bits[1];
			string zbits = "" + L1bits[2] + L2bits[2] + L3bits[2] + L4bits[2] + L5bits[2] + L6bits[2] + L7bits[2];

			int x = Convert.ToInt32 (xbits,2);
			int y = Convert.ToInt32 (ybits,2);
			int z = Convert.ToInt32 (zbits,2);

			xmin = x * 512; 
			ymin = y * 512;
			zmin = z * 512;

			return;

			//Discovery.Boundary bry = new Discovery.Boundary(xmin, xmax, ymin, ymax, zmin, zmax);
			//return bry;
		}

		public static List<string> GetNeighbors(Vector3 position)
		{
			List<string> neighborlist = new List<string>();
			int[,] neighbors = {{1,0,0}, {-1,0,0}, // x
				{0,1,0}, {0,-1,0}, // y
				{0,0,1}, {0,0,-1}, // z
				{1,1,0}, {1,-1,0}, {-1,1,0}, {-1,-1,0}, // x,y
				{1,0,1}, {1,0,-1}, {-1,0,1}, {-1,0,-1}, // x,z
				{0,1,1}, {0,1,-1}, {0,-1,1}, {0,-1,-1}, // y,z
				{1,1,1}, {-1,1,1}, {1,-1,1}, {1,1,-1}, {1,-1,-1}, {-1,1,-1}, {-1,-1,1},{-1,-1,-1} // x,y,z
			};

			Vector3 offset = new Vector3(0, 0, 0);
			string temp = null;
			for (int i = 0; i < 26; i++) {
				offset.x_ = neighbors [i, 0] * 512;
				offset.y_ = neighbors [i, 1] * 512;
				offset.z_ = neighbors [i, 2] * 512;

				temp = GetLabel (position + offset);
				if (temp != null) {
					neighborlist.Add (temp);
				}
			}
			return neighborlist;
		}

		public static bool InWorld(Vector3 position)
		{
			float worldsize = 65536; // 2^16

			if (position.x_ < 0 || position.y_ < 0 || position.z_ < 0) {
				return false;
			}
			if (position.x_ > worldsize || position.y_ > worldsize || position.z_ > worldsize) {
				return false;
			}
			return true;
		}
	}
}

