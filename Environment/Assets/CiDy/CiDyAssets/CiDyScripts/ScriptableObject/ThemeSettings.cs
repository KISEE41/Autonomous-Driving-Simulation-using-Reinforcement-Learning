using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CiDy
{
    [CreateAssetMenu(fileName = "Data", menuName = "CiDy/CiDyThemeSettings ScriptableObject", order = 1)]
    public class ThemeSettings : ScriptableObject
    {
        //Cell Settings, Building Placement
        public bool randomizeSeed = true;
        public bool autoFillBuildings = true;
        public bool huddleBuildings = true;
        //Sidewalk Settings
        public bool createSideWalks = true;
        public float sidewalkHeight = 5.4f;
        public float sidewalkWidth = 0.1618f;
        //Lot Settings
        public float lotWidth = 50;
        public float lotDepth = 50;
        public float lotInset = 0;
        public bool lotsUseRoadHeight = false;
        //SideWalk Light Settings
        public bool contourSideWalkLights = false;
        public float lightSpacing = 20;
        //Clutter Settings
        public GameObject streetLightPrefab;
        public bool contourSideWalkClutter = true;
        public bool randomizeClutterPlacement = false;
        public float clutterSpacing = 30;
        //Road Settings


    }
}