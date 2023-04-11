using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Swing.Editor;
using System.Threading;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

#if VEGETATION_STUDIO || VEGETATION_STUDIO_PRO
    using AwesomeTechnologies.VegetationSystem.Biomes;
#endif
//MapMAgic
#if MAPMAGIC2
using MapMagic.Core;
#endif

namespace CiDy
{
    public class CiDyEditor : EditorWindow
    {
        private string version = "2.7.3";
        //Window Creation Functions
        [MenuItem("Window/CiDy/City Editor")]
        public static void ShowWindow()
        {
            //2018 >
#if UNITY_5
		EditorWindow.GetWindow(typeof(CiDyEditor));
#else
            EditorWindow window = GetWindow<CiDyEditor>(false, "CiDy", true);
            window.titleContent = (new GUIContent("CiDy Editor", "CiDy Editor Window, for Designing City Layouts in Scene View.")); //Set a Window Title.
            window.minSize = new Vector2(360f, 360f);
#endif
        }

        //CiDy Graph
        public CiDyGraph graph;
        //LayerMasks
        public LayerMask roadMask = -1;
        public LayerMask roadMask2;
        public LayerMask cellMask;
        public LayerMask nodeMask;
        //Tagging System(Naming)
        private readonly string terrainTag = "Terrain";
        private readonly string cellTag = "Cell";
        private readonly string roadTag = "Road";
        private readonly string nodeTag = "Node";
        private bool displayingProgressBar = false;
        //Backup Skip Boolean
        private bool skipBackup = true;
        //Editor UI Textures
        // Define a texture and GUIContent for the Different Editor States
        private Texture splineBtn;
        private Texture intersectionBtn;
        private Texture buildingBtn;
        private Texture trafficBtn;
        private Texture powerLineBtn;
        private Texture loopBtn;//Weather we want to Loop a spline or not.
        //GUI content to Nest Textures In.
        private GUIContent splineBtn_con;
        private GUIContent intersectionBtn_con;
        private GUIContent buildingBtn_Con;
        private GUIContent trafficBtn_con;
        private GUIContent powerLineBtn_con;
        private GUIContent loopBtn_con;
        //Editor UI Textures for Buttons
        // Define a texture and GUIContent for the Different Editor States
        //Clear Graph
        private Texture clearGraphBtn;
        //GUI content to Nest Textures In.
        private GUIContent clearGraphBtn_con;
        //Grab Terrain Heights
        private Texture grabHeightsGraphBtn;
        //GUI content to Nest Textures In.
        private GUIContent grabHeightsGraphBtn_con;
        void SceneOpended(Scene newScene, OpenSceneMode mode)
        {
            if (graph == null)
            {
                InitializeGraph();
            }
            if (graph != null)
            {
                //Update Graph Folders
                graph.GrabFolders();
            }
            //Verfiy Terrains
            if (graph)
            {
                graph.VerifyTerrains();
            }
        }

        void OnSceneSaved(Scene scene)
        {
            //Debug.Log("OnSceneSaved: " + scene.name);
            //Clear CiDyGraph references to Scene Terrains.
            if (graph)
            {
                //If Multiple Scenes and Terrains are Not Null, We will clear there Scene Object Reference. (Workaround to Cross Scene Reference Error)
                graph.VerifyTerrains();
            }
            //Debug.LogFormat("Saved scene '{0}'", scene.name);
        }

        // register an event handler when the class is initialized
        void ModeStateChanged(PlayModeStateChange state)
        {
            //Debug.Log("Mode State Changed: " + state);
            //Re-Graph CiDy Cell Graph
            if (graph == null) {
                InitializeGraph();
            }

            //Entering Edit Mode?
            if ((state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) && !enterEditMode) {
                enterEditMode = true;
                //Enter Edit Mode
                ResetColliders();
            } else if ((state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode) && enterEditMode)
            {
                enterEditMode = false;
                //Enter Play Mode
                //Save Current State of User Settings for Collider Visuals
                savedShowNodes = showNodes;
                savedShowCells = showCells;
                //Debug.Log("Saved States, Nodes: " + savedShowNodes + " Cells: " + savedShowCells);
                //Turn off Cells and Node Colliders for Convienence
                //Turn off Node Colliders
                if (graph == null) {
                    InitializeGraph();
                }

                if (graph)
                {
                    //Now Turn them Both Off
                    showNodes = false;
                    graph.DisableNodeGraphics();
                    //Turn off Cell Colldiers
                    showCells = false;
                    graph.DisableCellGraphics();
                }
                UpdateState();
            }
        }

        void OnDestroy()
        {
            UpdateState();
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
#if UNITY_2018
        //2018 version
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
#elif UNITY_2019 || UNITY_2020
            //2019 and Up
            SceneView.duringSceneGui -= this.OnSceneGUI;
            EditorSceneManager.sceneOpened -= SceneOpended;//Scene has been Opened
            EditorSceneManager.sceneSaved -= OnSceneSaved;//Scene has been Saved
            EditorApplication.playModeStateChanged -= ModeStateChanged;
#endif
        }

        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= SceneOpended;//Scene has been Opened
            EditorSceneManager.sceneSaved -= OnSceneSaved;//Scene has been Saved
            EditorApplication.playModeStateChanged -= ModeStateChanged;
        }

        // Window has been selected
        void OnFocus()
        {
            // Remove delegate listener if it has previously
            // Add (or re-add) the delegate.
#if UNITY_2018
        ///2018 version
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
#elif UNITY_2019 || UNITY_2020
            //2019 and Up Version
            SceneView.duringSceneGui -= this.OnSceneGUI;
            SceneView.duringSceneGui += this.OnSceneGUI;
            EditorSceneManager.sceneOpened -= SceneOpended;//Scene has been Opened
            EditorSceneManager.sceneOpened += SceneOpended;//Scene has been Opened
            EditorSceneManager.sceneSaved -= OnSceneSaved;//Scene has been Saved
            EditorSceneManager.sceneSaved += OnSceneSaved;//Scene has been Saved
            EditorApplication.playModeStateChanged -= ModeStateChanged;
            EditorApplication.playModeStateChanged += ModeStateChanged;
#endif
        }

        //Grab or Create a CiDyGraph
        void OnEnable()
        {
            //Debug.Log ("OnEnable CiDyWindow");
            EditorSceneManager.sceneOpened += SceneOpended;//Scene Opened
            EditorSceneManager.sceneSaved += OnSceneSaved;//Scene is Saved
            EditorApplication.playModeStateChanged += ModeStateChanged;
            //Grab UI Textures
            GrabEditorUITextures();
            //Check for an existing graph
            //hideFlags = HideFlags.HideAndDontSave;
            //Set Layer Masks
            //Set Road Searching Mask
            roadMask = 1 << LayerMask.NameToLayer(terrainTag);
            roadMask2 = 1 << LayerMask.NameToLayer(roadTag);
            cellMask = 1 << LayerMask.NameToLayer(cellTag);
            nodeMask = (1 << LayerMask.NameToLayer(nodeTag) | 1 << LayerMask.NameToLayer(terrainTag));
            //Grab Material Resources
            //Grab Node Material Resources
            nodeMaterial = Resources.Load("CiDyResources/NodeMaterial", typeof(Material)) as Material;
            //Grab Active Node Material Resource
            activeMaterial = Resources.Load("CiDyResources/ActiveMaterial", typeof(Material)) as Material;
            //Cell Selection Material
            cellSelectionMaterial = Resources.Load("CiDyResources/CellSelection", typeof(Material)) as Material;
            cellMaterial = Resources.Load("CiDyResources/CellTransparency", typeof(Material)) as Material;
            //Grab CiDyGraph Materials for the Intersection and Roads.
            InitializeGraph(false);

            if (graph)
            {
                //Intersection
                definedIntersectionMat = graph.intersectionMaterial;
                //Road
                definedRoadMaterial = graph.roadMaterial;
                //SideWalk
                definedSideWalkMat = graph.sideWalkMaterial;
            }
            //Update Graph Visual State and References to CurRoad etc
            UpdateState();
        }

        void GrabEditorUITextures()
        {
            //Debug.Log("Grab Editor UI Textures");
            clearGraphBtn = Resources.Load<Texture2D>("EditorUITextures/ClearGraphEditorTexture");
            //Define a GUIContent which uses the texture
            clearGraphBtn_con = new GUIContent(clearGraphBtn);
            clearGraphBtn_con.tooltip = "Clear Graph: Allows you to Completely Clear the CiDy Graph";
            //Grab Terr Heights
            grabHeightsGraphBtn = Resources.Load<Texture2D>("EditorUITextures/GrabHeightsEditorTexture");
            //Define a GUIContent which uses the texture
            grabHeightsGraphBtn_con = new GUIContent(grabHeightsGraphBtn);
            grabHeightsGraphBtn_con.tooltip = "Grab Terrain Heights: Sets the Current Terrain Heights as Default Heights for CiDy Graph. (use this after you have made a change to terrain heights)";
            /*//Spline Editor Texture
            //splineBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/WindingRoadEditorTexture.png", typeof(Texture));
            //Load a Texture (Assets/Resources/Textures/texture01.png)
            splineBtn = Resources.Load<Texture2D>("EditorUITextures/WindingRoadEditorTexture");
            if (splineBtn == null)
            {
                Debug.LogError("No Winding Road Editor Texture .png");
            }
            //Define a GUIContent which uses the texture
            splineBtn_con = new GUIContent(splineBtn);
            splineBtn_con.tooltip = "Spline Editor: Allows you to Create/Modify the CiDy Road Network";
            //Traffic Editor Texture
            //trafficBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/TraiffcEditorTexture.png", typeof(Texture));
            trafficBtn = Resources.Load<Texture2D>("EditorUITextures/TraiffcEditorTexture");
            //Define a GUIContent which uses the texture
            trafficBtn_con = new GUIContent(trafficBtn);
            trafficBtn_con.tooltip = "Traffic Editor: Allows you to Create/Modify the CiDy Traffic Settings";
            //Intersections
            //intersectionBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/IntersectionEditorTexture.png", typeof(Texture));
            intersectionBtn = Resources.Load<Texture2D>("EditorUITextures/IntersectionEditorTexture");
            //Define a GUIContent which uses the texture
            intersectionBtn_con = new GUIContent(intersectionBtn);
            intersectionBtn_con.tooltip = "Intersection Editor: Allows you to Create/Modify the CiDy Intersections";
            //Telephone Poles/Power Lines
            //powerLineBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/PowerLineEditorTexture.png", typeof(Texture));
            powerLineBtn = Resources.Load<Texture2D>("EditorUITextures/PowerLineEditorTexture");
            //Define a GUIContent which uses the texture
            powerLineBtn_con = new GUIContent(powerLineBtn);
            powerLineBtn_con.tooltip = "Power Line Editor: Allows you to Create/Modify the Telephone/PowerLine Network";
            //Telephone Poles/Power Lines
            //buildingBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/BuildingEditorTexture.png", typeof(Texture));
            buildingBtn = Resources.Load<Texture2D>("EditorUITextures/BuildingEditorTexture");
            //Define a GUIContent which uses the texture
            buildingBtn_Con = new GUIContent(buildingBtn);
            buildingBtn_Con.tooltip = "Building Editor: Allows you to Edit any of the Buildings.";
            //Loop Btn
            //loopBtn = (Texture)AssetDatabase.LoadAssetAtPath("Assets/CiDy/CiDyAssets/Resources/EditorUITextures/LoopEditorTexture.png", typeof(Texture));
            loopBtn = Resources.Load<Texture2D>("EditorUITextures/LoopEditorTexture");
            //Define a GUIContent which uses the texture
            loopBtn_con = new GUIContent(loopBtn);
            loopBtn_con.tooltip = "Loop: Allows you to Change weather selected spline is looped.";*/
        }

        //Handle Terrain Objects
        SerializedObject graphSO;
        void CreateGraph()
        {
            //Debug.Log("Made Graph");
            GameObject go = new GameObject("CiDyGraph");
            go.transform.position = new Vector3(0, 0, 0);
            graph = (CiDyGraph)go.AddComponent(typeof(CiDyGraph));
            graph.InitilizeGraph();
            graph.EnableCellGraphics();
            graph.EnableNodeGraphics();
            //Serialize Graph object
            graphSO = new SerializedObject(graph);
        }
        //SceneView MovementToggle
        //[Range(0.1f, 2f), Tooltip("If Use Camera is On, CiDy scene view interaction will stop.")]
        //public bool useSceneCamera = false;
        public bool usingSceneCam = false;//Dynamic State Tester
        private bool showHelp = false;
        //Visual Toggles
        public bool showCells = true;
        [SerializeField]
        private bool savedShowCells = true;
        public bool showNodes = true;
        [SerializeField]
        private bool savedShowNodes = true;
        //Terrain Variables
        public bool grabVegitation = false;//
        public bool grabAllTerrains = false;
        public bool grabHeights = false;
        public bool resetHeights = false;
        public bool blendTerrain = false;
        public bool updateCityGraph = false;
        //public bool generateSideWalkPedestrians = false;//Weather or not we add Population Module.
        public bool generateRoad = false;
        public int userRoadSegmentLength = 6;
        //Clear Graph Bool
        public bool clearGraph = false;
        //Node Variables
        //Grab Node Material Resources
        public Material nodeMaterial;// = Resources.Load ("CiDyResources/NodeMaterial", typeof(Material)) as Material;
                                     //Grab Active Node Material Resource
        public Material activeMaterial;// = Resources.Load ("CiDyResources/ActiveMaterial", typeof(Material)) as Material;
                                       //Cell Selection Material
        public Material cellSelectionMaterial;// = Resources.Load ("CiDyResources/CellSelection", typeof(Material)) as Material;
        public Material cellMaterial;// = Resources.Load ("CiDyResources/CellTransparency", typeof(Material)) as Material;
                                     //Road
        public Material definedRoadMaterial;//The Material that the user wants to be put onto any created road meshes.
        public Material roadMaterial;//The Material that is on the Current Selected Road.
                                     //Intersection Material
        public Material definedIntersectionMat;//The Material that the user wants to be put onto any created Intersection meshes.
        public Material intersectionMaterial;//The Material that the user wants to be put onto any created intersection meshes.
        public Material definedSideWalkMat;//The Material the User wants to be put onto any created SideWalk Meshes of Cells.
        public bool replaceAllMaterials = false;//When this is true. We will Go through the CiDy System and replace the Materials applied to the Roads and Intersections.

        public int maxNodeScale = 200;
        public int nodeScale = 50;
        public int curNodeScale = 50;
        //Road Creation variables
        public int roadSegmentLength = 6;//The Mesh Resolution/Mesh Quadrilateral Segment Length.(Decreasing Length will increase GPU Cost)
        private int flattenAmount = 8;//How many points from the ends we will flatten for end Smoothing(Bezier)
        public bool uvsRoadSet = true;//If false then we will stretch Roads UV's to match a Up Right Road Texture Method
        public bool generateCrossWalks = true;//If False then we will not generate the roads CrossWalks at intersections.
                                              //Road Edit Variables
        public GameObject roadStopSign;
        public bool regenerateRoad = false;
        //Cell Edit Variables
        public float sideWalkWidth = 8f;//The Side Walk Width(Inset from Roads);
        public float sideWalkHeight = 0.25f;//The Height of the SideWalk
        public float sideWalkEdgeWidth = 4;//The Inset of the SideWalk Edge.
        public float sideWalkEdgeHeight = 0.1f;
        public float lotWidth = 50f;//Cell Building Lots
        public float lotDepth = 60f;//Cell Building Lots
        public float lotInset = 0f;//Amount the Cell Insets there Lots.
        public bool lotsUseRoadHeight = false;
        public bool autoFillBuildings = true;
        public bool huddleBuildings = true;
        public bool maximizeLotSpace = true;
        public bool createSideWalks = true;
        public bool contourSideWalkLights = false;
        public bool contourSideWalkClutter = false;
        public bool randomizeClutterPlacement = false;
        public bool regenerateCell = false;//Toggle used to Regenerate Cell Parameters
        private bool usePrefabBuildings = true;//Use Prefab Buildings for Cell Creation
        public float pathClutterSpacing;
        public float pathLightSpacing;
        public GameObject streetLight;
        //Current Selected Objects
        public CiDyNode curData;//Cur Node Being Edited.
        public CiDyRoad curRoad;//Cur Road that the user has Selected.
        public CiDyCell curCell;//Track What Cell if any we are working with.
        public float roadEditRadius = 20;
        public List<Vector3> roadLines = new List<Vector3>(0);
        public List<GameObject> roadPoints = new List<GameObject>(0);//The Visualized Road Control Points.
        public List<Vector3> cPoints = new List<Vector3>();

        public enum Options
        {
            Node = 0,
            Road = 1,
            Cell = 2
        }
        public Options selected = Options.Node;
        public Options curSelected = Options.Node;
        //Road Init Values
        private bool _flipStopSign = false;
        private CiDyRoad.RoadLevel _roadLevel = CiDyRoad.RoadLevel.Road;
        private CiDyRoad.LaneType _laneType = CiDyRoad.LaneType.TwoLane;
        private float laneWidth = 4;//Over 12 Feet US
        private float centerWidth = 0f;//
        private float leftShoulderWidth = 2;
        private float rightShoulderWidth = 2;
        [HideInInspector]
        public bool enterEditMode = true;
        //GUI ScrollPos
        public Vector2 scrollPos = Vector2.zero;
        //CiDy Cell Buttons Logic for Custom Themes
        bool showDistrictTheme = false;
        //Custom District Theme
        string customTheme = "";
        //Local Terrains
        StoredTerrain[] referencedTerrains;
        bool IsDuplicateTheme(string theme, string[] themes)
        {
            for (int i = 0; i < themes.Length; i++)
            {
                if (theme == themes[i])
                {
                    Debug.Log("Duplicate Found");
                    return true;
                }
            }
            return false;
        }
        void OnInspectorUpdate()
        {
            Repaint();
        }

        void InitializeGraph(bool createIt = false) {
            //Is there a Graph in the Scene?
            graph = FindObjectOfType<CiDyGraph>();//Grab the Graph
            if (graph != null) {
                //Serialize Graph object
                graphSO = new SerializedObject(graph);
            }
            if (createIt && graph == null)
            {
                CreateGraph();//Create it, if it doesn't exist in the Scene.
            }
        }

        void OnGUI()
        {
            if (graph == null)
            {
                EditorGUILayout.LabelField("No Scene Graph?", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Create/Re-Connect To Scene Graph: ", EditorStyles.boldLabel);
                //Show a Button that Will Create a Graph or Re-Graph Reference to it, if its in the Scene and Active.
                if (GUILayout.Button("Find Or Create Scene Graph"))
                {
                    InitializeGraph(true);
                }
                return;
            }
            //Check for Undo or Redo
            /*Event e = Event.current;
            if(e.isKey){
                if(e.control){
                    if(e.keyCode == KeyCode.Z){
                        Debug.Log("Undo");
                    } else if(e.keyCode == KeyCode.Y){
                        Debug.Log("Redo");
                    }
                } 
            }*/
            /*if (progress < secs)
                EditorUtility.DisplayProgressBar("Simple Progress Bar", "Shows a progress bar for the given seconds", progress / secs);
            else
                EditorUtility.ClearProgressBar();
            progress = (float)(EditorApplication.timeSinceStartup - startVal);*/

            //Handle Display Logic Changes
            if (displayingProgressBar)
            {
                //Update Display
                UpdateDisplay("Blending Terrain:", "Blending!", (1.0f - (graph.curProblems / graph.totalProblems)));

            }
            EditorGUI.BeginChangeCheck();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            //Integrete Builtin Undo System for Variable changes
            Undo.RecordObject(this, "Changed Settings");
            GUILayout.Label("CiDy 2, Ver. "+version, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            GUILayout.Label("---Instructions---", EditorStyles.boldLabel);
            //Help Button
            if (!showHelp)
            {
                if (GUILayout.Button("Show Help"))
                {
                    showHelp = true;
                }
            }
            else
            {
                //Show Help Text
                //Set Terrain Layers and Tags
                GUILayout.Label("Step 1.) Set Layer & Tag Terrain Object to -> Terrain");
                EditorGUILayout.Space();
                //Hold Shift for Control
                GUILayout.Label("Step 2.) Hold Shift to Activate CiDy Input Control and interact in Scene View");
                EditorGUILayout.Space();
                GUILayout.Label(" Step 3.) While Holding Shift, Right MB on Terrain to Create Node");
                EditorGUILayout.Space();
                GUILayout.Label(" Step 4.) While Holding Shift, Left MB to Select/Deselect or Move Selected Node, Road or Cell");
                EditorGUILayout.Space();
                GUILayout.Label(" Step 5.) While Holding Shift+Left Cntrl, Press Left MB to Delete Node or Road");
                //Turn Help Off
                if (GUILayout.Button("Hide Help"))
                {
                    showHelp = false;
                }
            }
            EditorGUILayout.Space();
            GUILayout.Label("---Visual Settings---", EditorStyles.boldLabel);
            //useSceneCamera = EditorGUILayout.Toggle(new GUIContent("Use Scene Camera", "Toggles CiDy Controls in Scene View."), useSceneCamera);
            showCells = EditorGUILayout.Toggle(new GUIContent("Show Cells", "Toggles CiDy Cell Colliders On/Off."), showCells);
            showNodes = EditorGUILayout.Toggle(new GUIContent("Show Nodes", "Toggles CiDy Node Colliders On/Off."), showNodes);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("MaxNodeScale:", "Set Maximum Scale for Node Scale Ranges."), EditorStyles.boldLabel);
            maxNodeScale = EditorGUILayout.IntField(maxNodeScale, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            nodeScale = EditorGUILayout.IntSlider(new GUIContent("NodeScale:", "Set Current Scale for Nodes in Scene."), nodeScale, 1, maxNodeScale);//,GUILayout.Width(50));// (nodeScale,GUILayout.Width(50));
            EditorGUILayout.Space();
            GUILayout.Label("--Terrains--", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Grab All Terrains", "Update Graph's Stored Terrain Data. Data is used when Laying Roads & Blending or Reseting Terrain")))
            {
                grabAllTerrains = true;
            }
            GUILayout.Label("--Active Terrains (Not Editable, Use Grab All Btn)--", EditorStyles.boldLabel);
            //Update Serialized Graph if its different on display count
            SerializedProperty terrains = graphSO.FindProperty("sceneTerrains");
            if (graph.sceneTerrains.Length != terrains.arraySize) {
                //Update
                graphSO = new SerializedObject(graph);
                terrains = graphSO.FindProperty("sceneTerrains");
            }
            //The variables and GameObject from the GameObject script are displayed in the Inspector and have the appropriate label
            EditorGUILayout.PropertyField(terrains, true);//Display List of Terrains
            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            //graphSO.ApplyModifiedProperties();//Update Any Changes.
            EditorGUILayout.Space();
            GUILayout.Label("---Global Graph Settings---", EditorStyles.boldLabel);
            GUILayout.BeginVertical();
            if (GUILayout.Button(new GUIContent("Clear Graph", "Clears City from Scene & Resets Heights & Vegetation.")))
            {
                clearGraph = true;
            }
            if (GUILayout.Button(new GUIContent("Grab Terrain Heights", "Update Graph's Stored Height Data. Data is used when Laying Roads & Blending or Reseting Terrain"))) {
                grabHeights = true;
            }
            if (GUILayout.Button(new GUIContent("Grab Terrain Vegetation", "Update Graph's Stored Vegetation Data. Data is used when Blending or Reseting Terrain"))) {
                grabVegitation = true;
            }
            if (GUILayout.Button(new GUIContent("Reset Terrain to Saved Data", "Return Terrain Heights & Vegetation to Graph's Stored Values"))) {
                resetHeights = true;
            }
            if (GUILayout.Button(new GUIContent("Blend City to Terrain", "Performs a Blending of Terrain/s to All City Elements in Scene.")))
            {
                blendTerrain = true;
            }
            if (GUILayout.Button(new GUIContent("Update City Layout to Current Terrain Heights", "Regenerates All City Elements to the New Stored Terrain Heights Data. Use this After Grabbing New Terrain Heights to update City Elements to a New Set of Terrain Heights Data.")))
            {
                updateCityGraph = true;
            }
            //Left or Right Hand Traffic
            //Left Hand Traffic
            graph.globalLeftHandTraffic = EditorGUILayout.Toggle(new GUIContent("Left Hand Traffic Routes:", "Update Graph's Left Hand Traffic Routes when Generated"), graph.globalLeftHandTraffic);
            /*if (GUILayout.Button(new GUIContent("Regenerate City Traffic Routes", "Regenerates All City Traffic Routes and is Stored in there Respective Components, Roads & Intersections Data.")))
            {
                graph.RegenerateTrafficData();
            }*/
            GUILayout.EndVertical();
            //clearGraph = EditorGUILayout.Toggle ("Clear Graph", clearGraph);//This is the Boolean visual Toggle
            if (clearGraph && EditorUtility.DisplayDialog("Clear Graph?", "This cannot be Un-done! You sure you want to Clear the CiDyGraph?", "Yes", "No"))
            {
                clearGraph = false;
                UpdateState();
                EditorCoroutine.Start(graph.ClearGraph());
            }
            else if (clearGraph)
            {
                clearGraph = false;
            }
            EditorGUILayout.Space();
            if (grabAllTerrains && EditorUtility.DisplayDialog("Grab All Terrains?", "Are you sure you want to Grab All Terrains? This will reset any previous blending?", "Yes", "No"))
            {
                grabAllTerrains = false;
                graph.GrabOriginalHeights(true);//Grab New Terrains
            } else
            {
                grabAllTerrains = false;
            }
            EditorGUILayout.Space();
            GUILayout.Label("Terrain Settings", EditorStyles.boldLabel);
            //grabHeights = EditorGUILayout.Toggle(new GUIContent("Grab Terrain Heights:", "Update Graph's Stored Height Data. Used when Blending/Reseting Terrain"), grabHeights);
            if (grabHeights && EditorUtility.DisplayDialog("Store Current TerrainHeights?", "Are you sure you want to Store current Terrain Data?", "Yes", "No"))
            {
                grabHeights = false;
                graph.GrabOriginalHeights();
            }
            else if (grabHeights)
            {
                grabHeights = false;
            }
            //Vegetation
            //grabVegitation = EditorGUILayout.Toggle(new GUIContent("Grab Terrain Vegitation:", "Update Graph's Stored Vegetation and Detail Data. Used when Blending/Reseting Terrain"), grabVegitation);
            if (grabVegitation && EditorUtility.DisplayDialog("Store Current Terrain Vegitation?", "Are you sure you want to Store current Terrain Data?", "Yes", "No"))
            {
                grabVegitation = false;
                graph.GrabTerrainVegetation();
            }
            else if (grabVegitation)
            {
                grabVegitation = false;
            }
            //Only Show Reset Heights When Cell Has Heights to Reset To
            if (graph.terrains != null && graph.terrains.Length > 0)
            {
                //resetHeights = EditorGUILayout.Toggle(new GUIContent("Reset Terrain Height & Vegetation Data:", "Return Terrain Heights & Vegetation to Graph's Stored Values"), resetHeights);
                if (resetHeights && EditorUtility.DisplayDialog("Reset Current Terrain To Stored Heights & Vegetation?", "Are you sure you want to Reset Terrain?", "Yes", "No"))
                {
                    resetHeights = false;
                    graph.RestoreOriginalTerrainHeights();
                }
                else if (resetHeights)
                {
                    resetHeights = false;
                }
            }
            //TODO Add GAIA Recognized Bool (To let the User decide if they want us to Call the Spawners after blending, As Calling the Spawners can be Very Long)
            //blendTerrain = EditorGUILayout.Toggle(new GUIContent("Blend Terrain:", "Performs a Blending Terrain/s to All City Elements in Scene."), blendTerrain);
            if (blendTerrain && EditorUtility.DisplayDialog("Blend Terrain To CiDyGraph?", "Are you sure you want to Blend Terrain to CiDyGraph?", "Yes", "No"))
            {
#if MAPMAGIC2
                //Check if MapMagic terrains are in the Scene.
                MapMagicObject mapObject = FindObjectOfType<MapMagicObject>();//Grab the Graph
                if (mapObject != null) {
                    //This is only possible if Map Magic is in the Project and is being used in this Scene.
                    skipBackup = true;
                }
#endif
                //Ask if they want to Save a BackUp Scene First?
                if (!skipBackup)
                {
                    if (EditorUtility.DisplayDialog("Create Backup Scene?", "Do you want to Create a Backup of your Scenes & Terrains?, Will Overwrite Previous Backup", "Save Backup", "Do Not Create Backup"))
                    {
                        //Before We Backup the Scene, We Should Check for Cross Scene Reference Issues.
                        VerifyCrossSceneReference();
                        //Backup Scene
                        BackupScene();
                        //Begin Blending
                        blendTerrain = false;
                        EditorCoroutine.Start(BlendTerrain());
                    }
                    else
                    {
                        //No Backup, But Begin Blending Terrain
                        blendTerrain = false;
                        EditorCoroutine.Start(BlendTerrain());
                    }
                }
                else {
                    //Skip Backup Question
                    //No Backup, But Begin Blending Terrain
                    blendTerrain = false;
                    EditorCoroutine.Start(BlendTerrain());
                }
            }
            else if (blendTerrain)
            {
                blendTerrain = false;
            }
            //Allow the User to One Click to Update All the Roads to new terrain Heights.
            //updateCityGraph = EditorGUILayout.Toggle(new GUIContent("Update City to Terrain:", "Regenerates All City Elements to the New Stored Terrain Heights Data. Use this After Grabbing New Terrain Heights to update City Elements to a New Set of Terrain Heights Data."), blendTerrain);
            if (updateCityGraph && EditorUtility.DisplayDialog("Blend Terrain To CiDyGraph?", "Are you sure you want to Update City Graph to Terrain?", "Yes", "No"))
            {
                updateCityGraph = false;
                EditorCoroutine.Start(UpdateCityToTerrain());
            }
            else if (updateCityGraph)
            {
                updateCityGraph = false;
            }
            //Draw Population Module
            /*generateSideWalkPedestrians = EditorGUILayout.Toggle("Generate Pedestrians", generateSideWalkPedestrians);

            if (generateSideWalkPedestrians && EditorUtility.DisplayDialog("Generate Population for your CiDy's Cells?", "Are you sure you want to Generate all Population?", "Yes", "No"))
            {
                generateSideWalkPedestrians = false;

                EditorCoroutine.Start(GenerateSideWalkPopulation());
            }
            else if (generateSideWalkPedestrians)
            {
                generateSideWalkPedestrians = false;
            }*/

            EditorGUILayout.Space();
            GUILayout.Label("Theme Type", EditorStyles.boldLabel);
            if (graph.index >= graph.districtTheme.Length)
            {
                graph.index = 0;
            }
            graph.index = EditorGUILayout.Popup(new GUIContent("District Theme:", "The Current Theme, this theme will be Applied when Creating Roads and City Blocks."), graph.index, graph.districtTheme);
            //Allow the User to Add a Custom District Theme Folder
            if (!showDistrictTheme)
            {
                if (GUILayout.Button("Add Custom District Theme"))
                {
                    //This will Create a New Folder with the Users Name
                    showDistrictTheme = true;
                }
            }
            else
            {
                //Show the String Field to Name the District and Folders needed for the User's District Items.
                customTheme = EditorGUILayout.TextField(new GUIContent("Custom District Name: ", "The Name that will Be Applied to Newly Created Theme & Theme Folders in CiDyResources"), customTheme);
                if (customTheme == "")
                {
                    EditorGUILayout.HelpBox("Must Enter a Text into District Name Field:", MessageType.Warning, true);
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Theme") && customTheme != "")
                {
                    if (EditorUtility.DisplayDialog("Add Custom Theme", "Are you sure you want to Create a New Theme Folder: " + customTheme + "?", "Yes", "No"))
                    {
                        showDistrictTheme = false;//Turn off Bool
                                                  //Make sure its Not a Duplicate
                        if (!IsDuplicateTheme(customTheme, graph.districtTheme))
                        {
                            //Add New One
                            int length = graph.districtTheme.Length;
                            string[] currentThemes = new string[length + 1];//Create New Array with additional slot
                                                                            //Clone Originals
                            for (int i = 0; i < length; i++)
                            {
                                //Cancle Adding if we already have this theme string name
                                currentThemes[i] = graph.districtTheme[i];
                            }
                            //Add New Theme Name
                            currentThemes[currentThemes.Length - 1] = customTheme;
                            //Check if Directory exist
                            //Debug.Log("Checking For Directory: " + Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme);
                            //check if Directory Needs to Be Created
                            if (!Directory.Exists(Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme))
                            {
                                //Create Buildings,Clutter,StreetLight Folders
                                //Debug.Log("Creating Directory: " + Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme);
                                Directory.CreateDirectory(Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme);//Main Foler
                                                                                                                                                     //Add Accompaning BuildingPrefabs
                                Directory.CreateDirectory(Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme + "/" + customTheme + "Buildings");
                                //Add Accompaning ClutterPrefabs
                                Directory.CreateDirectory(Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme + "/" + customTheme + "Clutter");
                                //Street Light
                                Directory.CreateDirectory(Application.dataPath + "/CiDy/CiDyAssets/Resources/CiDyResources/CiDyTheme" + customTheme + "/" + customTheme + "StreetLight");
                                graph.districtTheme = currentThemes;//Copy new Theme to Source Cell.
                                AssetDatabase.Refresh();//If we dont Refresh the user would have to manually to see the Folder in the Project Heiarchy
                            }
                        }
                        customTheme = "";//Reset Original Field
                        GUIUtility.ExitGUI();
                    }
                    else
                    {
                        showDistrictTheme = false;
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("Cancel"))
                {
                    showDistrictTheme = false;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            //Per Theme Settings
            GUILayout.Label("Theme Global Settings", EditorStyles.boldLabel);
            //Intersection
            GUILayout.Label(new GUIContent("Desired Intersection Material:", "This Material Will be Applied to All Intersections after Pressing->Replace All City Materials"), EditorStyles.boldLabel);
            definedIntersectionMat = (Material)EditorGUILayout.ObjectField(definedIntersectionMat, typeof(Material), false, GUILayout.Width(150));
            //Road
            GUILayout.Label(new GUIContent("Desired Road Material:", "This Material Will be Applied to All Roads after Pressing-> Replace All City Materials"), EditorStyles.boldLabel);
            definedRoadMaterial = (Material)EditorGUILayout.ObjectField(definedRoadMaterial, typeof(Material), false, GUILayout.Width(150));
            //SideWalk
            GUILayout.Label(new GUIContent("Desired SideWalk Material: ", "This Material Will be Applied to All Sidewalks after Pressing-> Replace All City Materials"), EditorStyles.boldLabel);
            definedSideWalkMat = (Material)EditorGUILayout.ObjectField(definedSideWalkMat, typeof(Material), false, GUILayout.Width(150));
            //Allow the User to One Click to Update All Materials applied to Roads,Intersections and Sidewalks.
            replaceAllMaterials = EditorGUILayout.Toggle(new GUIContent("Replace All City Materials: ", "This will Replace All City Intersections,Roads & Sidewalk Materials with the User Defined Materials."), replaceAllMaterials);
            if (replaceAllMaterials && EditorUtility.DisplayDialog("Replace All Materials Applied to CiDyGraph?", "Are you sure you want to Replace ALL Materials of City Graph?", "Yes", "No"))
            {
                replaceAllMaterials = false;
                EditorCoroutine.Start(ReplaceAllMaterials());
            }
            else if (replaceAllMaterials)
            {
                replaceAllMaterials = false;
            }
            //Check for Graph Change 
            if (graph != null)
            {
                UpdateDefinedMaterials();
            }
            EditorGUILayout.Space();
            //EditorGUILayout.EndHorizontal();
            GUILayout.Label("Placement Settings", EditorStyles.boldLabel);
            //groupEnabled = EditorGUILayout.BeginToggleGroup ("Pattern Settings", groupEnabled);
            selected = (Options)EditorGUILayout.EnumPopup(new GUIContent("Selection: ", "This is an Exposed Refernce to the Current Element Selection Type, This is Automatically Changed based on Context."), selected);
            if (selected == Options.Node)
            {
                //Node, But Show Road Initialization Values.
                //Left/Right Hand Traffic
                _flipStopSign = EditorGUILayout.Toggle(new GUIContent("Flip Stop Sign Side: ", "This changes the Side that Roads spawn there Stop Signs/Traffic Lights"), _flipStopSign);
                //Road Level
                //TODO Implement Ground Support
                //_roadLevel = (CiDyRoad.RoadLevel)EditorGUILayout.EnumPopup(new GUIContent("Road Level: ", "This Determines if a Road is Allowed to Have Ground Support"), _roadLevel);
                //Lane Type
                _laneType = (CiDyRoad.LaneType)EditorGUILayout.EnumPopup(new GUIContent("Lane Type: ", "Determines How Many Lanes a New Road will Have."), _laneType);
                //Lane Width
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Lane Width: ", "Determines How Wide a Lane is in Meters"), EditorStyles.boldLabel);
                laneWidth = EditorGUILayout.FloatField(laneWidth, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Left Shoulder Width: ", "Determines How Wide the Left Shoulder is in Meters"), EditorStyles.boldLabel);
                leftShoulderWidth = EditorGUILayout.FloatField(leftShoulderWidth, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Divider Lane Width: ", "Determines How Wide the Divider Lane is in Meters"), EditorStyles.boldLabel);
                centerWidth = EditorGUILayout.FloatField(centerWidth, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Right Shoulder Width: ", "Determines How Wide the Right Shoulder is in Meters"), EditorStyles.boldLabel);
                rightShoulderWidth = EditorGUILayout.FloatField(rightShoulderWidth, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                //Road Segement Length
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Road Segment Length: ", "Determines Distance Between Each Road Mesh Segement, Effects Road Resolution, Smaller Number = Higher Resolution"), EditorStyles.boldLabel);
                roadSegmentLength = EditorGUILayout.IntField((int)Mathf.Clamp(roadSegmentLength, 2, Mathf.Infinity), GUILayout.Width(50));
                GUILayout.EndHorizontal();
                /*GUILayout.BeginHorizontal();
                GUILayout.Label("FlattenAmount:");
                flattenAmount = EditorGUILayout.IntField(flattenAmount,GUILayout.Width(50));
                GUILayout.EndHorizontal();*/
            }
            else if (selected == Options.Road)
            {
                //Road
                if (curRoad)
                {
                    //Show Custom Road Editor
                    GUILayout.Label(" Selected Road: " + curRoad.name);
                    GUILayout.BeginVertical();
                    //Draw Inspector for CiDySpawner
                    CiDyRoadEditor roadEditor = (CiDyRoadEditor)Editor.CreateEditor(curRoad);
                    //spawnerEditor.DrawHeader();
                    roadEditor.OnInspectorGUI();
                    DestroyImmediate(roadEditor);
                    GUILayout.EndVertical();
                    //Show Active Spawners on this Road
                    if (curRoad.spawnerSplines != null && curRoad.spawnerSplines.Count > 0)
                    {
                        GUILayout.Label("----------Sub Spawner List----------", EditorStyles.boldLabel);
                        for (int i = 0; i < curRoad.spawnerSplines.Count; i++)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Label("----------Spawner Remove Function----------", EditorStyles.boldLabel);
                            if (GUILayout.Button("Remove Spawner Spline"))
                            {
                                if (EditorUtility.DisplayDialog("Remove This Spawner?", "Are you sure you want to Remove this spawner from Road?", "Yes", "No"))
                                {
                                    curRoad.RemoveSpawnerSpline(i);
                                }
                            }
                            else
                            {
                                //spawnerEditors[i].OnInspectorGUI();
                                CiDySpawner spawner = curRoad.spawnerSplines[i];
                                if (spawner != null)
                                {
                                    //Draw Inspector for CiDySpawner
                                    CiDySpawnerEditor spawnerEditor = (CiDySpawnerEditor)Editor.CreateEditor(spawner);
                                    //spawnerEditor.DrawHeader();
                                    spawnerEditor.OnInspectorGUI();
                                    DestroyImmediate(spawnerEditor);
                                }
                            }
                            GUILayout.EndVertical();
                        }
                    }
                    //Add CiDy Spawner
                    GUILayout.Label("----------Spawner Add Function----------", EditorStyles.boldLabel);
                    if (GUILayout.Button("Add Spawner Spline"))
                    {
                        curRoad.AddSpawnSpline();
                    }
                }
                else
                {
                    /*if (graph.userDefinedRoadPnts.Count > 0) {
                        //Allow the User to One Click to Update All the Roads to new terrain Heights.
                        generateRoad = EditorGUILayout.Toggle("Generate User Road", generateRoad);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Road Segment Length:");
                        userRoadSegmentLength = EditorGUILayout.IntField((int)Mathf.Clamp(userRoadSegmentLength, 2, Mathf.Infinity), GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                        if (generateRoad)
                        {
                            generateRoad = false;
                            //Turn User Defined Road Pnts. Into a Road. :)
                            graph.CreateRoadFromKnots(graph.userDefinedRoadPnts.ToArray(), roadWidth, nodeScale, userRoadSegmentLength, flattenAmount);
                            graph.ClearUserDefinedRoadPoints();
                        }
                    }*/
                }
            }
            else if (selected == Options.Cell)
            {
                //Cell
                if (curCell)
                {
                    //Use CiDyCellEditor
                    //Draw Inspector for CiDySpawner
                    CiDyCellEditor cellEditor = (CiDyCellEditor)Editor.CreateEditor(curCell);
                    //spawnerEditor.DrawHeader();
                    cellEditor.OnInspectorGUI();
                    //cellEditor.DrawDefaultInspector();
                    DestroyImmediate(cellEditor);
                    //Show Values for Vegetation Studio
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
                GUILayout.Label("Vegetation Studio Cell's Biome Mask:", EditorStyles.boldLabel);
                curCell.generateBiomeMaskForCell = EditorGUILayout.Toggle("Generate Biome Mask", curCell.generateBiomeMaskForCell);
                if (curCell.biomeMask != null)
                {
                    curCell.biomeMask.BiomeType = (AwesomeTechnologies.VegetationSystem.BiomeType)EditorGUILayout.EnumPopup("Biome Mask Type: ", curCell.biomeMask.BiomeType);
                }
#endif
                }
            }
            EditorGUILayout.EndScrollView();
            //EditorGUI.EndChangeCheck();
            if (EditorGUI.EndChangeCheck())
            {
                //Debug.Log("Value Changed");
                EditorUtility.SetDirty(this);
            }

            //Check if Dirty
            if (GUI.changed)
            {
                DirtyScene();
            }
            //color field
            //color = EditorGUILayout.ColorField(color, GUILayout.Width(200));
            //Repaint Windows
            SceneView.RepaintAll();
        }

        void VerifyCrossSceneReference()
        {
            //Clear CiDyGraph references to Scene Terrains. Before Saving Scene
            if (graph)
            {
                //If Multiple Scenes and Terrains are Not Null, We will clear there Scene Object Reference. (Workaround to Cross Scene Reference Error)
                if (SceneManager.sceneCount > 0 && graph.terrains != null && graph.terrains.Length > 0)
                {
                    graph.ClearCrossSceneReferences();
                }
            }
        }

        //This function will simply Save the Loaded Scenes.
        void SaveScenes()
        {
            //Backup All Scenes(For a Multi Streamed Scene Setup)
            int countLoaded = SceneManager.sceneCount;
            //Debug.Log("Loaded Scenes " + countLoaded);
            //Get Reference to All Scenes.
            Scene[] loadedScenes = new Scene[countLoaded];
            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = SceneManager.GetSceneAt(i);
            }
            //Iterate through LoadedScenes and Save each Scene.
            for (int i = 0; i < loadedScenes.Length; i++)
            {
                string[] path = loadedScenes[i].path.Split(char.Parse("/"));
                path[path.Length - 1] = path[path.Length - 1];
                bool saveOK = EditorSceneManager.SaveScene(loadedScenes[i], string.Join("/", path), false);
            }
        }

        //This Function will Save a Backup Scene of All Scenes and We will need to Deep Copy the Terrains so they do not share TerrainData.
        void BackupScene()
        {
            //Debug.Log("Backup Scene");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Backing Up Scene/s:", " Saving Current Scenes: ", 0f);
#endif
            //First We Save the Current Scenes before we blend anything;
            SaveScenes();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Backing Up Scene/s:", " Backing Up Terrains: ", 0.25f);
#endif
            //Get Reference To All Terrains even Inactive ones.(Streamed Scene would have this happen to it.)
            Terrain[] allTerrains = FindObjectsOfType(typeof(Terrain)) as Terrain[];
            //Sort Terrains By X and Z Axis, 
            allTerrains = allTerrains.OrderBy(x => x.GetPosition().x).ThenBy(x => x.GetPosition().z).ToArray();
            string[] originalTerrainAssetPaths = new string[allTerrains.Length];
            //Deep Copy Each Terrain saving its Copy next to the Original Terrain Data
            for (int i = 0; i < allTerrains.Length; i++)
            {
                //Get Original Terrains Asset Path
                //string originalDataPath = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(allTerrains[i].terrainData.GetInstanceID()));
                string origPath = AssetDatabase.GetAssetPath(allTerrains[i].terrainData);
                originalTerrainAssetPaths[i] = origPath;//Store reference to its Asset Location of Backup in same sequence of terrain array.
                string[] originalDataPath = origPath.Split(char.Parse("/"));
                originalDataPath[originalDataPath.Length - 1] = "BackUp_" + originalDataPath[originalDataPath.Length - 1];
                string terrainDataPath = string.Join("/", originalDataPath);
                //Debug.Log("Terrain Data Path: "+terrainDataPath);
                //Copy Terrain Data to New Path
                AssetDatabase.CopyAsset(origPath, terrainDataPath);
                //Now that we have this cloned. Replace Reference of Scene Terrain Object to the Backup
                allTerrains[i].terrainData = (TerrainData)AssetDatabase.LoadAssetAtPath(terrainDataPath, typeof(TerrainData));
                //Also Update Collider
                allTerrains[i].gameObject.GetComponent<TerrainCollider>().terrainData = allTerrains[i].terrainData;
                if (graph != null && graph.terrains != null)
                {
                    graph.terrains[i].terrData = allTerrains[i].terrainData;
                }
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Backing Up Scene/s:", " Backing Up Scenes: ", 0.5f);
#endif
            //Now That the Scene Object Components are referencing the BackupTerrains. Lets Save the Backup Scenes.
            //Backup All Scenes(For a Multi Streamed Scene Setup)
            int countLoaded = SceneManager.sceneCount;
            //Debug.Log("Loaded Scenes " + countLoaded);
            //Get Reference to All Scenes.
            Scene[] loadedScenes = new Scene[countLoaded];
            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = SceneManager.GetSceneAt(i);
            }
            List<string> unloadedSceneNames = new List<string>(0);
            List<Scene> loadedSceneNames = new List<Scene>(0);
            //Iterate through LoadedScenes and Create Backups for each next to there current Scene Locations
            for (int i = 0; i < loadedScenes.Length; i++)
            {
                if (i == 0)
                {
                    continue;
                }
                //Debug.Log("Saved Scene: " + loadedScenes[i].path+i);
                string[] path = loadedScenes[i].path.Split(char.Parse("/"));
                path[path.Length - 1] = "BackUp_" + path[path.Length - 1];
                string backupPath = string.Join("/", path);
                //Debug.Log("Save Path: " + backupPath);
                EditorSceneManager.SaveScene(loadedScenes[i], backupPath, true);
                //Now Swap the Two Scenes. Unless we are on the Last
                //Swap Scene With Backup Scene.
                loadedSceneNames.Add(EditorSceneManager.OpenScene(backupPath, OpenSceneMode.Additive));//Add Scene Path 
                unloadedSceneNames.Add(loadedScenes[i].path);//Store its Path
                EditorSceneManager.CloseScene(loadedScenes[i], true);//Specific Scene
            }
            //Debug.Log("Final Save of Back up Scene: " + loadedScenes[0].path);
            string[] path2 = loadedScenes[0].path.Split(char.Parse("/"));
            path2[path2.Length - 1] = "BackUp_" + path2[path2.Length - 1];
            string backupPath2 = string.Join("/", path2);
            //Debug.Log("Save Path: " + backupPath2);
            EditorSceneManager.SaveScene(loadedScenes[0], backupPath2, true);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Backing Up Scene/s:", " Organizing Scene/s Hierarchy: ", 0.75f);
#endif
            //System.Array.Reverse(loadedScenes);
            //Now the Scenes that we Removed need Re-Swapped
            for (int i = 1; i < loadedScenes.Length; i++)
            {
                //Now Swap the Two Scenes. Unless we are on the Last
                //Swap Scene With Backup Scene.
                //Load Original Scene
                EditorSceneManager.OpenScene(unloadedSceneNames[i - 1], OpenSceneMode.Additive);//Add Scene.
                                                                                                //Remove Previousl Loaded Scene
                EditorSceneManager.CloseScene(loadedSceneNames[i - 1], true);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Backing Up Scene/s:", " Finalizing Backup: ", 1f);
#endif
            allTerrains = FindObjectsOfType(typeof(Terrain)) as Terrain[];
            //Sort Terrains By X and Z Axis, 
            allTerrains = allTerrains.OrderBy(x => x.GetPosition().x).ThenBy(x => x.GetPosition().z).ToArray();
            //Now We want to Switch All the terrain Data back to its Orignial Terrain Data Before we Begin Blending the Terrain.
            for (int i = 0; i < allTerrains.Length; i++)
            {
                //Return Terrain Data and Colliders to there orignal Terrain Data.
                allTerrains[i].terrainData = (TerrainData)AssetDatabase.LoadAssetAtPath(originalTerrainAssetPaths[i], typeof(TerrainData));
                //Also Update Collider
                allTerrains[i].gameObject.GetComponent<TerrainCollider>().terrainData = allTerrains[i].terrainData;
                if (graph != null && graph.terrains != null)
                {
                    graph.terrains[i].terrData = allTerrains[i].terrainData;
                }
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        void UpdateDefinedMaterials()
        {
            if (definedIntersectionMat != graph.intersectionMaterial)
            {
                //check for Null
                if (definedIntersectionMat == null)
                {
                    //EditorCoroutine.Start(graph.NotificationPopup(this, "Cannot Set Intersection Material to Null!"));
                    //Reset
                    definedIntersectionMat = graph.intersectionMaterial;
                }
                else
                {
                    //User has changed Material
                    graph.intersectionMaterial = definedIntersectionMat;
                }
            }
            if (definedRoadMaterial != graph.roadMaterial)
            {
                //check for Null
                if (definedRoadMaterial == null)
                {
                    //EditorCoroutine.Start(graph.NotificationPopup(this, "Cannot Set Road Material to Null!"));
                    //Reset
                    definedRoadMaterial = graph.roadMaterial;
                }
                else
                {
                    graph.roadMaterial = definedRoadMaterial;
                }
            }
            if (definedSideWalkMat != graph.sideWalkMaterial)
            {
                if (definedSideWalkMat == null)
                {
                    //EditorCoroutine.Start(graph.NotificationPopup(this, "Cannot Set SideWalk Material to Null!"));
                    definedSideWalkMat = graph.sideWalkMaterial;
                }
                else
                {
                    graph.sideWalkMaterial = definedSideWalkMat;
                }
            }
        }

        //This function is called by the Graph when there Is an Editor Utility Display
        public void UpdateDisplay(string title, string info, float progress)
        {

            if (!displayingProgressBar)
            {
                displayingProgressBar = true;
            }
            //Display Desired Progress Bar State
            EditorUtility.DisplayProgressBar(title, info, progress);
        }

        public void CloseDisplayProgress()
        {
            if (displayingProgressBar)
            {
                Debug.Log("Progress End");
                displayingProgressBar = false;
            }
            //End Progress
            EditorUtility.ClearProgressBar();
        }

        Thread blendThread;
        //float blendProgress = 0;
        //This will blend the Graph to its Terrain if Applicable.
        IEnumerator BlendTerrain()
        {
            if (graph == null)
            {
                yield break;
            }
            //Blend/Cut Grass and Trees
            //UpdateDisplay("Blending City With Terrain: ", ": Blending", 0);
            //Check to See if the User has stored the Original Heights in the Graph?
            if (graph.terrains.Length == 0)
            {
                graph.GrabOriginalHeights();
            }
            //Start Blending
            EditorCoroutine.Start(graph.UpdateTerrainDetails());
            //End Coroutine
            yield break;
        }

        //This Function will Replace all Materials of the Graph for Road and Intersections to the Current Graph Selected ones.
        IEnumerator ReplaceAllMaterials()
        {
            EditorCoroutine.Start(graph.UpdateAllMaterials());
            yield break;
        }

        //Function will Update all the Roads. Allowing them to Replot to the Stored Terrain Heights.
        IEnumerator UpdateCityToTerrain()
        {
            //Iterate through Roads and Replot each Road. This will also cascade down the line and Update everything else.
            EditorCoroutine.Start(graph.UpdateCityGraph());
            yield break;
        }

        //Generate SideWalk Population
        IEnumerator GenerateSideWalkPopulation()
        {
            Debug.Log("Generate SideWalk Population");
            EditorCoroutine.Start(graph.GenerateSideWalkPopulation());
            Debug.Log("Yielded SideWalk");
            yield break;
        }

        /*public void CiDyUndoRedoCallback()
        {
            Debug.Log ("Undo/Redo Performed");
            if(graph){
                graph.UpdateGraphFromUndo();
                //Now update Cycles
                UpdateSideWalk();
            }
            //Call to Graph to update information.
            //Repaint Scene View
            //SceneView.RepaintAll();
        }*/

        void OnSceneGUI(SceneView sceneView)
        {
            //Grab Current Event.
            Event e = Event.current;
            // Do your drawing here using Handles.
            // Do your drawing here using GUI.
            if (e.type == EventType.Repaint)
            {
                Handles.color = Color.yellow;
                //Draw Road Points
                if (roadPoints.Count > 0)
                {
                    for (int i = 0; i < roadPoints.Count; i++)
                    {
                        if (i == 0 || i == roadPoints.Count - 1)
                        {
                            continue;
                        }
                        //Create Shape around Point and Draw it.
                        List<Vector3> drawPoints = CiDyUtils.PlotCircle(roadPoints[i].transform.position, roadEditRadius, 3);
                        for (int j = 0; j < drawPoints.Count; j++)
                        {
                            Vector3 pointPos = drawPoints[j];
                            Vector3 nxtPoint;
                            if (j == drawPoints.Count - 1)
                            {
                                nxtPoint = drawPoints[0];
                            }
                            else
                            {
                                nxtPoint = drawPoints[j + 1];
                            }
                            //Debug.DrawLine(pointPos,nxtPoint,Color.yellow);
                            Handles.DrawLine(pointPos, nxtPoint);
                        }
                    }
                }
                if (roadLines.Count > 0)
                {
                    for (int i = 0; i < roadLines.Count - 1; i++)
                    {
                        Vector3 pointPos = roadLines[i];
                        Vector3 nxtPoint = roadLines[i + 1];
                        //Debug.DrawLine(pointPos,nxtPoint,Color.yellow);
                        Handles.DrawLine(pointPos, nxtPoint);
                    }
                }
                //Draw CurCell Lots if Applicable
                if (curCell)
                {
                    //Draw SubLots
                    for (int i = 0; i < curCell.lots.Count; i++)
                    {
                        for (int j = 0; j < curCell.lots[i].vectorList.Count; j++)
                        {
                            Vector3 p0 = curCell.lots[i].vectorList[j];
                            Vector3 p1;
                            if (j == curCell.lots[i].vectorList.Count - 1)
                            {
                                p1 = curCell.lots[i].vectorList[0];
                            }
                            else
                            {
                                p1 = curCell.lots[i].vectorList[j + 1];
                            }
                            Handles.DrawLine(p0, p1);
                        }
                    }
                }
                //Draw User Defined Road Points.
                /*if (graph.userDefinedRoadPnts.Count > 0)
                {
                    Handles.color = Color.yellow;
                    for (int i = 0; i < graph.userDefinedRoadPnts.Count - 1; i++)
                    {
                        //Debug.DrawLine(pointPos,nxtPoint,Color.yellow);
                        Handles.DrawLine(graph.userDefinedRoadPnts[i], graph.userDefinedRoadPnts[i + 1]);
                    }
                    //Draw its Resulting Bezier
                    Handles.color = Color.blue;
                    for (int i = 0; i < graph.userDefinedRoad.Count - 1; i++)
                    {
                        //Debug.DrawLine(pointPos,nxtPoint,Color.yellow);
                        Handles.DrawLine(graph.userDefinedRoad[i], graph.userDefinedRoad[i + 1]);
                    }
                }*/
            }
            /*Handles.BeginGUI();
            Handles.EndGUI();*/

            //Draw Handles Etc before UseScene Camera Logic
            if (!e.shift)// || useSceneCamera)
            {
                return;
            }
            int controlID = GUIUtility.GetControlID(0, FocusType.Passive);
            //Listen For Mouse Position in Scene View.
            if (e.isMouse)
            {
                Vector3 mousePos = Event.current.mousePosition;
                Rect rect = GetWindow<SceneView>().camera.pixelRect;

                if (mousePos.x > rect.x && mousePos.y > rect.y && mousePos.x < rect.width && mousePos.y < rect.height)
                {
                    //Debug.Log(mousePos);
                    //Inside Screen
                    Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePos);
                    RaycastHit hit;
                    if (e.button == 1)
                    {
                        //Take Control from Unity Standard SceneView
                        //GUIUtility.hotControl = controlID;
                        //If Node Interaction is Active
                        if (selected == Options.Node)
                        {
                            if (e.type == EventType.MouseDown)
                            {
                                if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, nodeMask))
                                {
                                    string hitTag = hit.collider.tag;
                                    if (hitTag == terrainTag || hitTag == cellTag)
                                    {
                                        CiDyNode newData = graph.NewMasterNode(hit.point, nodeScale);
                                        if (newData == null)
                                        {
                                            Debug.Log("Cannot Place New Node at that position.");
                                            return;
                                        }
                                        else
                                        {
                                            //Node Created, Set Scene as Dirty

                                        }
                                        //This is an acceptable place for a new Node.
                                        if (curData != null)
                                        {
                                            //Connect Nodes creating an Edge in the Graph.
                                            if (graph.ConnectNodes(curData, newData, laneWidth, roadSegmentLength, flattenAmount, _flipStopSign, _roadLevel, _laneType, leftShoulderWidth, centerWidth, rightShoulderWidth))
                                            {
                                                //Debug.Log("Connecting Nodes");
                                                //Update curNode to new Node
                                                UpdateCurNode(newData);
                                                if (!showNodes)
                                                {
                                                    ActivateVisuals();
                                                }
                                                if (graph.masterGraph.Count > 2)
                                                {
                                                    UpdateCiDy();
                                                }
                                            }
                                            else
                                            {
                                                Debug.Log("Failed Graph Test Cannot Add Connection");
                                                //Destroy new Data
                                                graph.DestroyMasterNode(newData);
                                                //Special Case We know that we can wind back the count. Manually
                                                graph.nodeCount--;
                                            }
                                        }
                                        else
                                        {
                                            //Update CurNode to Equal New Node
                                            NewCurNode(newData);
                                            if (!showNodes)
                                            {
                                                ActivateVisuals();
                                            }
                                            //Graph has Changed. Dirty Scene for Saving
                                            DirtyScene();
                                        }
                                    }
                                    else if (hitTag == nodeTag)
                                    {
                                        //The user is trying to select or connect to an exsiting Node.
                                        //Grab this node Object
                                        GameObject tmpNode = hit.collider.transform.parent.gameObject;
                                        CiDyNode newData = graph.masterGraph.Find(x => x.name == tmpNode.name);

                                        //Debug.Log(newData.name);
                                        //Do they have a connecting node selected?
                                        if (curData != null)
                                        {
                                            if (tmpNode.name == curData.name)
                                            {
                                                //Reselected our CurNode do Nothing.
                                                return;
                                            }
                                            //Connect Nodes.
                                            if (!graph.ConnectNodes(curData, newData, laneWidth, roadSegmentLength, flattenAmount, _flipStopSign, _roadLevel, _laneType, leftShoulderWidth, centerWidth, rightShoulderWidth))
                                            {
                                                Debug.Log("Couldn't Make Connection In Graph");
                                            }
                                            else
                                            {
                                                //Debug.Log("Connected Nodes, Update Sidewalk");
                                                if (graph.masterGraph.Count > 2)
                                                {
                                                    UpdateCiDy();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //We do not have a Node selected yet. Lets make this node our curNode.
                                            UpdateCurNode(newData);
                                        }
                                    }
                                }
                            }
                        }
                        else if (selected == Options.Road)
                        {
                            /*//Drawing Visual Aids
                            if (curRoad)
                            {
                                Vector2 screenPos = Event.current.mousePosition;
                                userRect = new Rect(screenPos.x - userRect.width / 2, screenPos.y - userRect.height / 2, userRect.width, userRect.height);
                                if (e.type == EventType.MouseDown)
                                {
                                    //Test area round mouse point. curEvent.mousePosition
                                    FindSelectedObjects();
                                }
                                else if (e.type == EventType.MouseUp)
                                {
                                    //User wants to select a Road CP Point.
                                    //Clear selectedPoint if we have one
                                    if (selectedPoint)
                                    {
                                        //Debug.Log("Released MoustButton");
                                        curRoad.cpPoints[pointInt] = selectedPoint.transform.position;
                                        curRoad.ReplotRoad(curRoad.cpPoints);
                                        //curRoad.UpdateRoadNodes();
                                        selectedPoint = null;
                                        //Update Graph if needed
                                        if (graph.cells.Count > 0)
                                        {
                                            graph.UpdateRoadCell(curRoad);
                                        }
                                        //Graph has Changed. Dirty Scene for Saving
                                        DirtyScene();
                                    }
                                }
                                else if (e.type == EventType.MouseDrag)
                                {
                                    //Move selected Point if not null
                                    if (selectedPoint)
                                    {
                                        //Debug.Log("Holding Mouse Button");
                                        if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, roadMask))
                                        {
                                            selectedPoint.transform.position = hit.point;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //Switch to Node Selection
                                selected = Options.Node;
                                //If No Road is currently Selected. We Assume the User wants to Click out a road.
                                //Where did they Click?
                                /*if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, roadMask))
                                {
                                    //Hit
                                    Debug.Log("Hit: "+hit.collider.tag);

                                    //Now we want to store a reference to the points. The User is creating.
                                    //If not a duplicate. Add to list.
                                    graph.AddUserRoadPoints(hit.point, userRoadSegmentLength);

                                }*//*
                            }*/
                            if (!curRoad)
                            {
                                //Switch to Node Selection
                                selected = Options.Node;
                            }
                        }
                        else if (selected == Options.Cell)
                        {

                            if (curCell == null)
                            {
                                selected = Options.Node;
                            }
                        }
                    }
                    else if (e.button == 0)
                    {
                        //Take Control from Unity Standard SceneView
                        //GUIUtility.hotControl = controlID;
                        //Take an Extra Step here to determine What the User has just Clicked On
                        if (e.type == EventType.MouseDown)
                        {
                            //Debug.Log("User Pressed Left MB");
                            if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, nodeMask) && hit.collider.tag == nodeTag)
                            {
                                //Debug.Log("Clicked on Node");
                                //Clicked on a Node?
                                selected = Options.Node;
                            }
                            else if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, roadMask2))
                            {
                                //Debug.Log("Clicked on Road");
                                //Clicked on Road?
                                selected = Options.Road;
                            }
                            else if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, cellMask))
                            {
                                //Debug.Log("Clicked on Cell");
                                //Clicked on Cell?
                                selected = Options.Cell;
                            }
                        }

                        if (selected == Options.Node)
                        {
                            if (e.type == EventType.MouseDown)
                            {
                                //Take Control from Unity Standard SceneView
                                GUIUtility.hotControl = controlID;
                                if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, nodeMask))
                                {
                                    string hitTag = hit.collider.tag;
                                    //Debug.Log("User Pressed LftMSBtn hit= "+hitTag);
                                    if (hitTag == nodeTag)
                                    {
                                        //We have hit a node.
                                        //if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift)){
                                        if (e.control)
                                        {
                                            //User Wishes to Destroy this Node and All its Edges.
                                            NewCurNode(graph.masterGraph.Find(x => x.name == hit.collider.transform.parent.name));
                                            //Does the User wish to Destroy the Node?
                                            graph.DestroyMasterNode(curData);
                                            curData = null;
                                            //Update Graph
                                            UpdateCiDy();
                                        }
                                        else
                                        {
                                            //The user wishes to select this node as the CurNode. or deselect it
                                            //User Wishes to Destroy this Node and All its Edges.
                                            if (curData != null)
                                            {
                                                if (curData.name == hit.collider.transform.parent.name)
                                                {
                                                    //Release this node
                                                    curData.ChangeMaterial(nodeMaterial);
                                                    curData = null;
                                                }
                                                else
                                                {
                                                    //curData = graph.masterGraph.Find(x=> x.name == hit.collider.transform.parent.name);
                                                    UpdateCurNode(graph.masterGraph.Find(x => x.name == hit.collider.transform.parent.name));
                                                }
                                            }
                                            else
                                            {
                                                if (graph == null)
                                                {
                                                    Debug.Log("No Graph");
                                                }
                                                UpdateCurNode(graph.masterGraph.Find(x => x.name == hit.collider.transform.parent.name));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //If we have a curNode then Move it to the New Position.
                                        if (curData != null && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                                        {
                                            //Debug.Log("Trying to Move Node "+curData.name);
                                            //Move Node to New Position if graph will allow.
                                            if (!graph.MovedNode(ref curData, hit.point))
                                            {
                                                Debug.Log("Could Not Move Node New Position Conflicts Graph");
                                            }
                                        }
                                    }
                                }
                            }
                            else if (e.type == EventType.MouseUp)
                            {
                                //Take Control from Unity Standard SceneView
                                GUIUtility.hotControl = 0;
                            }
                        }
                        else if (selected == Options.Road)
                        {
                            if (e.type == EventType.MouseDown)
                            {
                                //Take Control from Unity Standard SceneView
                                GUIUtility.hotControl = controlID;
                                //We are Only Looking for Roads
                                if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, roadMask2))
                                {
                                    string hitTag = hit.collider.tag;
                                    if (hitTag == roadTag)
                                    {
                                        //Lets handle the situations that are needed to better handle Roads.
                                        if (e.control)
                                        {
                                            if (curRoad)
                                            {
                                                DeselectRoad();
                                            }
                                            //User wants to destroy this Road from the Graph.
                                            //Debug.Log("Destroying Road "+hit.collider.name);
                                            graph.DestroyRoad(hit.collider.gameObject.name);
                                            //Now Update Graph
                                            UpdateCiDy();
                                        }
                                        else
                                        {
                                            CiDyRoad tmpRoad = null;
                                            //User wants to select this road as our curRoad.
                                            if (hit.collider.name == "DividerLane")
                                            {
                                                tmpRoad = (CiDyRoad)hit.collider.transform.parent.GetComponent<CiDyRoad>();
                                            }
                                            else
                                            {
                                                tmpRoad = (CiDyRoad)hit.collider.GetComponent<CiDyRoad>();
                                            }
                                            //See if we are trying to deselect a road.
                                            if (curRoad != null && tmpRoad != null)
                                            {
                                                if (curRoad.name == tmpRoad.name)
                                                {
                                                    //We have a selected node. Change its material back to what it was.
                                                    DeselectRoad();
                                                }
                                                else
                                                {
                                                    //We are just changing our pick
                                                    DeselectRoad();
                                                    SelectRoad(tmpRoad);
                                                }
                                            }
                                            else
                                            {
                                                //We do not have one just grab it. :)
                                                SelectRoad(tmpRoad);
                                            }
                                        }
                                    }
                                }
                            }
                            else if (e.type == EventType.MouseUp)
                            {
                                //Take Control from Unity Standard SceneView
                                GUIUtility.hotControl = 0;
                            }
                        }
                        else if (selected == Options.Cell)
                        {
                            if (e.type == EventType.MouseDown)
                            {
                                //Take Control from Unity Standard SceneView
                                GUIUtility.hotControl = controlID;
                                //we have to shoot for the scenario where two cells are competing for clicking space.
                                if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, cellMask))
                                {
                                    //We only have one hit
                                    string hitTag = hit.collider.tag;
                                    if (hitTag == cellTag)
                                    {
                                        if (curCell != null)
                                        {
                                            if (hit.collider.name == curCell.name)
                                            {
                                                //Deselecting curCell
                                                //Debug.Log("De-Selecting Cell ");
                                                ReleaseCurCell();
                                                return;
                                            }
                                            else
                                            {
                                                //Debug.Log("Only One hitCell Selecting it ");
                                                ReleaseCurCell();
                                            }
                                        }
                                        //Grabbing a Cell. Lets turn on the CellCanvas
                                        SetCurCell(hit.collider.GetComponent<CiDyCell>());
                                        //Debug.Log("shortest cycle "+shortestCycle+" Cell "+curCell.name);
                                    }
                                }
                            }
                        }
                    }
                }
                if (e.type == EventType.MouseDown)
                {
                    e.Use();
                }
            }
        }

        //Dirty Scene for Saving
        void DirtyScene()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        public Rect userRect = new Rect(0, 0, 50, 50);
        public List<GameObject> selectedObjects = new List<GameObject>();//Dynamic list for the user interface.
        public GameObject selectedPoint;
        public int pointInt = 0;

        void FindSelectedObjects()
        {
            //Debug.Log ("Find Selected Objects() StoredTransform Cnt: "+roadPoints.Count);
            //Clear last Selected
            if (selectedObjects.Count > 0)
            {
                selectedObjects.Clear();
            }
            for (int i = 0; i < roadPoints.Count; i++)
            {
                //Skip first and last. We do not want these points manipulated
                if (i == 0 || i == roadPoints.Count - 1)
                {
                    continue;
                }
                //Is this transform inside the view frustrum the user created?
                //Vector3 screenPos = Camera.main.ViewportToScreenPoint(roadPoints[i].transform.position);
                //Vector3 screenPos = Camera.main.WorldToScreenPoint(roadPoints[i].transform.position);
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(roadPoints[i].transform.position);
                //screenPos.y = Screen.height - screenPos.y;
                //Debug.Log("testing "+roadPoints[i].name+" ScreenPos: "+screenPos);
                if (userRect.Contains(screenPos))
                {
                    //Debug.Log("Its inside User Rect");
                    selectedObjects.Add(roadPoints[i]);
                    pointInt = i;
                }
            }
            //Set selected Point to bottom of list.
            if (selectedObjects.Count > 0)
            {
                selectedPoint = selectedObjects[0];
                //Debug.Log("Set SelectedObject");
            }
            //isSelecting = false;
        }

        //Update CurNode
        void NewCurNode(CiDyNode newNode)
        {
            if (curData != null)
            {
                curData.ChangeMaterial(nodeMaterial);
            }
            //Debug.Log ("Designer Is Pushing Node to Graph " + newNode.name);
            curData = newNode;
            curData.ChangeMaterial(activeMaterial);
        }

        //Change CurData to the GameObject Node Referenced
        void UpdateCurNode(CiDyNode newNode)
        {
            //Do we have a node still selected?
            if (curData != null)
            {
                curData.ChangeMaterial(nodeMaterial);
            }
            //Select the newNode now. :)
            string desiredNode = newNode.name;
            curData = (graph.masterGraph.Find(x => x.name == desiredNode));
            curData.ChangeMaterial(activeMaterial);
        }
        void SelectRoad(CiDyRoad newRoad)
        {
            curRoad = newRoad;
            if (curRoad == null)
            {
                return;
            }
            Selection.activeObject = curRoad.gameObject;
            //Grab stop Sign if Applicable
            if (curRoad.stopSign)
            {
                roadStopSign = (GameObject)curRoad.stopSign;
            }
            laneWidth = curRoad.laneWidth;
            roadSegmentLength = curRoad.segmentLength;
            flattenAmount = curRoad.flattenAmount;
            uvsRoadSet = curRoad.uvsRoadSet;
            generateCrossWalks = curRoad.crossWalksAtIntersections;
            curRoad.SelectRoad();
            /*//Create the Interactive Points.
            //Grab the newly selected roads origPoints.
            cPoints = new List<Vector3> (curRoad.cpPoints);
            //Iterate through the list and create control points at those positions.
            for(int i=0;i<cPoints.Count;i++){
                CreatePoint(cPoints[i]);
                if(i==0 || i == cPoints.Count-1){
                    //Deactive these points.
                    roadPoints[roadPoints.Count-1].SetActive(false);
                }
            }

            Repaint ();*/
        }

        void DeselectRoad()
        {
            curRoad.DeselectRoad();
            roadStopSign = null;
            curRoad = null;
            Selection.activeObject = null;
            if (roadPoints.Count > 0)
            {
                for (int i = 0; i < roadPoints.Count; i++)
                {
                    DestroyImmediate(roadPoints[i]);
                }
                roadPointCount = 0;
                roadPoints.Clear();
                roadLines.Clear();
            }
        }

        void RegenerateRoad()
        {
            //Debug.Log("Regen Road");
            //This will update the Variables on the Selected CiDyRoad
            //Change Road GameObject
            if (roadStopSign != curRoad.stopSign)
            {
                curRoad.stopSign = roadStopSign;
            }
            else if (roadStopSign == null)
            {
                curRoad.stopSign = null;
            }
            //Check Road Material
            if (roadMaterial != null)
            {
                if (curRoad.roadMaterial != roadMaterial)
                {
                    //Update Material
                    curRoad.ChangeRoadMaterial(roadMaterial);
                }
            }
            //Debug.Log("After Stop Sign Regen Road Editor");
            //Update Uvs
            curRoad.uvsRoadSet = uvsRoadSet;
            curRoad.crossWalksAtIntersections = generateCrossWalks;
            curRoad.InitilizeRoad();
            graph.UpdateRoadCell(curRoad);
        }

        //This function will set the new curCell
        void SetCurCell(CiDyCell newCell)
        {
            if (curCell != null)
            {
                ReleaseCurCell();
            }

            curCell = newCell;
            //selectionColor.a = 0.8f;
            //curCell.GetComponent<Renderer>().material.SetColor("_Color", selectionColor);
            curCell.GetComponent<Renderer>().material = cellSelectionMaterial;
            //Update EditorWindow Variables to Reflect this cells cur Variables
            sideWalkWidth = curCell.sideWalkWidth;
            sideWalkHeight = curCell.sideWalkHeight;
            sideWalkEdgeWidth = curCell.sideWalkEdgeWidth;
            sideWalkEdgeHeight = curCell.sideWalkEdgeHeight;
            lotWidth = curCell.lotWidth;
            lotDepth = curCell.lotDepth;
            lotInset = curCell.lotInset;
            lotsUseRoadHeight = curCell.lotsUseRoadHeight;
            autoFillBuildings = curCell.autoFillBuildings;
            contourSideWalkLights = curCell.contourSideWalkLights;
            contourSideWalkClutter = curCell.contourSideWalkClutter;
            randomizeClutterPlacement = curCell.randomizeClutterPlacement;
            huddleBuildings = curCell.huddleBuildings;
            maximizeLotSpace = curCell.maximizeLotSpace;
            createSideWalks = curCell.createSideWalks;
            pathLightSpacing = curCell.pathLightSpacing;
            pathClutterSpacing = curCell.pathClutterSpacing;
            //Grab GameObjects if Applicable
            if (curCell.pathLight)
            {
                streetLight = (GameObject)curCell.pathLight;
            }
            Repaint();
        }

        void ReleaseCurCell()
        {
            //Release the last curCell
            //curCell.GetComponent<Renderer>().material.SetColor("_Color", curCell.RandomColor());
            curCell.GetComponent<Renderer>().material = cellMaterial;
            curCell = null;
        }

        void RegenerateCell()
        {
            if (curCell == null)
            {
                regenerateCell = false;
                return;
            }
            //Update Variables to Match EditorWindow Variables
            curCell.sideWalkWidth = sideWalkWidth;
            curCell.sideWalkHeight = sideWalkHeight;
            curCell.sideWalkEdgeWidth = sideWalkEdgeWidth;
            curCell.sideWalkEdgeHeight = sideWalkEdgeHeight;
            curCell.lotWidth = lotWidth;
            curCell.lotDepth = lotDepth;
            curCell.lotInset = lotInset;
            curCell.lotsUseRoadHeight = lotsUseRoadHeight;
            curCell.autoFillBuildings = autoFillBuildings;
            curCell.contourSideWalkLights = contourSideWalkLights;
            curCell.contourSideWalkClutter = contourSideWalkClutter;
            curCell.randomizeClutterPlacement = randomizeClutterPlacement;
            curCell.huddleBuildings = huddleBuildings;
            curCell.maximizeLotSpace = maximizeLotSpace;
            curCell.createSideWalks = createSideWalks;
            curCell.pathLightSpacing = pathLightSpacing;
            curCell.pathClutterSpacing = pathClutterSpacing;
            curCell.pathLight = streetLight;
            curCell.usePrefabBuildings = usePrefabBuildings;
            //Update Cell
            curCell.UpdateCell();
        }

        private int roadPointCount = 0;
        //This will Create a transform in world Space at MousePosition and add to world points
        void CreatePoint(Vector3 newPos)
        {
            //Make a cube at the point and add its transform to the list of worldPoints.
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "P" + roadPointCount;
            cube.transform.position = newPos;
            cube.layer = LayerMask.NameToLayer("Ignore Raycast");
            //Turn off Cubes Mesh Renderer
            cube.GetComponent<MeshRenderer>().enabled = false;
            //AddProjector (cube);
            roadPoints.Add(cube);
            roadPointCount++;
        }

        public void UpdateCiDy()
        {
            //Inset the Roads
            List<List<Vector3>> boundaryCell = new List<List<Vector3>>(0);
            List<List<CiDyNode>> newCycles = graph.FindCycles(ref boundaryCell);
            if (newCycles.Count > 0)
            {
                graph.UpdateCells(newCycles);
            }
            else
            {
                //All Cells Destroyed
                if (graph.cells.Count > 0)
                {
                    //Remove the Cells
                    graph.ClearCells();
                }
            }
            //Add Boundary Cell
            /*if (boundaryCell.Count > 0) {
                //We have boundary Cells.
                graph.UpdateBoundaryCells(boundaryCell);
            }*/
            //Graph has Changed. Dirty Scene for Saving
            DirtyScene();
        }

        void ResetColliders() {
            //Debug.Log("Reset Colliders");
            //Turn off Cells and Node Colliders for Convienence
            //Turn off Node Colliders
            if (graph == null) {
                InitializeGraph();
            }
            if (graph)
            {
                //Debug.Log("Resetting States, Nodes: " + savedShowNodes + " Cells: " + savedShowCells);
                //Update Node Colldiers to Previous Edit Mode State
                showNodes = savedShowNodes;
                if (showNodes)
                {
                    graph.EnableNodeGraphics();
                }
                //Update Cell Colldiers to Previous Edit Mode State
                showCells = savedShowCells;
                if (showCells)
                {
                    graph.EnableCellGraphics();
                }
            }
        }

        //Change Control State(What we can interact with)
        void UpdateState()
        {
            //Debug.Log ("Change State");
            curSelected = selected;
            //Clear temp stored state info.
            if (curData != null)
            {
                curData.ChangeMaterial(nodeMaterial);
                //Deselect node
                curData = null;
            }
            if (curRoad != null)
            {
                DeselectRoad();
            }
            if (curCell)
            {
                //Clear Cell Data
                ReleaseCurCell();
            }
        }

        //Keep Track Of Dynamic User Edited Variables
        void Update()
        {
            if (graph == null)
            {
                return;
            }
            //Update Changed Values
            if (curNodeScale != nodeScale)
            {
                curNodeScale = nodeScale;
                graph.ChangeNodeScale(nodeScale);
            }
            if (curSelected != selected)
            {
                UpdateState();
            }
            /*if (usingSceneCam != useSceneCamera)
            {
                UpdateState();
                usingSceneCam = useSceneCamera;
            }*/
            //Visual Toggle Logic
            if (showCells && !graph.activeCells)
            {
                graph.EnableCellGraphics();
            }
            if (!showCells && graph.activeCells)
            {
                graph.DisableCellGraphics();
            }
            if (showNodes && !graph.activeNodes)
            {
                graph.EnableNodeGraphics();
            }
            if (!showNodes && graph.activeNodes)
            {
                graph.DisableNodeGraphics();
            }
            //Cell Regenerate
            if (regenerateCell)
            {
                RegenerateCell();
                regenerateCell = false;
            }
            //Road Regenerate 
            if (regenerateRoad)
            {
                RegenerateRoad();
                regenerateRoad = false;
            }

            if (roadPoints.Count > 1)
            {
                roadLines = new List<Vector3>();
                for (int i = 0; i < roadPoints.Count; i++)
                {
                    roadLines.Add(roadPoints[i].transform.position);
                }
                roadLines = CiDyUtils.CreateBezier(roadLines, roadSegmentLength);
            }
        }

        //This function will simply turn all all visuals.
        void ActivateVisuals()
        {
            showNodes = true;
            showCells = true;
        }
    }
}