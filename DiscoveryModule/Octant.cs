using System;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Octant class describes an octant and its attributes.
	/// Each octant is considered to be a node in octree, whose root_ is hosted by Instance class.
	/// </summary>
	public class Octant
	{
		private int index_;
		private int length_;

		private NameDataset nameDataset_;
		private DigestComponent digestComponent_;

		private bool isLeaf_;

		private Octant leftChild_;
		private Octant rightSibling_;
		private Octant parent_;

		// tracking_ indicates whether the instance is still interested in this octant.
		private bool tracking_;

		public Octant()
		{
			nameDataset_ = new NameDataset ();
			digestComponent_ = new DigestComponent ();
			tracking_ = true;

			leftChild_ = null;
			rightSibling_ = null;
			parent_ = null;
		}

		public Octant(int index, bool isLeaf = true)
		{
			nameDataset_ = new NameDataset ();
			digestComponent_ = new DigestComponent ();
			tracking_ = true;

			index_ = index;
			isLeaf_ = isLeaf;
			leftChild_ = null;
			rightSibling_ = null;
			parent_ = null;
		}

		public int getIndex()
		{
			return index_;
		}

		// the order's reverse
		public List<int> getListIndex()
		{
			List<int> index = new List<int> ();
			Octant oct = this;
			while (oct != null && oct.getIndex () != Constants.rootIndex) {
				index.Add (oct.getIndex ());
				oct = oct.parent ();
			}
			return index;
		}

		// the order's not reverse, both this method and the one above are not tested.
		// this method is not tested yet
		public string getListIndexAsString()
		{
			string indexString = "";
			Octant oct = this;
			while (oct != null && oct.getIndex () != Constants.rootIndex) {
				indexString = oct.getIndex () + "/" + indexString;
				oct = oct.parent ();
			}
			return indexString;
		}

		public Octant leftChild()
		{
			return leftChild_;
		}

		public Octant rightSibling()
		{
			return rightSibling_;
		}

		public Octant parent()
		{
			return parent_;
		}

		public void setParent(Octant parent)
		{
			parent_ = parent;
		}

		public void setRightSibling(Octant rightSibling)
		{
			rightSibling_ = rightSibling;
		}

		public bool isLeaf()
		{
			return isLeaf_;
		}

		public bool isTracking()
		{
			return tracking_;
		}

		/// <summary>
		/// Start tracking this octant.
		/// </summary>
		public void startTracking()
		{
			tracking_ = true;
			return;
		}

		/// <summary>
		/// Stop tracking this octant only.
		/// </summary>
		public void stopTracking()
		{
			tracking_ = false;
			return;
		}

		public bool hasTrackingChildren()
		{
			if (isLeaf_) {
				return false;
			}

			// Built in BFS
			Queue q = new Queue ();
			q.Enqueue (this);

			Octant temp;
			while (q.Count != 0)
			{
				temp = (Octant)q.Dequeue ();
				if (temp.isTracking ()) {
					return true;
				}

				if (!temp.isLeaf ()) {
					temp = temp.leftChild ();
					while (temp != null) {
						q.Enqueue (temp);
						temp = temp.rightSibling ();
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Stop tracking this octant, and all of its child octants.
		/// </summary>
		/// >param name="stopChild">True, stops the tracking of all of its children as well. False, just stop tracking this octant.</param>
		public void stopTracking(bool stopChild)
		{
			if (!stopChild) {
				stopTracking ();
				return;
			}

			// Built in BFS
			Queue q = new Queue ();
			q.Enqueue (this);

			Octant temp;
			while (q.Count != 0)
			{
				temp = (Octant)q.Dequeue ();
				temp.stopTracking ();

				if (!temp.isLeaf ()) {
					temp = temp.leftChild ();
					while (temp != null) {
						q.Enqueue (temp);
						temp = temp.rightSibling ();
					}
				}
			}
			return;
		}

		/// <summary>
		/// Get the child of octant node by given index
		/// </summary>
		/// <returns>null, if current node does not have any children or among the children it has, none matches the index;
		/// The child octant, if there is a child that matches the given index.</returns>
		/// <param name="index">Index of the child node.</param>
		public Octant getChildByIndex(int index)
		{
			if (leftChild_ == null) {
				return null;
			} else {
				Octant temp = leftChild_;
				while (temp != null) {
					if (temp.getIndex () == index) {
						return temp;
					}
					temp = temp.rightSibling();
				}
				return null;
			}
		}

		/// <summary>
		/// Adds a child node to a certain octree node
		/// </summary>
		/// <returns>Void</returns>
		public void addChild(Octant child)
		{
			if (leftChild_ == null) {
				this.leftChild_ = child;
				child.setParent (this);
			} else {
				Octant temp = leftChild_;
				while (temp.rightSibling() != null) {
					temp = temp.rightSibling();
				}
				temp.setRightSibling (child);
				child.setParent (this);
			}
		}

		/// <summary>
		/// Remove a name from the octant's nameDataset
		/// </summary>
		/// <returns>True, if successfully removed; False, otherwise</returns>
		public bool removeName(string name)
		{
			if (isLeaf_) {
				if (!nameDataset_.removeName (name)) {
					Console.WriteLine ("Name " + name + " does not exist in octant.");
					return false;
				} else {
					return true;
				}
			} else {
				Console.WriteLine ("Trying to append names to a non-leaf node");
				return false;
			}
		}

		/// <summary>
		/// Add a name to the octant's nameDataset.
		/// </summary>
		public void addName(string name)
		{
			if (isLeaf_) {
				nameDataset_.appendName (name);
			} else {
				Console.WriteLine ("Trying to append names to a non-leaf node");
			}
		}

		/// <summary>
		/// Returns the nameDataset is the node is a leaf. 
		/// Returns all the names belonging to leaf nodes in subtrees by BFS traversal if the node is not
		/// </summary>
		/// <returns>The name dataset.</returns>
		public NameDataset getNameDataset()
		{
			if (isLeaf_) {
				return nameDataset_;
			} else {
				NameDataset result = new NameDataset();
				// Built-in BFS
				Queue q = new Queue ();
				q.Enqueue (this);
				Octant temp = new Octant ();
				while (q.Count != 0)
				{
					temp = (Octant)q.Dequeue ();
					if (!temp.isLeaf ()) {
						temp = temp.leftChild ();
						while (temp != null) {
							q.Enqueue (temp);
							temp = temp.rightSibling ();
						}
					} else {
						result.appendNames (temp.getNameDataset ());
					}
				}
				return result;
			}
		}

		/// <summary>
		/// Sets the digest component according to this octant's nameDataset
		/// </summary>
		public void setDigestComponent()
		{
			digestComponent_ = new DigestComponent (this);
		}

		public DigestComponent getDigestComponent()
		{
			return digestComponent_;
		}
	}
}

