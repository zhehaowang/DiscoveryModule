using System;
using System.Threading;

using net.named_data.jndn;
using net.named_data.jndn.security;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// ThreadsafeFace does not wrap security methods for Face, keyChain and certificateName are not included in this.
	/// </summary>
	public class ThreadsafeFace
	{
		public ThreadsafeFace (Face face)
		{
			face_ = face;
			faceLock_ = new Mutex ();
			faceThread_ = new Thread (this.processEvents);
		}

		public Face getFace()
		{
			return face_;
		}

		public void removeRegisteredPrefix(long registerPrefixID)
		{
			//faceLock_.WaitOne ();
			face_.removeRegisteredPrefix (registerPrefixID);
			//faceLock_.ReleaseMutex ();
			return;
		}

		public long expressInterest(Interest interest, OnData onData, OnTimeout onTimeout)
		{
			//faceLock_.WaitOne ();
			long id = face_.expressInterest (interest, onData, onTimeout);
			//faceLock_.ReleaseMutex ();
			return id;
		}

		public long registerPrefix(Name name, OnInterest onInterest, OnRegisterFailed onRegisterFailed)
		{
			//faceLock_.WaitOne ();
			long id = face_.registerPrefix (name, onInterest, onRegisterFailed);
			//faceLock_.ReleaseMutex ();
			return id;
		}

		// not actually sure if this is the best way
		public void processEvents()
		{
			while (true) {
				//faceLock_.WaitOne ();
				face_.processEvents ();
				//faceLock_.ReleaseMutex ();
				Thread.Sleep (25);
			}
		}

		public int startProcessing()
		{
			if (isProcessing_) {
				return 0;
			} else {
				faceThread_.Start ();
				isProcessing_ = true;
				return 1;
			}
		}

		public int stopProcessing()
		{
			if (isProcessing_) {
				faceThread_.Abort ();
				isProcessing_ = false;
				return 1;
			} else {
				return 0;
			}
		}

		private Boolean isProcessing_;
		private Face face_;
		private Mutex faceLock_;
		private Thread faceThread_;
	}
}

