using UnityEngine;
using System.Collections;

namespace CiDy
{
	public class CiDyVector3
	{
		public Vector3 pos;
		public bool isCorner = false;
		public bool isCuldesac = false;
		public int position;//0,1,2 0 = start, 1 = end, 2 = middle
		public bool insidePoly = false;

		//Initializer
		public CiDyVector3(float x, float y, float z)
		{
			pos = new Vector3(x, y, z);
		}
		//Initilizer
		public CiDyVector3(Vector3 newPos)
		{
			pos = newPos;
		}
		public void UpdatePos(Vector3 newPos)
		{
			pos = newPos;
		}
	}
}
