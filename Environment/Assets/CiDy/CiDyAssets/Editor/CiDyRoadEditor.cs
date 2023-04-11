using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace CiDy
{
    [CustomEditor(typeof(CiDyRoad))]
    //[CanEditMultipleObjects]
    public class CiDyRoadEditor : Editor
    {
        //Road being Edited
        CiDyRoad road;
        Transform roadTrans;
        //Road Level
        SerializedProperty roadLevel;
        //Lane Type
        SerializedProperty laneType;
        //Light Type
        SerializedProperty lightType;
        //Editor UI Textures for Buttons
        // Define a texture and GUIContent for the Different Editor States
        private Texture regenBtn;
        //GUI content to Nest Textures In.
        private GUIContent regenBtn_con;

        void OnEnable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                road = target as CiDyRoad;
                if (road)
                {
                    roadTrans = road.transform;
                    //Pre-Defined Spline Meshes
                    //roadLevel = serializedObject.FindProperty("roadLevel");
                    laneType = serializedObject.FindProperty("laneType");
                    //Stop Light Type
                    lightType = serializedObject.FindProperty("lightType");
                    //Grab Texture for Regenerate
                    GrabEditorUITextures();
                }
                //Hide Default Transform
                Hidden = false;
            }
        }

        void GrabEditorUITextures()
        {
            //Load a Texture (Assets/Resources/Textures/texture01.png)
            regenBtn = Resources.Load<Texture2D>("EditorUITextures/RegenerateRoadEditorTexture");
            if (regenBtn == null)
            {
                Debug.LogError("No RegenerateRoadEditorTexture.png");
            }
            //Define a GUIContent which uses the texture
            regenBtn_con = new GUIContent(regenBtn, "Regenerate Road: Allows you to Re-Generate the Current Road");
        }

        /*private void OnDisable()
        {
            Hidden = false;
        }*/

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            serializedObject.Update();
            EditorGUILayout.Space();
            GUILayout.Label("---Road Generation---", EditorStyles.boldLabel);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(regenBtn_con, GUILayout.Width(100), GUILayout.Height(100)))
            {
                if (road)
                {
                    //Call Function
                    road.Regenerate();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            EditorGUILayout.Space();
            GUILayout.Label("---Lane Type Settings---", EditorStyles.boldLabel);
            //Left/Right Hand Traffic
            road.flipStopSign = EditorGUILayout.Toggle("Flip Traffic Sign Side: ", road.flipStopSign);
            //Traffic/Stop Sign Enum
            //Pre-Defined Procedural Spline Mesh
            EditorGUILayout.PropertyField(lightType);
            //Pre-Defined Procedural Spline Mesh
            EditorGUILayout.PropertyField(laneType);
            //EditorGUILayout.PropertyField(roadLevel);
            //Spacing for Road Generation. LaneWidth, Left,Divider Lane, Right Shoulder
            road.laneWidth = EditorGUILayout.FloatField("Lane Width: ", road.laneWidth);
            road.leftShoulderWidth = EditorGUILayout.FloatField("Left Shoulder Width: ", road.leftShoulderWidth);
            road.centerSpacing = EditorGUILayout.FloatField("Divider Lane: ", road.centerSpacing);
            road.rightShoulderWidth = EditorGUILayout.FloatField("Right Shoulder Width: ", road.rightShoulderWidth);
            //Cross Walks At Intersection
            road.crossWalksAtIntersections = EditorGUILayout.Toggle("Cross Walks At Intersections: ", road.crossWalksAtIntersections);
            //One Way
            road.oneWay = EditorGUILayout.Toggle("One-Way: ", road.oneWay);
            EditorGUILayout.Space();
            GUILayout.Label("---Road Mesh Details---", EditorStyles.boldLabel);
            //Segment Length
            road.segmentLength = EditorGUILayout.IntField("Segment Length: ", road.segmentLength);
            road.flattenAmount = EditorGUILayout.IntField("Flatten Amount: ", road.flattenAmount);
            //Uvs Road Set
            road.uvsRoadSet = EditorGUILayout.Toggle("Uvs for Road: ", road.uvsRoadSet);
            EditorGUILayout.Space();
            GUILayout.Label("---Road Materials & Markings---", EditorStyles.boldLabel);
            road.createMarkings = EditorGUILayout.Toggle("Create Road Markings: ", road.createMarkings);
            //Materials that can be set for the Detailed Road Mesh
            EditorGUILayout.Space();
            GUILayout.Label("Road Material", EditorStyles.boldLabel);
            road.roadMaterial = (Material)EditorGUILayout.ObjectField(road.roadMaterial, typeof(Material), false, GUILayout.Width(150));
            EditorGUILayout.Space();
            GUILayout.Label("Divider Lane Material", EditorStyles.boldLabel);
            road.dividerLaneMaterial = (Material)EditorGUILayout.ObjectField(road.dividerLaneMaterial, typeof(Material), false, GUILayout.Width(150));
            EditorGUILayout.Space();
            GUILayout.Label("Shoulder Material", EditorStyles.boldLabel);
            road.shoulderMaterial = (Material)EditorGUILayout.ObjectField(road.shoulderMaterial, typeof(Material), false, GUILayout.Width(150));
            road.stopSign = (GameObject)EditorGUILayout.ObjectField("StopSign/Light", road.stopSign, typeof(GameObject), true);
            //TODO Add Road Level for Ground Support Logic
            /*///Ground support Variables
            GUILayout.Label("---Ground Support Settings---", EditorStyles.boldLabel);
            //TODO Add Road Level for Ground Support Logic
            switch (roadLevel.enumValueIndex)
            {
                case 0:
                    //Path
                    break;
                case 1:
                    //Road
                    //Show Snap to Ground
                    road.snapToGround = EditorGUILayout.Toggle("Contour To Terrain: ", road.snapToGround);
                    break;
                case 2:
                    //Highway
                    break;
            }
            road.supportSideWidth = EditorGUILayout.FloatField("Support Side Width: ", road.supportSideWidth);
            road.supportSideHeight = EditorGUILayout.FloatField("Support Side Height: ", road.supportSideHeight);
            road.beamBaseWidth = EditorGUILayout.FloatField("Beam-Base Width: ", road.beamBaseWidth);
            road.beamBaseHeight = EditorGUILayout.FloatField("Beam-Base Height: ", road.beamBaseHeight);*/
            /*GUILayout.Label("---Default Transform---", EditorStyles.boldLabel);
            if (GUILayout.Button("Show/Hide Default Transform"))
            {
                //Hide Default Transform
                Hidden = !Hidden;
            }*/
            //Apply Modified Properties
            serializedObject.ApplyModifiedProperties();
            /*GUILayout.Label("---Draw DEFAULT INSPECTOR<REMOVE THIS---", EditorStyles.boldLabel);
            DrawDefaultInspector();*/
        }

        //In Scene Editing
        protected virtual void OnSceneGUI()
        {
            CiDyRoad roadGraph = (CiDyRoad)target;

            if (roadGraph.cpPoints != null && roadGraph.cpPoints.Length > 0)
            {
                Vector3 ourPos = roadGraph.transform.position;
                for (int i = 0; i < roadGraph.cpPoints.Length; i++)
                {
                    if (i < roadGraph.cpPoints.Length - 1)
                    {
                        Handles.color = Color.yellow;
                        //Draw Connecting Line
                        Handles.DrawLine(roadGraph.cpPoints[i], roadGraph.cpPoints[i + 1]);
                    }
                    //Node Controls first and last position not user.
                    if (i == 0 || i == roadGraph.cpPoints.Length - 1)
                    {
                        continue;
                    }
                    //Get Directions
                    Vector3 curPos = roadGraph.cpPoints[i];
                    Vector3 nxtPos = roadGraph.cpPoints[i + 1];
                    Vector3 fwd = (nxtPos - curPos).normalized;
                    fwd.y = 0;
                    EditorGUI.BeginChangeCheck();
                    Vector3 newTargetPosition = Handles.PositionHandle(roadTrans.TransformVector(roadGraph.cpPoints[i]) + ourPos, Quaternion.Euler(fwd.x, fwd.y, fwd.z));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(roadGraph, "Change Road Control Point Position");
                        roadGraph.cpPoints[i] = newTargetPosition;
                    }
                }
            }
            //Now that the Control Points have been Drawn. Lets Draw its Projected New Position with two lines.
            //Calculate a Single Center Line.
            Vector3[] projectedCenterLine = CiDyUtils.CreateBezier(roadGraph.cpPoints, roadGraph.segmentLength);
            //Controue this 
            roadGraph.graph.ContourPathToTerrain(ref projectedCenterLine, roadGraph.blendingTerrains, !roadGraph.snapToGroundLocal);
            int projectedLength = projectedCenterLine.Length;
            //Now that we Have this Line. Offset it left by half width
            float halfWidth = roadGraph.width / 2;
            Vector3 srtDir = (projectedCenterLine[1] - projectedCenterLine[0]).normalized;
            Vector3 endDir = (projectedCenterLine[projectedLength - 1] - projectedCenterLine[projectedLength - 2]).normalized;

            Vector3[] leftLine = CiDyUtils.OffsetPath(projectedCenterLine, -halfWidth, ref srtDir, ref endDir);
            Vector3[] rightLine = CiDyUtils.OffsetPath(projectedCenterLine, halfWidth, ref srtDir, ref endDir);
            //Show This Line
            for (int i = 0; i < projectedLength; i++)
            {
                if (i < projectedLength - 1)
                {
                    Handles.color = Color.green;
                    //Draw Connecting Line
                    Handles.DrawLine(leftLine[i], leftLine[i + 1]);
                    Handles.DrawLine(rightLine[i], rightLine[i + 1]);
                }
            }
        }
        //Show/Hide Default Transform for Object
        public static bool Hidden
        {
            get
            {
                Type type = typeof(Tools);
                FieldInfo field = type.GetField("s_Hidden", BindingFlags.NonPublic | BindingFlags.Static);
                return ((bool)field.GetValue(null));
            }
            set
            {
                Type type = typeof(Tools);
                FieldInfo field = type.GetField("s_Hidden", BindingFlags.NonPublic | BindingFlags.Static);
                field.SetValue(null, value);
            }
        }
    }
}