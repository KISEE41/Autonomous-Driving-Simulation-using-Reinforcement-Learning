using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CiDy
{

	public class CiDyRdLine
	{
		public bool isMajor = true;//Just lets us now if its a major or minor rd.
		public string name = "";
		public float roadWidth = 1f;
		public bool leftIntersect = false;
		public bool rightIntersect = false;
		public Vector3 leftIntersection;
		public Vector3 rightIntersection;
		public Vector3 cenPoint;//only set after parrallel bool test.
		public Vector3 fwdDir;//Only Set after parrallel bool test.
							  //This class holdes two Vector3s as its end points.
		/*public Vector3 v1;
		public Vector3 v2;
		public Vector3 v3;
		public Vector3 v4;*/

		public Vector3[] leftLine;
		public Vector3[] rightLine;

		/*public CiDyRdLine(Vector3 vA, Vector3 vB, Vector3 vC, Vector3 vD, float newWidth){
			//Right line
			v1 = vA;
			v2 = vB;
			//Left line
			v3 = vC;
			v4 = vD;
			//Names
			name = (v1.ToString()+v2.ToString());
			roadWidth = newWidth;
		}*/

		public CiDyRdLine(List<Vector3> leftSide, List<Vector3> rightSide, float newWidth, string newName, bool roadState)
		{
			leftLine = leftSide.ToArray();
			rightLine = rightSide.ToArray();
			//Set RoadWidth
			roadWidth = newWidth;
			//Set Name
			name = newName;//(leftSide[0].ToString()+rightSide[0].ToString());
			isMajor = roadState;
		}

		public void UpdateLeftIntersection(Vector3 newIntersesction)
		{
			leftIntersect = true;
			leftIntersection = newIntersesction;
		}

		public void UpdateRightIntersection(Vector3 newIntersection)
		{
			rightIntersect = true;
			rightIntersection = newIntersection;
		}

		public bool IsParallel()
		{
			bool parallel = false;
			//We need to test the positions of our right intersection point  to the lefts
			if (leftLine.Length > 0)
			{
				fwdDir = (leftLine[1] - leftLine[0]).normalized;
			}

			Vector3 rightPoint = Vector3.Cross(Vector3.up, fwdDir).normalized;
			//Now test angle from left intersection facing rightIntersection.
			Vector3 targetDir = (rightIntersection - leftIntersection).normalized;
			float angle = Vector3.Angle(targetDir, rightPoint);
			if (angle < 1.0f)
			{
				parallel = true;
			}
			//Update cenPoint if we are parallel
			if (parallel)
			{
				//Update Cen Point.
				cenPoint = leftIntersection;
				cenPoint = (cenPoint + (rightPoint * (roadWidth / 2)));
			}
			return parallel;
		}

		//This function will return a parallel vector if the end intersections are not parallel
		public Vector3 Parallel()
		{
			//Vector3 dir = (leftLine[1]-leftLine[0]).normalized;
			Vector3 rightPoint = Vector3.Cross(Vector3.up, fwdDir).normalized;

			/*GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			//go.transform.localScale = new Vector3(0.2f,0.2f,0.2f);
			go.name = "RightPoint for "+name+" width = "+roadWidth;
			go.transform.position = leftIntersection;
			go.transform.position += (rightPoint * roadWidth);*/

			Vector3 newIntersection = new Vector3(0, 0, 0);
			//We need to Determine which point is closer to the roads end and not the intersection.
			//Test distance from leftIntersection to its end point.
			float dist = Vector3.Distance(leftIntersection, leftLine[0]);
			float dist2 = Vector3.Distance(rightIntersection, rightLine[0]);

			if (dist2 < dist)
			{
				//Left intersection does not need changed
				//Create intersection to the right of the left intersection spaced by roadWidth
				newIntersection = leftIntersection;
				newIntersection = (newIntersection + (rightPoint * roadWidth));
				//Update CenPoint now that we are parallel
				cenPoint = leftIntersection;
				cenPoint = (cenPoint + (rightPoint * (roadWidth / 2)));
				rightIntersection = newIntersection;
			}
			else
			{
				//Right intersection does not need changed.
				//Create Intersection to the Left of Right Interserction spaced by roadWidth;
				newIntersection = rightIntersection;
				newIntersection = (newIntersection + (-rightPoint * roadWidth));
				//Update CenPoint now that we are parallel
				cenPoint = rightIntersection;
				cenPoint = (cenPoint + (-rightPoint * (roadWidth / 2)));
				leftIntersection = newIntersection;
			}
			//Return Found intersection
			return newIntersection;
		}
	}
}