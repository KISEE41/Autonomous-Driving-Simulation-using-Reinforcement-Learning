using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace CiDy
{
    [CustomEditor(typeof(CiDyCell))]
    //[CanEditMultipleObjects]
    public class CiDyCellEditor : Editor
    {
        //Cell
        CiDyCell cell;

        void OnEnable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                cell = target as CiDyCell;
            }
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            serializedObject.Update();
            EditorGUILayout.LabelField("Cell District Theme:");
            if (cell.districtType >= cell.graph.districtTheme.Length)
            {
                cell.districtType = 0;
            }
            cell.districtType = EditorGUILayout.Popup(cell.districtType, cell.graph.districtTheme);

            EditorGUILayout.Space();
            GUILayout.Label("---Cell Generation---", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate Cell",GUILayout.Height(60)))
            {
                //Update Cell
                if (cell)
                    cell.UpdateCell();
            }
            //Expose Variables for this cell.GUILayout.Label ("Building Type", EditorStyles.boldLabel);
            //cell.buildingType = (CiDyCell.BuildingType)EditorGUILayout.EnumPopup("BuildingType: ", curCell.buildingType);
            EditorGUILayout.Space();
            GUILayout.Label("Building Placement SEED:", EditorStyles.boldLabel);
            cell.randomSeedOnRegenerate = EditorGUILayout.Toggle("Randomize Seed,:", cell.randomSeedOnRegenerate);
            cell.seedValue = EditorGUILayout.IntField("Building Seed: ", cell.seedValue);
            //Procedural Buildings are Not Ready for public use yet.
            //AutoFillPrefabBuildings
            EditorGUILayout.Space();
            GUILayout.Label("Automatically Fill Cell Buildings from Theme Folder:", EditorStyles.boldLabel);
            cell.autoFillBuildings = EditorGUILayout.Toggle("AutoFillBuildings", cell.autoFillBuildings);
            EditorGUILayout.Space();
            GUILayout.Label("Building Spawn Logic:", EditorStyles.boldLabel);
            //Group Buildings in a Lot as tight as possible
            cell.huddleBuildings = EditorGUILayout.Toggle("Huddle Buildings", cell.huddleBuildings);
            if (!cell.huddleBuildings)
            {
                //Match Lots with Buildings that Use maximum Space of Lot
                cell.maximizeLotSpace = EditorGUILayout.Toggle("MaximizeLotSpace", cell.maximizeLotSpace);
            }
            EditorGUILayout.Space();
            GUILayout.Label("Sidewalk Parameters:", EditorStyles.boldLabel);
            cell.createSideWalks = EditorGUILayout.Toggle("Create Side Walks", cell.createSideWalks);
            if (cell.createSideWalks)
            {
                //Editable Variables
                //SideWalk
                GUILayout.BeginHorizontal();
                GUILayout.Label("SideWalkWidth:");
                cell.sideWalkWidth = EditorGUILayout.FloatField(Mathf.Clamp(cell.sideWalkWidth, 1, Mathf.Infinity), GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("SideWalkHeight:");
                cell.sideWalkHeight = EditorGUILayout.FloatField(Mathf.Clamp(cell.sideWalkHeight, 0.1f, Mathf.Infinity), GUILayout.Width(50));
                GUILayout.EndHorizontal();
                /*GUILayout.BeginHorizontal();
                GUILayout.Label("SideWalkEdgeWidth:");
                cell.sideWalkEdgeWidth = EditorGUILayout.FloatField(Mathf.Clamp(cell.sideWalkEdgeWidth, 0.1f, Mathf.Infinity), GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("SideWalkEdgeHeight:");
                cell.sideWalkEdgeHeight = EditorGUILayout.FloatField(Mathf.Clamp(cell.sideWalkEdgeHeight, 0.1f, Mathf.Infinity), GUILayout.Width(50));
                GUILayout.EndHorizontal();*/
            }
            EditorGUILayout.Space();
            GUILayout.Label("Lot size Parameters:", EditorStyles.boldLabel);
            //Lot Dimensions
            GUILayout.BeginHorizontal();
            GUILayout.Label("LotWidth:");
            cell.lotWidth = EditorGUILayout.FloatField(cell.lotWidth, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("LotDepth:");
            cell.lotDepth = EditorGUILayout.FloatField(cell.lotDepth, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("LotInset:");
            cell.lotInset = EditorGUILayout.FloatField(Mathf.Clamp(cell.lotInset, 0, Mathf.Infinity), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            cell.lotsUseRoadHeight = EditorGUILayout.Toggle("LotsUseRoadHeight", cell.lotsUseRoadHeight);
            EditorGUILayout.Space();
            GUILayout.Label("Sidewalk Clutter Parameters:", EditorStyles.boldLabel);
            //Street Lights/Clutter
            cell.contourSideWalkLights = EditorGUILayout.Toggle("ContourSideWalkLights", cell.contourSideWalkLights);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Light Spacing:");
            cell.pathLightSpacing = EditorGUILayout.FloatField(cell.pathLightSpacing, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            cell.pathLight = (GameObject)EditorGUILayout.ObjectField("Street Light", cell.pathLight, typeof(GameObject), false, GUILayout.Width(275));
            cell.contourSideWalkClutter = EditorGUILayout.Toggle("Contour SideWalk Clutter", cell.contourSideWalkClutter);
            cell.randomizeClutterPlacement = EditorGUILayout.Toggle("Randomize Clutter Placement", cell.randomizeClutterPlacement);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Clutter Spacing:");
            cell.pathClutterSpacing = EditorGUILayout.FloatField(cell.pathClutterSpacing, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            //scrollPos = GUILayout.BeginScrollView(scrollPos, true, true, GUILayout.Width(325), GUILayout.Height(100));
            EditorGUILayout.Space();
            GUILayout.Label("Clutter Prefabs:", EditorStyles.boldLabel);
            //Handle Street Clutter Array
            SerializedObject so = new SerializedObject(cell);
            SerializedProperty clutter = so.FindProperty("clutterObjects");
            EditorGUILayout.PropertyField(clutter, true);
            EditorGUILayout.Space();
            GUILayout.Label("Building Prefabs:", EditorStyles.boldLabel);
            //Handle Building Array
            SerializedProperty buildings = so.FindProperty("prefabBuildings");
            EditorGUILayout.PropertyField(buildings, true);
            //Apply Changes to SerializedObject
            so.ApplyModifiedProperties();
            //GUILayout.EndArea();
            //GUILayout.EndScrollView();
            //Apply Modified Properties
            serializedObject.ApplyModifiedProperties();
        }
    }
}
