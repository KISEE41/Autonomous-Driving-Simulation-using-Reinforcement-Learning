using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if VEGETATION_STUDIO_PRO
using AwesomeTechnologies;
#endif

namespace CiDy
{
    [System.Serializable]
    public class CiDyRoad : MonoBehaviour
    {
        //Public variables for Editor Use etc.
        public bool snapToGround = true;//Default as true. This will mean that this road will always stay on the terrain
        [HideInInspector]
        public bool snapToGroundLocal = true;
        //Materials for Detailed Road Mesh
        public Material roadMaterial;//The Material we want to be applied to this road when its created or updated.(This doesn't Effect Road Markings)
        public Material dividerLaneMaterial;//The Material for the Divider Lane if we want.
        public Material shoulderMaterial;//The Shoulder Material, This can also double as part of your SideWalk Material.
        public bool uvsRoadSet = true;
        public bool flipStopSign = false;//If true, we spawn stop signs on opposite ends.(Left End)
        public bool crossWalksAtIntersections = true;//If true we will generate CrossWalks at intersections, If false we will not generate them.
        public bool oneWay = false;//Always true if LaneType is OneLane. If Lane Type is anything else it may be twoWay or OneWay based on Boolean.
        public bool createMarkings = true;//Determines if we want to Spawn the Marking Spawners.
        //Level Layers
        public enum RoadLevel
        {
            //None = 0, // Custom name for "Nothing" option
            Path = 1 << 1,//Path Flag = Bit Shift 0
            Road = 1 << 2,//Road Flat = Bit Shift 1
            HighWay = 1 << 3,//Highway Flag = Bit Shift 2
            //Path_Road = Road | Path, // Combination of two flags (As a Preset Option)
            //Road_Highway = HighWay | Road, // Combination of two flags (As a Preset Option)
            //C_Highway_Path = HighWay | Path,// Combination of two flags (As a Preset Option)
            //B_Highway_Road = HighWay | Road, // Combination of two flags (As a Preset Option)
            //All = ~0, // Custom name for "Everything" option
        }
        public RoadLevel roadLevel = RoadLevel.Road;
        //Edge Prefab/Spline Mesh Variables
        public enum LaneType
        {
            OneLane,
            TwoLane,
            FourLane,
            SixLane
        }
        [HideInInspector]
        public int laneCount = 1;
        public LaneType laneType = LaneType.TwoLane;
        public float centerSpacing = 0f;
        public float leftShoulderWidth = 1.8288f;//4 Feet US
        public float rightShoulderWidth = 1.8288f;//4 Feet US
        public float laneWidth = 3.6576f;//12 Feet US
                                         //Nodes that this road Connects.
        public CiDyNode nodeA;
        public CiDyNode nodeB;
        float nodeARadius;
        float nodeBRadius;
        //Road
        public float width = 0.0f;
        public Mesh roadMesh;
        public MeshFilter mFilter;
        [SerializeField]
        MeshRenderer mRender;
        [SerializeField]
        MeshCollider mCollider;
        //Road Left and Right Edge Points used for Interior Cell Creation Logic.
        [HideInInspector]
        public Vector3[] leftEdge;//The Left Edge Vertices of the Road.(Generated in SnipRoadMesh());
        public Vector3[] rightEdge;//The Right Edge Vertices of the Road.(Generated in SnipRoadMesh());
                                   //Ground Support Variables
        public float supportSideWidth = 1.5f;
        public float supportSideHeight = 6f;
        public float beamBaseWidth = 2;
        public float beamBaseHeight = 3;
        public float beamSpacing = 16;
        [SerializeField]
        GameObject[] supportBases;//Reference to Support Base Objects
        [HideInInspector]
        public GameObject bridgeBlendColHolder;
        [HideInInspector]
        public MeshCollider bridgeBlendCollider;//We use this for Terrain Blending a Bridged Road.
        [HideInInspector]
        public Vector3[] blendMeshOrigPoints;
        //Guard Rail Materials
        public float leftRailOffset = 0f;
        public float rightRailOffset = 0f;
        public Material railMat;
        public Material postMat;
        public Transform leftRail;
        public Transform rightRail;
        // public to help debug in editor
        public int n = 0;
        [HideInInspector]
        public Vector3[] cpPoints = new Vector3[0];
        public Vector3[] origPoints = new Vector3[0];
        [HideInInspector]
        public List<Vector3> snipPoints = new List<Vector3>(0);
        [SerializeField]
        Vector3 startRightDir;
        [SerializeField]
        Vector3 endRightDir;
        public List<Vector3> vs = new List<Vector3>(0);
        //private List<Vector2> uvs = new List<Vector2>(0);
        //private Vector3 endA = new Vector3(0,0,0);
        //private Vector3 endB = new Vector3(0,0,0);
        public int updateCall = 0;//Reset after equals 2;
                                  //Secondary Roads
        public Vector3 growthPoint;
        public Vector3 growthDir;
        public bool selected = false;//Weather this road is being modified in real-time or not. :)
        public int segmentLength = 4;//Default segment length for Bezier Algos
        public int flattenAmount = 12;//Determines how many points we flatten near the ends of the Bezier Curve
        public GameObject parent;
        //StopSign/Traffic Light State
        public enum LightType
        {
            None,
            StopSign,
            TrafficLight
        }
        public LightType lightType = LightType.TrafficLight;//Default Traffic Light
        private LightType curLightType = LightType.TrafficLight;
        //Stop Sign Variables
        public GameObject stopSign;//This is Both Stop/Traffic Lights
        public GameObject stopSignTwoLane;
        public GameObject stopSignThreeLane;
        public List<Transform> decals;
        //Traffic Values
        public bool stsActive = false;//Simple Traffic System Integration
        public CiDyRouteData leftRoutes;
        public CiDyRouteData rightRoutes;
        [HideInInspector]
        public bool flipLocalRoutes = false;
        [HideInInspector]
        public GameObject[] spawnedSigns = new GameObject[2];
        //Graph
        [HideInInspector]
        public CiDyGraph graph;

        [HideInInspector]
        public Vector3 position;
        //Used to Prep for Threaded functions
        public void StorePos()
        {
            position = transform.position;
        }
        [HideInInspector]
        public int[] blendingTerrains;//Stored Ids of the Terrains this Road is blending to currently.
        //Match Terrain
        public bool MatchTerrain(int terrainId)
        {
            if (blendingTerrains == null)
            {
                return false;
            }
            for (int i = 0; i < blendingTerrains.Length; i++)
            {
                if (terrainId == blendingTerrains[i])
                {
                    return true;
                }
            }
            return false;
        }

        //This function will perform a Bounding Box Check and See what terrains of Graph are under us.(If Any)
        public void FindTerrains(Vector3[] rawPath = null, float pathWidth = 0)
        {
            if (rawPath == null)
            {
                rawPath = cpPoints;
            }
            if (pathWidth == 0)
            {
                pathWidth = width;
            }
            float halfWidth = pathWidth * 2;
            //Project Bounding Box of Spline.
            Vector3 firstPoint = rawPath[0];
            Vector3 middlePoint = rawPath[rawPath.Length / 2];
            Vector3 lastPoint = rawPath[rawPath.Length - 1];
            //Project Points based on PathWidth
            Vector3 fwd = (middlePoint - firstPoint).normalized;
            Vector3 endFwd = (lastPoint - middlePoint).normalized;
            Vector3 fwdCross = Vector3.Cross(fwd, Vector3.up);
            Vector3 endCross = Vector3.Cross(endFwd, Vector3.up);
            //Project 6 Points for Bounding Box.
            Vector3 flPoint = firstPoint + (fwdCross * halfWidth);
            Vector3 frPoint = firstPoint + (-fwdCross * halfWidth);
            //Middle Points
            Vector3 mlPoint = middlePoint + (endCross * halfWidth);
            Vector3 mrPoint = middlePoint + (-endCross * halfWidth);
            //End Points
            Vector3 eLPoint = lastPoint + (endCross * halfWidth);
            Vector3 erPoint = lastPoint + (-endCross * halfWidth);
            //Create Bounds
            Bounds pathBounds = new Bounds();
            //Add All 6 Points to Bounds Projection
            pathBounds.Encapsulate(flPoint);
            pathBounds.Encapsulate(frPoint);
            pathBounds.Encapsulate(mlPoint);
            pathBounds.Encapsulate(mrPoint);
            pathBounds.Encapsulate(eLPoint);
            pathBounds.Encapsulate(erPoint);
            //Compare this Bounds to All Potential Terrains and there Bounds.
            List<int> newBlendingTerrains = new List<int>(0);
            for (int i = 0; i < graph.terrains.Length; i++)
            {
                if (graph.terrains[i] == null)
                {
                    continue;
                }
                //Terrain Bounds to Test Against.
                Bounds terrBounds = graph.terrains[i].ReturnBounds();
                //Compare
                if (terrBounds.Intersects(pathBounds))
                {
                    //Add to Match Terrain Array
                    newBlendingTerrains.Add(graph.terrains[i]._Id);
                }
            }
            //Convert and Store Long Term
            blendingTerrains = newBlendingTerrains.ToArray();
        }

        void CheckIntegration() {
            //Simple Traffic System
            #if SimpleTrafficSystem
                        stsActive = true;
            #elif !SimpleTrafficSystem
                stsActive = false;
            #endif
        }

        void CreateRenderer()
        {
            //Determine if Simple Traffic System is Here
            CheckIntegration();
            //First time this component as been created.
            mRender = (MeshRenderer)gameObject.AddComponent<MeshRenderer>();
            mRender.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mFilter = (MeshFilter)gameObject.AddComponent<MeshFilter>();
            if (graph.roadMaterial == null)
            {
                roadMaterial = (Material)Resources.Load("CiDyResources/Road", typeof(Material));
                dividerLaneMaterial = roadMaterial;
                shoulderMaterial = roadMaterial;
            }
            else
            {
                roadMaterial = graph.roadMaterial;
                dividerLaneMaterial = roadMaterial;
                shoulderMaterial = roadMaterial;
            }
            //Set Road Material
            mRender.sharedMaterial = roadMaterial;
            //Add Collider for later
            mCollider = (MeshCollider)gameObject.AddComponent<MeshCollider>();
            mCollider.sharedMesh = new Mesh();
            //Grab Guard Rail Material if Applciable
            railMat = (Material)Resources.Load("CiDyResources/RailMat", typeof(Material));
            postMat = (Material)Resources.Load("CiDyResources/PostMat", typeof(Material));
            //Grab TrafficLightPrefab
            GrabTrafficLightPrefab();
        }

        void GrabTrafficLightPrefab() {
            //Debug.Log("Grab Traffic Light Prefab");
            //Set LightType
            curLightType = lightType;
            //Setup Theme Based Stop Signs enum
            switch (lightType)
            {
                case LightType.StopSign:
                    //Debug.Log("Stop Sign");
                    //Grab Stop Sign Prefab
                    if (stopSign == null)
                    {
                        //Check for Theme, Preference
                        stopSign = (GameObject)Resources.Load("CiDyResources/CiDyTheme" + graph.districtTheme[graph.index] + "/" + graph.districtTheme[graph.index] + "StreetLight/StopSign", typeof(GameObject));
                        if (stopSign == null)
                        {
                            //No theme, Grab Default Resource
                            stopSign = (GameObject)Resources.Load("CiDyResources/StopSign", typeof(GameObject));
                        }
                    }
                    break;
                case LightType.TrafficLight:
                    //Debug.Log("Traffic Light");
                    //Grab Traffic Light Prefab, Check for STS
                    if (!stsActive)
                    {
                        //Debug.Log("Standard Traffic Light");
                        //No STS Available, Grab Standard Theme Prefabs
                        if (stopSign == null)
                        {
                            stopSign = (GameObject)Resources.Load("CiDyResources/CiDyTheme" + graph.districtTheme[graph.index] + "/" + graph.districtTheme[graph.index] + "StreetLight/StopSign", typeof(GameObject));
                            if (stopSign == null)
                            {
                                stopSign = (GameObject)Resources.Load("CiDyResources/TrafficLight", typeof(GameObject));
                            }
                        }
                        if (stopSignTwoLane == null)
                        {
                            stopSignTwoLane = (GameObject)Resources.Load("CiDyResources/CiDyTheme" + graph.districtTheme[graph.index] + "/" + graph.districtTheme[graph.index] + "StreetLight/StopSignTwoLane", typeof(GameObject));
                            if (stopSignTwoLane == null)
                            {
                                stopSignTwoLane = (GameObject)Resources.Load("CiDyResources/TrafficLightTwoLane", typeof(GameObject));
                            }
                        }
                        if (stopSignThreeLane == null)
                        {
                            stopSignThreeLane = (GameObject)Resources.Load("CiDyResources/CiDyTheme" + graph.districtTheme[graph.index] + "/" + graph.districtTheme[graph.index] + "StreetLight/StopSignThreeLane", typeof(GameObject));
                            if (stopSignThreeLane == null)
                            {
                                stopSignThreeLane = (GameObject)Resources.Load("CiDyResources/TrafficLightThreeLane", typeof(GameObject));
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("STS Traffic Light");
                        //Grab STS Traffic Lights that contain there Needed Light Component Script
                        if (stopSign == null)
                        {
                            stopSign = (GameObject)Resources.Load("CiDyResources/TrafficLight_STS", typeof(GameObject));
                        }
                        if (stopSignTwoLane == null)
                        {
                            stopSignTwoLane = (GameObject)Resources.Load("CiDyResources/TrafficLightTwoLane_STS", typeof(GameObject));
                        }
                        if (stopSignThreeLane == null)
                        {
                            stopSignThreeLane = (GameObject)Resources.Load("CiDyResources/TrafficLightThreeLane_STS", typeof(GameObject));
                        }
                    }
                    break;
            }
        }

        //Used by RoadEditor to Regenerate based on Changed CP Points.
        public void Regenerate()
        {
            //Debug.Log("Regenerate");
            if (curLightType != lightType)
            {
                //Reset as user has changed there Light Type Setting.
                stopSign = null;
                GrabTrafficLightPrefab();
            }
            InitilizeRoad();
            graph.UpdateRoadCell(this);
            //Update Selected Material
            lastMaterial = roadMaterial;
        }

        //Raw Path That needs to be Bezier/BSpline
        public void InitilizeRoad(Vector3[] rawPath, float newWidth, int newSegmentLength, int newFlatAmount, CiDyNode newA, CiDyNode newB, GameObject holder, CiDyGraph newGraph, bool normalizePath, CiDyRoad.RoadLevel level = CiDyRoad.RoadLevel.Road, CiDyRoad.LaneType lane = CiDyRoad.LaneType.TwoLane, float _laneWidth = 0, float _leftShoulderWidth = 0, float _centerWidth = 0, float _rightShoulderWidth = 0)
        {
            //Debug.Log("Init: " + level + " Lane: " + lane);
            if (normalizePath)
            {
                Debug.LogWarning("Normalize Path");
            }
            //Debug.Log ("Initilized "+name+" with "+rawPath.Length+" points and width of "+newWidth+"NodeA: "+newA.name+" NewB: "+newB.name+" RoadMaterial: "+newGraph.roadMaterial);
            graph = newGraph;//Set Graph Reference
            if (mRender == null)
            {
                CreateRenderer();
            }
            width = newWidth;
            //Get the Terrains that this Road is going to blend to.(If Any)
            FindTerrains(rawPath, newWidth);
            segmentLength = newSegmentLength;//Segment Length Must Not Be Large than Half RoadWidth(This is for the Terrain Blending)
            segmentLength = Mathf.Clamp(segmentLength, 0, (int)(width / 2));
            flattenAmount = newFlatAmount;
            roadLevel = level;
            laneType = lane;
            laneWidth = _laneWidth;
            centerSpacing = _centerWidth;
            leftShoulderWidth = _leftShoulderWidth;
            rightShoulderWidth = _rightShoulderWidth;
            nodeA = newA;
            nodeB = newB;
            parent = holder;

            //Run Function that will Update our Lane Count
            CalculateLanes();

            if (normalizePath)
            {
                cpPoints = CiDyUtils.CreateBezier(rawPath);
            }
            else
            {
                cpPoints = rawPath;
            }


            origPoints = CiDyUtils.CreateBezier(cpPoints, (float)segmentLength);
            SetSnapBasedOnRoadLevel();
            //Now Lets Contour these Points to the Terrain.
            graph.ContourPathToTerrain(ref origPoints, blendingTerrains, !snapToGroundLocal);
            //origPoints = CiDyUtils.CreateBezier(cpPoints, (float)segmentLength);
            origPoints = FlattenRoadPath(origPoints);
            //
            if (decals == null)
            {
                decals = new List<Transform>(0);
            }
            //Tell Nodes Road is Attached and Needs to Be Accounted For.
            nodeA.AddRoad(this);
            nodeB.AddRoad(this);
        }
        void SetSnapBasedOnRoadLevel()
        {
            switch (roadLevel)
            {
                case RoadLevel.Path:
                    //Path
                    snapToGroundLocal = true;
                    break;
                case RoadLevel.Road:
                    //User Selected
                    snapToGroundLocal = snapToGround;
                    break;
                case RoadLevel.HighWay:
                    //HighWay
                    snapToGroundLocal = false;
                    break;
            }
        }

        void CheckForGroundSupport()
        {
            Debug.Log("Check For Ground Support");
            //Clear Previous Bases
            ClearSupportBases();
            if (snapToGroundLocal)
            {
                //This road is a snap to ground so we do not create ground support
                return;
            }
            Debug.Log("Calculate GroundSupport Collider");
            //We have support Systems. Generate SubMesh that will only hold a Collider.
            //Generate a Bridge Blend Mesh
            blendMeshOrigPoints = new Vector3[snipPoints.Count];
            for (int i = 0; i < blendMeshOrigPoints.Length; i++)
            {
                blendMeshOrigPoints[i] = snipPoints[i];
            }
            //Now Lets Contour these Points to the Terrain.
            graph.ContourPathToTerrain(ref blendMeshOrigPoints, blendingTerrains);
            blendMeshOrigPoints = FlattenRoadPath(blendMeshOrigPoints);
            //Set to Collider
            if (bridgeBlendColHolder == null)
            {
                bridgeBlendColHolder = new GameObject("ColliderHolder");
                bridgeBlendColHolder.transform.SetParent(transform);
                bridgeBlendCollider = bridgeBlendColHolder.AddComponent<MeshCollider>();
            }
            bridgeBlendCollider.sharedMesh = CiDyUtils.CreateRoad(blendMeshOrigPoints, width);
            //set Layer
            bridgeBlendColHolder.layer = LayerMask.NameToLayer("Road");
            bridgeBlendCollider.enabled = true;
            //Now Generate Appropriate Ground Support Systems for areas that require it.
            //Switch Road Layer so it doesnt interfere with collider's
            gameObject.layer = LayerMask.NameToLayer("Default");
            //Now we want to find the Areas along the Road that will require Ground Support.
            List<List<Vector3>> raisedPoints = CiDyUtils.FindRaisedPoints(snipPoints.ToArray(), 1 << LayerMask.NameToLayer("Road"));
            //Turn Collider Off
            bridgeBlendCollider.enabled = false;
            if (raisedPoints.Count > 0)
            {
                if (supportBases == null || supportBases.Length == 0)
                {
                    supportBases = new GameObject[raisedPoints.Count];
                }
                for (int i = 0; i < raisedPoints.Count; i++)
                {
                    GameObject newBase = CiDyUtils.GenerateRoadSupport(raisedPoints[i].ToArray(), width, supportSideHeight, supportSideWidth, beamBaseWidth, beamBaseHeight, beamSpacing, transform);
                    supportBases[i] = newBase;
                }
            }
            gameObject.layer = LayerMask.NameToLayer("Road");
        }
        void ClearSupportBases()
        {
            if (supportBases == null || supportBases.Length == 0)
            {
                return;
            }

            for (int i = 0; i < supportBases.Length; i++)
            {
                if (supportBases[i] != null)
                {
                    DestroyImmediate(supportBases[i]);
                }
            }

            supportBases = new GameObject[0];
        }

        void CalculateLanes()
        {
            //Calculate Lane Count etc
            //Generate Highway
            switch (laneType)
            {
                case LaneType.SixLane:
                    //6 Lane Road (3/3 split)
                    laneCount = 6;
                    break;
                case LaneType.FourLane:
                    //Four Lane Road (2/2 split)
                    laneCount = 4;
                    break;
                case LaneType.TwoLane:
                    //Standard 2 Lane Road(1 / 1 split)
                    laneCount = 2;
                    break;
                case LaneType.OneLane:
                    laneCount = 1;
                    break;
            }
        }
        //Update RoadWidth,SegmentLength,FlattenAmount
        public void InitilizeRoad()
        {
            //Debug.Log("Initialize Road: "+name+" New Seg Lenght: "+newSegmentLength+" New Flat Amount: "+newFlatAmount);
            //Recalc Lane Count
            CalculateLanes();
            //cpPoints = CiDyUtils.CreateBezier(cpPoints);
            origPoints = CiDyUtils.CreateBezier(cpPoints, (float)segmentLength);
            SetSnapBasedOnRoadLevel();
            //Now Lets Contour these Points to the Terrain.
            //TODO Update Contour to Handle Ground Support Logic
            graph.ContourPathToTerrain(ref origPoints, blendingTerrains, !snapToGroundLocal);
            origPoints = FlattenRoadPath(origPoints);
            //Tell Nodes Road is Attached and Needs to Be Accounted For.
            nodeA.UpdatedRoad();
            nodeB.UpdatedRoad();
            //Debug.Log("Done Initializing Road");
        }

        //Raw Path That needs to be Bezier/BSpline
        /*public void InitilizeBSpline(Vector3[] rawPath, float newWidth, int newSegmentLength, int newFlatAmount, CiDyNode newA, CiDyNode newB, GameObject holder, CiDyGraph newGraph){
            Debug.Log ("Initilized "+name+" with "+rawPath.Length+" points and width of "+newWidth);
            width = newWidth;
            segmentLength = newSegmentLength;
            flattenAmount = newFlatAmount;
            nodeA = newA;
            nodeB = newB;
            parent = holder;
            graph = newGraph;
            cpPoints = rawPath;
            //origPoints = CiDyUtils.CreateBSpline (rawPath, segmentLength);
            origPoints = CiDyUtils.CreateBezier (rawPath, (float)segmentLength);
            //Now Lets Contour these Points to the Terrain.
            graph.ContourPathToTerrain(ref origPoints);
            int flattenInt;
            if(origPoints.Length <= flattenAmount){
                flattenInt = origPoints.Length / 2;
            } else {
                flattenInt = flattenAmount;
            }
            //Iterate through and Update the Slopes
            for(int i = 1;i<origPoints.Length - 1;i++){
                Vector3 v0 = origPoints[i];
                Vector3 v1 = origPoints[origPoints.Length - 1];
                if(i<flattenInt){
                    origPoints[i] = new Vector3(v0.x,origPoints[0].y,v0.z);
                } else if(i>origPoints.Length - flattenInt){
                    origPoints[i] = new Vector3(v0.x,v1.y,v0.z);
                } else {
                    continue;
                }
            }
            origPoints = CiDyUtils.CreateBezier (origPoints, segmentLength);
            //Tell Nodes Road is Attached and Needs to Be Accounted For.
            nodeA.AddRoad (this);
            nodeB.AddRoad (this);
        }
        */
        //Replot Road Based on New Path And cur RoadWidth and Nodes A & B.
        public void ReplotRoad(Vector3[] rawPath)
        {
            //Debug.Log ("ReplotRoad "+name+" RawPath: "+rawPath.Length);
            cpPoints = rawPath;
            //Replot Path to Nodes.
            origPoints = CiDyUtils.CreateBezier(cpPoints, (float)segmentLength);
            //Now Lets Contour these Points to the Terrain.
            graph.ContourPathToTerrain(ref origPoints, blendingTerrains);
            //origPoints = CiDyUtils.CreateBezier(cpPoints, (float)segmentLength);
            origPoints = FlattenRoadPath(origPoints);
            //Update Connected Nodes and allow the Road Change to Take Full Effect
            UpdateRoadNodes();
        }

        Vector3[] FlattenRoadPath(Vector3[] inputPath)
        {

            float totalDist = CiDyUtils.FindTotalDistOfPoints(inputPath);
            float flatDist = (width * 3.2f) + (segmentLength * 2);
            if (flatDist > (totalDist / 2))
            {
                flatDist = totalDist / 2;
            }
            //Debug.Log("FlattenRoadPath: "+ flatDist);
            //Project a Point 12 Meters towards B end from A End to visualize the are a of flattening.
            Vector3 strPos = nodeA.position;
            Vector3 endPos = nodeB.position;
            strPos.y = 0;
            endPos.y = 0;
            //Pre calculate Flattend Ends
            for (int i = 0; i < inputPath.Length; i++)
            {
                Vector3 v0 = inputPath[i];
                //CiDyUtils.MarkPoint(v0,i);
                v0.y = 0;

                //Calculate Distance
                float distA = Vector3.Distance(v0, strPos);
                float distB = Vector3.Distance(v0, endPos);

                if (i == 1 || i == inputPath.Length - 2)
                {
                    if (distA < distB)
                    {
                        v0.y = nodeA.position.y;
                        inputPath[i] = v0;
                    }
                    else
                    {
                        v0.y = nodeB.position.y;
                        inputPath[i] = v0;
                    }
                    //CiDyUtils.MarkPoint(origPoints[i], i+999);
                }
                if (distA <= flatDist)//flatDist)
                {
                    v0.y = nodeA.position.y;
                    inputPath[i] = v0;
                    //CiDyUtils.MarkPoint(v0, i);
                }
                else if (distB <= flatDist)//(nodeBRadius + (segmentLength * 3)))
                {
                    v0.y = nodeB.position.y;
                    inputPath[i] = v0;
                    //CiDyUtils.MarkPoint(v0, 200 + i);
                }
            }

            return CiDyUtils.CreateBezier(inputPath, (float)segmentLength);
        }

        public void UpdateRoadNodes()
        {
            nodeA.UpdatedRoad();
            nodeB.UpdatedRoad();
        }

        int count = 0;
        public void NodeDone(CiDyNode doneNode, float newRadius, bool updateMesh)
        {
            //Debug.Log ("Road: "+name+" Node Done " + doneNode.name+" newRadius: "+newRadius);
            if (count >= 2)
            {
                count = 0;
            }
            if (doneNode.name == nodeA.name)
            {
                nodeARadius = newRadius;
                count++;
            }
            else if (doneNode.name == nodeB.name)
            {
                nodeBRadius = newRadius;
                count++;
            }

            if (count >= 2 || updateMesh)
            {
                if (nodeARadius > 0 && nodeBRadius > 0)
                {
                    SnipRoadMesh();
                }
            }
        }

        //This will cut a the Road Mesh At its Node Ends (ORIGINAL SNIP MESH FUNCTION)

        public void SnipRoadMesh()
        {
            //Debug.Log("Snip");
            //If Vegitation Studio. Update Layouer
            //Grab its Initialized Point Data
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
            //Debug.Log("Generate Vegitation LayerMask");
            VegetationMaskLine vegetationMaskLine = this.gameObject.GetComponent<VegetationMaskLine>();
            if (vegetationMaskLine == null) {
                vegetationMaskLine = this.gameObject.AddComponent<VegetationMaskLine>();
            }
            if (vegetationMaskLine)
            {
                vegetationMaskLine.RemoveGrass = true;
                vegetationMaskLine.RemovePlants = true;
                vegetationMaskLine.RemoveTrees = true;
                vegetationMaskLine.RemoveObjects = true;
                vegetationMaskLine.RemoveLargeObjects = true;
                vegetationMaskLine.LineWidth = width+1;
                vegetationMaskLine.ClearNodes();
                Vector3 origPos = transform.position;
                Vector3[] worldPosArray = new Vector3[origPoints.Length];
                for (int i = 0; i < origPoints.Length; i++)
                {
                    worldPosArray[i] = origPoints[i] + origPos;
                }
                vegetationMaskLine.AddNodesToEnd(worldPosArray);
                //Points in the array list needs to be in worldspace positions.
                vegetationMaskLine.UpdateVegetationMask();
            }
#endif
            //Grab the Circle Radius for the line Testing that needs to happen on both nodeA and nodeB.
            //Grab roads OrigPoints
            //Iterate through the Vertices and determine all the Points that are to the Right of the line and Remove them.Then push the p0andp1 as the v0 and v1 of the list.
            //Determine which end we are snipping
            if (origPoints.Length <= 0)
            {
                return;
            }
            List<Vector3> tmpPoints = new List<Vector3>(origPoints);
            //float totalDist = CiDyUtils.FindTotalDistOfPoints(origPoints);
            Vector3[] endPoints = new Vector3[4];
            List<int> tris = new List<int>(0);
            List<Vector2> uvs = new List<Vector2>(0);
            Vector3 tmpIntersection = Vector3.zero;
            bool hasLeft = false;
            bool hasRight = false;
            //Using the OrigPionts from the Road we will calculate dynamic lines based on roadWidth From Each End that the nodes in testing are on.
            int startPlace = 0;//Start of Mesh Points
            int endPlace = 0;//End of Mesh Points
            int startPlaceR = 0;//Special MinorRoadMerge Snip Logic
            int endPlaceR = 0;//Special MinorRoadMerge Snip Logic
            float dist1 = Vector3.Distance(tmpPoints[0], nodeA.position);
            float dist2 = Vector3.Distance(tmpPoints[0], nodeB.position);
            if (dist2 < dist1)
            {
                tmpPoints.Reverse();
            }
            //Debug.Log("Start Check: " + name);
            //Calculate the Snip points this road will have with its intersection nodes circles
            //Node A is at the start of list.//And Node B is at the End.
            //Iterate through center line of road
            for (int i = 0; i < tmpPoints.Count; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = tmpPoints[i];
                Vector3 vector2;//Next Point
                Vector3 vector3;//thrid point.If Applicable
                                //Dir based on next in line.And Direction Based on Second Line.
                Vector3 vectorDir;//Direction from cur to Next
                Vector3 vectorDir2;//Direction from nxt to third. If Applicable
                if (i < tmpPoints.Count - 2)
                {
                    //Beginning or Middle
                    vector2 = tmpPoints[i + 1];
                    vectorDir = (vector2 - vector);
                    vector3 = tmpPoints[i + 2];
                    vectorDir2 = (vector3 - vector2);
                }
                else
                {
                    //At End
                    vector2 = tmpPoints[i - 1];
                    vectorDir = (vector - vector2);
                    vectorDir2 = Vector3.zero;
                }
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector + (-cross) * (width / 2);
                Vector3 rightVector = vector + cross * (width / 2);
                Vector3 leftVector2;
                Vector3 rightVector2;
                if (i < tmpPoints.Count - 2)
                {
                    Vector3 cross2 = Vector3.Cross(Vector3.up, vectorDir2).normalized;
                    leftVector2 = vector2 + (-cross2) * (width / 2);
                    rightVector2 = vector2 + cross2 * (width / 2);
                }
                else
                {
                    leftVector2 = vector2 + (-cross) * (width / 2);
                    rightVector2 = vector2 + cross * (width / 2);
                }
                //Are we testing against a Circle Radius(Standard Intersection)
                //Standard Circle Test of NodeA
                if (!hasLeft)
                {
                    if (CiDyUtils.CircleIntersectsLine(nodeA.position, nodeARadius, 360, leftVector, leftVector2, ref tmpIntersection))
                    {
                        //Found Left :)
                        tmpIntersection.y = nodeA.position.y;
                        endPoints[0] = tmpIntersection;
                        hasLeft = true;
                        startPlace = i + 1;//originally just i   
                    }
                }
                if (!hasRight)
                {
                    if (CiDyUtils.CircleIntersectsLine(nodeA.position, nodeARadius, 360, rightVector, rightVector2, ref tmpIntersection))
                    {
                        //Found Right :)
                        tmpIntersection.y = nodeA.position.y;
                        //Debug.Log("Second : "+tmpIntersection);
                        endPoints[1] = tmpIntersection;
                        hasRight = true;
                        startPlaceR = i + 1;
                        //CiDyUtils.MarkPoint(tmpIntersection,1);
                        //Debug.Log("Found First Left foundPoints: "+foundPoints);
                    }
                }
                if (hasLeft && hasRight)
                {
                    //Debug.Log("Found First Points");
                    //We are Done with this side now find the Other sides.
                    break;
                }
            }
            //Debug.Log("EndOf First Cycle: "+name);
            hasLeft = false;
            hasRight = false;
            for (int i = tmpPoints.Count - 1; i > 0; i--)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = tmpPoints[i];
                Vector3 vector2;
                Vector3 vector3;//thrid point.If Applicable
                                //Dir based on next in line.And Direction Based on Second Line.
                Vector3 vectorDir;//Direction from cur to Next
                Vector3 vectorDir2;//Direction from nxt to third. If Applicable
                if (i > 1)
                {
                    //At End or Middle
                    vector2 = tmpPoints[i - 1];
                    vectorDir = (vector2 - vector);
                    vector3 = tmpPoints[i - 2];
                    vectorDir2 = (vector3 - vector2);
                }
                else if (i == 1)
                {
                    vector2 = tmpPoints[i - 1];
                    vectorDir = (vector2 - vector);
                    vectorDir2 = Vector3.zero;
                }
                else
                {
                    vector2 = tmpPoints[i + 1];
                    vectorDir = (vector - vector2);
                    vectorDir2 = Vector3.zero;
                }
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector + (-cross) * (width / 2);
                Vector3 rightVector = vector + cross * (width / 2);
                Vector3 leftVector2;
                Vector3 rightVector2;
                if (i > 1)
                {
                    Vector3 cross2 = Vector3.Cross(Vector3.up, vectorDir2).normalized;
                    leftVector2 = vector2 + (-cross2) * (width / 2);
                    rightVector2 = vector2 + cross2 * (width / 2);
                }
                else
                {
                    leftVector2 = vector2 + (-cross) * (width / 2);
                    rightVector2 = vector2 + cross * (width / 2);
                }
                //Debug.Log("Standard Circle B: " + this.name);
                if (!hasLeft)
                {
                    //Debug.Log("Node B Radius Left: " + nodeBRadius);
                    if (CiDyUtils.CircleIntersectsLine(nodeB.position, nodeBRadius, 360, rightVector, rightVector2, ref tmpIntersection))
                    {
                        //Found Left :)
                        tmpIntersection.y = nodeB.intersection.transform.position.y;
                        endPoints[2] = tmpIntersection;
                        hasLeft = true;
                        endPlace = i;
                    }
                }
                if (!hasRight)
                {
                    //Debug.Log("Node B Radius Right: " + nodeBRadius);
                    if (CiDyUtils.CircleIntersectsLine(nodeB.position, nodeBRadius, 360, leftVector, leftVector2, ref tmpIntersection))
                    {
                        //Found Left :)
                        tmpIntersection.y = nodeB.intersection.transform.position.y;
                        endPoints[3] = tmpIntersection;
                        hasRight = true;
                        endPlaceR = i;
                    }
                }
                if (hasLeft && hasRight)
                {
                    //We are Done with this side now find the Other sides.
                    break;
                }
            }
            if (endPoints.Length < 4)
            {
                Debug.Log("Not Enough Points: " + name);
                return;
            }

            //Debug.Log("EndOf Second Cycle: " + name);
            //int totalPoints = (endPlace - startPlace)*2;
            snipPoints = new List<Vector3>(0);
            List<Vector3> newVerts = new List<Vector3>();
            List<int> tmpLeftVerts = new List<int>(0);
            List<int> tmpRightVerts = new List<int>(0);
            //float totalDist = 0;
            for (int i = startPlace; i < endPlace; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = tmpPoints[i];
                Vector3 vector2;
                //Dir based on next in line.
                Vector3 vectorDir;
                if (i != tmpPoints.Count - 1)
                {
                    vector2 = tmpPoints[i + 1];
                    vectorDir = (vector2 - vector);
                }
                else
                {
                    vector2 = tmpPoints[i - 1];
                    vectorDir = (vector - vector2);
                }
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector + (-cross) * (width / 2);
                Vector3 rightVector = vector + cross * (width / 2);
                newVerts.Add(leftVector);
                tmpLeftVerts.Add(newVerts.Count - 1);//Add Left Vert
                newVerts.Add(rightVector);
                tmpRightVerts.Add(newVerts.Count - 1);//Add Left Vert
                snipPoints.Add(tmpPoints[i]);
            }
            if (newVerts.Count < 4)
            {
                return;
            }
            //Debug.Log("EndOf Last Cycle: " + name);
            newVerts[0] = endPoints[0];
            newVerts[1] = endPoints[1];
            startRightDir = (newVerts[1] - newVerts[0]).normalized;
            //Update
            snipPoints[0] = (newVerts[0] + newVerts[1]) / 2;
            newVerts[newVerts.Count - 2] = endPoints[2];
            newVerts[newVerts.Count - 1] = endPoints[3];
            endRightDir = (newVerts[newVerts.Count - 2] - newVerts[newVerts.Count - 1]).normalized;
            snipPoints[snipPoints.Count - 1] = (newVerts[newVerts.Count - 2] + newVerts[newVerts.Count - 1]) / 2;
            //Store Left Verts positions into Array
            leftEdge = new Vector3[newVerts.Count / 2];
            for (int n = 0; n < leftEdge.Length; n++)
            {
                leftEdge[n] = newVerts[tmpLeftVerts[n]];
            }
            //Store Right Verts positions into Array
            rightEdge = new Vector3[newVerts.Count / 2];
            for (int n = 0; n < rightEdge.Length; n++)
            {
                rightEdge[n] = newVerts[tmpRightVerts[n]];
            }
            //Clear Sub Holders
            if (dividerLane != null)
            {
                //Destroy
                DestroyImmediate(dividerLane);
            }
            if (shoulderLane != null)
            {
                DestroyImmediate(shoulderLane);
            }
            //There are two Road Markings required for any Multi-Lane Road, We need to Setup the Spawners for the Road Markings.
            //Create Details Mesh
            width = CiDyUtils.GenerateDetailedRoad(snipPoints.ToArray(), laneCount, laneWidth, laneType, leftShoulderWidth, rightShoulderWidth, centerSpacing, transform, this, roadMaterial, dividerLaneMaterial, shoulderMaterial, createMarkings, startRightDir, endRightDir);
            //Reference Divider Lane
            Transform divLane = transform.Find("DividerLane");
            if (divLane != null)
            {
                dividerLane = divLane.gameObject;
            }
            Transform shouldLane = transform.Find("Shoulders");
            if (shouldLane != null)
            {
                shoulderLane = shouldLane.gameObject;
            }
            ///////////////////////////////////////////////////CROSSWALK Decal LOGIC
            if (decals != null && decals.Count > 0)
            {
                for (int i = 0; i < decals.Count; i++)
                {
                    if (decals[i] != null)
                        DestroyImmediate(decals[i].gameObject);
                }
                decals = new List<Transform>(0);
            }
            if (crossWalksAtIntersections)
            {
                float decalRaisedHeight = 0.06f;

                if (nodeA.type == CiDyNode.IntersectionType.tConnect)
                {
                    Vector3 leftLineDir = (newVerts[2] - newVerts[0]).normalized;
                    Vector3 rightLineDir = (newVerts[3] - newVerts[1]).normalized;
                    Vector3 leftVert = newVerts[0] + (leftLineDir * 3.6576f);
                    Vector3 rightVert = newVerts[1] + (rightLineDir * 3.6576f);
                    //Create Decal and Nest it into this Road Holder
                    Vector3[] decalVerts = new Vector3[4];
                    decalVerts[0] = newVerts[0];
                    decalVerts[1] = leftVert;
                    decalVerts[2] = rightVert;
                    decalVerts[3] = newVerts[1];
                    //Create Quad Mesh
                    GameObject decalA = new GameObject("DecalA");
                    decalA.transform.position = transform.position + (Vector3.up * decalRaisedHeight);
                    decalA.transform.SetParent(transform);
                    MeshRenderer rendere = decalA.AddComponent<MeshRenderer>();
                    rendere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    MeshFilter filter = decalA.AddComponent<MeshFilter>();
                    rendere.sharedMaterial = (Material)Resources.Load("CiDyResources/CrossWalkDecal", typeof(Material));
                    //Create Mesh and Set it to Filter
                    Mesh crossWalkMesh = new Mesh();
                    crossWalkMesh = CiDyUtils.AddQuad(crossWalkMesh, decalVerts[0], decalVerts[1], decalVerts[2], decalVerts[3]);
                    filter.sharedMesh = crossWalkMesh;
                    decals.Add(decalA.transform);
                }
                if (nodeB.type == CiDyNode.IntersectionType.tConnect)
                {
                    Vector3 leftLineDir = (newVerts[newVerts.Count - 4] - newVerts[newVerts.Count - 2]).normalized;
                    Vector3 rightLineDir = (newVerts[newVerts.Count - 5] - newVerts[newVerts.Count - 1]).normalized;
                    Vector3 leftVert = newVerts[newVerts.Count - 2] + (leftLineDir * 3.6576f);
                    Vector3 rightVert = newVerts[newVerts.Count - 1] + (rightLineDir * 3.6576f);
                    //Create Decal and Nest it into this Road Holder
                    Vector3[] decalVerts = new Vector3[4];
                    decalVerts[0] = newVerts[newVerts.Count - 2];
                    decalVerts[1] = leftVert;
                    decalVerts[2] = rightVert;
                    decalVerts[3] = newVerts[newVerts.Count - 1];
                    //Create Quad Mesh
                    GameObject decalB = new GameObject("DecalB");
                    decalB.transform.position = transform.position + (Vector3.up * decalRaisedHeight);
                    decalB.transform.SetParent(transform);
                    MeshRenderer rendere = decalB.AddComponent<MeshRenderer>();
                    rendere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    MeshFilter filter = decalB.AddComponent<MeshFilter>();
                    rendere.sharedMaterial = (Material)Resources.Load("CiDyResources/CrossWalkDecal", typeof(Material));
                    //Create Mesh and Set it to Filter
                    Mesh crossWalkMesh = new Mesh();
                    crossWalkMesh = CiDyUtils.AddQuad(crossWalkMesh, decalVerts[0], decalVerts[1], decalVerts[2], decalVerts[3], true);
                    filter.sharedMesh = crossWalkMesh;
                    decals.Add(decalB.transform);
                }
            }

            tris = new List<int>();
            uvs = new List<Vector2>();
            //Look at four points at a time
            for (int i = 0; i < newVerts.Count - 2; i += 2)
            {
                tris.Add(i);//0
                tris.Add(i + 2);//2
                tris.Add(i + 1);//1

                tris.Add(i + 1);//1
                tris.Add(i + 2);//2
                tris.Add(i + 3);//3
            }
            //Setup UVs
            if (uvsRoadSet)
            {
                float uvDist = 0;
                float zDist = 0;
                //Set up UVs for Three Segments and Up.
                for (int i = 0; i < newVerts.Count - 2; i += 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(1, uvDist));
                    //Get Vertical Distance
                    Vector3 midPointA = (newVerts[i] + newVerts[i + 1]) / 2;
                    Vector3 midPointB = (newVerts[i + 2] + newVerts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = (Vector3.Distance(midPointA, midPointB)) / 60;
                    uvDist += zDist;
                }
                //Add Last Two Points
                uvs.Add(new Vector2(0, uvDist));
                uvs.Add(new Vector2(1, uvDist));
            }
            else
            {
                //Set Uvs based on X/Z Values
                for (int i = 0; i < newVerts.Count; i++)
                {
                    uvs.Add(new Vector2(newVerts[i].x, newVerts[i].z));
                }
            }
            //Set Triangles and 
            Mesh roadMesh = new Mesh
            {
                vertices = newVerts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            roadMesh.RecalculateBounds();
            roadMesh.RecalculateNormals();
            //Spawn Sign if it exist
            if (spawnedSigns[0] != null)
            {
                DestroyImmediate(spawnedSigns[0]);
            }
            if (spawnedSigns[1] != null)
            {
                DestroyImmediate(spawnedSigns[1]);
            }
            //Debug.Log("Finalizing Stop Sign: "+name);
            if (stopSign != null)
            {
                //Determine what Stop Sign Prefab we will spawn based on Lane Type.
                GameObject signPrefab = stopSign;//Initialize
                if (lightType == LightType.TrafficLight)
                {
                    switch (laneType)
                    {
                        case CiDyRoad.LaneType.OneLane:
                            signPrefab = stopSign;
                            break;
                        case CiDyRoad.LaneType.TwoLane:
                            signPrefab = stopSign;
                            break;
                        case CiDyRoad.LaneType.FourLane:
                            signPrefab = stopSignTwoLane;
                            break;
                        case CiDyRoad.LaneType.SixLane:
                            signPrefab = stopSignThreeLane;
                            break;
                    }
                }
                if (roadMesh.vertices.Length >= 4)
                {
                    GameObject sign = null;
                    Vector3 fwd = Vector3.zero;
                    Vector3 nxt = Vector3.zero;
                    Vector3 rightDir = Vector3.zero;
                    //Create Sign Prefab with Instance to Original
                    //Check this Nodes end.
                    if (nodeA.type == CiDyNode.IntersectionType.tConnect && laneType != LaneType.OneLane)
                    {
                        rightDir = (roadMesh.vertices[0] - roadMesh.vertices[1]).normalized;
                        rightDir *= 1.618f;
                        if (flipStopSign)
                        {
                            //Left Hand Spawn StopSign
#if UNITY_EDITOR
                            //Get path to nearest (in case of nested) prefab from this gameObject in the scene
                            string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(signPrefab);
                            //Get prefab object from path
                            Object prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
                            //Instantiate the prefab in the scene, as a sibling of current gameObject
                            sign = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#if SimpleTrafficSystem
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(sign, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
#endif
#endif
                            //Calculate forward
                            fwd = (roadMesh.vertices[2] - roadMesh.vertices[0]).normalized;
                            fwd.y = 0;
                            sign.transform.position = (roadMesh.vertices[1] + (-rightDir)) + (Vector3.up * 0.1618f)+(fwd*1.618f);
                            sign.transform.parent = transform;
                            nxt = (roadMesh.vertices[1] + (fwd * 2)) + (-rightDir) + (Vector3.up * 0.1618f);
                            sign.transform.LookAt(nxt, Vector3.up);
                            spawnedSigns[0] = sign;
                        }
                        else
                        {
                            //USA Right Hand Traffic
                            //Create First
                            //Left Hand Spawn StopSign
#if UNITY_EDITOR
                            //Get path to nearest (in case of nested) prefab from this gameObject in the scene
                            string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(signPrefab);
                            //Get prefab object from path
                            Object prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
                            //Instantiate the prefab in the scene, as a sibling of current gameObject
                            sign = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#if SimpleTrafficSystem
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(sign, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
#endif
#endif
                            //Calculate Forward
                            fwd = (roadMesh.vertices[2] - roadMesh.vertices[0]).normalized;
                            fwd.y = 0;
                            sign.transform.position = (roadMesh.vertices[0] + rightDir + (Vector3.up * 0.1618f))+(fwd * 1.618f);
                            sign.transform.parent = transform;
                            nxt = (roadMesh.vertices[0] + (fwd * 2)) + (rightDir + (Vector3.up * 0.1618f));
                            sign.transform.LookAt(nxt, Vector3.up);
                            spawnedSigns[0] = sign;
                        }
                    }
                    //Check this Nodes end.
                    if (nodeB.type == CiDyNode.IntersectionType.tConnect)
                    {
                        rightDir = (roadMesh.vertices[roadMesh.vertices.Length - 1] - roadMesh.vertices[roadMesh.vertices.Length - 2]).normalized;
                        if (flipStopSign)
                        {
                            //Left Hand Traffic
#if UNITY_EDITOR
                            //Get path to nearest (in case of nested) prefab from this gameObject in the scene
                            string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(signPrefab);
                            //Get prefab object from path
                            Object prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
                            //Instantiate the prefab in the scene, as a sibling of current gameObject
                            sign = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#if SimpleTrafficSystem
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(sign, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
#endif
#endif
                            //Calculate Fwd
                            fwd = (roadMesh.vertices[roadMesh.vertices.Length - 3] - roadMesh.vertices[roadMesh.vertices.Length - 1]).normalized;
                            fwd.y = 0;
                            sign.transform.position = (roadMesh.vertices[roadMesh.vertices.Length - 2] + (-rightDir)) + (Vector3.up * 0.1618f) + (fwd * 1.618f);
                            sign.transform.parent = transform;
                            nxt = (roadMesh.vertices[roadMesh.vertices.Length - 2] + (fwd * 2)) + ((-rightDir) + (Vector3.up * 0.1618f));
                            sign.transform.LookAt(nxt, Vector3.up);
                            spawnedSigns[1] = sign;
                        }
                        else
                        {
                            //USA Right Hand Traffic
                            //Create Second
#if UNITY_EDITOR
                            //Get path to nearest (in case of nested) prefab from this gameObject in the scene
                            string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(signPrefab);
                            //Get prefab object from path
                            Object prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
                            //Instantiate the prefab in the scene, as a sibling of current gameObject
                            sign = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#if SimpleTrafficSystem
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(sign, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
#endif
#endif
                            //Calculate Fwd
                            fwd = (roadMesh.vertices[roadMesh.vertices.Length - 3] - roadMesh.vertices[roadMesh.vertices.Length - 1]).normalized;
                            fwd.y = 0;
                            sign.transform.position = (roadMesh.vertices[roadMesh.vertices.Length - 1] + rightDir) + (Vector3.up * 0.1618f) + (fwd * 1.618f);
                            sign.transform.parent = transform;
                            nxt = (roadMesh.vertices[roadMesh.vertices.Length - 1] + (fwd * 2)) + (rightDir + (Vector3.up * 0.1618f));
                            sign.transform.LookAt(nxt, Vector3.up);
                            spawnedSigns[1] = sign;
                        }
                    }
                }
            }

            if (!snapToGroundLocal)
            {
                //Check Ground Support
                CheckForGroundSupport();
            }
            //Update CiDySpawners
            UpdateSpawners();
            //Update Traffic Lines
            GenerateTrafficLanes();
        }

        //Clear Traffic Routes
        public void ClearRoutes() {
            leftRoutes = new CiDyRouteData();
            rightRoutes = new CiDyRouteData();
        }

        public void ClearNewRoutes() {
            //Clear Prevoius Connections that were generated as they may no longer be valid.
            for (int j = 0; j < leftRoutes.routes.Count; j++)
            {
                leftRoutes.routes[j].newRoutePoints.Clear();
            }
            for (int j = 0; j < rightRoutes.routes.Count; j++)
            {
                rightRoutes.routes[j].newRoutePoints.Clear();
            }
        }

        //Generates Traffic Lines for 
        public void GenerateTrafficLanes() {
            //Traffic Waypoint Spacing
            //Debug.Log("Generating Traffic Lanes: ");
            //initialize Routes Data
            ClearRoutes();
            float lastPointSpacing = graph.crossWalkTrafficStopDistance;
            float lastPointStopSpacing = 2f;
            //We Always Calculate and Store Lanes from Left to Right of roads natural forward direction.
            Vector3 strtDir = Vector3.zero;
            Vector3 endDir = Vector3.zero;
            //Create updated source Lane with Desired Spacing
            Vector3[] sourcePoints = CiDyUtils.CreateTrafficWaypoints(snipPoints.ToArray(), (float)graph.globalTrafficWaypointDistance);
            //Determine if Node A and Node B are Intersections?
            bool intersectionAtA = false;//Initialize
            bool intersectionAtB = false;//Initialize
            bool continuedAtA = false;
            bool continuedAtB = false;
            if (nodeA.type == CiDyNode.IntersectionType.tConnect) {
                intersectionAtA = true;
            }
            if (nodeA.type == CiDyNode.IntersectionType.continuedSection)
            {
                continuedAtA = true;
            }
            if (nodeB.type == CiDyNode.IntersectionType.tConnect)
            {
                intersectionAtB = true;
            }
            if (nodeB.type == CiDyNode.IntersectionType.continuedSection)
            {
                continuedAtB = true;
            }
            //Single Lane or Multi?
            if (laneCount > 1)
            {
                //We need to know when to account for the divider Lane.
                int halfCount = laneCount / 2;
                //Multi-Lane Calculation, So We offset a starting path at farthest left of road.
                Vector3[] startingLane = CiDyUtils.OffsetPath(sourcePoints.ToArray(), -((centerSpacing / 2) + (laneWidth * (laneCount / 2)) - (laneWidth / 2)), ref strtDir, ref endDir, false, false, true);
                //Store Starting Lane
                CiDyRoute newRoute = new CiDyRoute();
                newRoute.waypoints = new List<Vector3>(startingLane);
                //Starting Lane is Also the Left side
                if (!graph.globalLeftHandTraffic)
                {
                    //Reverse as left hand side of right handed traffic is reversed from forward road direction.
                    newRoute.waypoints.Reverse();
                }
                //Calculate Reverse Direction
                Vector3 reverseDir = (newRoute.waypoints[newRoute.waypoints.Count - 2] - newRoute.waypoints[newRoute.waypoints.Count - 1]).normalized;
                if (!graph.globalLeftHandTraffic)
                {
                    if (intersectionAtA)
                    {
                        //Push the Last point 3.6576f back(That is size of CrossWalk
                        newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                        //Now push second to last to 2 meter spacing
                        newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                    }
                    else if (continuedAtA) {
                        //Push the Last point 3.6576f back(That is size of CrossWalk
                        newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                    }
                }
                else if (graph.globalLeftHandTraffic) {
                    if (intersectionAtB)
                    {
                        //Push the Last point 3.6576f back(That is size of CrossWalk
                        newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                        //Now push second to last to 2 meter spacing
                        newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                    }
                    else if (continuedAtB)
                    {
                        //Push the Last point 3.6576f back(That is size of CrossWalk
                        newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                    }
                }
                //Add Route
                leftRoutes.routes.Add(newRoute);
                //This is our Starting Lane of the Farthest Left.
                //From here we iterate and increment the offset.
                for (int n = 1; n < laneCount; n++)
                {
                    //Initialize
                    Vector3[] currentLane = new Vector3[0];

                    //New Route
                    newRoute = new CiDyRoute();

                    if (n >= halfCount)
                    {
                        //Right Side of Center Divider
                        currentLane = CiDyUtils.OffsetPath(startingLane, (n * laneWidth) + centerSpacing, ref strtDir, ref endDir);
                        //Update Route Points
                        newRoute.waypoints = new List<Vector3>(currentLane.ToList());
                        //If Not Left Hand Traffic, Then These Lanes get Reversed
                        if (graph.globalLeftHandTraffic)
                        {
                            //Reverse Direction
                            newRoute.waypoints.Reverse();
                        }
                        //Push the Last point 3.6576f back(That is size of CrossWalk)
                        reverseDir = (newRoute.waypoints[newRoute.waypoints.Count - 2] - newRoute.waypoints[newRoute.waypoints.Count - 1]).normalized;
                        if (!graph.globalLeftHandTraffic)
                        {
                            if (intersectionAtB)
                            {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                                //Now push second to last to 1 meter spacing
                                newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                            }
                            else if (continuedAtB) {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                            }
                        }
                        else if (graph.globalLeftHandTraffic)
                        {
                            if (intersectionAtA)
                            {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                                //Now push second to last to 1 meter spacing
                                newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                            }
                            else if (continuedAtA) {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                            }
                        }
                        //Add Route
                        rightRoutes.routes.Add(newRoute);
                    }
                    else
                    {
                        //Left Side Offset
                        currentLane = CiDyUtils.OffsetPath(startingLane, (n * laneWidth), ref strtDir, ref endDir);
                        //Update Route Points
                        newRoute.waypoints = new List<Vector3>(currentLane.ToList());
                        //If Not Left Hand Traffic, Then These Lanes get Reversed
                        if (!graph.globalLeftHandTraffic)
                        {
                            //Reverse Direction
                            newRoute.waypoints.Reverse();
                        }
                        //Push the Last point 3.6576f back(That is size of CrossWalk)
                        reverseDir = (newRoute.waypoints[newRoute.waypoints.Count - 2] - newRoute.waypoints[newRoute.waypoints.Count - 1]).normalized;
                        if (graph.globalLeftHandTraffic)
                        {
                            if (intersectionAtB)
                            {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                                //Now push second to last to 1 meter spacing
                                newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                            }
                            else if (continuedAtB) {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                            }
                        }
                        else if (!graph.globalLeftHandTraffic)
                        {
                            if (intersectionAtA)
                            {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                                //Now push second to last to 1 meter spacing
                                newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                            }
                            else if (continuedAtA) {
                                //Push the Last point 3.6576f back(That is size of CrossWalk
                                newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                            }
                        }
                        //Add Route
                        leftRoutes.routes.Add(newRoute);
                    }
                }
            }
            else
            {
                //New Route
                CiDyRoute newRoute = new CiDyRoute();
                //Update Route Points
                newRoute.waypoints = CiDyUtils.OffsetPath(sourcePoints.ToArray(), 0, ref strtDir, ref endDir, false, false, true).ToList();
                //Always check Node B
                if (intersectionAtB)
                {
                    //Push the Last point 3.6576f back(That is size of CrossWalk)
                    Vector3 reverseDir = (newRoute.waypoints[newRoute.waypoints.Count - 2] - newRoute.waypoints[newRoute.waypoints.Count - 1]).normalized;
                    newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointSpacing;
                    //Now push second to last to 1 meter spacing
                    newRoute.waypoints[newRoute.waypoints.Count - 2] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * lastPointStopSpacing;
                }
                else if (continuedAtB) {
                    //Push the Last point 3.6576f back(That is size of CrossWalk)
                    Vector3 reverseDir = (newRoute.waypoints[newRoute.waypoints.Count - 2] - newRoute.waypoints[newRoute.waypoints.Count - 1]).normalized;
                    newRoute.waypoints[newRoute.waypoints.Count - 1] = newRoute.waypoints[newRoute.waypoints.Count - 1] + reverseDir * 1.6f;
                }
                //Add to Left Routes
                leftRoutes.routes.Add(newRoute);
            }
        }

        //This function is called when we want to change the applied Material to the Road.
        public void ChangeRoadMaterial()
        {
            mRender.sharedMaterial = graph.roadMaterial;
            lastMaterial = graph.roadMaterial;
        }
        //Specific Material
        public void ChangeRoadMaterial(Material newMat)
        {
            mRender.sharedMaterial = newMat;
            lastMaterial = newMat;
        }

        //Functions used to Select and Deselect the Road(IE. Change Material)
        [SerializeField]
        Material lastMaterial;
        [SerializeField]
        Material selectedMaterial;

        public void SelectRoad()
        {
            //Debug.Log ("Select Road "+selectedMaterial.name);
            //Change Material
            if (selectedMaterial == null)
            {
                selectedMaterial = (Material)Resources.Load("CiDyResources/ActiveRoadMaterial", typeof(Material));
            }
            lastMaterial = mRender.sharedMaterial;
            mRender.sharedMaterial = selectedMaterial;
            selected = true;
        }

        public void DeselectRoad()
        {
            //Debug.Log ("Deselected Road "+lastMaterial.name);
            mRender.sharedMaterial = lastMaterial;
            selected = false;
        }

        //Divider Lane Reference
        private GameObject dividerLane = null;
        //Shoulder Lane Reference
        private GameObject shoulderLane = null;
        //Special Road Markings Spawners
        public CiDySpawner[] markingSpawners;
        //CiDySpawners
        public List<CiDySpawner> spawnerSplines;
        //Add Spawn Spline
        public void AddSpawnSpline()
        {
            if (spawnerSplines == null)
            {
                spawnerSplines = new List<CiDySpawner>(0);
            }
            //Add Spawner Spline
            GameObject spawnerSpline = new GameObject("CiDySpawnerSpline");
            spawnerSpline.transform.SetParent(transform);
            //Add Component
            CiDySpawner spawner = spawnerSpline.AddComponent<CiDySpawner>();
            //Set Path to Spawner
            spawner.SetPath(snipPoints.ToArray(), width, startRightDir, endRightDir);
            //Add to List.
            spawnerSplines.Add(spawner);
        }

        //This is Called when Generating a Road and its Markings. (Multi-Lane or Single Lane)
        public void AddMarkingSpawner(CiDySpawner newSpawner, int index)
        {
            //If we dont have any yet, Initialize
            if (markingSpawners == null)
            {
                markingSpawners = new CiDySpawner[2];//Max is two
            }
            else
            {
                //Markings are not Empty and we are putting a max of two markings per road.This means we are overwrighting and must delete the previous marking.
                if (markingSpawners[index] != null)
                {
                    //Delete It
                    GameObject.DestroyImmediate(markingSpawners[index].gameObject);
                }
            }
            //Add to List.
            markingSpawners[index] = newSpawner;
        }

        //Simple Function that Clears Any Marking Spawners.
        public void ClearMarkingSpawners()
        {
            if (markingSpawners != null && markingSpawners.Length > 0)
            {
                for (int i = 0; i < markingSpawners.Length; i++)
                {
                    //Delete It
                    if (markingSpawners[i] != null)
                        GameObject.DestroyImmediate(markingSpawners[i].gameObject);
                }
                markingSpawners = new CiDySpawner[2];//Max is two
            }
        }

        public void RemoveSpawnerSpline(int idx)
        {
            //Destroy the GameObject and Remove Null Reference from list
            GameObject spawnerObj = spawnerSplines[idx].gameObject;
            spawnerSplines.RemoveAt(idx);
            //Destroy Object
            DestroyImmediate(spawnerObj);
        }

        void UpdateSpawners()
        {
            if (spawnerSplines == null || spawnerSplines.Count == 0)
            {
                //Nothing to Update
                return;
            }

            //Update Paths for Spawners
            for (int i = 0; i < spawnerSplines.Count; i++)
            {
                CiDySpawner spawner = spawnerSplines[i];
                if (spawner == null)
                {
                    continue;
                }
                if (spawner.spawnType == CiDySpawner.SpawnerType.RoadMarkings)
                {
                    //spawner.SetPath(snipPoints.ToArray(), width, startRightDir, endRightDir, true);
                }
                else
                {
                    //Update Spawners Path points.
                    spawner.SetPath(snipPoints.ToArray(), width, startRightDir, endRightDir, true);
                }
            }

        }
    }

    //This class is used To Rebuild a Road Mesh after Intersections have determined there Shapes. (Doesnt need to be serialized)
    public class TmpVert
    {
        //Needs to hold its Position and If its On or Off.
        public bool state = true;
        public Vector3 pos;//The Position its at.

        public TmpVert(Vector3 newPos, bool isOn)
        {

            state = isOn;
            pos = newPos;
        }
    }

    [System.Serializable]
    public class CiDyRouteData
    {
        public List<CiDyRoute> routes;
        public List<CiDyIntersectionRoute> intersectionRoutes;

        public CiDyRouteData()
        {
            routes = new List<CiDyRoute>(0);
            intersectionRoutes = new List<CiDyIntersectionRoute>(0);
        }

        public void Clear() {
            routes = new List<CiDyRoute>(0);
            intersectionRoutes = new List<CiDyIntersectionRoute>(0);
        }

        public void ClearNewRoutes()
        {
            for(int i = 0; i < routes.Count; i++)
            {
                routes[i].ClearNewRoutes();
            }
        }
    }
    [System.Serializable]
    public class CiDyRoute
    {
        public int routeId;//Used by CiDy to test Cross Over Routes at an Intersection.
        public List<Vector3> waypoints;
        public List<Vector3> newRoutePoints;

        public CiDyRoute()
        {
            waypoints = new List<Vector3>(0);
            newRoutePoints = new List<Vector3>(0);
        }

        public CiDyRoute(int newId)
        {
            routeId = newId;
            waypoints = new List<Vector3>(0);
            newRoutePoints = new List<Vector3>(0);
        }

        public CiDyRoute(List<Vector3> newWaypoints)
        {
            waypoints = newWaypoints;
            newRoutePoints = new List<Vector3>(0);
        }

        public CiDyRoute(Vector3[] newWaypoints)
        {
            waypoints = newWaypoints.ToList();
            newRoutePoints = new List<Vector3>(0);
        }

        public void Clear() {
            waypoints = new List<Vector3>(0);
            newRoutePoints = new List<Vector3>(0);
        }

        public void ClearNewRoutes()
        {
            newRoutePoints = new List<Vector3>(0);
        }
    }

    //For STS Traffic Light Integration
    [System.Serializable]
    public class CiDyIntersectionRoute
    {
        //CiDy Specifc
        public CiDyRoute route;//The Route for this Intersection.
        public Vector3 finalRoutePoint; // can be used to find the route
        public Transform light; // light that will control this route
        public int sequenceIndex; // determines which routes are active in a AITrafficLightManager sequence together

        public CiDyIntersectionRoute(int sequenceId,int routeId, Vector3[] routeList, Vector3 finalPoint, Transform trafficLightHolder) {
            sequenceIndex = sequenceId;
            route = new CiDyRoute(routeId);
            route.waypoints = routeList.ToList();
            finalRoutePoint = finalPoint;
            light = trafficLightHolder;
        }
    }
}



