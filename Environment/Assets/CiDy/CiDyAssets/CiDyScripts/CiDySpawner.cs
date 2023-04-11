using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CiDy
{
    public class CiDySpawner : MonoBehaviour
    {
        public float splineDetail = 1;
        public Transform[] userPoints;
        [SerializeField]
        private float roadWidth;
        [SerializeField]
        private Vector3[] spline;
        private Vector3[] normalizedSplineRange;
        private Vector3 startRightDir;
        private Vector3 endRightDir;
        [SerializeField]
        private bool flipUVS = false;
        //Spawn Type
        public enum SpawnerType
        {
            Prefab,
            MeshExtrusion,
            //PrefabMeshExtrusion,
            RoadMarkings,
            PowerLines
        }

        public SpawnerType spawnType = SpawnerType.RoadMarkings;
        //Markings Type
        public enum RoadMarkings
        {
            DoubleLine,
            LeftPassLine,
            RightPassLine,
            OneWayLine,
            OneWayWhite_Yellow_Line,
            SixLane,
            FourLane,
            TwoLane
        }

        public RoadMarkings roadMarkings = RoadMarkings.DoubleLine;
        //Offset Float
        public float pathOffset = 0;
        public bool reverseDirection = false;//Determines if we want to Flip the Path Input.
                                             //Normalized Length Offset
        [Range(0, 1)]
        public float normalizedOffsetMin = 0f;
        [Range(0, 1)]
        public float normalizedOffsetMax = 1f;
        //Prefab Variables
        public GameObject prefab;
        public float spacing = 1f;
        public Vector3 rotationCorrection = Vector3.zero;
        public Vector3 positionCorrection = Vector3.zero;
        public bool worldUp = false;//Uses World up(true) or Local Up.(False)
        public bool setToGround = false;//If true, we do a raycast to the first thing we hit below us.
                                        //Telegrpah Prefab Variables
        public GameObject telegraphPrefab;
        public float cableDrop = 1.5f;
        [SerializeField]
        private GameObject[] objectArray;
        //Pre-Defined Spline Mesh
        public GameObject mainMesh;
        public GameObject capMesh;

        //Spawn Type
        public enum SplineMeshType
        {
            GuardRail,
            Sidewalk,
            Barrier
        }

        public SplineMeshType splineMeshType = SplineMeshType.GuardRail;
        //Variables for Specific Spline MeshTypes
        //SideWalk
        public float sidewalkHeight = 0.1618f;
        public float sideWalkWidth = 5.4f;
        public float sideWalkEdgeWidth = 0.4f;
        public float sideWalkEdgeHeight = 0.1f;
        public Material sideWalkMaterial;
        public Material sideWalkEdgeMaterial;
        //Guard Rail
        public bool leftFacing = false;//Determines which Side the Rail will Be Configured.
        public Material railMaterial;
        public Material postMaterial;
        //Barrier 
        public float bottomWidth = 1f;
        public float middleWidth = 0.5f;
        public float topWidth = 0.25f;
        public float topHeight = 1f;
        public float bottomHeight = 0.5f;
        public Material barrierMaterial;

        private bool loadResources = false;
        public void SetPath(Vector3[] newPath, float width, Vector3 _startRightDir, Vector3 _endRightDir, bool regenerate = false, bool _FlipUvs = false)
        {
            spline = newPath;
            roadWidth = width;
            startRightDir = _startRightDir;
            endRightDir = _endRightDir;
            flipUVS = _FlipUvs;

            //Set Prefabs
            if (!loadResources)
            {
                LoadResources();
            }
            if (regenerate)
            {
                Generate();
            }
        }

        //This function will Grab the Prefabs from Resources to Initialize the Fields.
        void LoadResources()
        {
            loadResources = true;

            //Grab Telegraph Prefab
            telegraphPrefab = Resources.Load("CiDyResources/PowerPole", typeof(GameObject)) as GameObject;
            //Grab SideWalk Materials
            sideWalkMaterial = Resources.Load("CiDyResources/SideWalk", typeof(Material)) as Material;
            sideWalkEdgeMaterial = Resources.Load("CiDyResources/SideWalkEdge", typeof(Material)) as Material;
            //Grab Barrier Material
            barrierMaterial = Resources.Load("CiDyResources/Concrete", typeof(Material)) as Material;
            //Grab GaurdRail Material
            railMaterial = Resources.Load("CiDyResources/GuardRail", typeof(Material)) as Material;
            postMaterial = railMaterial;
        }
        //
        public void Generate()
        {
            //Modify Spline to Account for the Desired Range.
            normalizedSplineRange = NormalizeSpline(spline, normalizedOffsetMin, normalizedOffsetMax);
            //Determine if we need start and end dir
            Vector3 _startDir = startRightDir;
            Vector3 _endDir = endRightDir;
            if (normalizedOffsetMin != 0)
            {
                _startDir = default;
            }
            if (normalizedOffsetMax != 1)
            {
                _endDir = default;
            }
            if (spawnType == SpawnerType.RoadMarkings)
            {
                //pathOffset = 0;
                setToGround = false;
            }
            //Offset Path
            normalizedSplineRange = CiDyUtils.OffsetPath(normalizedSplineRange, pathOffset, ref _startDir, ref _endDir, reverseDirection, setToGround);
            //Now that we have created a Spline for Testing.
            //Based on SpawnType
            switch (spawnType)
            {
                case SpawnerType.Prefab:
                    GeneratePrefabAlongPath(prefab, normalizedSplineRange);
                    break;
                case SpawnerType.MeshExtrusion:
                    GenerateSplineMesh(normalizedSplineRange, _startDir, _endDir);
                    break;
                //case SpawnerType.PrefabMeshExtrusion:
                //   break;
                case SpawnerType.RoadMarkings:
                    //Generate a Road Marking
                    GenerateRoadMarkingMesh(normalizedSplineRange, _startDir, _endDir, flipUVS);
                    break;
                case SpawnerType.PowerLines:
                    //Tell it to Generate the Prefabs along the Path and Create Connecting Powerlines.
                    GeneratePrefabAlongPath(telegraphPrefab, normalizedSplineRange, true);
                    break;
            }
        }

        //This Function will Generate a Road Mesh = to Roads Width with User Defined Marking Material.
        void GenerateRoadMarkingMesh(Vector3[] path, Vector3 _startDir = default, Vector3 _endDir = default, bool flipUvs = false)
        {
            ClearPreviousObjects();
            objectArray = new GameObject[1];
            //Extrude a Side Walk Along the Path
            //Create the GameObject that will hold this Mesh.
            GameObject newMarkings = new GameObject("Markings");
            newMarkings.transform.position = transform.position;
            newMarkings.transform.SetParent(transform);
            //Move Transform Up to correct Z-Fighting
            newMarkings.transform.position += (Vector3.up * 0.08f);
            MeshRenderer mRenderer = newMarkings.AddComponent<MeshRenderer>();
            mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFilter mFilter = newMarkings.AddComponent<MeshFilter>();
            //Generate MarkingMesh
            Mesh markingMesh = CiDyUtils.CreateRoad(path, roadWidth, _startDir, _endDir);
            if (flipUvs)
            {
                Vector2[] uvs = markingMesh.uv;
                //Flip Uvs for other Direction Lane while retaining proper distance stretching
                for (int i = 0; i < uvs.Length; i++)
                {
                    Vector2 uv = uvs[i];
                    uvs[i] = new Vector2(-uv.x, -uv.y);
                }
                markingMesh.uv = uvs;//Set Them Back
            }
            //Set Mesh to Filter
            mFilter.sharedMesh = markingMesh;

            switch (roadMarkings)
            {
                case RoadMarkings.DoubleLine:
                    //Double Line
                    //Set Material
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/DoubleLineMarking", typeof(Material));
                    break;
                case RoadMarkings.LeftPassLine:
                    //Left Pass Line
                    //Set Material
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/LeftLineMarking", typeof(Material));
                    break;
                case RoadMarkings.RightPassLine:
                    //Right Pass Line
                    //Set Material
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/RightLineMarking", typeof(Material));
                    break;
                case RoadMarkings.OneWayLine:
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/OneWayWhiteLine", typeof(Material));
                    break;
                case RoadMarkings.OneWayWhite_Yellow_Line:
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/OneWayLine", typeof(Material));
                    break;
                case RoadMarkings.SixLane:
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/SixLaneMarking", typeof(Material));
                    break;
                case RoadMarkings.FourLane:
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/FourLaneMarking", typeof(Material));
                    break;
                case RoadMarkings.TwoLane:
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/TwoLaneMarking", typeof(Material));
                    break;
            }
            //Set Object Array
            objectArray[0] = newMarkings;
        }

        Vector3[] NormalizeSpline(Vector3[] spline, float minVal, float maxVal)
        {
            List<Vector3> normalizedSpline = new List<Vector3>(0);
            //Calculate total Distance of Original Spline
            float totalDist = CiDyUtils.FindTotalDistOfPoints(spline);
            float curDist = 0;
            bool started = false;
            //Iterate through Points.
            for (int i = 0; i < spline.Length - 1; i++)
            {
                Vector3 curPos = spline[i];
                Vector3 nxtPos = spline[i + 1];
                float stepDist = Vector3.Distance(curPos, nxtPos);
                curDist += stepDist;
                float normalizedDist = (curDist / totalDist);// Mathf.Round((curDist/totalDist)*100)/100;
                                                             //Looking for MinVal?
                if (!started)
                {
                    if (normalizedDist >= minVal)
                    {
                        //Start Adding Points
                        normalizedSpline.Add(spline[i]);
                        started = true;
                    }
                }
                else
                {
                    //We have Already passed the MinVal, Now we are searching for the Max Val
                    if (normalizedDist <= maxVal)
                    {
                        normalizedSpline.Add(spline[i]);
                    }
                    else
                    {
                        //We are outside of our Range. End
                        break;
                    }
                }
            }
            //Add Last Point if > 0.99%
            if (maxVal >= 0.99f)
            {
                normalizedSpline.Add(spline[spline.Length - 1]);
            }
            if (normalizedSpline.Count < 3)
            {
                normalizedSpline = spline.ToList();
                Debug.LogWarning("Normalized Spline Failed, Returning Full Original Path", gameObject);
            }
            //Return List as Array.
            return normalizedSpline.ToArray();
        }

        void GenerateSplineMesh(Vector3[] path, Vector3 startDir = default, Vector3 endDir = default)
        {
            if (path == null || path.Length == 0)
            {
                Debug.LogWarning("No Path is Present");
                return;
            }
            //Clear Previouse Objects
            ClearPreviousObjects();
            //Switch to Determine What Mesh will be Generated.
            switch (splineMeshType)
            {
                case SplineMeshType.Sidewalk:
                    objectArray = new GameObject[1];
                    //Extrude a Side Walk Along the Path
                    //Create the GameObject that will hold this Mesh.
                    GameObject newSideWalk = new GameObject("SideWalk");
                    newSideWalk.transform.position = transform.position;
                    newSideWalk.transform.SetParent(transform);
                    MeshRenderer mRenderer = newSideWalk.AddComponent<MeshRenderer>();
                    MeshFilter mFilter = newSideWalk.AddComponent<MeshFilter>();
                    MeshCollider collider = newSideWalk.AddComponent<MeshCollider>();
                    //Set Material
                    mRenderer.sharedMaterial = sideWalkMaterial;
                    //Generate SideWalkMesh
                    Mesh sideWalkMesh = CiDyUtils.ExtrudeDetailedSideWalk(path, leftFacing, sidewalkHeight, sideWalkWidth, sideWalkEdgeWidth, sideWalkEdgeHeight, newSideWalk.transform, newSideWalk.transform, sideWalkEdgeMaterial, true, startDir, endDir);
                    //Add Mesh Collider
                    collider.sharedMesh = sideWalkMesh;
                    //Set Mesh to Filter
                    mFilter.sharedMesh = sideWalkMesh;
                    objectArray[0] = newSideWalk;
                    break;
                case SplineMeshType.GuardRail:
                    objectArray = new GameObject[1];
                    //Calculate Left Rail Line and Right Rail Line
                    //Now generate Rails down the road.(RailRoad Meshes)
                    GameObject railing = CiDyUtils.GenerateGuardRailandPost(path, leftFacing, railMaterial, postMaterial, true, startDir, endDir);
                    railing.transform.parent = transform;
                    objectArray[0] = railing;
                    break;
                case SplineMeshType.Barrier:
                    objectArray = new GameObject[1];
                    //Extrude a Side Walk Along the Path
                    //Create the GameObject that will hold this Mesh.
                    GameObject barrier = new GameObject("Barrier");
                    barrier.transform.position = transform.position;
                    barrier.transform.SetParent(transform);
                    mRenderer = barrier.AddComponent<MeshRenderer>();
                    mFilter = barrier.AddComponent<MeshFilter>();
                    MeshCollider mCollider = barrier.AddComponent<MeshCollider>();
                    //Set Material
                    mRenderer.sharedMaterial = barrierMaterial;
                    //Generate SideWalkMesh
                    Vector3 position = path[0];
                    Vector3[] shape = new Vector3[6];
                    //Bottom Points
                    Vector3 pointA = position + (-Vector3.right * (bottomWidth / 2));
                    Vector3 pointB = position + (Vector3.right * (bottomWidth / 2));
                    //Middle Points
                    Vector3 pointC = position + (Vector3.up * bottomHeight) + (-Vector3.right * (middleWidth / 2));
                    Vector3 pointD = position + (Vector3.up * bottomHeight) + (Vector3.right * (middleWidth / 2));
                    //Top Points
                    Vector3 pointE = position + (Vector3.up * (bottomHeight + topHeight)) + (-Vector3.right * (topWidth / 2));
                    Vector3 pointF = position + (Vector3.up * (bottomHeight + topHeight)) + (Vector3.right * (topWidth / 2));
                    shape[0] = pointB - position;//B
                    shape[1] = pointD - position;//D
                    shape[2] = pointF - position;//F
                    shape[3] = pointE - position;//E
                    shape[4] = pointC - position;//C
                    shape[5] = pointA - position;//A
                                                 //Set Mesh to Filter
                    mFilter.sharedMesh = CiDyUtils.ExtrudeRail(shape, path, transform, startDir, endDir);
                    //Add Collider
                    mCollider.sharedMesh = mFilter.sharedMesh;
                    //Add To Object Array
                    objectArray[0] = barrier;
                    break;
            }
        }

        //This Function will Generate a Prefab along a Path
        void GeneratePrefabAlongPath(GameObject obj, Vector3[] path, bool powerLines = false)
        {
            if (obj == null)
            {
                Debug.LogWarning("Prefab is Empty?", this.gameObject);
                return;
            }
            if (path == null || path.Length == 0)
            {
                Debug.LogWarning("Path is Empty?", this.gameObject);
                return;
            }
            //Iterate along Path and Populate Objects.
            ClearPreviousObjects();
            //Create Tmp array
            List<GameObject> tmpObjectArray = new List<GameObject>(0);
            float stepSize = 0.1f;
            float lightsCurDist = 0;
            Vector3 lastLightPoint = path[0];
            Vector3 actualLastPoint = path[0];

            for (int j = 0; j < path.Length - 1; j++)
            {
                //Determine Vectors
                Vector3 p0 = path[j];
                Vector3 p1 = path[j + 1];

                Vector3 fwd = (p1 - p0).normalized;
                //Determine Directions
                Vector3 right = Vector3.Cross(Vector3.up, fwd);//Right by Default
                Vector3 up = Vector3.Cross(fwd, right).normalized;
                //Calculate Distance Between Cur and P1
                float moveDist = Vector3.Distance(lastLightPoint, p0);
                lightsCurDist += moveDist;
                lastLightPoint = p0;

                if (j == 0)
                {
                    //Always Place First at Starting Point.
                    if (obj)
                    {
                        //Place Point
                        //Place Light nxtToCurb End. Reuse GameObject Memory
                        actualLastPoint = lastLightPoint;
                        PlacePrefab(ref tmpObjectArray, obj, lastLightPoint, fwd, up, worldUp);
                    }
                }

                float segDist = Vector3.Distance(p0, p1);
                int stepSpace = Mathf.RoundToInt(segDist / stepSize);
                if (stepSpace > 0)
                {
                    for (int k = 0; k < stepSpace; k++)
                    {
                        Vector3 newLightPoint = lastLightPoint + (fwd * stepSize);
                        lastLightPoint = newLightPoint;
                        lightsCurDist += stepSize;
                        //Place Light
                        if (obj)
                        {
                            if (lightsCurDist >= spacing)
                            {
                                //Place Point
                                //Place Light nxtToCurb End. Reuse GameObject Memory
                                PlacePrefab(ref tmpObjectArray, obj, lastLightPoint, fwd, up, worldUp);
                                actualLastPoint = lastLightPoint;
                                //Reset Distance Moved.
                                lightsCurDist = 0f;
                            }
                        }
                    }
                }
                //Set Last one if its at least 2/3 the Desired Distance.
                if (j == path.Length - 2)
                {
                    //Calculate Distance Between Cur and P1
                    moveDist = Vector3.Distance(p1, actualLastPoint);
                    if (moveDist >= (spacing * 0.5f))
                    {
                        //Always Place at End Point.
                        if (obj)
                        {
                            lastLightPoint = p1;
                            //Place Point
                            //Place Light nxtToCurb End. Reuse GameObject Memory
                            PlacePrefab(ref tmpObjectArray, obj, lastLightPoint, fwd, up, worldUp);
                            actualLastPoint = lastLightPoint;
                        }
                    }
                }
            }
            //Set Back to Stored ObjectArray
            objectArray = tmpObjectArray.ToArray();
            //Now we want to Generate the Lines that Connect the Poles. IF they have the Needed Script Attached
            if (powerLines && objectArray != null && objectArray.Length > 0 && objectArray[0].GetComponent<CiDyTelegraphPole>() != null)
            {
                //Make sure script has points available. //No Loop Logic is implemented Yet.
                for (int i = 0; i < objectArray.Length - 1; i++)
                {
                    //Make sure these objects
                    CiDyTelegraphPole poleAData = objectArray[i].GetComponent<CiDyTelegraphPole>();
                    CiDyTelegraphPole poleBData = objectArray[i + 1].GetComponent<CiDyTelegraphPole>();
                    if (!ValidPoleData(poleAData, poleBData))
                    {
                        continue;
                    }
                    Transform formA = poleAData.transform;
                    Transform formB = poleBData.transform;
                    Vector3 aPos = formA.position;
                    Vector3 bPos = formB.position;
                    float dist = Vector3.Distance(aPos, bPos);
                    float segments = (dist / 12);
                    if (segments <= 0)
                    {
                        segments = 0.1f;
                    }
                    //If we are here then our data has been validated.
                    //Connect these Two Poles with there Cable Lines.
                    //Create the Holding Line GameObject
                    GameObject lineHolder = new GameObject("PowerLines");
                    Transform holderTrans = lineHolder.transform;
                    holderTrans.SetParent(formA);//Set Parent
                                                 //Prep Mesh Combin Array
                    CombineInstance[] combine = new CombineInstance[poleAData.cablePoints.Count];
                    //Iterate and Create Mesh Data.
                    for (int j = 0; j < poleAData.cablePoints.Count; j++)
                    {
                        Vector3 pointA = formA.TransformVector(poleAData.cablePoints[j]) + aPos;
                        Vector3 pointB = formB.TransformVector(poleBData.cablePoints[j]) + bPos;
                        //Create a Cable Mesh
                        Vector3 midPoint = ((pointA + pointB) / 2) + (Vector3.down * cableDrop);
                        //Convert these Three Points into Bezier
                        Vector3[] curve = new Vector3[3];
                        curve[0] = pointA;
                        curve[1] = midPoint;
                        curve[2] = pointB;
                        try
                        {
                            curve = CiDyUtils.CreateBezier(curve, segments);
                        }
                        catch
                        {
                            Debug.LogWarning("Power Line Bezier was invalid, Defaulting to Core Line");
                        }
                        //Convert into a Mesh and Stuff this Mesh into the Poles Transform
                        combine[j].mesh = CiDyUtils.ExtrudeRail(GenerateCableShape(), curve, holderTrans);
                        combine[j].transform = holderTrans.localToWorldMatrix;
                    }
                    //Combine Power Line Meshes into Single Mesh and Shared Material.
                    Mesh powerLineMesh = new Mesh();
                    //Combine
                    powerLineMesh.CombineMeshes(combine, true, true);
                    //Add Needed Items to Line Holder Object
                    MeshRenderer mRenderer = lineHolder.AddComponent<MeshRenderer>();
                    MeshFilter mFilter = lineHolder.AddComponent<MeshFilter>();
                    mFilter.sharedMesh = powerLineMesh;
                    //Set Material
                    mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/PowerLineMaterial", typeof(Material));
                }
            }
        }

        //Creates  Circle Shape with Defined Radius and Count
        static Vector3[] GenerateCableShape(int numberOfPoints = 6, float radius = 0.0268224f)
        {
            Vector3[] shape = new Vector3[numberOfPoints];
            Transform turtle = new GameObject("Turtle").transform;
            turtle.rotation = Quaternion.Euler(-90, 0, 0);
            for (int i = 0; i < numberOfPoints; i++)
            {
                float angle = i * Mathf.PI * 2 / numberOfPoints;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 pos = turtle.TransformVector(new Vector3(x, 0, z));
                //float angleDegrees = -angle * Mathf.Rad2Deg;
                shape[i] = pos;
            }

#if UNITY_EDITOR
            DestroyImmediate(turtle.gameObject);
#elif !UNITY_EDITOR
        Destroy(turtle.gameObject);
#endif

            //Return Created Shape
            return shape;
        }

        //This will check and make sure two Poles are Setup to Connect
        bool ValidPoleData(CiDyTelegraphPole poleA, CiDyTelegraphPole poleB)
        {
            //Null Check
            if (poleA == null || poleB == null)
            {
                return false;
            }
            //Points List Check
            if (poleA.cablePoints == null || poleA.cablePoints.Count == 0)
            {
                return false;
            }
            if (poleB.cablePoints == null || poleB.cablePoints.Count == 0)
            {
                return false;
            }
            //Comparable Check
            if (poleA.cablePoints.Count != poleB.cablePoints.Count)
            {
                return false;
            }

            //These Two Poles have Passed the Validation Check
            return true;
        }

        void PlacePrefab(ref List<GameObject> objectArray, GameObject prefab, Vector3 pos, Vector3 fwd, Vector3 up, bool worldUp = false)
        {
            //Perfectly aligned with next Point in list
            //Instantiate Prefab
            GameObject newObj;

            //If we want World Up
            if (worldUp)
            {
                fwd.y = 0;
                newObj = Instantiate(prefab, pos+positionCorrection, Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(rotationCorrection.x, rotationCorrection.y, rotationCorrection.z), transform);
            }
            else
            {
                newObj = Instantiate(prefab, pos + positionCorrection, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(rotationCorrection.x, rotationCorrection.y, rotationCorrection.z), transform);
            }
            objectArray.Add(newObj);
        }

        void ClearPreviousObjects()
        {
            if (objectArray != null && objectArray.Length > 0)
            {
                for (int i = 0; i < objectArray.Length; i++)
                {
#if UNITY_EDITOR
                    DestroyImmediate(objectArray[i]);
#elif !UNITY_EDITOR
                    Destroy(objectArray[i]);
#endif
                }
                objectArray = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (spline != null && spline.Length > 0)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < spline.Length - 1; i++)
                {
                    Gizmos.DrawLine(spline[i], spline[i + 1]);
                    Gizmos.DrawSphere(spline[i], 0.1f);
                    if (i == spline.Length - 2)
                    {
                        Gizmos.DrawSphere(spline[i + 1], 0.1f);
                    }
                }
            }
            if (userPoints != null)
            {
                Gizmos.color = Color.blue;
                //Draw User Points
                for (int i = 0; i < userPoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(userPoints[i].position, userPoints[i + 1].position);
                }
            }
            //Draw Normalized Spline Range for User to See.
            if (normalizedSplineRange != null && normalizedSplineRange.Length > 0)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < normalizedSplineRange.Length - 1; i++)
                {
                    Gizmos.DrawLine(normalizedSplineRange[i], normalizedSplineRange[i + 1]);
                }
            }
        }
    }
}