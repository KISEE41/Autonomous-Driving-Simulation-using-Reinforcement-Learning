using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace CiDy
{
    class CiDyDeletePostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool updatedTheme = false;
            //Imported Assets
            if (importedAssets.Length > 0)
            {
                string[] stringSeparators = new string[] { "/CiDyTheme" };
                //Did the User Delete District Theme Folder?
                foreach (string str in importedAssets)
                {
                    string[] splitPath = str.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    //There is a Theme in this Folder.
                    if (splitPath.Length > 1)
                    {
                        //Yes, the User did in deed delete a CiDyTheme Folder, Update Graph Folders.
                        //Debug.Log("Added Theme: " + str);
                        updatedTheme = true;
                        //Dont need to check further.
                        break;
                    }
                }
            }
            //Deleted Assets
            if (deletedAssets.Length > 0)
            {
                string[] stringSeparators = new string[] { "/CiDyTheme" };
                //Did the User Delete District Theme Folder?
                foreach (string str in deletedAssets)
                {
                    string[] splitPath = str.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    //There is a Theme in this Folder.
                    if (splitPath.Length > 1)
                    {
                        //Yes, the User did in deed delete a CiDyTheme Folder, Update Graph Folders.
                        //Debug.Log("Deleted Theme: " + str);
                        updatedTheme = true;
                        //Dont need to check further.
                        break;
                    }
                }
            }

            if (updatedTheme)
            {
                //Update Graph Theme Folders.
                CiDyGraph graph = (CiDyGraph)SceneAsset.FindObjectOfType(typeof(CiDyGraph));
                if (graph != null)
                {
                    //Update Graph
                    graph.GrabFolders();
                }
            }
        }
    }
}