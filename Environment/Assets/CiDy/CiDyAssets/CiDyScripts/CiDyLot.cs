using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CiDy
{
	[System.Serializable]
	public class CiDyLot
	{

		//Lot Boundary
		public List<Vector3> lotPrint;
		//LotBoundary Roads with Road Access
		public List<CiDyListWrapper> roadSides;
		//Determined Forward Direction from Lot Center
		public Vector3 fwd;
		//Does lot have Building on it?
		public bool empty;
		//Building FootPrint
		public List<Vector3> footPrint;//Set By Editor
									   //Does lot know where the Door is?
		public bool useDoor;//Set By Editor
							//Location of Door in Lot Area.
		public GameObject doorPos;//Set By Editor
		public GameObject[] buildings;//The Building on this Lot.

		//Initilizer Requires the Lot Points in cyclic format. Lists of Sides With Road Access, Desired Direction to be Front Facing for Building.
		public CiDyLot(List<Vector3> newLot, List<CiDyListWrapper> roadAccess, Vector3 front)
		{
			lotPrint = new List<Vector3>(newLot);
			roadSides = new List<CiDyListWrapper>(roadAccess);
			fwd = front;
			empty = true;
		}

		//Null Initilization
		public CiDyLot()
		{
			lotPrint = new List<Vector3>(0);
			roadSides = new List<CiDyListWrapper>(0);
			fwd = Vector3.zero;
		}

		public void SetBuildings(GameObject[] newBuildings)
		{
			//Debug.Log("Set Buildings: " + newBuildings.Length);
			buildings = newBuildings;
			if (empty)
			{
				empty = false;//Update Cell State
			}
		}
	}
}