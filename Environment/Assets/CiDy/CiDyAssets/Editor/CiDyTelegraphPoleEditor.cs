using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CiDy
{
    [CustomEditor(typeof(CiDyTelegraphPole))]
    //[CanEditMultipleObjects]
    public class CiDyTelegraphPoleEditor : Editor
    {
        CiDyTelegraphPole pole;
        //Cable Points
        //SerializedProperty cableTrans;
        Transform cableTrans;

        void OnEnable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                pole = target as CiDyTelegraphPole;
                //cableTrans = serializedObject.FindProperty("ourTrans");
                cableTrans = pole.transform;
            }
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            // Show default inspector property editor
            DrawDefaultInspector();
            //serializedObject.Update();

            if (GUILayout.Button("Add Point"))
            {
                if (pole)
                {
                    //Call Function
                    pole.AddPoint();
                }
            }

            //Apply Modified Properties
            //serializedObject.ApplyModifiedProperties();
        }

        //In Scene Editing
        protected virtual void OnSceneGUI()
        {
            CiDyTelegraphPole poleGraph = (CiDyTelegraphPole)target;

            if (poleGraph.cablePoints != null && poleGraph.cablePoints.Count > 0)
            {
                Vector3 ourPos = cableTrans.position;
                for (int i = 0; i < poleGraph.cablePoints.Count; i++)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newTargetPosition = Handles.PositionHandle(cableTrans.TransformVector(poleGraph.cablePoints[i]) + ourPos, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(poleGraph, "Change Cable Point Position");
                        poleGraph.cablePoints[i] = newTargetPosition;
                    }
                }
            }
        }
    }
}