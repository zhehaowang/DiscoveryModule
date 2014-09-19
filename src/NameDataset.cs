using System;
using System.Text;
using System.Collections.Generic;

using System.Threading;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Name dataset is the set of names belonging to a specific octant.
	/// </summary>
	public class NameDataset
	{
		private List<string> names_;
		private UInt32 hash_;

		// To make it compatible with DotNet 2, which Unity uses, we are not using Threadsafe collection featured in 4.0,
		// instead, we lock name list explicitly.
		private Mutex nameListLock_;

		public NameDataset()
		{
			names_ = new List<string> ();
			nameListLock_ = new Mutex ();
		}

		public NameDataset(List<string> names)
		{
			names_ = names;
			nameListLock_ = new Mutex ();
		}

		public bool removeName(string prefix, string name)
		{
			nameListLock_.WaitOne ();
			int idx = names_.IndexOf (prefix + ":" + name);
			if (idx != -1) {
				names_.RemoveAt (idx);
				nameListLock_.ReleaseMutex ();
				return true;
			} else {
				nameListLock_.ReleaseMutex ();
				return false;
			}
		}

		public void appendName(string prefix, string name)
		{
			nameListLock_.WaitOne ();
			names_.Add (prefix + ":" + name);
			nameListLock_.ReleaseMutex ();
		}

		public void appendNames(NameDataset nameDataset)
		{
			nameListLock_.WaitOne ();
			foreach (string str in nameDataset.getNames())
			{
				names_.Add (str);
			}
			nameListLock_.ReleaseMutex ();
			return;
		}

		public bool containsName(string prefix, string name)
		{
			nameListLock_.WaitOne ();
			bool value = names_.Contains (prefix + ":" + name);
			nameListLock_.ReleaseMutex ();
			return value;
		}

		/// <summary>
		/// Debugs the list.
		/// </summary>
		public void debugList()
		{
			nameListLock_.WaitOne ();
			foreach (string str in names_) {
				Console.WriteLine (str);
			}
			nameListLock_.ReleaseMutex ();
		}

		public string getNamesAsString()
		{
			string result = "";
			nameListLock_.WaitOne ();
			foreach (string str in names_) {
				result += (str + " ");
			}
			nameListLock_.ReleaseMutex ();
			return result;
		}

		/// <summary>
		/// Gets the names.
		/// </summary>
		/// <returns>The names.</returns>
		public List<string> getNames()
		{
			return names_;
		}

		/// <summary>
		/// Xors the string.
		/// </summary>
		/// <returns>The string.</returns>
		/// <param name="key">Key.</param>
		/// <param name="input">Input.</param>
		public string xorStr(string key, string input)
		{
			StringBuilder sb = new StringBuilder();
			for(int i=0; i<input.Length; i++)
				sb.Append((char)(input[i] ^ key[(i % key.Length)]));
			String result = sb.ToString ();

			return result;
		}

		/// <summary>
		/// Gets the hash.
		/// </summary>
		/// <returns>The hash.</returns>
		public UInt32 getHash()
		{
			// remember not to call calculateHash before getHash()
			calculateHash ();
			return hash_;
		}

		/// <summary>
		/// Calculates the hash and assigns it to hash field. Should abstract it from actual hash functions.
		/// For now, using FNV hash, directly called in this function
		/// </summary>
		public void calculateHash()
		{
			string xorNames = Constants.XorDebugKey;

			nameListLock_.WaitOne ();
			foreach (string str in names_) {
				xorNames = xorStr(xorNames, str);
			}
			nameListLock_.ReleaseMutex ();

			//Console.WriteLine (xorNames.Length);
			hash_ = fnvHash (xorNames, xorNames.Length);
		}

		/// <summary>
		/// Implementation of FNV hash.
		/// </summary>
		/// <returns>The hash.</returns>
		/// <param name="key">Key.</param>
		/// <param name="length">Length.</param>
		public UInt32 fnvHash(string key, int length)
		{
			UInt32 hash = Constants.FNVOffset32;
			for (int i = 0; i < length; i++) {
				hash = hash ^ (key [i]);
				hash = hash * Constants.FNVPrime32;
			}
			return hash;
		}
	}
}

