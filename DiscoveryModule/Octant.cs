using System;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace remap.NDNMOG.DiscoveryModule
{
	/// <summary>
	/// Octant class describes an octant and its attributes.
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

		public Octant()
		{
			nameDataset_ = new NameDataset ();
			digestComponent_ = new DigestComponent ();
		}

		public Octant(int index, bool isLeaf = true)
		{
			nameDataset_ = new NameDataset ();
			digestComponent_ = new DigestComponent ();

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
		public string getListIndexAsString()
		{
			string indexString = "";
			Octant oct = this;
			while (oct != null && oct.getIndex () != Constants.rootIndex) {
				indexString = oct.getIndex () + "/" + oct.getIndex ();
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
				// Built-in BFS tree traversal
				// TODO: test this BFS traversal for real case
				Queue q = new Queue ();
				q.Enqueue (this);
				Octant temp = new Octant ();
				while (q.Count != 0)
				{
					temp = (Octant)q.Dequeue ();
					if (!temp.isLeaf ()) {
						q.Enqueue (temp.leftChild ());
						while (temp.rightSibling () != null) {
							temp = temp.rightSibling ();
							q.Enqueue (temp);
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

		/*
		public string getIndexAsString()
		{
			string returnStr = "";
			int i = 0;
			for (i = 0; i < length_; i++)
			{
				returnStr += index_ [i];
				returnStr += "/";
			}
			return returnStr;
		}

		public void debugOctant()
		{
			string str = getIndexAsString ();
			Console.WriteLine (str);
		}
		*/

	}
}

