using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CiDy
{
    [System.Serializable]
    public class CiDyEdge
    {

        public Vector3 value = Vector3.zero;

        public string name = "";
        //This class holdes two Vector3s as its end points.
        public CiDyNode v1;
        public CiDyNode v2;
        public Vector3 pos1;
        public Vector3 pos2;
        [HideInInspector]
        public Vector2 splitPoint;//This is used for Splitting an Edge road Points list into Two.

        public CiDyEdge(CiDyNode vA, CiDyNode vB)
        {
            v1 = vA;
            v2 = vB;
            SetName();
            pos1 = v1.position;
            pos2 = v2.position;
        }

        public CiDyEdge(CiDyNode vA, CiDyNode vB, int nullInt)
        {
            //Sort by x and z;
            v1 = vA;
            v2 = vB;
            SetName();
            pos1 = v1.position;
            pos2 = v2.position;
        }
        //Used for random Edges that are just holding two positions. :)
        public CiDyEdge(Vector3 v1, Vector3 v2)
        {

            pos1 = v1;
            pos2 = v2;
        }

        public void CorrectEdge(Vector3 _V2)
        {
            v2.position = _V2;
            //Update V2
            pos2 = _V2;

            //Value equals both combined
            value = new Vector3(pos1.x + pos2.x, pos1.y + pos2.y, pos1.z + pos2.z);
        }

        public void CorrectEdge(CiDyNode _V2)
        {
            //Update V2
            v2 = _V2;
            pos2 = _V2.position;

            //Value equals both combined
            value = new Vector3(pos1.x + pos2.x, pos1.y + pos2.y, pos1.z + pos2.z);
        }

        public void SeperateEdge()
        {
            //Debug.Log ("Seperate Edge");
            //Beak the node connectins
            v1.RemoveNode(v2);
            v2.RemoveNode(v1);
        }

        //Called from graph when connection is official
        public void ConnectNodes()
        {
            //Debug.Log ("Connect Nodes "+name);
            //User out nodes. :)
            v1.AddNode(v2);
            v2.AddNode(v1);
        }

        public void UpdateNodesPos(CiDyNode nodeA, CiDyNode nodeB)
        {
            v1 = nodeA;
            v2 = nodeB;
            pos1 = v1.position;
            pos2 = v2.position;
        }

        void SetName()
        {
            //Sort by x and z;
            if (v1.nodeNumber < v2.nodeNumber)
            {
                //v1 = vA;
                //v2 = vB;
                name = v1.name + v2.name;
            }
            else
            {
                ///v1 = vB;
                //v2 = vA;
                name = v2.name + v1.name;
            }
        }
    }
}
