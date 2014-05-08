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
		/// <returns>The bytes from U int32.</returns>
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
		/// Print the input list of integers as string, each element in the list is separated by /.
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
	}
}

