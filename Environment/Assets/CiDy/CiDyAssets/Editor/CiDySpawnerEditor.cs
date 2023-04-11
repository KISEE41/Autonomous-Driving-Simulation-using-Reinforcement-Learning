using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CiDy
{
    [CustomEditor(typeof(CiDySpawner))]
    //[CanEditMultipleObjects]
    public class CiDySpawnerEditor : Editor
    {
        CiDySpawner spawner;
        //Spawn Type
        SerializedProperty spawnType;
        //Prefab Spawn Type
        SerializedProperty prefab;
        //Power Line prefab
        SerializedProperty telegraphPrefab;
        //Power Line Cable Drop Amount
        SerializedProperty cableDrop;
        //Pre-Defined Spline Mesh Types
        SerializedProperty splineMeshType;
        //Road Markings
        SerializedProperty roadMarkingsType;
        //User Based Spline Mesh
        SerializedProperty mainMesh;
        SerializedProperty capMesh;
        void OnEnable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                spawner = target as CiDySpawner;
                spawnType = serializedObject.FindProperty("spawnType");
                //Prefab Spawn Object
                prefab = serializedObject.FindProperty("prefab");
                //Telegraph Prefab
                telegraphPrefab = serializedObject.FindProperty("telegraphPrefab");
                //Power Line Drop Value
                cableDrop = serializedObject.FindProperty("cableDrop");
                //Pre-Defined Spline Meshes
                splineMeshType = serializedObject.FindProperty("splineMeshType");
                //Road Markings
                roadMarkingsType = serializedObject.FindProperty("roadMarkings");
                //User-Defined Spline Meshes
                mainMesh = serializedObject.FindProperty("mainMesh");
                capMesh = serializedObject.FindProperty("capMesh");
            }
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            serializedObject.Update();
            //Normalized Length Offset
            spawner.normalizedOffsetMin = Mathf.Round(spawner.normalizedOffsetMin * 100) / 100;
            spawner.normalizedOffsetMax = Mathf.Round(spawner.normalizedOffsetMax * 100) / 100;
            EditorGUILayout.LabelField("Start Range:", (spawner.normalizedOffsetMin * 100).ToString() + " %");
            EditorGUILayout.LabelField("End Range:", (spawner.normalizedOffsetMax * 100).ToString() + " %");
            EditorGUILayout.MinMaxSlider("Normalized Length Range: ", ref spawner.normalizedOffsetMin, ref spawner.normalizedOffsetMax, 0, 1);
            EditorGUILayout.PropertyField(spawnType);

            switch (spawnType.enumValueIndex)
            {
                case 0:
                    //Offset Width Position
                    spawner.pathOffset = EditorGUILayout.FloatField("Path Offset: ", spawner.pathOffset);
                    //Fip Direction
                    spawner.reverseDirection = EditorGUILayout.Toggle("Reverse Direction: ", spawner.reverseDirection);
                    //PlaceOnGround
                    spawner.setToGround = EditorGUILayout.Toggle("PlaceOnGround: ", spawner.setToGround);
                    //Procedurally Spawn Prefabs along a Path
                    EditorGUILayout.PropertyField(prefab);
                    //Show Spacing
                    spawner.spacing = EditorGUILayout.FloatField("Spacing: ", spawner.spacing);
                    //World Up Option
                    spawner.worldUp = EditorGUILayout.Toggle("World Up: ", spawner.worldUp);
                    //Position Correction
                    spawner.positionCorrection = EditorGUILayout.Vector3Field("Position Correction: ", spawner.positionCorrection);
                    //Rotation Correction
                    spawner.rotationCorrection = EditorGUILayout.Vector3Field("Rotation Correction: ", spawner.rotationCorrection);
                    break;
                case 1:
                    //Offset Width Position
                    spawner.pathOffset = EditorGUILayout.FloatField("Path Offset: ", spawner.pathOffset);
                    //PlaceOnGround
                    spawner.setToGround = EditorGUILayout.Toggle("PlaceOnGround: ", spawner.setToGround);
                    //Pre-Defined Procedural Spline Mesh
                    EditorGUILayout.PropertyField(splineMeshType);
                    switch (splineMeshType.enumValueIndex)
                    {
                        case 0:
                            //Guard Rail Variables
                            //Bool Left Facing
                            spawner.leftFacing = EditorGUILayout.Toggle("Face Left: ", spawner.leftFacing);
                            //RailMaterial
                            spawner.railMaterial = (Material)EditorGUILayout.ObjectField("Rail_Material", spawner.railMaterial, typeof(Material), false);
                            //Post Material
                            spawner.postMaterial = (Material)EditorGUILayout.ObjectField("Post_Material", spawner.postMaterial, typeof(Material), false);
                            break;
                        case 1:
                            //SideWalk Variables
                            //Bool Left Facing
                            spawner.leftFacing = EditorGUILayout.Toggle("Face Left: ", spawner.leftFacing);
                            //Width
                            spawner.sideWalkWidth = EditorGUILayout.FloatField("Side_Walk_Width: ", spawner.sideWalkWidth);
                            //Height
                            spawner.sidewalkHeight = EditorGUILayout.FloatField("Side_Walk_Height: ", spawner.sidewalkHeight);
                            //Edge Width
                            spawner.sideWalkEdgeWidth = EditorGUILayout.FloatField("Side_Walk_Edge_Width: ", spawner.sideWalkEdgeWidth);
                            //Edge Height
                            spawner.sideWalkEdgeHeight = EditorGUILayout.FloatField("Side_Walk_Edge_Height: ", spawner.sideWalkEdgeHeight);
                            //SideWalk Material
                            spawner.sideWalkMaterial = (Material)EditorGUILayout.ObjectField("Side_Walk_Material", spawner.sideWalkMaterial, typeof(Material), false);
                            //SideWalk Edge Material
                            spawner.sideWalkEdgeMaterial = (Material)EditorGUILayout.ObjectField("Side_Walk_Edge_Material", spawner.sideWalkEdgeMaterial, typeof(Material), false);
                            break;
                        case 2:
                            //Barrier Variables
                            //Top Width
                            spawner.topWidth = EditorGUILayout.FloatField("Barrier_Top_Width: ", spawner.topWidth);
                            //Top Height
                            spawner.topHeight = EditorGUILayout.FloatField("Barrier_Top_Height: ", spawner.topHeight);
                            //Middle Width
                            spawner.middleWidth = EditorGUILayout.FloatField("Barrier_Middle_Width: ", spawner.middleWidth);
                            //Bottom Width
                            spawner.bottomWidth = EditorGUILayout.FloatField("Barrier_Bottom_Width: ", spawner.bottomWidth);
                            //Bottom Height
                            spawner.bottomHeight = EditorGUILayout.FloatField("Barrier_Bottom_Height: ", spawner.bottomHeight);
                            //SideWalk Material
                            spawner.barrierMaterial = (Material)EditorGUILayout.ObjectField("Barrier_Material", spawner.barrierMaterial, typeof(Material), false);
                            break;
                    }
                    break;
                /*case 2:
                    //Offset Width Position
                    spawner.pathOffset = EditorGUILayout.FloatField("Path Offset: ", spawner.pathOffset);
                    //PlaceOnGround
                    spawner.setToGround = EditorGUILayout.Toggle("PlaceOnGround: ", spawner.setToGround);
                    //Prefab Based Extruded Meshes
                    EditorGUILayout.PropertyField(mainMesh);
                    EditorGUILayout.PropertyField(capMesh);
                    break;*/
                case 2:
                    //Road Markings
                    //Offset Width Position
                    spawner.pathOffset = EditorGUILayout.FloatField("Path Offset: ", spawner.pathOffset);
                    //Reverse Direction
                    spawner.reverseDirection = EditorGUILayout.Toggle("Reverse Direction: ", spawner.reverseDirection);
                    EditorGUILayout.PropertyField(roadMarkingsType);
                    break;
                case 3:
                    //Power Lines (Uses Prefab Spawing Logic as well)
                    //Offset Width Position
                    spawner.pathOffset = EditorGUILayout.FloatField("Path Offset: ", spawner.pathOffset);
                    //PlaceOnGround
                    spawner.setToGround = EditorGUILayout.Toggle("PlaceOnGround: ", spawner.setToGround);
                    //Procedurally Spawn Prefabs along a Path
                    EditorGUILayout.PropertyField(telegraphPrefab);
                    //Show Cable Drop Amount
                    spawner.cableDrop = EditorGUILayout.FloatField("Cable Drop: ", spawner.cableDrop);
                    //Show Spacing
                    spawner.spacing = EditorGUILayout.FloatField("Spacing: ", spawner.spacing);
                    //World Up Option
                    spawner.worldUp = EditorGUILayout.Toggle("World Up: ", spawner.worldUp);
                    //Rotation Correction
                    spawner.rotationCorrection = EditorGUILayout.Vector3Field("Rotation Correction: ", spawner.rotationCorrection);
                    break;
            }

            if (GUILayout.Button("Generate Mesh"))
            {
                if (spawner)
                {
                    //Call Function
                    spawner.Generate();
                }
            }

            //Apply Modified Properties
            serializedObject.ApplyModifiedProperties();
            // Show default inspector property editor
            //DrawDefaultInspector();
        }
    }
}