using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

[InitializeOnLoad]
public class CiDyTags
{

    //STARTUP
    static CiDyTags()
    {
        CreateLayer();
    }

    //creates a new layer
    static void CreateLayer()
    {
        List<string> cidyTags = new List<string>(0);
        cidyTags.Add("Terrain");
        cidyTags.Add("Node");
        cidyTags.Add("Road");
        cidyTags.Add("Cell");
        List<string> cidyLayers = new List<string>(cidyTags);//Clone Tags.
        //Get This Projects Current Tags
        var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
        //Is it Null?
        if (asset != null)
        {
            var so = new SerializedObject(asset);//SerializedObject
            var tags = so.FindProperty("tags");//Get Tags
            //We Now have the Tags of the Project.
            var numTags = tags.arraySize;//Length of array
            // do not create duplicates, Iterate through checking for duplicates.
            for (int i = 0; i < numTags; i++)
            {
                var existingTag = tags.GetArrayElementAtIndex(i);//Get Current Tag
                for (int j = 0; j < cidyTags.Count; j++) {
                    if (existingTag.stringValue == cidyTags[j]) {
                        //Duplicate Tag, So Remove it from Tags
                        cidyTags.RemoveAt(j);
                        break;
                    }
                }
            }
            //What Tags are Left?
            for (int i = 0; i < cidyTags.Count; i++) {
                tags.InsertArrayElementAtIndex(numTags+i);
                tags.GetArrayElementAtIndex(numTags+i).stringValue = cidyTags[i];
            }
            //Now Do the Layers
            var layers = so.FindProperty("layers");//Get Tags
            //We Now have the Tags of the Project.
            var numLayers = layers.arraySize;//Length of array
            // do not create duplicates, Iterate through checking for duplicates.
            for (int i = 8; i < numLayers; i++)
            {
                var existingLayer = layers.GetArrayElementAtIndex(i);//Get Current Tag
                for (int j = 0; j < cidyLayers.Count; j++)
                {
                    if (existingLayer.stringValue == cidyLayers[j])
                    {
                        //Duplicate Tag, So Remove it from Tags
                        cidyLayers.RemoveAt(j);
                        break;
                    }
                }
            }
            //What Layers are Left? Put them into empty Spots on the Layers list.
            for (int i = 8; i < numLayers; i++)
            {
                var existingLayer = layers.GetArrayElementAtIndex(i);//Get Current Layer
                //Is this Layer Spot Empty? and do we have any left over layers that need set?
                if (existingLayer.stringValue == "" && cidyLayers.Count > 0) {
                    layers.GetArrayElementAtIndex(i).stringValue = cidyLayers[0];
                    cidyLayers.RemoveAt(0);//Update Layer
                }
            }
            //Save Changes to Project
            so.ApplyModifiedProperties();
            so.Update();
        }
    }
}