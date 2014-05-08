using System;
using System.Text;
using System.Collections.Generic;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Each digest DigestComponent is a member of a certain octant.
	/// This class is separated from Octant class so that changes in digest algorithms can be more easily adapted. 
	/// </summary>
	public class DigestComponent
	{
		private UInt32 digest_;
		//private List<int> index_;

		public DigestComponent (Octant oct)
		{
			digest_ = oct.getNameDataset().getHash ();
			//index_ = oct.getIndex ();
		}

		public DigestComponent ()
		{
			digest_ = 0;
		}

		public byte [] getDigestAsByteArray()
		{
			return CommonUtility.getBytesFromUInt32 (digest_);
		}

		public UInt32 getDigest()
		{
			return digest_;
		}
	}
}

