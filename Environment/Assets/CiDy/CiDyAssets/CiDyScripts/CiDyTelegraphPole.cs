using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace CiDy
{

    //This Script will allow the User to Setup a Prefab for Procedural Telegraph Poles.
    public class CiDyTelegraphPole : MonoBehaviour
    {
        //The Cable Points that the User has defined for this TelegraphPole.
        public List<Vector3> cablePoints;
        [HideInInspector]
        public Transform ourTrans;
        //TODO Add ID display and Button that allows us to remove a Specific Cable Point.
        private void Awake()
        {
            GetTransform();
        }
        //This will Add a New Point for Editing
        public void AddPoint()
        {
            if (cablePoints == null)
            {
                cablePoints = new List<Vector3>(0);
            }
            cablePoints.Add(Vector3.zero);
        }

        void GetTransform()
        {
            ourTrans = transform;
        }

        private readonly float sphereRadius = 0.0618f;
        private void OnDrawGizmosSelected()
        {
            if (ourTrans == null)
            {
                GetTransform();
            }
            if (cablePoints != null && cablePoints.Count > 0)
            {
                Gizmos.color = Color.yellow;
                Vector3 pos = ourTrans.position;
                for (int i = 0; i < cablePoints.Count; i++)
                {
                    Gizmos.DrawWireSphere(ourTrans.TransformVector(cablePoints[i]) + pos, sphereRadius);
                }
            }
        }
    }
}
