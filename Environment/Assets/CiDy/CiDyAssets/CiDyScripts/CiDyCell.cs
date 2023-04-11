using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StraightSkeletonNet;
using StraightSkeletonNet.Primitives;
using ClipperLib;
using System.IO;
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
using AwesomeTechnologies;
#endif
#if VEGETATION_STUDIO_PRO
using AwesomeTechnologies.VegetationSystem.Biomes;
#endif

namespace CiDy
{
	[ExecuteInEditMode]
	[System.Serializable]
	public class CiDyCell : MonoBehaviour
	{
		//Debug variables
		public bool debugCell = false;//If this is true it will create a cell out of preset Nodes.
		public bool isBoundary = false;//If this is true. This is a Boundary Cell.
		public bool createBoundary = false;//Do we want to test one line or a growth algorithm??
		public bool createRoadMeshes = false;
		public float growthRate = 0f;
		//public CiDyDesigner designer;
		public CiDyGraph graph;
		public Transform startPoint;
		public Transform endPoint;
		//Cell Nodes
		GameObject nodePrefab;
		List<CiDyNode> boundaryNodes = new List<CiDyNode>(0);
		List<CiDyNode> subNodes = new List<CiDyNode>(0);
		[HideInInspector]
		[SerializeField]
		public List<CiDyNode> cycleNodes = new List<CiDyNode>(0);
		//private List<List<CiDyNode>> cycles = new List<List<CiDyNode>>(0);
		private List<List<CiDyNode>> filaments = new List<List<CiDyNode>>(0);
		//private List<List<CiDyNode>> cycleEdges = new List<List<CiDyNode>> (0);
		List<CiDyRoad> boundaryRoads = new List<CiDyRoad>(0);
		CiDyRoad[] longestRoads = new CiDyRoad[2];
		public List<CiDyEdge> boundaryEdges = new List<CiDyEdge>(0);
		//SecondaryRoad Data
		//List<CiDyNode> secondaryNds = new List<CiDyNode>(0);//Stores the nodes of the Secondary Rds.
		public List<CiDyEdge> secondaryEdges = new List<CiDyEdge>(0);//Stores the Edges for the SecondaryRds. 
																	 //List<GameObject> secondaryRds = new List<GameObject>(0);//Stores the Secondary Road GameObject
		public int testCount;//For the Cells Interior Cell Points. Finite number based on boundary Road Nodes
		public int subCount;//The Nodes that are actual gameObject node points connected in this cell by the Growth Algorithm. :)
							//Secondary Road Growth Control Parameters
		public bool createSideWalks = true;//If False then we do not create sideWalks
		public float sideWalkWidth = 5.4f;//The Side Walk Width(Inset from Roads);
		public float sideWalkHeight = 0.1618f;
		public float sideWalkEdgeWidth = 0.54f;
		public float sideWalkEdgeHeight = 0.1f;
		public int maxSubRoads = 400;//The Maximum amount of Roads allowed to be in the cell at any one given moment.
		public float roadWidth = 3.0f;//Width of Created Roads.
		public int roadSegmentLength = 6;//The Mesh Resolution/Mesh Quadrilateral Segment Length.(Decreasing Length will increase GPU Cost)
										 //SideWalk Street Spawn Points.
		public float pathClutterSpacing = 30f;
		public GameObject[] clutterObjects = new GameObject[0];
		public float pathLightSpacing = 20f;
		public Object pathLight;
		//Building lots
		public bool useGreenSpace = false;
		public float lotWidth = 50f;//Cell Building Lots
		public float lotDepth = 50f;//Cell Building Lots
									//Extruded Buildings Heights.
		public float heightMin = 25f;//10 Ft (1 Story)
		public float heightMax = 60;
		[HideInInspector]
		public bool usePrefabBuildings = true;
		public int districtType = 0;
		private int curType = 0;
		public float lotInset = 0f;
		public bool lotsUseRoadHeight = false;//Defualt is false. If active lots will grab road height. If Default then the lots will match sideWalk Height.

		public int seedValue = 0;//This value is used to feed into the Cell Growth Parameters for reproducable results.
		public int minDegree = 4;//Number of times a Road branches.
		public int maxDegree = 4;//
		public float minSegmentSize = 10f;//Length of Roads in cell.
		public float maxSegmentSize = 10f;//Max Length of Roads
		float paramLength;
		public float snapSize = 3f;//Distance to connect to existing roads.
		[Range(0f, 1f)]
		float nodeSpacing;
		public float connectivity = 0.5f;//probability that segments connect.
		public GameObject holder;//The Holder of the Cell.
		public int cellInt = -1;//Default

		//Cell Collider Mesh Info
		readonly MeshCollider cellCollider;
		[HideInInspector]
		public GameObject subRoadHolder;
		public GameObject nodeHolder;
		public GameObject buildingHolder;
		public GameObject clutterHolder;
		public GameObject pathLightHolder;
		//ColliderMesh
		[SerializeField]
		Mesh colliderMesh;
		[SerializeField]
		MeshFilter mFilter;
		[SerializeField]
		public MeshRenderer mRenderer;
		[SerializeField]
		public MeshCollider mCollider;
		//This function will UpdateColliderMesh(create == null)
		public List<Vector3> interiorPoints = new List<Vector3>(0);
		List<Vector3> extPoints = new List<Vector3>(0);
		List<List<Vector3>> pointsList = new List<List<Vector3>>(0);//Street Interior SidePoints
		List<List<Vector3>> curveList = new List<List<Vector3>>(0);//Street Interior CurvePoints.
		List<Vector3> cornerPoints = new List<Vector3>(0);//Corner Points for Street Interior SideWalks.

		//Building Generation
		void GrabAssets()
		{
			//Debug.Log ("Grab Assets For Cell");
			//Find Node Prefab in Resources.
			nodePrefab = Resources.Load("CiDyResources/NodePrefab", typeof(GameObject)) as GameObject;
			districtType = graph.index;
			//Get Theme Assets
			GrabThemeObjects();
			//Min 1 enforced for RoadWidth
			if (roadWidth <= 0)
			{
				roadWidth = 1f;
			}
			//Require minSegLenght based on RoadWidth
			if (minSegmentSize < (roadWidth * 3.5f))
			{
				minSegmentSize = (roadWidth * 3.5f);
				//Debug.LogWarning("Increased MinSegSize "+minSegmentSize);
			}
			//Update maxSeg if Necessary
			if (maxSegmentSize < minSegmentSize)
			{
				maxSegmentSize = minSegmentSize;
			}
			//Create secondaryRd Holder
			subRoadHolder = new GameObject("SubRoads");
			subRoadHolder.transform.parent = transform;
			subRoadHolder.transform.position = transform.position;
			//Create Node Holder
			nodeHolder = new GameObject("Nodes");
			nodeHolder.transform.parent = transform;
			nodeHolder.transform.position = transform.position;
			//Building Holder
			buildingHolder = new GameObject("Buildings");
			buildingHolder.transform.parent = transform;
			buildingHolder.transform.position = transform.position;
			//Clutter Holder
			clutterHolder = new GameObject("StreetClutter");
			clutterHolder.transform.parent = transform;
			clutterHolder.transform.position = transform.position;
			//Path Light Holder
			pathLightHolder = new GameObject("LightHolder");
			pathLightHolder.transform.parent = transform;
			pathLightHolder.transform.position = transform.position;
			//Create Mesh Filter
			if (colliderMesh != null)
			{
				//Debug.Log("Have Mesh");
				colliderMesh.Clear();
				interiorPoints.Clear();
			}
			else
			{
				//This is a newly created Cell. :)
				//Add Filter Component
				mFilter = (MeshFilter)gameObject.AddComponent<MeshFilter>();
				mRenderer = (MeshRenderer)gameObject.AddComponent<MeshRenderer>();
				//renderer.enabled = false;
				//GetComponent<Renderer>().material.shader = Shader.Find("Transparent/Diffuse");
				mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/CellTransparency");
				//mRenderer.material.SetColor("_TintColor", RandomColor());
				//mRenderer.material.SetColor("_Color", RandomColor());
				mCollider = (MeshCollider)gameObject.AddComponent<MeshCollider>();
			}
		}
		[SerializeField]
		int[] blendTerrains;
		//this function is called by CiDyGraph to determine if this Cell is blending on a terrain or not
		public bool MatchTerrain(int terrainIdx)
		{
			if (blendTerrains == null)
			{
				//No Terrains for this Cell to Blend to.
				return false;
			}
			//Iterate through Terrains and determine if this cell is blending to the Terrain Idx
			for (int i = 0; i < blendTerrains.Length; i++)
			{
				if (terrainIdx == blendTerrains[i])
				{
					return true;
				}
			}

			return false;
		}

		//This function will perform a Bounding Box Check and See what terrains of Graph are under us.(If Any)
		public void FindTerrains()
		{
			//Compare Bounding Box of Intersection Mesh to Terrain Bounds
			Bounds cellBounds = mRenderer.bounds;
			cellBounds.extents = cellBounds.extents * 2;
			//Compare this Bounds to All Potential Terrains and there Bounds.
			List<int> newBlendingTerrains = new List<int>(0);
			if (graph == null || graph.terrains == null || graph.terrains.Length == 0)
			{
				return;
			}
			for (int i = 0; i < graph.terrains.Length; i++)
			{
				if (graph.terrains[i] == null)
				{
					continue;
				}
				//Terrain Bounds to Test Against.
				Bounds terrBounds = graph.terrains[i].ReturnBounds();
				//Compare
				if (terrBounds.Intersects(cellBounds))
				{
					//Add to Match Terrain Array
					newBlendingTerrains.Add(graph.terrains[i]._Id);
				}
			}
			//Convert and Store Long Term
			blendTerrains = newBlendingTerrains.ToArray();
		}


		public void ReplaceMaterials()
		{
			if (graph == null)
			{
				graph = FindObjectOfType<CiDyGraph>();
				if (graph == null)
				{
					return;
				}
			}
			//Get SideWalk Material
			if (sideWalkHolders != null && sideWalkHolders.Length > 0)
			{
				for (int i = 0; i < sideWalkHolders.Length; i++)
				{
					MeshRenderer mrender = sideWalkHolders[i].GetComponent<MeshRenderer>();
					if (mrender != null)
					{
						//Apply Road Texture
						mrender.sharedMaterial = graph.sideWalkMaterial;
					}
				}
			}
		}

		/*//Used in debug mode.
		void Start(){
			//CreateSecondaryRoads();
			StartCoroutine(DrawSecondaryRds());
		}*/

		public void SetGraph(CiDyGraph newGraph)
		{
			graph = newGraph;
			GrabAssets();
			//Grab designer for visual assistance.
			//designer = FindObjectOfType(typeof(CiDyDesigner)) as CiDyDesigner;
		}

		public bool isStamped = false;
		public float startTime;
		public float curTime;
		public float tmpTime;

		//Grab Name from our cells Boundary Sequence
		void UpdateName()
		{
			if (cycleNodes.Count > 0)
			{
				//Set Cell Name
				string newName = "";
				for (int i = 0; i < cycleNodes.Count; i++)
				{
					newName = (newName + cycleNodes[i].name);
				}
				holder.name = newName;
			}
		}

		//This function will simply update the cell with builtin info.
		public void UpdateCell()
		{
			if (processing)
			{
				Debug.LogError("This Shouldn't Be Happening Stop what you are doing! This cell is STILL processing from the last Update! I am only a machine!");
				return;
			}
			//Make Sure Seed is Set to User Seed value
			if (randomSeedOnRegenerate)
			{
				seedValue = (int)Random.Range(-999999, 999999);
			}
			//Random.seed = huddleBuildingsSeed;Depreceated
			Random.InitState(seedValue);
			//Update Cell Name based on Boundary Roads.
			UpdateName();
			//Debug.Log ("Updating Cell New Name" + name);
			CenterHolder();
			//Grab Theme
			UpdateMesh();
			//Update Secondary
			//StartCoroutine(CreateSecondaryRoads());
			//CreateSecondaryRoads ();
			//Debug.LogWarning ("Need to Reactivate Sub Road Creation");
			//Debug.Log ("Cell Finished Updating "+name+" cellInt "+cellInt);
		}

		//This function will Extrapolate this cells data from its boundary roads. :)
		public void UpdateCellCycle(int cellCycle)
		{
			if (processing)
			{
				Debug.LogError("Error! Cell Is Processing and Cannot Update.");
				return;
			}
			//Debug.Log ("Update CellCycle " + cellCycle);
			//Grab points from graph.
			cellInt = cellCycle;
			cycleNodes = graph.ReturnCell(cellInt, ref filaments);//graph.runtimeCycles [cellInt];
			/*for(int i = 0;i<filaments.Count;i++){
				Debug.Log("Filament Chain "+i);
				for(int j = 0;j<filaments[i].Count;j++){
					Debug.Log(filaments[i][j].name);
				}
			}*/
			//Set Node Spacing based on roadWidth.
			nodeSpacing = roadWidth * 2;
			//Initilize A Cell
			//Set Boundary Nodes
			//Debug.Log ("Cell Nodes Count after Shallow Copy "+cycleNodes.Count);
			UpdateName();
			//Debug.Log ("UpdateCellCycle "+name);
			//Place holder in center area on z and x Axis.
			CenterHolder();
			//Create colider Mesh for interaction and further data.
			UpdateMesh();
			//StartCoroutine(CreateSecondaryRoads());
			//CreateSecondaryRoads();
			//Debug.Log ("Setting Cell " + name + " to " + cellInt);
		}

		//This function will clear the sub roads so new ones can be created.
		void ClearSubRoads()
		{
			//We need to iterate through all the sub roads and destroy them and any connections we have to them.
			for (int i = 0; i < subNodes.Count; i++)
			{
				subNodes[i].DestroyNode();
			}
			subNodes.Clear();
		}

		//This will simply update the holder Position and Mesh Collider based on boundary nodes/roads
		public void CenterHolder()
		{
			//Debug.Log ("Centered Holder of " + name);
			Vector3 cellCenter = new Vector3(0, 0, 0);
			for (int i = 0; i < cycleNodes.Count; i++)
			{
				cellCenter += cycleNodes[i].position;
			}
			Vector3 finalCenter = (cellCenter / cycleNodes.Count);
			//Debug.Log("Holder Has Moved Position");
			holder.transform.position = finalCenter;
		}

		//Turn Collider Designer Mesh Off
		public void EnableGraphic()
		{
			//Debug.Log ("Enable Graphics on "+name);
			mRenderer.enabled = true;
			mCollider.enabled = true;
		}

		//Turn Collider Designer Mesh on
		public void DisableGraphic()
		{
			//Debug.Log ("Disable Graphics on "+name);
			mRenderer.enabled = false;
			mCollider.enabled = false;
		}

		List<List<Vector3>> insetLines = new List<List<Vector3>>(0);
		List<List<Vector3>> insetCurves = new List<List<Vector3>>(0);
		List<int> deadEnds = new List<int>(0);
		//Create or Update the Cells Selectable Mesh.
		public void UpdateMesh()
		{
			//Debug.Log("Cell Update Mesh");
			//Get Theme Objects
			if (curType != districtType)
			{
				curType = districtType;
			}
			GrabThemeObjects();
			insetTmpPoints = new List<List<Vector3>>(0);
			//Clear Visuals
			if (visualNodes.Count > 0)
			{
				//Delete
				for (int i = 0; i < visualNodes.Count; i++)
				{
					DestroyImmediate(visualNodes[i]);
				}
			}
			//Debug.Log ("Updating Mesh CycleNodes: " + cycleNodes.Count);
			if (colliderMesh != null)
			{
				colliderMesh.Clear();
				interiorPoints.Clear();
			}
			pointsList = new List<List<Vector3>>(0);
			cornerPoints = new List<Vector3>(0);
			curveList = new List<List<Vector3>>(0);
			deadEnds = new List<int>(0);
			insetLines = new List<List<Vector3>>(0);
			insetCurves = new List<List<Vector3>>(0);
			//Debug.Log("Started Update Mesh");
			//Determine Interior Road Mesh Points based on CycleNodes Directions
			for (int i = 0; i < cycleNodes.Count; i++)
			{
				CiDyNode curNode = cycleNodes[i];
				CiDyNode nxtNode;
				if (i == cycleNodes.Count - 1)
				{
					//We are at end
					nxtNode = cycleNodes[0];
				}
				else
				{
					//In Beginning or Middle
					nxtNode = cycleNodes[i + 1];
				}
				//Now determine the curRoad for these Nodes.
				string roadName = new CiDyEdge(curNode, nxtNode).name;
				//Debug.Log("Road Name: "+roadName);
				CiDyRoad newRoad = null;
				int arrayInt = 0;
				//Debug.Log("CurNode Connected Roads: "+curNode.connectedRoads.Count);
				//Now find this road in our list.
				for (int j = 0; j < curNode.connectedRoads.Count; j++)
				{
					CiDyRoad curRoad = curNode.connectedRoads[j];
					if (curRoad.name == roadName)
					{
						//This is the Road we want. Stop search
						//Add this road to boundaryRoads
						boundaryRoads.Add(curRoad);
						newRoad = curRoad;
						break;
					}
				}
				//Debug.Log("nxtNode Connected Roads: " + nxtNode.connectedRoads.Count);
				for (int j = 0; j < nxtNode.connectedRoads.Count; j++)
				{
					if (nxtNode.connectedRoads[j].name == roadName)
					{
						arrayInt = j;
						break;
					}
				}
				//Debug.Log("After");
				//Temporary List.
				List<Vector3> roadPoints = new List<Vector3>();
				//Now Add this Roads Interior Vertices to the List.
				if (newRoad != null)
				{
					//Which Node of This Road is closest to this CycleNode
					float nodeDist = Vector3.Distance(newRoad.nodeA.position, curNode.position);
					float nodeDist2 = Vector3.Distance(newRoad.nodeB.position, curNode.position);
					if (nodeDist < nodeDist2)
					{

						//NodeA is Closer
						//Since Node A Is Closer we only Need to Grab the Even VS of this RoadMesh
						roadPoints = newRoad.leftEdge.ToList();
						//CiDyUtils.GrabEvenVs(newRoad, ref roadPoints);
					}
					else
					{

						//NodeB is Closer
						//Since NodeB Is Closer we only need to Grab the Odds and Reverse there List.
						//CiDyUtils.GrabOddVs(newRoad, ref roadPoints);
						roadPoints = newRoad.rightEdge.ToList();
						roadPoints.Reverse();
					}
				}
				//Store seperate reference
				List<Vector3> finalPoints = new List<Vector3>(roadPoints);
				insetLines.Add(finalPoints);
				//Debug.Log("Road Name "+roadName+" CurNode "+curNode.name+" RoadPoints: "+roadPoints.Count);
				//Add to Temp List<List<Vector3>>
				pointsList.Add(roadPoints);
				//Debug.Log("After 2");
				//Test for CulDeSac scenario
				if (nxtNode.adjacentNodes.Count > 1)
				{
					//Standard Corner/Curved point of two roads meeting
					//Debug.Log("After 3A, Corner Points Array: "+nxtNode.cornerPoints.Count+" arrayInt: "+arrayInt);
					if (nxtNode.cornerPoints.Count != 0)
					{
						curveList.Add(nxtNode.cornerPoints[arrayInt].vectorList);
					}
					//Debug.Log("After 3B");
				}
				else
				{
					//CulDeSac
					//Debug.Log("After 4A");
					curveList.Add(nxtNode.culDeSacPoints);
					deadEnds.Add(pointsList.Count - 1);
					//Debug.Log("After 4B");
				}
			}
			//Debug.Log("First Iteration");
			//We have the Roads interior Points and there end Culdesac/Corner Curves
			//Now we have the interiorPoints for this Road. Lets determine the corner Curve for the Intersecting Roads.
			//Iterate through interiorPoints at the BreakPoints.
			for (int i = 0; i < pointsList.Count; i++)
			{
				Vector3 intersection = Vector3.zero;
				bool skip = false;
				for (int j = 0; j < deadEnds.Count; j++)
				{
					if (i == deadEnds[j])
					{
						skip = true;
						break;
					}
				}
				if (!skip)
				{
					//Meaning this is a Standard Intersection/Corner of Two Roads.
					List<Vector3> curPoints = pointsList[i];
					//Now lets Determine the Corners Piece.
					Vector3 posA = curPoints[curPoints.Count - 1];
					Vector3 prevA = curPoints[curPoints.Count - 2];
					Vector3 posB;
					Vector3 nxtB;
					//Determine where we are in the InteriorPoints List to Grab the Proper Points.
					if (i == pointsList.Count - 1)
					{
						//PosA is Last List
						posB = pointsList[0][0];
						nxtB = pointsList[0][1];
					}
					else
					{
						//PosA is in the Middle.
						posB = pointsList[i + 1][0];
						nxtB = pointsList[i + 1][1];
					}
					//Calculate Directions for Infinite Line Intersection Test.
					Vector3 posADir = (posA - prevA).normalized;
					Vector3 posBDir = (posB - nxtB).normalized;
					//Project New EndPoints for Line Test
					Vector3 posAEnd = (posA + (posADir * 500f));
					Vector3 posBEnd = (posB + (posBDir * 500f));
					//Test LineIntersection between the four Points
					if (CiDyUtils.LineIntersection(posA, posAEnd, posB, posBEnd, ref intersection))
					{
						//Change Y Axis to equal creating Points Y Values.
						intersection.y = posA.y;
					}
					else
					{
						//Just pick the Middle :)
						intersection = (posA + posB) / 2;
						//Debug.LogError("InteriorPoints Did Not Create an Intersection! Check Code");
					}
					cornerPoints.Add(intersection);
					//CiDyUtils.MarkPoint(intersection,9999);
				}
				else
				{
					//This is a dead end so we will add a null point. (Vector3.zero)
					cornerPoints.Add(Vector3.zero);
				}
				////Now that we have our intersection Lets Calculate the Curve.
				//List<Vector3> curve = new List<Vector3>(0);
				//curve.Add(posA);
				//curve.Add(intersection);
				//curve.Add(posB);
				//curve = CiDyUtils.CreateBezier(curve,1);
			}
			//Debug.Log("AFter 2nd");
			List<Vector3> drawnPoints = new List<Vector3>(0);
			crossLines = new List<List<Vector3>>(0);
			//colors = new List<Color>(0);
			List<List<Vector3>> properInteriorPoints = new List<List<Vector3>>(0);
			//Offset by sideWalk Width
			for (int i = 0; i < insetLines.Count; i++)
			{
				drawnPoints = new List<Vector3>(0);
				for (int j = 0; j < insetLines[i].Count; j++)
				{
					//Determine Fwd Direction
					Vector3 curPos = insetLines[i][j];
					Vector3 nxtPos = Vector3.zero;
					if (j == insetLines[i].Count - 1)
					{
						nxtPos = insetLines[i][j];
						curPos = insetLines[i][j - 1];
					}
					else
					{
						nxtPos = insetLines[i][j + 1];
					}
					Vector3 fwd = (nxtPos - curPos).normalized;
					Vector3 cross = Vector3.Cross(fwd, Vector3.up);
					drawnPoints.Add(insetLines[i][j]);
					insetLines[i][j] += (cross * sideWalkWidth);
					//drawnPoints.Add(insetLines[i][j]+(Vector3.up * 6));
				}
				properInteriorPoints.Add(drawnPoints);
				//crossLines.Add(drawnPoints);
				//insetTmpPoints.Add(drawnPoints);
			}
			drawnPoints = new List<Vector3>(0);

			int maxIndex = insetLines.Count - 1;
			//Debug.Log("After Third: "+maxIndex);
			//Extend Each Line end and Check for Intersection against Next Line.
			for (int i = 0; i < insetLines.Count; i++)
			{
				//This line i must be tested against line+1
				int nxt = 0;
				if (i >= maxIndex)
				{
					nxt = 0;
				}
				else
				{
					nxt = i + 1;
				}
				int maxCurrent = insetLines[i].Count - 1;
				if (maxCurrent == 0)
				{
					Debug.LogWarning("Max Current == 0, This Shouldn't happen.");
					break;
				}
				//Debug.Log("Nxt int: "+nxt+" Inset Lines Count: "+insetLines[i].Count);
				//Debug.Log(maxIndex + " NXt: " + insetLines[nxt].Count);
				//int maxNxtIndex = insetLines[nxt].Count - 1;
				//Debug.Log("Nxt Index: "+maxNxtIndex);
				//Debug.Log("Fine: "+maxCurrent+" Desired: "+(maxCurrent-1));
				Vector3 dirA = (insetLines[i][maxCurrent] - insetLines[i][maxCurrent - 1]).normalized;
				//Debug.Log("Fine");
				//Project Peice of this line
				Vector3 pA = insetLines[i][maxCurrent];
				//Debug.Log("Fine");
				Vector3 pB = pA + (dirA * 1000);
				//CiDyUtils.MarkPoint(pA, 999);
				//CiDyUtils.MarkPoint(pB, 1000);
				if (insetLines[nxt].Count < 2 || insetLines[i].Count < 2)
				{
					//Debug.LogError("We got a break, One of our Inset Lines is not useable???, This Shouldn't Happen");
					break;
				}
				//Project end of Nxt Line
				Vector3 dirB = (insetLines[nxt][0] - insetLines[nxt][1]).normalized;
				//Debug.Log("Fine");
				//Project Peice of this line
				Vector3 pC = insetLines[nxt][0];
				Vector3 pD = pC + (dirB * 1000);
				//CiDyUtils.MarkPoint(pC, 9999);
				//CiDyUtils.MarkPoint(pD, 2000);
				Vector3 intersection = Vector3.zero;
				List<Vector3> curve = new List<Vector3>(0);
				//If parallel
				//Debug.Log("Parallel Test: "+Vector3.Angle(dirA,dirB));
				if ((curveList.Count >= i && curveList[i].Count != 0) && Vector3.Angle(dirA, dirB) <= 10f)
				{
					//Debug.Log("Inset Line Parallel: " + i+" CurveList: "+curveList[i].Count);
					//Debug.Log("Is Culdesac");
					//Culdesac, So we want to test the Line segements against a Projected Circle
					//This is a culdesac.(Dead End Road)
					//CiDyUtils.MarkPoint(((pA + pC) / 2) + (dirA * ((roadWidth * 3))), 0);
					//CiDyUtils.MarkPoint(((pA + pC)/2), -999);
					List<Vector3> smallLine = curveList[i];//The Actual Interior Round Part of the Culdesac is the Small Line(CurveList[i])
					int thirds = (smallLine.Count / 3) - 1;//Thirds

					//float tmpWidth = ((Vector3.Distance(pA, pC) / 2)+sideWalkWidth);
					//Debug.LogWarning("TmpWidth: "+tmpWidth);
					//float radius = ((tmpWidth/2) + sideWalkWidth);
					float refRad = 0;
					//Get its Centroid
					Vector3 centroid = CiDyUtils.FindCircumCircle(smallLine[thirds], smallLine[thirds * 2], smallLine[thirds * 3], ref refRad);
					centroid.y = pA.y;
					refRad += sideWalkWidth;
					//Test this Line against a Circle that is roadWidth(+SideWalkWidth) increased radius
					List<Vector3> circle = CiDyUtils.PlotCircle(centroid, refRad, 90);
					int circleState = 0;
					int loopIndex = 0;
					int maxIteration = 500;
					int stepCount = 0;
					while (stepCount < maxIteration)
					{
						stepCount++;
						//Loop around the Circle
						Vector3 pE = circle[loopIndex];
						Vector3 pF = circle[loopIndex];
						if (loopIndex == circle.Count - 1)
						{
							pF = circle[0];
						}
						else
						{
							pF = circle[loopIndex + 1];
						}
						//CiDyUtils.MarkPoint(pE, loopIndex);
						switch (circleState)
						{
							case 0:
								//Iterate through Line Points.
								for (int k = 0; k < insetLines[i].Count; k++)
								{
									//Test from end inward
									Vector3 pG = insetLines[i][k];
									//Test from end inward
									Vector3 pH = insetLines[i][k];
									if (k == insetLines[i].Count - 1)
									{
										pH = insetLines[i][0];
									}
									else
									{
										pH = insetLines[i][k + 1];
									}
									//Do they intersect?
									if (CiDyUtils.LineIntersection(pG, pH, pE, pF, ref intersection))
									{
										//CiDyUtils.MarkPoint(intersection, 99);
										//Debug.LogWarning("Found Start");
										//Project Height
										intersection.y = centroid.y;
										//Now 
										//We found the start point.
										curve.Add(intersection);
										circleState = 1;
										break;
									}
								}
								break;
							case 1:
								//We want to find the End point now.
								//Check to see if we hit the last line yet.
								curve.Add(pE);
								//Iterate through Line Points.
								for (int k = insetLines[nxt].Count - 1; k > 0; k--)
								{
									//Test from end inward
									Vector3 pG = insetLines[nxt][k];
									//Test from end inward
									Vector3 pH = insetLines[nxt][k];
									if (k == 0)
									{
										//loop
										pH = insetLines[nxt][insetLines[nxt].Count - 1];
									}
									else
									{
										//Nxt
										pH = insetLines[nxt][k - 1];
									}
									//Do they intersect?
									if (CiDyUtils.LineIntersection(pE, pF, pG, pH, ref intersection))
									{
										//CiDyUtils.MarkPoint(intersection, 100);
										//Debug.LogWarning("Found End");
										//Project Height
										intersection.y = centroid.y;
										//Now 
										//We found the start point.
										curve.Add(intersection);
										circleState = 3;
										stepCount = maxIteration;
										break;
									}
								}
								break;
						}
						if (loopIndex < circle.Count - 1)
						{
							//Increment
							loopIndex++;
						}
						else
						{
							//Start Over
							loopIndex = 0;
						}
					}
				}
				else
				{
					//Debug.Log("Inset Line: " + i);
					//Debug.Log("Not a Culdesac");
					//Do they intersect?
					if (!CiDyUtils.LineIntersection(pA, pB, pC, pD, ref intersection))
					{
						//Debug.Log("Extended Line Failed, Phase 2 start");
						//They don't
						//Now we need to perform phase 2. Testing the Entire chain against the First extended Line
						for (int j = 0; j < insetLines[nxt].Count - 1; j++)
						{
							//Test line segments against LineA
							if (CiDyUtils.LineIntersection(pA, pB, insetLines[nxt][j], insetLines[nxt][j + 1], ref intersection))
							{
								intersection.y = pA.y;
								//We are done we have found it, break out of loop after generating this corner curve
								curve.Add(pA);
								curve.Add(intersection);
								curve.Add(insetLines[nxt][j]);
								//CiDyUtils.MarkPoint(pA, 0);
								//CiDyUtils.MarkPoint(intersection, 0);
								//CiDyUtils.MarkPoint(insetLines[nxt][j], 2);
								//insetLines[nxt].RemoveRange(j, insetLines[nxt].Count - (j + 1));
								int diff = (insetLines[nxt].Count - (j + 1));
								//Debug.Log(diff);
								if (diff > 1)
								{
									insetLines[nxt].RemoveRange(0, j);
								}
								else
								{
									insetLines[nxt].RemoveAt(j);
								}
								//Debug.Log("Made it");
								break;
							}
						}
						//Debug.Log("Out of Loop: "+intersection);
						if (intersection == Vector3.zero)
						{
							//Debug.Log("Phase 3 Start");
							//We have failed to find an intersection.
							//Now we need to Perform Phase 3. Testing the Entire Chain of First Line against Extended Second Line
							for (int j = 0; j < insetLines[i].Count - 1; j++)
							{
								//Test line segments against LineA
								if (CiDyUtils.LineIntersection(pC, pD, insetLines[i][j], insetLines[i][j + 1], ref intersection))
								{
									intersection.y = pC.y;
									//We are done we have found it, break out of loop after generating this corner curve
									curve.Add(insetLines[i][j]);
									curve.Add(intersection);
									curve.Add(pC);
									//CiDyUtils.MarkPoint(insetLines[i][j], 0);
									//CiDyUtils.MarkPoint(intersection, 1);
									//CiDyUtils.MarkPoint(pC, 2);
									insetLines[i].RemoveRange(j, insetLines[i].Count - j);
									break;
								}
							}
							if (intersection == Vector3.zero)
							{
								//Debug.Log("Final Phase Start");
								//Final Phase. Chain Test to second Chain
								for (int j = 0; j < insetLines[nxt].Count - 1; j++)
								{
									for (int n = 0; n < insetLines[i].Count - 1; n++)
									{
										pA = insetLines[i][n];
										pB = insetLines[i][n + 1];
										pC = insetLines[nxt][j];
										pD = insetLines[nxt][j + 1];
										//Test line segments against LineA
										if (CiDyUtils.LineIntersection(pA, pB, pC, pD, ref intersection))
										{
											intersection.y = pA.y;
											curve.Add(pA);
											curve.Add(intersection);
											curve.Add(insetLines[nxt][j + 1]);
											//CiDyUtils.MarkPoint(pA, 0);
											//CiDyUtils.MarkPoint(intersection, 2);
											//CiDyUtils.MarkPoint(insetLines[nxt][j + 1], 2);
											//This means there is a split.
											//Cut after PC, and Cut Before PB
											insetLines[i].RemoveRange(n, insetLines[i].Count - n);
											insetLines[nxt].RemoveRange(0, j + 1);
											//We are done we have found it, break out of loop after generating this corner curve
											break;
										}
									}
								}
								if (intersection == Vector3.zero)
								{
									//We assume this is a degeneracy event that happens when two lines are facing eachother but are not crossing. (Almost Parallel) We grab the Middle Point.
									//Debug.LogWarning("Intersection of Road Sections has Failed, Final Phase!");
									dirA = (insetLines[i][maxCurrent] - insetLines[i][maxCurrent - 1]).normalized;
									//Debug.Log("Fine");
									//Project Peice of this line
									Vector3 tmpPA = insetLines[i][maxCurrent];
									//Debug.Log("Fine");
									//Vector3 tmpPB = tmpPA + (dirA * 1000);
									//CiDyUtils.MarkPoint(pA, 999);
									//CiDyUtils.MarkPoint(pB, 1000);
									if (insetLines[nxt].Count < 2)
									{
										Debug.Log("We got a break");
										break;
									}
									//Project end of Nxt Line
									dirB = (insetLines[nxt][0] - insetLines[nxt][1]).normalized;
									//Debug.Log("Fine");
									//Project Peice of this line
									Vector3 tmpPC = insetLines[nxt][0];
									//Vector3 tmpPD = tmpPC + (dirB * 1000);

									intersection = (tmpPA + tmpPC) / 2;

									curve.Add(tmpPA);
									curve.Add(intersection);
									curve.Add(tmpPC);

									if (intersection == Vector3.zero)
									{
										Debug.LogWarning("Intersection of Road Sections has Failed, Every Phase!");
									}
								}
							}
						}
					}
					else
					{
						//Debug.Log("Extended Line Intersected");
						//Extended Lines have intersected
						intersection.y = pA.y;
						//We are done we have found it, break out of loop after generating this corner curve
						curve.Add(pA);
						curve.Add(intersection);
						curve.Add(pC);
						//CiDyUtils.MarkPoint(intersection, 111);
					}
				}
				//We are out of the Closing Brackets. Store Curve
				if (curve.Count == 3)
				{
					//Convert Curve
					curve = CiDyUtils.CreateBezier(curve.ToArray(), 0.5f).ToList();
				}
				//Add to Curve
				insetCurves.Add(curve);
				//Debug.Log("Out another step");
			}
			//Debug.Log("After 4th");
			List<Vector3> drawPoints = new List<Vector3>(0);
			//Now Lets put the pointsList and Curves into the Interior Points List.
			for (int i = 0; i < pointsList.Count; i++)
			{
				//Now Add Striaght
				for (int j = 0; j < pointsList[i].Count; j++)
				{
					interiorPoints.Add(pointsList[i][j]);
				}
				//Now add curve
				for (int j = 0; j < curveList[i].Count; j++)
				{
					interiorPoints.Add(curveList[i][j]);
				}
			}
			//Debug.Log("After 5th");
			//Generate Mesh
			//Orient for Mesh
			for (int i = 0; i < interiorPoints.Count; i++)
			{
				interiorPoints[i] = interiorPoints[i] - transform.position;
			}
			//Debug.Log("After 6th");
			//Debug.Log("Generate Mesh");
			//Send Points to Mesh Creator
			CiDyVoxelSquare[,] newVoxels = new CiDyVoxelSquare[32, 32];
			colliderMesh = CiDyUtils.CreateMeshFromPoly(interiorPoints.ToArray(), 32, ref newVoxels);
			//Create UVS from Positions x,z of Points in mesh.
			Vector2[] newUVs = new Vector2[colliderMesh.vertices.Length];
			for (int i = 0; i < colliderMesh.vertices.Length; i++)
			{
				newUVs[i] = new Vector2(colliderMesh.vertices[i].x, colliderMesh.vertices[i].z);
			}
			//Debug.Log("After 7th");
			colliderMesh.uv = newUVs;
			colliderMesh.RecalculateBounds();
			mFilter.mesh = colliderMesh;
			mCollider.sharedMesh = colliderMesh;
			//Should we Display it or Not?
			if (!graph.activeCells)
			{
				DisableGraphic();
			}
			//Update Terrains that this Cell is Overlapping
			FindTerrains();
#if VEGETATION_STUDIO_PRO
		//VEGETATION STUDIO BIOME MASK iNTEGRATION
		if (biomeMask == null)
		{
			//Create a Biome Mask for this Cell using Interior Points.
			Transform biomeTransform = new GameObject("BiomeMaskArea").transform;
			//Now Set Boundary Points of Biome Mask.
			biomeTransform.parent = transform;
			//Move to Cell Center
			biomeTransform.position = transform.position;
			//Set Mask and its Boundary Points.
			biomeMask = biomeTransform.gameObject.AddComponent<BiomeMaskArea>();
			List<AwesomeTechnologies.VegetationSystem.Biomes.Node> vegNodes = new List<AwesomeTechnologies.VegetationSystem.Biomes.Node>();
			for (int i = 0; i < interiorPoints.Count; i++)
			{
				AwesomeTechnologies.VegetationSystem.Biomes.Node newNode = new AwesomeTechnologies.VegetationSystem.Biomes.Node();
				newNode.Position = interiorPoints[i];
				vegNodes.Add(newNode);
			}
			//Clear Old
			biomeMask.ClearNodes();
			//Set New
			biomeMask.Nodes = vegNodes;
		}
		else {
			//Just Update the Mask we Have.
			//Create a Biome Mask for this Cell using Interior Points.
			Transform biomeTransform = biomeMask.transform;
			//Now Set Boundary Points of Biome Mask.
			biomeTransform.parent = transform;
			//Move to Cell Center
			biomeTransform.position = transform.position;
			//Set Mask and its Boundary Points.
			List<AwesomeTechnologies.VegetationSystem.Biomes.Node> vegNodes = new List<AwesomeTechnologies.VegetationSystem.Biomes.Node>();
			for (int i = 0; i < interiorPoints.Count; i++)
			{
				AwesomeTechnologies.VegetationSystem.Biomes.Node newNode = new AwesomeTechnologies.VegetationSystem.Biomes.Node();
				newNode.Position = interiorPoints[i];
				vegNodes.Add(newNode);
			}
			//Clear Old
			biomeMask.ClearNodes();
			//Set New
			biomeMask.Nodes = vegNodes;
		}
#endif
			//Re-orient
			//Orient for World Space
			for (int i = 0; i < interiorPoints.Count; i++)
			{
				interiorPoints[i] += transform.position;
			}
			//Clear SideWalks and SideWalkObjects if they Exist from a previous Creation
			//Clear Previous Sidewalks if they exist.
			if (sideWalkHolders.Length > 0)
			{
				for (int i = 0; i < sideWalkHolders.Length; i++)
				{
					DestroyImmediate(sideWalkHolders[i]);
				}
			}
			sideWalkHolders = new GameObject[0];
			//Clear SideWalkObjects
			if (placedObjects.Count > 0)
			{
				for (int i = 0; i < placedObjects.Count; i++)
				{
					DestroyImmediate(placedObjects[i]);
				}
			}
			extPoints = new List<Vector3>(0);
			for (int i = 0; i < interiorPoints.Count; i++)
			{
				extPoints.Add(interiorPoints[i]);
			}
			//Debug.Log("Create SideWalks: "+createSideWalks);
			//If the Interior Points have changed then the SideWalks Must Be Updated As Well.
			if (createSideWalks)
			{
				CreateSideWalks();
				//Lets plot sidewalk Spawn Points.
				PlaceSideWalkObjects();
			}
			else
			{

				List<CiDyVector3> insetPoly = new List<CiDyVector3>(0);

				//Create Polygon to Inset for the SideWalk interior.
				for (int i = 0; i < pointsList.Count; i++)
				{
					for (int j = 0; j < pointsList[i].Count; j++)
					{
						insetPoly.Add(new CiDyVector3(pointsList[i][j]));
					}
					bool skip = false;
					for (int k = 0; k < deadEnds.Count; k++)
					{
						if (i == deadEnds[k])
						{
							//This is a culdesac we cannot calculate a corner for it.
							skip = true;
							break;
						}
					}
					if (!skip)
					{
						//Now Add Corner Point.
						CiDyVector3 lastPoly = insetPoly[insetPoly.Count - 1];
						Vector3 testPoly = cornerPoints[i];
						float dist = Mathf.Round(Vector3.Distance(lastPoly.pos, testPoly));
						if (dist > 1f)
						{
							//Debug.Log(dist);
							CiDyVector3 poly = new CiDyVector3(testPoly);
							poly.isCorner = true;
							insetPoly.Add(poly);
						}
						else
						{
							insetPoly[insetPoly.Count - 1].isCorner = true;
						}
					}
					else
					{
						//Just add culdesac Curve
						for (int j = 0; j < curveList[i].Count; j++)
						{
							CiDyVector3 newVector = new CiDyVector3(curveList[i][j]);
							newVector.isCorner = true;
							newVector.isCuldesac = true;
							insetPoly.Add(newVector);
							//CiDyUtils.MarkPoint(newVector.pos,j);
						}
					}
				}

				//Simplify Poly by removing straight line intermediate Points.
				for (int i = 0; i < insetPoly.Count; i++)
				{
					//Skip Corners as they should not be removed
					if (insetPoly[i].isCorner)
					{
						continue;
					}
					//Check poly Sign from current to Next
					Vector3 curPoly = insetPoly[i].pos;
					Vector3 nxtPoly;
					Vector3 prevPoly;

					if (i == 0)
					{
						//At Beginning
						prevPoly = insetPoly[insetPoly.Count - 1].pos;
						nxtPoly = insetPoly[i + 1].pos;
					}
					else if (i == insetPoly.Count - 1)
					{
						//At End
						prevPoly = insetPoly[i - 1].pos;
						nxtPoly = insetPoly[0].pos;
					}
					else
					{
						//At Middle
						prevPoly = insetPoly[i - 1].pos;
						nxtPoly = insetPoly[i + 1].pos;
					}
					Vector3 fwdDir = (curPoly - prevPoly).normalized;
					Vector3 targetDir = (nxtPoly - curPoly).normalized;
					float sign = CiDyUtils.AngleDir(fwdDir, targetDir, Vector3.up);
					if (sign == 0)
					{
						//Collinear
						insetPoly.RemoveAt(i);
						i--;
					}
				}

				List<List<CiDyNode>> insetNodes = CiDyUtils.InsetPolygon(insetPoly, 0f);
				lotPoly = insetNodes;
				for (int i = 0; i < lotPoly.Count; i++)
				{
					drawPoints = new List<Vector3>(0);
					for (int j = 0; j < lotPoly[i].Count; j++)
					{
						drawPoints.Add(lotPoly[i][j].position);
					}
					insetTmpPoints.Add(drawPoints);
				}
			}
			//Now that we have SideWalks. Lets SubDivide the Inset Poly to Lots.
			FindLots();
			//Now that we have our Building Lots lets Extrude them From there FootPrints.
			ExtrudeLots();
		}

		void GrabEven2Vs(CiDyRoad newRoad, ref List<Vector3> interiorPnts)
		{
			Vector3[] meshVerts = newRoad.GetComponent<MeshFilter>().sharedMesh.vertices;
			//Iterate through vs list and grab when i is even only.
			for (int i = meshVerts.Length - 1; i > 0; i--)
			{
				if (i % 2 == 0)
				{ // Is odd,
					/*GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					sphere.name = "Odd";
					sphere.transform.localScale = new Vector3(5,5,5);
					sphere.transform.position = newRoad.vs[i];*/
					interiorPnts.Add(meshVerts[i]);
				}
			}
		}

		void GrabOdd2Vs(CiDyRoad newRoad, ref List<Vector3> interiorPnts)
		{
			Vector3[] meshVerts = newRoad.GetComponent<MeshFilter>().sharedMesh.vertices;
			//Iterate through vs list and grab when i is odd only.
			for (int i = meshVerts.Length - 1; i > 0; i--)
			{
				if (i % 2 == 1)
				{ // Is odd,
					/*GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					sphere.name = "Odd";
					sphere.transform.localScale = new Vector3(5,5,5);
					sphere.transform.position = newRoad.vs[i];*/
					interiorPnts.Add(meshVerts[i]);
				}
			}
		}
		/*List<CiDyNode> boundaryCells = new List<CiDyNode>();
		//Send
		void CreateTestRoad(){
			//Create Nodes at the cycleNodes points.
			for(int i = 0;i<cycleNodes.Count;i++){
				CiDyNode thisNode = new CiDyNode("V"+i,cycleNodes[i].transform.position,i);
				boundaryCells.Add(thisNode);
			}
			//Iterate through Nodes and Turn them into secondaryEdges for Debug Testing.
			for(int i = 0;i<boundaryCells.Count-1;i++){
				CiDyEdge newEdge = new CiDyEdge(boundaryCells[i],boundaryCells[i+1]);
				AddSubRoad(newEdge);
				i++;
			}
			//Use the Proposed Segment startPoint,endPoint;
			CiDyNode sourceNode = null;//startPoint.GetComponent (typeof(CiDyNode)) as CiDyNode;
			//Node can be placed.
			GameObject nodeObject = Instantiate(nodePrefab, new Vector3(0,0,0), Quaternion.identity) as GameObject;
			roadObjects.Add (nodeObject);
			nodeObject.transform.parent = graph.nodeHolder.transform;
			nodeObject.name = "V"+testCount;
			testCount++;
			CiDyNode newNode = nodeObject.GetComponent(typeof(CiDyNode)) as CiDyNode;
			//Create direction from endPoint-startPoint.
			Vector3 newDir = (endPoint.transform.position - startPoint.transform.position).normalized;
			//Now run place Segment.
			if(PlaceSegment(sourceNode,newDir,ref newNode)){
				Debug.Log("Added To Queue");
				//Set Node adjacency
				//sourceNode.AddNode(newNode);
				//newNode.AddNode (sourceNode);
			}/* else {
				//No Placement was made destroy object from memory
				DestroyImmediate(nodeObject);
			}*/
		//}
		//Draw the Edges using Draw Lines.
		/*void LateUpdate(){
			if(boundaryEdges.Count>0){
				for(int i=0;i<boundaryEdges.Count;i++){
					//Draw edge
					CiDyEdge drawEdge = boundaryEdges[i];
					Debug.DrawLine(drawEdge.v1.position, drawEdge.v2.position, Color.blue);
				}
			}
			if(secondaryEdges.Count > 0){
				for(int i=0;i<secondaryEdges.Count;i++){
					//Draw edge
					CiDyEdge drawEdge = secondaryEdges[i];
					Debug.DrawLine(drawEdge.v1.position, drawEdge.v2.position, Color.white);
				}
			}
			//iterate through secondary edges and draw lines to represent them.
			if(boxExclusions.Count>0){
				for(int i = 0;i<boxExclusions.Count;i++){
					//Draw Edge
					CiDyEdge drawEdge = boxExclusions[i];
					Debug.DrawLine(drawEdge.v1.position,drawEdge.v2.position, Color.red);
				}
			}
			//Draw Cycles
			if(cycles.Count > 0){
				for(int i = 0;i<cycles.Count;i++){
					List<CiDyNode> cycle = cycles[i];
					for(int j = 0;j<cycle.Count;j++){
						Vector3 v0 = cycle[j].position;
						Vector3 v1;
						if(j != cycle.Count-1){
							//Not at the End
							v1 = cycle[j+1].position;
						} else {
							//At the End
							v1 = cycle[0].position;
						}
						Debug.DrawLine(v0,v1,Color.yellow);
					}
				}
			}
			//Draw SubLots
			if(greenLots.Count > 0){
				for(int i = 0;i<greenLots.Count;i++){
					for(int j = 0;j<greenLots[i].Count;j++){
						Vector3 p0 = greenLots[i][j];
						Vector3 p1;
						if(j == greenLots[i].Count-1){
							p1 = greenLots[i][0];
						} else {
							p1 = greenLots[i][j+1];
						}
						Debug.DrawLine(p0,p1,Color.green);
					}
				}
			}
			if(lots.Count>0){
				for(int i = 0;i<lots.Count;i++){
					for(int j = 0;j<lots[i].vectorList.Count;j++){
						Vector3 p0 = lots[i].vectorList[j];
						Vector3 p1;
						if(j == lots[i].vectorList.Count-1){
							p1 = lots[i].vectorList[0];
						} else {
							p1 = lots[i].vectorList[j+1];
						}
						Debug.DrawLine(p0,p1,Color.blue);
					}
				}
			}
		}*/

		void EndCreation()
		{
			Debug.Log("Ended Creation");
			//End Coroutine
			//StopCoroutine ("CreateSecondaryRoads");
			processing = false;
			//Find scenario when the algorithm is finished.
			if (isStamped == true && processing == false)
			{
				isStamped = false;
				//This means that we want to check the current time.
				//Check start time
				curTime = Time.realtimeSinceStartup;
				tmpTime = (curTime - startTime);
				//Debug.Log("CurTime "+curTime);
				//Debug.Log("Total Time Taken "+tmpTime);
			}
			if (boxExclusions.Count > 0)
			{
				boxExclusions = new List<CiDyEdge>(0);
			}
			/*if(visualNode.position != Vector3.zero){
				visualNode.position = Vector3.zero;
			}*/
			if (visualBounds.Count > 0)
			{
				visualBounds.Clear();
			}
		}

		//The InsetPoly/s that we will subDivide.
		List<List<CiDyNode>> lotPoly = new List<List<CiDyNode>>(0);
		public List<CiDyListWrapper> lots = new List<CiDyListWrapper>(0);
		List<List<Vector3>> greenLots = new List<List<Vector3>>(0);
		[HideInInspector]
		[SerializeField]
		public List<CiDyLot> cidyLots;

		public void FindLots()
		{
			//Debug.Log ("FindLots : " + name+" LotPoly: "+lotPoly.Count);
			lots.Clear();
			greenLots.Clear();
			cidyLots = new List<CiDyLot>(0);

			if (lotPoly == null || lotPoly.Count < 1)
			{
				Debug.Log("No lots to Find");
				//No Lots to Find
				return;
			}
			for (int i = 0; i < lotPoly.Count; i++)
			{
				List<List<CiDyNode>> greenSpace = new List<List<CiDyNode>>(0);
				List<List<CiDyNode>> dividedPoly = new List<List<CiDyNode>>(0);
				if (useGreenSpace)
				{
					dividedPoly = graph.SubdivideLots2(lotPoly[i], lotWidth, lotDepth, ref greenSpace);//With Green Space lots :)
				}
				else
				{
					dividedPoly = graph.SubdivideLots2(lotPoly[i], lotWidth, lotDepth);//No Green Space Lots
				}
				//Debug.Log("Divided Poly");
				for (int j = 0; j < dividedPoly.Count; j++)
				{
					List<Vector3> tmpDivid = new List<Vector3>(0);
					for (int k = 0; k < dividedPoly[j].Count; k++)
					{
						tmpDivid.Add(dividedPoly[j][k].position);
						if (k == dividedPoly[j].Count - 1)
						{
							tmpDivid.Add(dividedPoly[j][0].position);
						}
						//CiDyUtils.MarkPoint(dividedPoly[i][j].position, j);
					}
					crossLines.Add(tmpDivid);
				}
				//Now seperate the points into Vector3 list and add to lots.
				for (int j = 0; j < dividedPoly.Count; j++)
				{
					List<Vector3> tmpList = new List<Vector3>(0);
					//List<CiDyNode> tmpNodes = new List<CiDyNode>(0);
					List<CiDyNode> flippedNode = dividedPoly[j];
					for (int k = 0; k < flippedNode.Count; k++)
					{
						//Check poly Sign from current to Next
						Vector3 curPoly = flippedNode[k].position;
						Vector3 nxtPoly;
						Vector3 prevPoly;

						if (k == 0)
						{
							//At Beginning
							prevPoly = flippedNode[flippedNode.Count - 1].position;
							nxtPoly = flippedNode[k + 1].position;
						}
						else if (k == flippedNode.Count - 1)
						{
							//At End
							prevPoly = flippedNode[k - 1].position;
							nxtPoly = flippedNode[0].position;
						}
						else
						{
							//At Middle
							prevPoly = flippedNode[k - 1].position;
							nxtPoly = flippedNode[k + 1].position;
						}
						Vector3 fwdDir = (curPoly - prevPoly).normalized;
						Vector3 targetDir = (nxtPoly - curPoly).normalized;
						float sign = CiDyUtils.AngleDir(fwdDir, targetDir, Vector3.up);
						if (sign == 0)
						{
							//Collinear
							flippedNode.RemoveAt(k);
							k--;
						}
					}

					//Lets project the y axis to the Lots with Road Access for building placement.
					bool hasAccess = false;
					List<Vector3> longestSide = new List<Vector3>(0);
					List<CiDyListWrapper> roadAccessSides = new List<CiDyListWrapper>(0);
					float totalLength = 0;
					float longest = 0;
					List<Vector3> bestSide = new List<Vector3>(0);
					//Find Longest Road Access Side
					for (int k = 0; k < flippedNode.Count; k++)
					{
						if (flippedNode[k].roadAccess)
						{
							if (!hasAccess)
							{
								//This is the First 
								hasAccess = true;
								longestSide = new List<Vector3>(0);
								totalLength = 0;
								longestSide.Add(flippedNode[k].position);
							}
							else
							{
								//Add to List
								longestSide.Add(flippedNode[k].position);
							}
							//Calculate Side Dist.
							Vector3 v0 = flippedNode[k].position;
							Vector3 v1;
							if (k == flippedNode.Count - 1)
							{
								v1 = flippedNode[0].position;
							}
							else
							{
								v1 = flippedNode[flippedNode.Count - 1].position;
							}
							//Calcuate dist
							totalLength += Vector3.Distance(v0, v1);

							if (flippedNode[k].isCorner)
							{
								if (hasAccess)
								{
									//This must be a split point.
									//End of this List and start of next
									//Determine if this list is the Longest
									if (totalLength > longest)
									{
										longest = totalLength;
										bestSide = longestSide;
									}
									CiDyListWrapper newWrapper = new CiDyListWrapper(longestSide);
									roadAccessSides.Add(newWrapper);
									//Start New List
									longestSide = new List<Vector3>(0);
									totalLength = 0;
									longestSide.Add(flippedNode[k].position);
								}
							}
						}
						else
						{
							if (hasAccess)
							{
								hasAccess = false;
								//We are at the End of the Access List Add Final Point.
								longestSide.Add(flippedNode[k].position);
								//Determine if this list is the Longest
								if (totalLength > longest)
								{
									longest = totalLength;
									bestSide = longestSide;
								}
								CiDyListWrapper newWrapper = new CiDyListWrapper(longestSide);
								roadAccessSides.Add(newWrapper);
							}
						}
					}

					//Set RoadAccesss Sides list so the Best Side is roadList[0]
					Vector3 a = bestSide[0];
					Vector3 b = bestSide[bestSide.Count - 1];
					for (int k = 1; k < roadAccessSides.Count; k++)
					{
						if (CiDyUtils.SameVector3s(a, roadAccessSides[k].vectorList[0]))
						{
							//This is the Best Side and its not in 0 position. Re Insert it into the Front.
							roadAccessSides.RemoveAt(k);
							CiDyListWrapper newWrapper = new CiDyListWrapper(bestSide);
							roadAccessSides.Insert(0, newWrapper);
							//Do not need to Continue there will only be one match.
							break;
						}
					}
					//Debug.Log("Interpolate Points");
					//Determine Fwd Direction to Project Point
					Vector3 dir = (b - a).normalized;
					Vector3 fwd = Vector3.Cross(Vector3.up, dir).normalized;
					Vector3 center = ((a + b) / 2) + ((-fwd * (lotDepth / 2)));
					Vector3 end = center + (fwd * (lotDepth / 2 + sideWalkWidth * 2));

					//Find Interpolated Point in 3D
					Vector3 interPoint = Vector3.zero;
					end = center + (fwd * 1000);
					CiDyUtils.FindInterpolatedPointInList(center, end, extPoints.ToArray(), ref interPoint);
					center = a + ((-fwd * (lotDepth / 2)));
					end = center + (fwd * 1000);//center + (fwd * (lotDepth / 2 + sideWalkWidth * 2));
					Vector3 topPoint = Vector3.zero;
					topPoint.y = interPoint.y;
					CiDyUtils.FindInterpolatedPointInList(center, end, extPoints.ToArray(), ref topPoint);
					center = b + ((-fwd * (lotDepth / 2)));
					end = center + (fwd * 1000);
					Vector3 bottomPoint = Vector3.zero;
					bottomPoint.y = interPoint.y;
					CiDyUtils.FindInterpolatedPointInList(center, end, extPoints.ToArray(), ref bottomPoint);
					//Which is lower?
					if (topPoint.y < bottomPoint.y && topPoint.y < interPoint.y)
					{
						interPoint.y = topPoint.y;
					}
					else if (bottomPoint.y < topPoint.y && bottomPoint.y < interPoint.y)
					{
						interPoint.y = bottomPoint.y;
					}
					//interPoint.y = finalY;
					/*if(interPoint.y == 0){
						Debug.LogWarning("No Interpolated Point Found Lot y axis is flat!");
						CiDyUtils.MarkPoint(center,200);
						CiDyUtils.MarkPoint(end,201);
					}*/
					//CiDyUtils.MarkPoint(interPoint,99);
					//GameObject tNode = CiDyUtils.MarkPoint(interPoint,j);
					//visualNodes.Add(tNode);
					bool removedLot = false;
					//Inset Lots
					if (lotInset > 0)
					{
						//Reverse for Inset Algorithm
						flippedNode.Reverse();
						List<List<Vector3>> insetTmp = CiDyUtils.PolygonOffset(flippedNode, -lotInset);
						//List<Vector3> insetTmp = CiDyUtils.PolygonInset(lotInset, flippedNode.ToArray());

						if (insetTmp.Count > 0)
						{
							//Reverse Back
							///insetTmp.Reverse();
							//We have our Inset Poly
							tmpList = insetTmp[0];
						}
						else
						{
							//During inset Process we have shrunk to 0.
							removedLot = true;
						}
					}
					else
					{
						//No Insetting
						for (int k = 0; k < flippedNode.Count; k++)
						{
							tmpList.Add(flippedNode[k].position);
						}
					}
					if (!removedLot)
					{
						//Project Y Axis
						for (int k = 0; k < tmpList.Count; k++)
						{
							tmpList[k] = new Vector3(tmpList[k].x, interPoint.y, tmpList[k].z);
						}
						//Create CiDyLot from this information
						CiDyLot newLot = new CiDyLot(tmpList, roadAccessSides, fwd);
						cidyLots.Add(newLot);
						tmpList.Reverse();
						lots.Add(new CiDyListWrapper(tmpList));
					}
				}
				if (greenSpace.Count > 0)
				{
					//Do the Same for GreenSpace
					for (int j = 0; j < greenSpace.Count; j++)
					{
						List<Vector3> tmpList = new List<Vector3>(0);
						for (int k = 0; k < greenSpace[j].Count; k++)
						{
							tmpList.Add(greenSpace[j][k].position);
						}
						greenLots.Add(tmpList);
					}
				}
			}
			//GameObject tNode = CiDyUtils.MarkPoint(lotNodes[i][j].position,j);
			//visualNodes.Add(tNode);
			//Visualize Lots
			//Debug.Log("End of FindLots()");
			//Clear Lot PolyNode references
			lotPoly.Clear();
		}

		[SerializeField]
		List<GameObject> placedObjects = new List<GameObject>(0);
		public bool contourSideWalkLights = false;
		public bool contourSideWalkClutter = true;
		public bool randomizeClutterPlacement = false;//If this is true. We will randomize clutter placement.(If false, we will use a repeating pattern of all the objects.
													  //This Function will Place Street Clutter Spawn Points.
		void PlaceSideWalkObjects()
		{
			int objPlace = -1;//Used for Clutter Placement
							  //Clear cells Previous Objects if Applicable
			if (placedObjects.Count > 0)
			{
				for (int i = 0; i < placedObjects.Count; i++)
				{
					DestroyImmediate(placedObjects[i]);
				}
			}
			float stepSize = 1f;
			for (int i = 0; i < pointsList.Count; i++)
			{
				float lightsCurDist = (pathLightSpacing / 4);
				float cluttersCurDist = (pathClutterSpacing / 4);
				Vector3 lastLightPoint = pointsList[i][0];
				Vector3 lastClutterPoint = pointsList[i][0];
				bool cantPlace = false;
				Vector3 lastPoint = pointsList[i][pointsList[i].Count - 1];
				for (int j = 0; j < pointsList[i].Count - 1; j++)
				{
					//Determine Vectors
					Vector3 p0 = pointsList[i][j];
					Vector3 p1 = pointsList[i][j + 1];
					//Visualize 
					/*GameObject tNode = CiDyUtils.MarkPoint(p0,j);
					visualNodes.Add(tNode);
					//Visualize 
					tNode = CiDyUtils.MarkPoint(p1,j);
					visualNodes.Add(tNode);*/

					Vector3 fwd = (p1 - p0).normalized;
					Vector3 left = Vector3.Cross(fwd, Vector3.up).normalized;
					Vector3 up = Vector3.Cross(left, fwd).normalized;
					//Calculate Distance Between Cur and P1
					float moveDist = Vector3.Distance(lastLightPoint, p0);
					float moveDist2 = Vector3.Distance(lastClutterPoint, p0);
					lightsCurDist += moveDist;
					cluttersCurDist += moveDist2;
					lastLightPoint = p0;
					lastClutterPoint = p0;

					float segDist = Vector3.Distance(p0, p1);
					int stepSpace = Mathf.RoundToInt(segDist / stepSize);

					if (stepSpace > 0)
					{
						for (int k = 0; k < stepSpace; k++)
						{
							Vector3 newLightPoint = lastLightPoint + (fwd * stepSize);
							Vector3 newClutterPoint = lastClutterPoint + (fwd * stepSize);

							lastLightPoint = newLightPoint;
							lastClutterPoint = newClutterPoint;
							lightsCurDist += stepSize;
							cluttersCurDist += stepSize;
							//Place Light
							if (pathLight)
							{
								if (lightsCurDist >= pathLightSpacing)
								{
									//Place Point
									//Place Light nxtToCurb End. Reuse GameObject Memory
									Vector3 lightPoint = lastLightPoint + (left * sideWalkWidth / 5) + (Vector3.up * sideWalkHeight);
									//Make sure that if this is the last segment we are lightSpacing away from the Last Seg Point.
									if (Vector3.Distance(lightPoint, lastPoint) < (pathLightSpacing / 2))
									{
										cantPlace = true;
									}
									if (!cantPlace)
									{
										GameObject newLight = null;

#if UNITY_EDITOR
										//Instantiate the prefab in the scene, as a sibling of current gameObject
										newLight = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pathLight);
#endif
										newLight.transform.position = lightPoint;
										if (contourSideWalkLights)
										{
											Vector3 localFwd = (lightPoint + (-left) - lightPoint);
											Vector3 localUp = (lightPoint + (up) - lightPoint);
											newLight.transform.rotation = Quaternion.LookRotation(localFwd, localUp);
										}
										else
										{
											newLight.transform.LookAt(lightPoint + (-left));
										}
										newLight.transform.parent = pathLightHolder.transform;
										placedObjects.Add(newLight);
										//CiDyUtils.MarkPoint(lastPoint,k);
										//Reset Distance Moved.
										lightsCurDist = 0f;
									}
								}
							}
							if (clutterObjects.Length > 0)
							{
								//Place Clutter
								if (cluttersCurDist >= pathClutterSpacing)
								{
									//Determine Spawn Points
									Vector3 clutterPoint = lastClutterPoint + (left * (sideWalkWidth / 2.5f)) + (Vector3.up * sideWalkHeight);

									if (Vector3.Distance(clutterPoint, lastPoint) < (pathClutterSpacing / 2))
									{
										cantPlace = true;
									}
									if (!cantPlace)
									{
										if (randomizeClutterPlacement)
										{
											objPlace = Mathf.RoundToInt(Random.Range(0f, clutterObjects.Length - 1));
										}
										else
										{
											objPlace++;
											if (objPlace > clutterObjects.Length - 1)
											{
												objPlace = 0;
											}
										}
										//Get path to nearest (in case of nested) prefab from this gameObject in the scene
										GameObject newClutter = null;
#if UNITY_EDITOR
										string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(clutterObjects[objPlace]);
										//Get prefab object from path
										Object prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
										//Instantiate the prefab in the scene, as a sibling of current gameObject
										newClutter = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#endif
										newClutter.transform.position = clutterPoint;
										//Place and Rotate ClutterGameObject
										if (contourSideWalkClutter)
										{
											Vector3 localFwd = (clutterPoint + (-left) - clutterPoint);
											Vector3 localUp = (clutterPoint + (up) - clutterPoint);
											newClutter.transform.rotation = Quaternion.LookRotation(localFwd, localUp);
										}
										else
										{
											newClutter.transform.LookAt(newClutter.transform.position + (-left));
										}
										newClutter.transform.parent = clutterHolder.transform;
										placedObjects.Add(newClutter);
										//Reset Distance Moved
										cluttersCurDist = 0f;
									}
								}
							}
						}
					}
				}
			}
		}

		public GameObject[] prefabBuildings = new GameObject[0];
		public bool randomSeedOnRegenerate = true;//This will automatically Randomize the Seed Value when we click regenerate.
		public bool autoFillBuildings = true;//Do we want this cell to Use the Resource Buildings Folders if the user has left the prefabs blank?
		public bool maximizeLotSpace = true;
		public bool huddleBuildings = true;//If true then we will place buildings as close as possible, in an attempt to achieve a Sanfransico esq look.
#if VEGETATION_STUDIO_PRO
	public bool generateBiomeMaskForCell = true;//True when a user with Vegetation Studio wants us to Generate a Biome Mask and Expose the Enum.
	//Generate Biome Mask
	[HideInInspector]
	public BiomeMaskArea biomeMask;//One per Cell
#endif
		[SerializeField]
		List<GameObject> buildings = new List<GameObject>(0);//So we can clear them when we update.
		//List<BuildingRuntime> buildRs = new List<BuildingRuntime>(0);
		//int buildingCount = 0;
		//private List<List<Vector3>> boundaries = new List<List<Vector3>>(0);
		void GrabThemeObjects()
		{
			//Check That CurType index is still valid
			if (districtType > graph.districtTheme.Length || curType > graph.districtTheme.Length)
			{
				//This type must have been removed.
				districtType = 0;
				curType = 0;
			}
			//Get Theme Settings
			GetThemeSettings();
			//Buildings
			if (autoFillBuildings)
			{
				GetThemeBuildings();
			}
			//Theme Clutter
			GetThemeClutter();
			//Theme Lights
			GetThemeLights();
		}

		void GetThemeSettings()
		{
			//Debug.Log("Grab Theme Settings");

		}

		void GetThemeBuildings()
		{
			//Get Prefab Buildings from Resources Folder
			prefabBuildings = Resources.LoadAll("CiDyResources/CiDyTheme" + graph.districtTheme[curType] + "/" + graph.districtTheme[curType] + "Buildings", typeof(GameObject)).Cast<GameObject>().ToArray();
		}

		void GetThemeClutter()
		{
			//No User defined Clutter objects. Self Fill
			clutterObjects = Resources.LoadAll("CiDyResources/CiDyTheme" + graph.districtTheme[curType] + "/" + graph.districtTheme[curType] + "Clutter", typeof(GameObject)).Cast<GameObject>().ToArray();
		}

		void GetThemeLights()
		{
			//Check Theme
			Object originalLightPrefab = Resources.Load("CiDyResources/CiDyTheme" + graph.districtTheme[curType] + "/" + graph.districtTheme[curType] + "StreetLight/StreetLight", typeof(Object));
			if (originalLightPrefab == null) {
				//Get Backup
				originalLightPrefab = Resources.Load("CiDyResources/StreetLight", typeof(GameObject));
			}
#if UNITY_EDITOR
			//Get path to nearest (in case of nested) prefab from this gameObject in the scene
			string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(originalLightPrefab);
			//Get prefab object from path
			pathLight = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
#endif
		}

		void ExtrudeLots()
		{
			//buildingCount = 0;
			if (buildings.Count > 0)
			{
				for (int i = 0; i < buildings.Count; i++)
				{
					DestroyImmediate(buildings[i]);
				}
			}

			/*if(!usePrefabBuildings){
				//Iterate through Lot Vectors and Extrude the Buildings based on Height Values.
				if (lots.Count > 0)
				{
					//Create BuildingGenerator for Lots Points.
					for (int i = 0; i < lots.Count; i++)
					{
						GameObject newBuilding = new GameObject("Building" + buildingCount);
						newBuilding.transform.parent = buildingHolder.transform;

						BuildingRuntime building = new BuildingRuntime();
						//Generate Buildings Using BuildR2
						//Convert Lot to BuildR2 Vector2[]
						Vector2[] buildLot = new Vector2[lots[i].vectorList.Count];
						Vector3 height = CiDyUtils.FindCentroid(lots[i].vectorList, 1);
						newBuilding.transform.position = height;

						for (int j = 0; j < buildLot.Length; j++)
						{
							Vector3 tmpPoint = lots[i].vectorList[j]-newBuilding.transform.position;
							buildLot[j] = new Vector2(tmpPoint.x, tmpPoint.z);
						}
						//Generate Buildings Using BuildR2
						GenesisSettings _settings = GenesisSettings.Get();
						//Generate Building
						GRandom.GenerateNewSeed();
						_settings.seed = GRandom.seedInt;
						//Create Genesis Plot
						GenesisPlot selectedPlot = newBuilding.AddComponent<GenesisPlot>();
						selectedPlot.density = GRandom.Range(10, 20);
						//selectedPlot.density = 20;
						BuildingContraints constraint = (BuildingContraints)Resources.Load("CiDyRome", typeof(BuildingContraints));
						selectedPlot.AddPoints(buildLot);
						building = BuildingGenerator.CreateRuntime(selectedPlot, constraint, _settings.defaultGeneratedBuildingName);
						//CiDy 2.0 Procedural Buildings
						/*CiDyBuildingGen buildGen = newBuilding.AddComponent<CiDyBuildingGen>();
						buildGen.ExtrudePrint(lots[i].vectorList, Random.Range(heightMin, heightMax), true);*/
			/*CiDyBuildGen buildGen = newBuilding.AddComponent<CiDyBuildGen>();
			buildGen.ExtrudePrint(lots[i].vectorList, Random.Range(heightMin,heightMax));*//*
			//.transform.position = height;
			building.SetParentTransform(newBuilding.transform);
			building.Place(height);
			buildRs.Add(building);
			buildings.Add(newBuilding);
			buildingCount++;
			GameObject[] tmpBuildings = new GameObject[1];
			tmpBuildings[0] = newBuilding;
			cidyLots[i].SetBuildings(tmpBuildings);
			//DestroyImmediate(newBuilding);
		}
	}
} else {*/
			if (usePrefabBuildings)
			{
				if (prefabBuildings.Length == 0)
				{
					Debug.Log("No Buildings to Place");
					//No Buildings to Place
					return;
				}
				//Get Boundaries List for Height grabs of roads.
				Vector3[] lotBounds = new Vector3[interiorPoints.Count];
				for (int n = 0; n < interiorPoints.Count; n++)
				{
					lotBounds[n] = interiorPoints[n] + transform.position;
				}
				//For Debug
				//boundaries = new List<List<Vector3>>(0);
				//These points are the polygon we want to fit to.
				//Iterate through Lot and Find Longest Edge With RoadAccess
				for (int i = 0; i < cidyLots.Count; i++)
				{
					//Debug.Log("CiDy Lots Road Sides Count: " + cidyLots[i].roadSides.Count);
					Vector3 fwd = cidyLots[i].fwd;
					Vector3 cntr = CiDyUtils.FindCentroid(cidyLots[i].lotPrint, 1);
					Vector3[] lotBoundary = cidyLots[i].lotPrint.ToArray();
					//boundaries.Add(lotBoundary.ToList());
					bool placed = false;
					List<int> testedPrefabs = new List<int>(0);
					int bestFit = -1;
					float maxArea = 0;
					int pickedPrefab = 0;
					int maxTrys = prefabBuildings.Length;
					//Find Edge Point
					Vector3 frontCenter = Vector3.zero;
					//Calculate Prefab Building Sizes into a list that matches the Index.
					//We want to know what the Smallest Width Possible is for the Prefabs Available.
					List<float> prefabWidths = new List<float>(0);
					float smallestWidth = Mathf.Infinity;
					//Find the Smallest Width Building.
					for (int c = 0; c < prefabBuildings.Length; c++)
					{
						//Prefab Bounds
						var combinedBounds = prefabBuildings[c].GetComponentInChildren<Renderer>().bounds;
						var renderers = prefabBuildings[c].GetComponentsInChildren<Renderer>();
						foreach (Renderer render in renderers)
						{
							if (render != null)
							{
								combinedBounds.Encapsulate(render.bounds);
							}
						}

						//Prefab Bounds
						Bounds prefabBounds = combinedBounds;
						//CreateBoundsList for Intersection Testing
						Vector3 boundsCntr = prefabBounds.center;
						Vector3 extents = prefabBounds.extents;
						//Extract bounds
						Vector3[] boundFootPrint = new Vector3[4];
						boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
						boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
						boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
						boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
						int leftFntBoundPnt = 1;//Default reference to first point in Bounds
						int rightFntBoundPnt = 0;//Default reference to Second Point in Bounds

						//Area = L*W;//Calculate Area of Bounding Box
						float width = Vector3.Distance(boundFootPrint[leftFntBoundPnt], boundFootPrint[rightFntBoundPnt]);//Width
						prefabWidths.Add(width);//This will also tell us the Width of the Prefab at the Index equal to this index.
						if (width < smallestWidth)
						{
							smallestWidth = width;
						}
					}
					//Find Front Center Point
					CiDyUtils.IntersectsList(cntr, cntr + (fwd * 100), lotBoundary.ToList(), ref frontCenter, true);

					if (!huddleBuildings)
					{
						//Place One Building Prefab per lot.
						while (!placed)
						{
							//Lets pick a Prefab and Visualize its Bounding Box FootPrint.
							for (int k = 0; k < 1; k++)
							{
								pickedPrefab = Mathf.RoundToInt(Random.Range(0, prefabBuildings.Length));
								//Is this a Duplicate?
								if (DuplicateInt(pickedPrefab, testedPrefabs))
								{
									//Duplicate test again.
									k = 0;
								}
								else
								{
									//Add to list
									testedPrefabs.Add(pickedPrefab);
								}
							}
							//Create Building an Grab its Bounding Box.
							Object prefab = null;
#if UNITY_EDITOR
							//Get path to nearest (in case of nested) prefab from this gameObject in the scene
							string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabBuildings[pickedPrefab]);
							//Get prefab object from path
							prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
							//Instantiate the prefab in the scene, as a sibling of current gameObject
							GameObject placedBuilding = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#elif !UNITY_EDITOR
						GameObject placedBuilding = null;
						if(prefab){
							prefab = null;
						}
						placedBuilding = null;
#endif
							//Prefab Bounds
							var combinedBounds = placedBuilding.GetComponentInChildren<Renderer>().bounds;
							var renderers = placedBuilding.GetComponentsInChildren<Renderer>();
							foreach (Renderer render in renderers)
							{
								if (render != null)
								{
									combinedBounds.Encapsulate(render.bounds);
								}
							}

							//Prefab Bounds
							Bounds prefabBounds = combinedBounds;
							Vector3 extents = prefabBounds.extents;
							Vector3 boundsCntr = prefabBounds.center;
							//Move Object to cntrPosition
							placedBuilding.transform.position = cntr;
							//Now Rotate Object and bounds based on transform rotation
							placedBuilding.transform.LookAt(cntr + (fwd * 20));
							placedBuilding.isStatic = true;
							//Extract bounds
							Vector3[] boundFootPrint = new Vector3[4];
							boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
							boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
							boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
							boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
																											  //Translate based on Transform Directions
							for (int j = 0; j < boundFootPrint.Length; j++)
							{
								boundFootPrint[j] = placedBuilding.transform.TransformPoint(boundFootPrint[j]);
							}
							//Area = L*W;//Calculate Area of Bounding Box
							float width = Vector3.Distance(boundFootPrint[0], boundFootPrint[1]);//Width
							float length = Vector3.Distance(boundFootPrint[0], boundFootPrint[3]);//Length
							float area = length * width;
							//Test this List of Points for Intersection Against the Polygon List
							if (!CiDyUtils.BoundsIntersect(lotBoundary, boundFootPrint))
							{
								//This Building Fits
								if (maximizeLotSpace)
								{
									if (area > maxArea)
									{
										maxArea = area;
										bestFit = pickedPrefab;
									}
								}
								else
								{
									//Visualize for Debug
									//boundaries.Add(boundFootPrint.ToList());
									//Now Determine if we can Shift the Building Closer to the Road without going over the Road.
									Vector3 buildFwd = placedBuilding.transform.forward;
									//Now that we have the Front Points. Left detemine. its Distance from Front Center Point.
									float frontDist = Vector3.Distance((boundFootPrint[1] + boundFootPrint[0]) / 2, frontCenter);
									//Determine Normalized Value by dividing. frontDist/Center to Front Distnace.(range 0 - 1). Lerp Value
									float centerDist = Vector3.Distance(cntr, frontCenter);
									float lerpValue = frontDist / centerDist;
									placedBuilding.transform.position = Vector3.Lerp(cntr, frontCenter, lerpValue);
									//Add to Cell
									placed = true;
									buildings.Add(placedBuilding);
									placedBuilding.transform.parent = transform;//Parent into Cell
									if (!lotsUseRoadHeight)
									{
										//Project it to the Y Access
										placedBuilding.transform.position = new Vector3(placedBuilding.transform.position.x, cntr.y, placedBuilding.transform.position.z);
									}
									else
									{
										//Project it to the Y Access
										placedBuilding.transform.position = new Vector3(placedBuilding.transform.position.x, cntr.y - sideWalkHeight, placedBuilding.transform.position.z);
									}
									cidyLots[i].SetBuildings(buildings.ToArray());
									//cidyLots[i].empty = false;//Update Cell State
								}
							}
							else
							{
								//Breaks the Lot Line. Destroy this building
								DestroyImmediate(placedBuilding);
							}
							if (maximizeLotSpace)
							{
								DestroyImmediate(placedBuilding);
							}
							//Do not go over the Max Tries
							if (testedPrefabs.Count >= maxTrys)
							{
								placed = true;
							}
						}

						//Place Best Fit Buildings if Applicable
						if (bestFit != -1)
						{
							//Now Place the Best Fit
							//Create Building an Grab its Bounding Box.
							Object prefab = null;
#if UNITY_EDITOR
							//Get path to nearest (in case of nested) prefab from this gameObject in the scene
							string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabBuildings[bestFit]);
							//Get prefab object from path
							prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
							//Instantiate the prefab in the scene, as a sibling of current gameObject
							GameObject newBuilding = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#elif !UNITY_EDITOR
						if(prefab){
							prefab = null;
						}
						GameObject newBuilding = null;
#endif
							//Prefab Bounds
							var combinedBounds = newBuilding.GetComponentInChildren<Renderer>().bounds;
							var renderers = newBuilding.GetComponentsInChildren<Renderer>();
							foreach (Renderer render in renderers)
							{
								if (render != null)
								{
									combinedBounds.Encapsulate(render.bounds);
								}
							}

							//Prefab Bounds
							Bounds prefabBounds = combinedBounds;
							Vector3 extents = prefabBounds.extents;
							Vector3 boundsCntr = prefabBounds.center;
							//Move 
							newBuilding.transform.position = cntr;
							//Place Building
							newBuilding.transform.LookAt(newBuilding.transform.position + (fwd * 20));
							//newBuilding.transform.position = Vector3.Lerp(cntr, frontCenter, 0.5f);
							//CreateBoundsList for Intersection Testing
							//Extract bounds
							Vector3[] boundFootPrint = new Vector3[4];
							boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
							boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
							boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
							boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
																											  //Translate based on Transform Directions
							for (int j = 0; j < boundFootPrint.Length; j++)
							{
								boundFootPrint[j] = newBuilding.transform.TransformPoint(boundFootPrint[j]);
							}
							//Visualize for Debug
							//boundaries.Add(boundFootPrint.ToList());
							//Now Determine if we can Shift the Building Closer to the Road without going over the Road.
							Vector3 buildFwd = newBuilding.transform.forward;
							//Now that we have the Front Points. Left detemine. its Distance from Front Center Point.
							float frontDist = Vector3.Distance((boundFootPrint[1] + boundFootPrint[0]) / 2, frontCenter);
							//Determine Normalized Value by dividing. frontDist/Center to Front Distnace.(range 0 - 1). Lerp Value
							float centerDist = Vector3.Distance(cntr, frontCenter);
							float lerpValue = frontDist / centerDist;
							newBuilding.transform.position = Vector3.Lerp(cntr, frontCenter, lerpValue);
							//Add to Cell
							buildings.Add(newBuilding);
							newBuilding.transform.parent = transform;//Parent into Cell
							if (!lotsUseRoadHeight)
							{
								//Project it to the Y Access
								newBuilding.transform.position = new Vector3(newBuilding.transform.position.x, cntr.y, newBuilding.transform.position.z);
							}
							else
							{
								newBuilding.transform.position = new Vector3(newBuilding.transform.position.x, cntr.y - sideWalkHeight, newBuilding.transform.position.z);
							}
							List<GameObject> buildingArray;
							if (cidyLots[i].buildings != null)
							{
								buildingArray = cidyLots[i].buildings.ToList();
							}
							else
							{
								buildingArray = new List<GameObject>(0);
							}
							buildingArray.Add(newBuilding);
							cidyLots[i].SetBuildings(buildingArray.ToArray());
							//cidyLots[i].empty = false;//Update Cell State
						}
					}
					else
					{
						//TODO Increase Huddle Efficient by Desiring Maximum Space as well as Huddle Fitting.
						//Store reference to all the Added Buildings so we can offset them later.
						List<GameObject> rowBuildings = new List<GameObject>(0);
						//Debug.Log("CiDy Lots Road Sides Count: " + cidyLots[i].roadSides.Count);
						//Place As Many Buildings Side By Side in this Lot as we can fit.
						//For Every Road with Road Access. We want to Cram Buildings in along the Road Edge.
						for (int t = 0; t < cidyLots[i].roadSides.Count; t++)
						{
							//Iterate through RoadSides(May only be one)
							float remainingRoadAccess = CiDyUtils.FindTotalDistOfPoints(cidyLots[i].roadSides[t].vectorList);
							//Place Buildings As Close Together as Possible.(Huddle Them)
							maxTrys = Mathf.RoundToInt(remainingRoadAccess * 16);
							//Debug.Log(t + " : " + maxTrys+" Remaining Dist: "+ remainingRoadAccess);
							//Debug.Log("Road Access Length for LOT: " + i + " Length: " + remainingRoadAccess);
							//Now that we know the Road Length. Lets start at the First Point and work our way down the line.
							Vector3 lastRightPoint = cidyLots[i].roadSides[t].vectorList[0];//Right Point
																							//Vector3 lastLeftPoint = cidyLots[i].roadSides[0].vectorList[cidyLots[i].roadSides[0].vectorList.Count - 1];
																							//This is Line we will use to determine lerp point?
							Vector3 leftPoint = cidyLots[i].roadSides[t].vectorList[cidyLots[i].roadSides[t].vectorList.Count - 1];//Left Point of Road Access
							Vector3 right = (lastRightPoint - leftPoint).normalized;//Right Direction
																					//Update Fwrd Direction as we have multiple Road Sides to Consider now.
							fwd = Vector3.Cross(Vector3.up, -right);
							//Move it by 0.1
							//lastRightPoint += (-right * 0.1f);
							int tryCount = 0;
							//Now iterate through and Fit buildings
							while (remainingRoadAccess > 0)
							{
								//Pick a Building that fits into the RemainingRoadAccess Space(If one is avialable)
								if (remainingRoadAccess >= smallestWidth)
								{
									//Debug.Log(remainingRoadAccess + ">");
									//testedPrefabs = new List<int>(0);
									//There is a Building in here that will fit. But lets not keep grabbing the same ones.
									int k = 0;
									while (k == 0)
									{
										pickedPrefab = Random.Range(0, prefabWidths.Count);
										if (prefabWidths[pickedPrefab] > remainingRoadAccess)
										{
											//Debug.Log("Picked: " + pickedPrefab + " Is too Large, " + prefabWidths[pickedPrefab] + " > " + remainingRoadAccess);
											//Wont fit Try Again but also add it to the List of Tested.
											k = 0;
											if (!DuplicateInt(pickedPrefab, testedPrefabs))
											{
												//Add to list
												testedPrefabs.Add(pickedPrefab);
											}
										}
										else
										{
											k = 1;
										}

										if (testedPrefabs.Count >= prefabWidths.Count)
										{
											k = 1;
										}
									}
								}
								//Debug.Log("Lot: " + i + "DuplicateInt: " + testedPrefabs.Count + " Remaining Road Access: " + remainingRoadAccess + " Smallest Width: " + smallestWidth + " Picked Prefab: " + pickedPrefab + " Prefab Width: " + prefabWidths[pickedPrefab]);
								Object prefab = null;
#if UNITY_EDITOR
								//Get path to nearest (in case of nested) prefab from this gameObject in the scene
								string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabBuildings[pickedPrefab]);
								//Get prefab object from path
								prefab = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Object));
								//Instantiate the prefab in the scene, as a sibling of current gameObject
								GameObject placedBuilding = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#elif !UNITY_EDITOR
						if(prefab){
							prefab = null;
						}
						GameObject placedBuilding = null;
#endif
								//Prefab Bounds
								var combinedBounds = placedBuilding.GetComponentInChildren<Renderer>().bounds;
								var renderers = placedBuilding.GetComponentsInChildren<Renderer>();
								foreach (Renderer render in renderers)
								{
									if (render != null)
									{
										combinedBounds.Encapsulate(render.bounds);
									}
								}

								Bounds prefabBounds = combinedBounds;
								Vector3 extents = prefabBounds.extents;
								Vector3 boundsCntr = prefabBounds.center;
								//Move 
								placedBuilding.transform.position = cntr;
								//Place Building
								placedBuilding.transform.LookAt(placedBuilding.transform.position + (fwd * 20));
								placedBuilding.isStatic = true;
								//CreateBoundsList for Intersection Testing
								//Extract bounds
								Vector3[] boundFootPrint = new Vector3[4];
								boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
								boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
								boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
								boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
																												  //Translate based on Transform Directions
								for (int j = 0; j < boundFootPrint.Length; j++)
								{
									boundFootPrint[j] = placedBuilding.transform.TransformPoint(boundFootPrint[j]);
								}
								//CreateBoundsList for Intersection Testing
								int leftFntBoundPnt = 1;//Default reference to first point in Bounds
								int rightFntBoundPnt = 0;//Default reference to Second Point in Bounds
														 //Debug.Log("Added: " + pickedPrefab + " Width: " + prefabWidths[pickedPrefab]);
														 //Get Center Bounds to Front Right Dist and Direction
								Vector3 heading = (boundFootPrint[rightFntBoundPnt] - lastRightPoint);
								float distance = heading.magnitude;//Distance from Bound Front Right to Bounds Center(Flattend to 2D)
								Vector3 direction = heading / distance; // This is now the normalized direction.
																		//Project Where Center point should be based on LastRightPoint. and Bounds Dist/Direction
								placedBuilding.transform.position += (-direction * (distance - 0.1f));
								//Update Bounds
								boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
								boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
								boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
								boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
																												  //Translate based on Transform Directions
								for (int j = 0; j < boundFootPrint.Length; j++)
								{
									boundFootPrint[j] = placedBuilding.transform.TransformPoint(boundFootPrint[j]);
								}
								//Make Sure Width is Within Road Width and not Overlapping any previously placed buildings.
								//if (prefabWidths[pickedPrefab] <= remainingRoadAccess && !CiDyUtils.BoundsIntersectOverload(lotBoundary, boundFootPrint))
								if (prefabWidths[pickedPrefab] <= remainingRoadAccess)
								{
									//TODO Change BoundsIntersect Test from LotBoundary and BoundFootprint but to the Full Cell Boundary that the Lots were cut from.
									//Check this Buildings Boundaries to the Already Placed Buildings of this Cell.//Make sure we do not place a building into another cell building of any other lots.
									if (!CiDyUtils.BoundsIntersect(boundFootPrint, buildings.ToArray()))// && !CiDyUtils.BoundsIntersect(lotBounds,boundFootPrint))
									{
										//Update Last Point for Next Building
										lastRightPoint = boundFootPrint[leftFntBoundPnt] + (-right * 0.1f);
										//Add to Array
										placedBuilding.transform.parent = transform;
										buildings.Add(placedBuilding);
										//boundaries.Add(boundFootPrint.ToList());
										//Subtract Width of Building from RoadAccessSpace Remaining
										remainingRoadAccess -= prefabWidths[pickedPrefab];
										//Get Y height
										if (!lotsUseRoadHeight)
										{
											//Test from building position
											placedBuilding.transform.position = new Vector3(placedBuilding.transform.position.x, cntr.y, placedBuilding.transform.position.z);
										}
										else
										{
											placedBuilding.transform.position = new Vector3(placedBuilding.transform.position.x, cntr.y - sideWalkHeight, placedBuilding.transform.position.z);
										}
										//Test Line from Projected Building Position center to boundary
										Vector3 p0 = placedBuilding.transform.position;
										//Project 160 meters forward
										Vector3 p1 = p0 + (placedBuilding.transform.forward * 160f);
										//Now Test Againts Boundary Line
										Vector3 heightPoint = Vector3.zero;
										CiDyUtils.FindInterpolatedPointInList(p0, p1, lotBounds, ref heightPoint);
										//Did we get the Height?
										if (heightPoint != Vector3.zero)
										{
											if (!lotsUseRoadHeight)
											{
												//We have height data
												placedBuilding.transform.position = new Vector3(p0.x, heightPoint.y, p0.z);
											}
											else
											{
												placedBuilding.transform.position = new Vector3(p0.x, heightPoint.y - sideWalkHeight, p0.z);
											}
										}
										rowBuildings.Add(placedBuilding);//Add to Row Buildings Temp List for final Offset if needed
										List<GameObject> buildingArray;
										if (cidyLots[i].buildings != null)
										{
											buildingArray = cidyLots[i].buildings.ToList();
										}
										else
										{
											buildingArray = new List<GameObject>(0);
										}
										buildingArray.Add(placedBuilding);
										cidyLots[i].SetBuildings(buildingArray.ToArray());
										placedBuilding = null;
									}
									else
									{
										//Move the Test Point by the Smallest Width
										lastRightPoint += (-right * 0.1f);
										remainingRoadAccess -= 0.1f;

										//Destory Buildings
										DestroyImmediate(placedBuilding);
									}
								}
								else
								{
									//Move the Test Point by the Smallest Width
									lastRightPoint += (-right * 0.1f);
									remainingRoadAccess -= 0.1f;

									//Destory Buildings
									DestroyImmediate(placedBuilding);
									//Are there any Prefabs that Could Fit or Not?
									if (smallestWidth > remainingRoadAccess)
									{
										//Debug.Log("No Remaining Prefabs are Small enough for Remaining Road Access");
										//Clear remaining 
										remainingRoadAccess = 0;
									}
								}
								tryCount++;
								if (maxTrys <= tryCount)
								{
									//Debug.Log("We are out of Tries");
									//Clear remaining 
									remainingRoadAccess = 0;
								}
							}
						}
						//We have finished Filling this Lot.
						placed = true;
					}
				}
			}
			//Handle Vegetation Pro Masking of Lots that have buildings on them only
			//Create Vegitation Mask
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
            for (int i = 0; i < cidyLots.Count; i++) {
            if (cidyLots[i].empty) {
                continue;
            }
            //This lot must have something on it, so lets create an area mask to cover its interior of vegetation for each occupied Lot.
                //Generate SideWalk Vegitation Masks.
                VegetationMaskArea vegetationMaskArea = cidyLots[i].buildings[0].GetComponent<VegetationMaskArea>();
                //Calculate Needed Points
                List<Vector3> print = cidyLots[i].lotPrint;
                Vector3[] vegPoints = new Vector3[print.Count];
                Vector3 walkPos = transform.position;
                for (int j = 0; j < print.Count; j++)
                {
                    //Add Orig Pos
                    vegPoints[j] = (print[j]);
                }
                if (vegetationMaskArea)
                {
                    //Set Values
                    vegetationMaskArea.RemoveGrass = false;
                    vegetationMaskArea.RemovePlants = true;
                    vegetationMaskArea.RemoveTrees = true;
                    vegetationMaskArea.RemoveObjects = true;
                    vegetationMaskArea.RemoveLargeObjects = true;
                    vegetationMaskArea.AdditionalGrassPerimiter = 5f;
                    vegetationMaskArea.ClearNodes();
                    vegetationMaskArea.AddNodesToEnd(vegPoints);
                    //Points in the array list needs to be in worldspace positions.
                    vegetationMaskArea.UpdateVegetationMask();
                }
                else
                {
                    vegetationMaskArea = cidyLots[i].buildings[0].AddComponent<VegetationMaskArea>();
                    //Set Values
                    vegetationMaskArea.RemoveGrass = false;
                    vegetationMaskArea.RemovePlants = true;
                    vegetationMaskArea.RemoveTrees = true;
                    vegetationMaskArea.RemoveObjects = true;
                    vegetationMaskArea.RemoveLargeObjects = true;
                    vegetationMaskArea.AdditionalGrassPerimiter = 5f;
                    vegetationMaskArea.ClearNodes();
                    vegetationMaskArea.AddNodesToEnd(vegPoints);
                    //Points in the array list needs to be in worldspace positions.
                    vegetationMaskArea.UpdateVegetationMask();
                }
            }
#endif
		}

		bool DuplicateInt(int testInt, List<int> intList)
		{
			for (int i = 0; i < intList.Count; i++)
			{
				if (testInt == intList[i])
				{
					return true;
				}
			}
			return false;
		}

		//Holders for SideWalks/Corners
		public GameObject[] sideWalkHolders = new GameObject[0];
		private GameObject[] cornerPieces = new GameObject[0];
		//Visual Nodes
		public List<GameObject> visualNodes = new List<GameObject>(0);
		//private List<List<CiDyNode>> insetPolygons = new List<List<CiDyNode>>(0);
		private List<List<Vector3>> insetTmpPoints = new List<List<Vector3>>(0);
		private List<List<Vector3>> crossLines = new List<List<Vector3>>(0);
		//Using the Cells SideWalkWidth Value.
		void CreateSideWalks()
		{
			//TODO FIX SIDEWALK CREATION To ALLOW FOR ADVANCED GEOMETRY
			//Call Create SideWalk from here
			if (sideWalkHolders.Length != 0)
			{
				//Destroy
				for (int i = 0; i < sideWalkHolders.Length; i++)
				{
					DestroyImmediate(sideWalkHolders[i]);
				}
			}
			//Handle Corner SideWalk Meshes.
			if (cornerPieces.Length != 0)
			{
				//Destroy
				for (int i = 0; i < cornerPieces.Length; i++)
				{
					DestroyImmediate(cornerPieces[i]);
				}
			}
			sideWalkHolders = new GameObject[cornerPoints.Count];
			cornerPieces = new GameObject[cornerPoints.Count];
			Vector3[] connectorVectors = new Vector3[cornerPieces.Length];
			//Debug.Log("Create SideWalks: "+name);
			//Create Tmp List for InsetPoly Data to be converted to a list of inset polys
			List<CiDyVector3> insetPoly = new List<CiDyVector3>(0);

			for (int i = 0; i < pointsList.Count; i++)
			{
				//Road and its Curve points
				for (int j = 0; j < pointsList[i].Count; j++)
				{
					Vector3 pos = pointsList[i][j];
					pos.y = 0;
					insetPoly.Add(new CiDyVector3(pos));
				}
				//Now handle Curve (corner) and Culdesac connections
				bool skip = false;
				for (int k = 0; k < deadEnds.Count; k++)
				{
					if (i == deadEnds[k])
					{
						//This is a culdesac we cannot calculate a corner for it.
						skip = true;
						break;
					}
				}

				//Are we entering a Culdesac? or a standard Corner?
				if (!skip)
				{
					//Standard Corner
					//Now Add Corner Point.
					CiDyVector3 lastPoly = insetPoly[insetPoly.Count - 1];
					lastPoly.pos.y = 0;
					Vector3 testPoly = cornerPoints[i];
					testPoly.y = 0;
					float dist = Mathf.Round(Vector3.Distance(lastPoly.pos, testPoly));
					if (dist > 1f)
					{
						//Debug.Log(dist);
						CiDyVector3 poly = new CiDyVector3(testPoly);
						poly.isCorner = true;
						insetPoly.Add(poly);
					}
					else
					{
						insetPoly[insetPoly.Count - 1].isCorner = true;
					}
					//Previous Point and Next Point plus desired Width and Height of Sidewalk will Create the Corner Mesh
					Vector3 pA = pointsList[i][pointsList[i].Count - 1];
					Vector3 pB = cornerPoints[i];
					Vector3 pC;
					if (i == pointsList.Count - 1)
					{
						//Last List
						pC = pointsList[0][0];//Pushed to Next Point.
					}
					else
					{
						pC = pointsList[i + 1][0];
					}
					/*CiDyUtils.MarkPoint(pA, 11111);
					CiDyUtils.MarkPoint(pB, 22222);
					CiDyUtils.MarkPoint(pC, 33333);*/

					//Create CornerSideWalk
					/*Vector3 newConnectorVector = Vector3.zero;
					GameObject newCorner = new GameObject("Corner: " + i);
					Mesh newCornerMesh = CiDyUtils.ExtrudeCornerSideWalk(ref newConnectorVector, pA, pB, pC, sideWalkWidth, sideWalkHeight, transform, newCorner.transform, 0.618f);
					connectorVectors[i] = newConnectorVector;
					//Create Throway Mesh
					MeshRenderer cornerRenderer = newCorner.AddComponent<MeshRenderer>();
					MeshFilter cornerFilter = newCorner.AddComponent<MeshFilter>();
					cornerRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/SideWalk", typeof(Material));
					cornerFilter.sharedMesh = newCornerMesh;
					//Add To SideWalkHolders
					cornerPieces[i] = newCorner;*/
				}
				else
				{
					//Just add culdesac Curve
					for (int j = 0; j < curveList[i].Count; j++)
					{
						Vector3 pos = curveList[i][j];
						pos.y = 0;
						CiDyVector3 newVector = new CiDyVector3(pos);
						newVector.isCorner = true;
						newVector.isCuldesac = true;
						insetPoly.Add(newVector);
					}
				}
			}

			/*//Create SideWalk
			GameObject sideWalk = new GameObject("SideWalk: " + i);
			MeshRenderer mrender = sideWalk.AddComponent<MeshRenderer>();
			//Apply SideWalk Texture
			mrender.sharedMaterial = (Material)Resources.Load("CiDyResources/SideWalk");
			sideWalk.AddComponent<MeshFilter>().sharedMesh = CiDyUtils.ExtrudeDetailedSideWalk(insetLines[i].ToArray(), sideWalkHeight, sideWalkWidth, sideWalkEdgeWidth, sideWalkEdgeHeight, transform, sideWalk.transform);
			sideWalk.AddComponent<MeshCollider>();
			sideWalk.transform.parent = transform;
			sideWalkHolders[i] = sideWalk;
			cornerPieces[i].transform.parent = sideWalk.transform;
			Debug.Log("Created SideWalk: " + i);*/
			//Simplify Poly by removing straight line intermediate Points and Close Points collapse to Single Point.
			for (int i = 0; i < insetPoly.Count; i++)
			{
				//Skip Corners as they should not be removed
				if (insetPoly[i].isCorner)
				{
					continue;
				}
				//Check poly Sign from current to Next
				Vector3 curPoly = insetPoly[i].pos;
				curPoly.y = 0;
				Vector3 nxtPoly;
				Vector3 prevPoly;

				if (i == 0)
				{
					//At Beginning
					prevPoly = insetPoly[insetPoly.Count - 1].pos;
					nxtPoly = insetPoly[i + 1].pos;
				}
				else if (i == insetPoly.Count - 1)
				{
					//At End
					prevPoly = insetPoly[i - 1].pos;
					nxtPoly = insetPoly[0].pos;
				}
				else
				{
					//At Middle
					prevPoly = insetPoly[i - 1].pos;
					nxtPoly = insetPoly[i + 1].pos;
				}
				prevPoly.y = 0;
				nxtPoly.y = 0;
				Vector3 fwdDir = (curPoly - prevPoly).normalized;
				Vector3 targetDir = (nxtPoly - curPoly).normalized;
				float sign = CiDyUtils.AngleDir(fwdDir, targetDir, Vector3.up);

				if (sign == 0)
				{
					//Collinear
					insetPoly.RemoveAt(i);
					//i = 0;
					i--;
				}
				//Debug.Log("Create 5");
			}

			//insetTmpPoints = new List<List<Vector3>>(0);

			//Get Inside Line
			List<Vector3> hole = new List<Vector3>(0);
			for (int i = 0; i < insetLines.Count; i++)
			{
				Vector3[] tmpPoints = new Vector3[insetLines[i].Count + 2];
				//Add Corner Connector
				if (i == 0)
				{
					tmpPoints[0] = connectorVectors[connectorVectors.Length - 1];
				}
				else
				{
					tmpPoints[0] = connectorVectors[i - 1];
				}
				for (int j = 0; j < insetLines[i].Count; j++)
				{
					tmpPoints[j + 1] = insetLines[i][j];
					hole.Add(insetLines[i][j]);
				}
				//Add Corner Connector
				tmpPoints[tmpPoints.Length - 1] = connectorVectors[i];
				for (int j = 0; j < insetCurves[i].Count; j++)
				{
					hole.Add(insetCurves[i][j]);
				}
				/*GameObject sideWalk = new GameObject("SideWalk: " + i);
				MeshRenderer mrender = sideWalk.AddComponent<MeshRenderer>();
				//Apply SideWalk Texture
				mrender.sharedMaterial = (Material)Resources.Load("CiDyResources/SideWalk");
				sideWalk.AddComponent<MeshFilter>().sharedMesh = CiDyUtils.ExtrudeDetailedSideWalk(tmpPoints, sideWalkHeight, sideWalkWidth, sideWalkEdgeWidth, sideWalkEdgeHeight, transform, sideWalk.transform);
				sideWalk.AddComponent<MeshCollider>();
				sideWalk.transform.parent = transform;
				sideWalkHolders[i] = sideWalk;
				cornerPieces[i].transform.parent = sideWalk.transform;*/
				//Debug.Log("Created SideWalk: " + i);
			}

			//Generate and Display Meshes of SideWalks.
			GameObject sideWalk = new GameObject("SideWalk");
			MeshRenderer mrender = sideWalk.AddComponent<MeshRenderer>();
			sideWalk.AddComponent<MeshFilter>().sharedMesh = CiDyUtils.ExtrudeSideWalk(interiorPoints, hole, sideWalkHeight, transform);
			//sideWalk.AddComponent<MeshFilter>().sharedMesh = CiDyUtils.ExtrudeDetailedSideWalk(tmpPoints.ToArray(), sideWalkHeight, sideWalkWidth, sideWalkEdgeWidth, sideWalkEdgeHeight, transform, sideWalk.transform);
			sideWalk.AddComponent<MeshCollider>();
			sideWalk.transform.parent = transform;
			sideWalkHolders = new GameObject[1];
			sideWalkHolders[0] = sideWalk;
			//Apply Road Texture
			mrender.sharedMaterial = (Material)Resources.Load("CiDyResources/SideWalk");

			//Debug.Log("Create Inset: "+name);
			List<List<CiDyNode>> insetNodes = null;
			try
			{
				insetNodes = CiDyUtils.CreateSkeletonInset(insetPoly, sideWalkWidth);
			}
			catch
			{
				//We Failed
				Debug.LogError("Straight Skeleton Inset has Failed. This Cell is Not Viable. Please Modify Cell Shape, If issue Persist. Contact Developer Support recklessGames@gmail.com");
			}
			//List<Vector3> tmpPons = new List<Vector3>(0);
			lotPoly = insetNodes;
			//Create Vegitation Mask
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
            //Generate SideWalk Vegitation Masks.
            VegetationMaskLine vegetationMaskLine = sideWalk.GetComponent<VegetationMaskLine>();
			if (!vegetationMaskLine)
			{
				vegetationMaskLine = sideWalk.AddComponent<VegetationMaskLine>();
			}
			//Calculate Needed Points
			Vector3[] vegPoints = new Vector3[interiorPoints.Count];
            Vector3 walkPos = transform.position;
            for (int i = 0; i < interiorPoints.Count; i++) {
                //We need line direction to caclulate offset
                Vector3 cur = interiorPoints[i];
                Vector3 nxt;
                if (i == interiorPoints.Count - 1) {
                    //At End
                    nxt = interiorPoints[0];
                } else {
                    nxt = interiorPoints[i+1];
                }
                Vector3 fwd = (nxt-cur).normalized;
                Vector3 left = Vector3.Cross(fwd,Vector3.up);
                vegPoints[i] = (interiorPoints[i]+walkPos)+(left*(sideWalkWidth/2));
            }
            if (vegetationMaskLine)
            {
                //Set Values
                vegetationMaskLine.RemoveGrass = true;
                vegetationMaskLine.RemovePlants = true;
                vegetationMaskLine.RemoveTrees = true;
                vegetationMaskLine.RemoveObjects = true;
                vegetationMaskLine.RemoveLargeObjects = true;
                vegetationMaskLine.AdditionalPlantPerimiter = 1f;
                vegetationMaskLine.AdditionalGrassPerimiter = 1f;
                vegetationMaskLine.LineWidth = sideWalkWidth + 1.618f;
                vegetationMaskLine.ClearNodes();
                //Add RepeatingNode
                vegPoints[vegPoints.Length - 1] = vegPoints[0];
                vegetationMaskLine.AddNodesToEnd(vegPoints);
                //Points in the array list needs to be in worldspace positions.
                vegetationMaskLine.UpdateVegetationMask();
            }
#endif
		}

		//Special Function that will adapt line A to match Line B with segments amount and depth.(y axis)
		List<Vector3> MatchLine(List<Vector3> lineA, List<Vector3> lineB, int segmentLength)
		{
			//Debug.Log ("MatchLine LineACnt: " + lineA.Count+" LineBCnt: "+lineB.Count+" SegmentLength: "+segmentLength);
			//int desiredAmount = lineB.Count;
			List<Vector3> newLine = new List<Vector3>();
			Vector3 intersection = Vector3.zero;
			//Iterate through the LineB list and Match up new Points for LineA
			lineA[0] = new Vector3(lineA[0].x, lineB[0].y, lineA[0].z);
			lineA[lineA.Count - 1] = new Vector3(lineA[lineA.Count - 1].x, lineB[lineB.Count - 1].y, lineA[lineA.Count - 1].z);
			for (int i = 1; i < lineB.Count - 1; i++)
			{
				Vector3 v0 = lineB[i];
				Vector3 v1 = lineB[i + 1];
				Vector3 fwd = (v1 - v0).normalized;
				Vector3 left = Vector3.Cross(fwd, Vector3.up);
				Vector3 lineEnd = v0 + left * (sideWalkWidth * 2);
				for (int j = 0; j < lineA.Count - 1; j++)
				{
					Vector3 p0 = lineA[j];
					Vector3 p1 = lineA[j + 1];
					if (CiDyUtils.LineIntersection(p0, p1, v0, lineEnd, ref intersection))
					{
						newLine.Add(new Vector3(intersection.x, v0.y, intersection.z));
					}
				}
			}
			newLine.Insert(0, lineA[0]);
			newLine.Add(lineA[lineA.Count - 1]);
			lineA = newLine;
			//Return Finalized Line
			return lineA;
		}

		//The secondary placement Queue.
		public ArrayList queue = new ArrayList();
		//This function will create and place the Secondary Roads for this Cell based on the Control Parameters
		public IEnumerator CreateSecondaryRoads()
		{
			Debug.Log("Creating Sub Roads in Cell: " + name);
			//yield return new WaitForSeconds (growthRate);
			processing = true;
			isStamped = true;
			startTime = Time.realtimeSinceStartup;
			//Debug.Log ("Cell "+name+" Generating Sub-Roads");
			//Set Seed
			Random.InitState(seedValue);
			if (subNodes.Count > 0)
			{
				for (int i = 0; i < subNodes.Count; i++)
				{
					subNodes[i].DestroyNode();
				}
				subNodes.Clear();
			}
			subCount = 0;//Must be equal to amount of subNodes in Cell.
						 //Now Check to actual Road Objects and Clean them up.
			if (roadObjects.Count > 0)
			{
				for (int i = 0; i < roadObjects.Count; i++)
				{
					DestroyImmediate(roadObjects[i]);
				}
				roadObjects.Clear();
			}
			//Determine Secondary Rd Start points and Directions based on BoundaryRds List
			FindSecondaryRdStartPoints();//Updated LongestRoads Lis*/
										 //Debug.Log ("Returned From FindSecondary");
										 //Set source Referenced Values
			CiDyNode curNode = null;//Proposed Source Point
			Vector3 curDir = new Vector3(0, 0, 0);//Propsed Source Direction
			bool growing = true;//Sets to false once we are out of nodes in the queue.
								//Run the Queue we use a List<Vector3> [i]=point,[i+1]=direction
								//Debug.Log ("Queue Cnt = "+queue.Count+" processing = "+processing);
			while (growing)
			{
				//Make sure we do not keep making roads if we are at this cells user defined limit.(maxSubRoads)
				if (secondaryEdges.Count >= maxSubRoads)
				{
					//We are Done. :) No more roads can be built in this cell.
					growing = false;
					Debug.LogWarning("Cell Hit MAX ROAD Amount");
					continue;
				}
				//yield return new WaitForSeconds (growthRate);
				//Do we have a source point?
				if (curNode == null)
				{
					//Debug.Log("CurNode == Null");
					//Are there new Segments for Proposal?
					if (queue.Count > 1)
					{
						//Grab the Point and its direction.
						curNode = (CiDyNode)queue[0];
						curDir = (Vector3)queue[1];
						//Pop them from the queue.
						//Point
						queue.RemoveAt(0);
						//Direction
						queue.RemoveAt(0);
						//Debug.Log("Selected New CurNode: "+curNode.name+" CurDir: "+curDir);
						//yield return new WaitForSeconds (growthRate);
					}
					else
					{
						//Debug.Log("Road Construction Complete :)");
						if (proposedLine.Count > 0)
						{
							proposedLine = new List<CiDyEdge>();
						}
						//We are out of new Segments to Create end Growth Iterations.
						growing = false;
						continue;
					}
				}
				//Debug.Log("Test for placement");
				//We have a Source Point and Direction. Lets determine where its targetPos will be based on Control Parameters.
				//Seed Degree from 0-Degree(Range)
				float paramDegree = Random.Range(minDegree, maxDegree + 1);
				//Debug.Log("Set ParamDegree: "+paramDegree);
				/*if(Random.Range(0,2)== 1){
					//Switch to Negative Angle
					paramDegree = -paramDegree;
				}*/
				//int paramDegree = Random.Range(minDegree,maxDegree+1);
				proposedLine = new List<CiDyEdge>();
				//Rotate from curNode position and suggest new Growth Directions.
				for (int i = 0; i < paramDegree; i++)
				{
					yield return new WaitForSeconds(0f);
					//New direction
					float rotation = (i * 360 / paramDegree);
					//float rotation = Random.Range(minRotation, maxRotation);
					//lastRotation = Random.Range(0, lastRotation);
					//changeRotation positive and negative using seed system.
					/*if(Random.Range(0,2) == 0){
						rotation = -rotation;
					}*/
					//Perform Angle Test Min Angles determined by Graph World Setting: intersectionMinAngleLimit.
					Vector3 newDir = (Quaternion.AngleAxis(rotation, Vector3.up) * curDir).normalized;
					//float minAngle = Vector3.Angle(newDir, curDir);
					//Vector3 newDir =(Quaternion.AngleAxis(paramDegree, Vector3.up) * curDir).normalized;
					//Create gameObject to hold node and create Node for placement
					//Node can be placed.
					CiDyNode newNode = NewNode("S" + subCount, Vector3.zero);
					//newNode.subNode = true;
					//graph.masterGraph.Add(newNode);
					//GameObject.Find("Intersection").GetComponent<Renderer>().enabled = false;
					//yield return new WaitForSeconds (growthRate);
					if (PlaceSegment(curNode, newDir, newNode))
					{
						//Debug.Log("Rotation "+rotation+" newDir "+newDir);
						//Add new Point
						queue.Add(newNode);
						//Add new Direction
						queue.Add(newDir);
					}
					//yield return new WaitForSeconds (growthRate);
					//curDir = newDir;
				}
				//Clear curNode and Dir
				curNode = null;
				//curDir = new Vector3(0,0,0);
			}
			//yield return new WaitForSeconds(growthRate);
			if (!growing)
				EndCreation();
		}

		//function to place road segment, returns true on success
		//Will run the Snap algorith and determine if the proposed segment can be placed.
		public bool PlaceSegment(CiDyNode sourceNode, Vector3 newDir, CiDyNode newNode)
		{
			//Debug.Log ("Place Segment");
			//paramLength = Random.Range (minSegmentSize, maxSegmentSize);
			paramLength = Mathf.Round(Random.Range(minSegmentSize, maxSegmentSize) * 10f) / 10f;
			//Create dynamic Bool that will be set based on switch
			bool isPlaced = false;//Default setting = false;
								  //Create new Point/End of Line segment.
			Vector3 targetPos = sourceNode.position + (newDir * paramLength);
			newNode.MoveNode(targetPos);
			//Set Connectivity odds
			float paramConnectivity = Random.Range(0.0f, 1.0f);
			//Run Snap Algorithm based on (snapSize, sourcePoint, targetPoint) sourcePoint-targetPoint is A-B Line Segment
			switch (SnapTest(snapSize, sourceNode, ref newNode, paramConnectivity))
			{
				case 0://Nothing in the Way :) Placing Road
					   //Add to SecondaryEdges.
					CiDyEdge newEdge = new CiDyEdge(sourceNode, newNode);
					//Make sure its not a duplicate edge
					if (!DuplicateEdge(newEdge))
					{
						Debug.Log("New Edge case:0 " + newEdge.name);
						//secondaryEdges.Add(new CiDyEdge(sourceNode,newNode));
						isPlaced = true;
						newEdge.ConnectNodes();
						//CreateSubRoad(newEdge);
						secondaryEdges.Add(newEdge);
					}
					else
					{
						//Cant Place. Destroy Node
						RemoveSubNode(newNode);
					}
					break;
				case 1://Road Snap Event.(Intersection Chosen)
					newEdge = new CiDyEdge(sourceNode, newNode);
					//Make sure its not a duplicate edge
					if (!DuplicateEdge(newEdge))
					{
						Debug.Log("New Edge case:1 " + newEdge.name);
						//secondaryEdges.Add(new CiDyEdge(sourceNode,newNode));
						isPlaced = false;
						newEdge.ConnectNodes();
						//CreateSubRoad(newEdge);
						secondaryEdges.Add(newEdge);
					}
					break;
				case 2://Node Snap Event. (Existing Node Chosen)
					newEdge = new CiDyEdge(sourceNode, newNode);
					//Make sure its not a duplicate edge
					if (!DuplicateEdge(newEdge))
					{
						Debug.Log("New Edge case:2 " + newEdge.name);
						//secondaryEdges.Add(new CiDyEdge(sourceNode,newNode));
						isPlaced = false;
						newEdge.ConnectNodes();
						secondaryEdges.Add(newEdge);
						//CreateSubRoad(newEdge);
					}
					break;
				default://When we cannot place the road determined in the snap test.
						//Cant Place. Destroy Node
					Debug.Log("Can't Place");
					RemoveSubNode(newNode);
					break;
			}
			//Return Dynamically Set Bool
			//Debug.Log ("IS PLACED = " + isPlaced);
			return isPlaced;
		}

		//This function will make a Sub Road out of the CiDyEdge. And Add it to the SubEdges.
		void CreateSubRoad(CiDyEdge newEdge)
		{
			//Debug.Log ("Created SubRoad");
			//Create Path for Bezier Creation
			//List<Vector3> newPath = new List<Vector3> ();
			Vector3[] newPath = new Vector3[4];
			newPath[0] = newEdge.v1.position;// newPath.Add (newEdge.v1.position);
											 //Add Middle Point (min 3 required for Bezier Curve Algorithm)
			newPath[1] = (newEdge.v2.position + newEdge.v1.position) / 2; //newPath.Add ((newEdge.v2.position+newEdge.v1.position)/2);
			newPath[2] = newEdge.v2.position;//newPath.Add (newEdge.v2.position);
											 //newPath = CreateBezier (newPath);
											 //AddSubRoad (newEdge);//Only needed if potential clones
			secondaryEdges.Add(newEdge);
			//Connect Nodes
			newEdge.ConnectNodes();
			//Create Road Mesh. :)
			GameObject newRoad = new GameObject(newEdge.name);
			newRoad.transform.parent = subRoadHolder.transform;
			string roadTag = graph.roadTag;
			newRoad.layer = LayerMask.NameToLayer(roadTag);
			newRoad.tag = roadTag;
			//CiDyRoad tmpRoad = (CiDyRoad)newRoad.AddComponent<CiDyRoad> ();//new Road (path, roadWidth).parent;
			//tmpRoad.InitilizeRoad (newPath, roadWidth, roadSegmentLength, designer.flattenAmount, newEdge.v1, newEdge.v2, newRoad, graph, false);
			roadObjects.Add(newRoad);
			/*newEdge.v1.UpdateRoad (newRoad);
			newEdge.v2.UpdateRoad (newRoad);*/
		}

		List<CiDyEdge> boxExclusions = new List<CiDyEdge>();
		List<CiDyEdge> proposedLine = new List<CiDyEdge>();
		//Snap Algorithm tests on proposed Line Segment.
		int SnapTest(float snapDist, CiDyNode sourceNode, ref CiDyNode targetNode, float paramConnectivity)
		{
			return -1;//Remove line to reach all code
			/*
			//visualNode = targetNode.position;
			//Set dynamic Integer to be returned as representation of snap event. (Default = 0 No snap event)
			int snapEvent = 0;
			//Test Bounding Box Against Boundary Edges and Secondary Edges
			CiDyEdge newEdge = new CiDyEdge (sourceNode, targetNode);
			float proposedEdgeLength = Vector3.Distance(sourceNode.position,targetNode.position);
			//Debug.Log ("Length "+proposedEdgeLength);
			//Debug.Log ("Snap Test SRC:"+sourceNode.name+" DST: "+targetNode.name+" ProposedEdge: "+newEdge.name);
			//Clear previous tested edges.
			if(boxExclusions.Count > 0){
				boxExclusions.Clear();
			}
			//Visual Boxes
			if(visualBounds.Count > 0){
				visualBounds.Clear();
			}
			//Debug.Log ("Boundary Edges: " + boundaryEdges.Count);
			if(secondaryEdges.Count > 0){
				boxExclusions = BoundingBoxTest (ref newEdge, ref secondaryEdges, ref boxExclusions);
				//Debug.Log("Secondary Edges cnt: "+secondaryEdges.Count+" intersectingRds: "+intersectingRds.Count);
			}
			if(boundaryEdges.Count > 0){
				boxExclusions = BoundingBoxTest(ref newEdge, ref boundaryEdges, ref boxExclusions);
				//Debug.Log("BoundaryEdges cnt: "+boundaryEdges.Count+" boxExclusions: "+boxExclusions.Count);
			}
			//Do we have any potential Snap Events?
			if(boxExclusions.Count > 0){
				Debug.Log("BoundaryBox Exclusions Found= "+boxExclusions.Count);
				//We have potential Events continue testing.
				//Peform Test 1(Node Proximity)
				//Call = DistanceToLine(p.position,endA.position,endB.position, ref distToP, ref r, ref s);
				//Test our Proposed line against boxExclusions.v1 and v2 points.
				//Store nodes that are within range for snap events.
				//List<CiDyNode> nodeProximity = new List<CiDyNode>();
				//List<CiDyEdge> nodeProximityEdges = new List<CiDyEdge>(0);
				//float closestR = Mathf.Infinity;
				//CiDyNode nodeEvent = null;
				Vector3 segmentIntersection = sourceNode.position;
				//Vector3 finalIntersection = sourceNode.position;
				List<CiDyNode> closeNodes = new List<CiDyNode>(0);
				Vector3 closeIntersection = Vector3.zero;
				float closestIntersection = Mathf.Infinity;
				CiDyNode[] intersectionNodes = new CiDyNode[2];//These nodes are connected to closeIntersection Edge.
				//bool invalidSegment = false;
				//Iterate through potential intersecting rds.
				for(int i = 0;i<boxExclusions.Count;i++){
					CiDyEdge testEdge = boxExclusions[i];
					//Debug.Log(testEdge.name);
					CiDyNode testNA = testEdge.v1;
					CiDyNode testNB = testEdge.v2;

					//if(!testNA.name.Contains("C")){
					//This node is not already connected to SourceNode.
					testNA.distToP = CiDyUtils.DistanceToLine(testNA.position, sourceNode.position,targetNode.position, ref testNA.r, ref testNA.s, ref segmentIntersection);
					//Debug.Log(testNA.name+" Values r: "+testNA.r+" s: "+testNA.s+" DistToP: "+testNA.distToP);
					if(testNA.r>=0.1f && testNA.r<=1+(snapDist/proposedEdgeLength) && testNA.distToP <= (roadWidth/2)+snapDist){
						if(!DuplicateCloseNode(testNA, closeNodes)){
							closeNodes.Add(testNA);
							Debug.Log("Added CloseNode "+testNA.name);
						}
					}
					//}
					//if(!testNB.name.Contains("C")){
					//This node is not already connected to SourceNode.
					testNB.distToP = CiDyUtils.DistanceToLine(testNB.position, sourceNode.position,targetNode.position, ref testNB.r, ref testNB.s, ref segmentIntersection);
					//Debug.Log(testNB.name+" Values r: "+testNB.r+" s: "+testNB.s+" DistToP: "+testNB.distToP);
						if(testNB.r>=0.1f && testNB.r<=1+(snapDist/proposedEdgeLength) && testNB.distToP <= (roadWidth/2)+snapDist){
							if(!DuplicateCloseNode(testNB, closeNodes)){
								closeNodes.Add(testNB);
								Debug.Log("Added CloseNode "+testNB.name);
							}
						}
					//}

					//Debug.Log(testNA.name+".r: "+testNA.r+" .s: "+testNA.s+" "+testNB.name+" .r "+testNB.r+" .s: "+testNB.s);
					//1+(snapDist/(testNB.position-testNA.position).magnitude)
					//Test for Intersection with the edge. 
					//Only Run Intersection Test on ProposedEdge and Test Edge if the A-B Nodes S values are opossite polarites. other wise no intersection could happen.
					//Dont Run Intersection Test on SourceNode.Adj Nodes.
					if(testNA.name != sourceNode.name && testNB.name != sourceNode.name){
						if(testNA.s < 0 && testNB.s > 0 || testNA.s > 0 && testNB.s < 0){
							if(CiDyUtils.LineIntersection(testNA.position,testNB.position,sourceNode.position,targetNode.position, ref segmentIntersection)){
								//Do Not inforce Boundary Nodes to have roadWidth Dimensions.
								float dist = Vector3.Distance(segmentIntersection,sourceNode.position);
								if(dist <= nodeSpacing){
									Debug.Log("SegIntersection dist: "+dist+" <= "+nodeSpacing);
									//There is no room between the source node and the intersection no road can be placed.
									return -1;
								} else {
									if(dist < closestIntersection){
										closestIntersection = dist;
										Debug.Log("Added Intersection");
										closeIntersection = segmentIntersection;
										intersectionNodes[0] = testNA;
										intersectionNodes[1] = testNB;
									}
								}
							}
						}
					}
				}
				//Now that they are sorted determine if there are potential close nodes that are better suited(closer)
				if(closeNodes.Count > 0){
					if(paramConnectivity > connectivity){
						Debug.Log("Failed Connectivity");
						return -1;
					}
					closeNodes = closeNodes.OrderBy(x=> x.r).ThenBy(x=> x.distToP).ToList();
					//Test Against Potential Intersections if they exist.
					if(closeIntersection != Vector3.zero){
						//for(int i = 0;i<closeIntersections.Count;i++){
							//Distance from source to intersection
							float dist = Vector3.Distance(sourceNode.position,closeIntersection);
							for(int j=0;j<closeNodes.Count;j++){
								//Distance from source to closest node.
								float dist2 = Vector3.Distance(sourceNode.position,closeNodes[j].position);
								if(dist2<dist){
									for(int k = 0;k<boxExclusions.Count;k++){
										CiDyEdge testEdge = boxExclusions[k];
										CiDyNode testNA = testEdge.v1;
										CiDyNode testNB = testEdge.v2;
										if(testNA.name != sourceNode.name && testNB.name != sourceNode.name){
											continue;
										}
										dist = Vector3.Distance(closeNodes[j].position,testNA.position);
										dist2 = Vector3.Distance(closeNodes[j].position,testNB.position);//CiDyUtils.DistanceToLine(closeNodes[j].position,testNA.position,testNB.position);		
										//Make sure we maintain nodeSpacing
										if(dist <= nodeSpacing || dist2<=nodeSpacing){
											Debug.Log("closeNode[j] Dist: "+dist+" < "+nodeSpacing);
											//This intersection cannot be used and this road cannot be placed.
											return -1;
										}
									}
									//The Node is the Best Choice.
									//Clear orig Node. :)
									RemoveSubNode(targetNode);
									targetNode = closeNodes[j];
									Debug.Log("(Chose Node Evnt Vs Intersect)Updated TargetNode- "+targetNode.name);
									return 2;
								} else {
									//The Intersection is the Best Choice
									//Make sure we are not creating too close to sourceNode
									dist = Vector3.Distance(sourceNode.position,closeIntersection);
									if(dist <= nodeSpacing){
										//Cannot create Road
										Debug.Log("Road Length Segment <= nodeSpacing");
										return -1;
									}
									//Perform one last test on the Intersection. If its too close to all other nodes then we cannot use it.
									for(int i = 0;i<boxExclusions.Count;i++){
										CiDyEdge testEdge = boxExclusions[i];
										CiDyNode testNA = testEdge.v1;
										CiDyNode testNB = testEdge.v2;

										//Dont test dist between boundary EDGES as these will not have intersections.
										/*if(testNA.name.Contains("C") && testNB.name.Contains("C")){
											Debug.Log("Found C "+testEdge.name);
											continue;
										}*///Make sure that the Distance from this point to all nodes is greater or equal to roadWidth
			/*dist = Vector3.Distance(closeIntersection,testNA.position);
			dist2 = Vector3.Distance(closeIntersection,testNB.position);
			//Only Test Sub Edges with this and not boundary.
			if(dist <= nodeSpacing || dist2 <= nodeSpacing){
				Debug.Log("Dist: "+Mathf.Min(dist,dist2)+" <= "+nodeSpacing);
				//This intersection cannot be used and this road cannot be placed.
				return -1;
			}

		}
		if(paramConnectivity < connectivity){
			targetNode.MoveNode(closeIntersection);
			//This causes a break in adjacency with the original line. and a new adjacency in between them.
			//Dissconnect the Two Nodes from there adjacency lists.
			CiDyNode nodeA = intersectionNodes[0];
			CiDyNode nodeB = intersectionNodes[1];

			nodeB.RemoveNode(nodeA);
			nodeA.RemoveNode(nodeB);
			//Now add newNode
			nodeA.AddNode(targetNode);
			targetNode.AddNode(nodeA);
			nodeB.AddNode(targetNode);
			targetNode.AddNode(nodeB);
			Debug.Log("Chose Intersection Instead of Node, Moved NewNode to Intersection");
			return 1;
		} else {
			return -1;
		}
	}
}
//}
} else {
//There are no intersections for comparison. Accept lowest CloseNode
if(!closeNodes[0].name.Contains("C")){
//Dont Test shared edges
//We need to perform one last dist test.
//Perform one last test on the Intersection. If its too close to all other nodes then we cannot use it.
for(int i = 0;i<boxExclusions.Count;i++){
	CiDyEdge testEdge = boxExclusions[i];
	CiDyNode testNA = testEdge.v1;
	CiDyNode testNB = testEdge.v2;
	if(testNA.name != sourceNode.name && testNB.name != sourceNode.name){
		continue;
	}
	//Skip this nodes edges
	if(testNA.name == closeNodes[0].name || testNB.name == closeNodes[0].name){
		continue;
	}
	float dist = Vector3.Distance(closeNodes[0].position,testNA.position);
	float dist2 = Vector3.Distance(closeNodes[0].position,testNB.position);//CiDyUtils.DistanceToLine(closeNodes[0].position,testNA.position,testNB.position);		
	//Only Test Sub Edges with this and not boundary.
	if(dist <= nodeSpacing || dist2 <= nodeSpacing){
		Debug.Log("CloseNodes[0] dist<= nodeSpacing "+Mathf.Min(dist,dist2)+" nodeSpacing "+nodeSpacing);
		//This intersection cannot be used and this road cannot be placed.
		return -1;
	}
}
RemoveSubNode(targetNode);
targetNode = closeNodes[0];
Debug.Log("Closest Node Picked "+closeNodes[0].name+" Out of "+closeNodes.Count);
return 2;
} else {
//Cannot do this
Debug.Log("Didnt want to attach to Boundary Node");
return -1;
}
}
} else {
//This is the scenario where we only have intersections.
if(closeIntersection != Vector3.zero){
//for(int j = 0;j<closeIntersections.Count;j++){
//Perform one last test on the Intersection. If its too close to all other nodes then we cannot use it.
for(int i = 0;i<boxExclusions.Count;i++){
	CiDyEdge testEdge = boxExclusions[i];
	CiDyNode testNA = testEdge.v1;
	CiDyNode testNB = testEdge.v2;
	//Make sure that the Distance from this point to all nodes is greater or equal to roadWidth
	float dist = Vector3.Distance(closeIntersection,testNA.position);
	float dist2 = Vector3.Distance(closeIntersection,testNB.position);
	if(dist <= nodeSpacing || dist2 <= nodeSpacing){
		Debug.Log("Failed intersection to Nodes on Edge. Dist: "+Mathf.Min(dist,dist2)+" < "+nodeSpacing);
		//This intersection cannot be used and this road cannot be placed.
		return -1;
	} else {
		if(paramConnectivity < connectivity){
			targetNode.MoveNode(closeIntersection);
			//This causes a break in adjacency with the original line. and a new adjacency in between them.
			//Dissconnect the Two Nodes from there adjacency lists.
			CiDyNode nodeA = intersectionNodes[0];
			CiDyNode nodeB = intersectionNodes[1];
			nodeB.RemoveNode(nodeA);
			nodeA.RemoveNode(nodeB);
			//Now add newNode
			nodeA.AddNode(targetNode);
			targetNode.AddNode(nodeA);
			nodeB.AddNode(targetNode);
			targetNode.AddNode(nodeB);
			Debug.Log("Picked Closest Intersection");
			return 1;
		} else {
			return -1;
		}
	}
}
}
}
//}
//Test1&Test2 Have passed Perform Final Test to make sure the Proposed Edge is Not too Close to any existing roads.
//DistanceToLine(targetNode.position, testNA.position,testNB.position, ref targetNode.distToP, ref targetNode.r, ref targetNode.s, ref segmentIntersection); 
//Iterate through Edges
//Debug.Log("Test3");
if(!SegmentProximity(ref targetNode,boxExclusions,ref segmentIntersection,snapDist)){
Debug.Log("TargetNode DistToLine <= NodeSpacing :( Cannot Place Road");
//This is too Close. :( Cannot Set this node.
return -1;
}
/*for(int i = 0;i<boxExclusions.Count;i++){
CiDyEdge testEdge = boxExclusions[i];
CiDyNode testNA = testEdge.v1;
CiDyNode testNB = testEdge.v2;
//Debug.Log("Test3 Against: "+testEdge.name);
targetNode.distToP = DistanceToLine(targetNode.position, testNA.position,testNB.position, ref targetNode.distToP, ref targetNode.r, ref targetNode.s, ref segmentIntersection);
//Debug.Log("ProposedNode: "+targetNode.name+" Values r: "+targetNode.r+" s: "+targetNode.s+" DistToP: "+targetNode.distToP);
//Now that we know the Values make sure targetNode's Dist to edge is < snapDist
if(Mathf.Abs(targetNode.s) > 0.1f && targetNode.distToP <= (roadWidth+snapDist/2)){
//Debug.Log("TargetNode DistToLine <= RoadWidth :( Cannot Place Road");
//This is too Close. :( Cannot Set this node.
return -1;
}
}*/
			/*}
			//Return dynamic integer's final state.
			return snapEvent;*/
		}

		//This will say if the point is closer than desired distance from array of lines.
		bool SegmentProximity(ref CiDyNode targetNode, List<CiDyEdge> boxExclusions, ref Vector3 segmentIntersection, float snapDist)
		{
			for (int i = 0; i < boxExclusions.Count; i++)
			{
				CiDyEdge testEdge = boxExclusions[i];
				CiDyNode testNA = testEdge.v1;
				CiDyNode testNB = testEdge.v2;
				//Debug.Log("Test3 Against: "+testEdge.name);
				targetNode.distToP = CiDyUtils.DistanceToLine(targetNode.position, testNA.position, testNB.position, ref targetNode.r, ref targetNode.s, ref segmentIntersection);
				if (targetNode.r > 0)
				{
					//Debug.Log("ProposedNode: "+targetNode.name+" Values r: "+targetNode.r+" s: "+targetNode.s+" DistToP: "+targetNode.distToP+" Must Be Greater than "+minDist);
					if (Mathf.Abs(targetNode.s) > 0.1f)
					{
						//Now that we know the Values make sure targetNode's Dist to edge is < snapDist
						if (targetNode.distToP < nodeSpacing)
						{
							//Debug.Log("TargetNode DistToLine <= RoadWidth :( Cannot Place Road");
							//This is too Close. :( Cannot Set this node.
							return false;
						}
					}
				}
			}
			return true;
		}

		//Need List to Sort and Focus point
		List<Vector3> SortTargetsByDistance(List<Vector3> targets, Vector3 pos)
		{
			// bubble-sort transforms
			for (int e = 0; e < targets.Count - 1; e++)
			{
				float sqrMag1 = (targets[e] - pos).sqrMagnitude;
				float sqrMag2 = (targets[e] - pos).sqrMagnitude;

				if (sqrMag2 < sqrMag1)
				{
					Vector3 tempStore = targets[e];
					targets[e] = targets[e + 1];
					targets[e + 1] = tempStore;
					e = 0;
				}
			}
			//targets.Reverse ();
			return targets;
		}

		//Create a Node
		CiDyNode NewNode(string newName, Vector3 position)
		{
			//Debug.Log ("Graph is Adding SubNode "+newName+" Count: "+subCount);
			GameObject nodeObject = Instantiate(nodePrefab, position, Quaternion.identity) as GameObject;
			nodeObject.transform.parent = nodeHolder.transform;
			nodeObject.name = newName;
			//CiDyNode newNode = new CiDyNode(newName, position, this, subCount, nodeObject);
			CiDyNode newNode = ScriptableObject.CreateInstance<CiDyNode>().Init(newName, position, this, subCount, nodeObject);
			subNodes.Add(newNode);
			subCount++;

			return newNode;
		}

		void RemoveSubNode(CiDyNode oldNode)
		{
			//Debug.Log ("Removing SubNode " + oldNode.name);
			DestroyImmediate(oldNode.nodeObject);
			subNodes.Remove(oldNode);
			subCount--;
		}

		//This will simply make sure we do not add duplicate edges to the sub roads.
		void AddSubRoad(CiDyEdge newEdge)
		{
			CiDyEdge duplicateEdge = (secondaryEdges.Find(x => x.name == newEdge.name));
			if (duplicateEdge == null)
			{
				//This edge doesnt exist yet. :)
				secondaryEdges.Add(newEdge);
			}
		}

		//This function will fill the Growth Queue with the First two Starting Points and there Directions.
		public void FindSecondaryRdStartPoints()
		{
			//Debug.Log ("FindSecondaryRdStartPoints();");
			queue = new ArrayList();
			//secondaryEdges = new List<CiDyEdge> ();
			//longestRoads = new List<CiDyRoad> ();
			//Now determine the midpoints for the proper growth starting points.
			//We need to iterate through roads and find the two longest roads.
			/*CiDyRoad longestRoad = null;
			float bestDist = -1;//Start at 0.

			//Iterate through boundary Roads in there Cycle Counter Clockwise Order
			for(int i = 0;i<boundaryRoads.Count;i++){
				//Find road Length.
				CiDyRoad testRoad = boundaryRoads[i];
				if(testRoad == null){
					boundaryRoads.RemoveAt(i);
					i=0;
					//This road was destroyed but not removed from the list.
					continue;
				}
				//Find its total Length
				float totalLength = CiDyUtils.FindTotalDistOfPoints(testRoad.origPoints);
				//Update this roads totalLength as it may be needed later.
				testRoad.totalLength = totalLength;
				//Debug.Log("Total Length for "+testRoad.name);
				//Debug.Log(" Lngth "+totalLength);
				//Debug.Log("bestDist "+bestDist);
				//Compare to last bestDist.
				if(totalLength > bestDist){
					//Debug.Log("totalLenght > BestDist. New Best Dist = "+totalLength+" Road: "+testRoad.name);
					bestDist = totalLength;
					longestRoad = testRoad;
					//longestRoads.Add(testRoad);
				}
			}
			if(longestRoad!=null){
				//Debug.Log("Added Road "+longestRoad.name+" Current LongestRoads Count= "+(longestRoads.Count+1));
				longestRoads.Add(longestRoad);
			}*/
			CiDyRoad longestRoad = longestRoads[0];//Pre determined when Calculated Mesh.
												   //longestRoads.Add(longestRoad);
												   //Now we have the longest road Find its parrallel rd or best matched one using orientation from its EndB-EndA direction compared to ours.
												   //Grab direction of longest Rd
			if (longestRoad == null)
			{
				Debug.LogError("Bull Shit!!!");
				return;
			}
			//Debug.Log (longestRoad.endB);
			//Debug.Log (longestRoad.endA);
			/*GameObject sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
			sphere.transform.position = longestRoad.endA;
			sphere.name = "EndA";

			sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.transform.position = longestRoad.endB;
			sphere.name = "EndB";*/
			Vector3 longDir = (longestRoad.nodeB.position - longestRoad.nodeA.position).normalized;
			float bestDiff = Mathf.Infinity;
			CiDyRoad parallelRd = null;
			//Grab all other boundaryRd directions for comparison test.
			for (int i = 0; i < boundaryRoads.Count; i++)
			{
				CiDyRoad testRoad = boundaryRoads[i];
				if (testRoad == null)
				{
					continue;
				}
				//Get direction of test road if its not equal to longest road.
				if (testRoad.name != longestRoad.name)
				{
					//Grab direction of testRd.
					Vector3 testDir = (testRoad.nodeB.position - testRoad.nodeA.position).normalized;
					//Now compare angle to determine parrallel.
					float angle = Vector3.Angle(testDir, longDir);
					float angleDiff = Mathf.Abs(angle - 180.0f);//Find the closest angle to 90.
																//Debug.Log(testRoad.name+" longDir "+longDir+" testDir "+testDir+"angle "+angle+" angleDiff "+angleDiff+" <? BestDiff "+bestDiff);
					if (angleDiff < bestDiff)
					{
						bestDiff = angleDiff;
						parallelRd = testRoad;
						if (bestDiff < 10)
						{
							//We found it. Stop looking
							break;
						}
					}
				}
			}
			//Add to List.
			//Debug.Log("Parralell "+parallelRd.name);
			longestRoads[1] = parallelRd;

			//Now that we have the full list of boundaryEdge lines turn them into CiDyEdges and connect the ends with a straight peice.
			//Turn BoundaryEdges into CidyEdgse and Update/add to SecondaryEdges list for proper boundary collision Testing.
			/*for(int i = 0;i<edges.Count;i++){
				List<Vector3> boundaryList = edges[i];
				//Turn BoundaryEdges into CidyEdgse and Update/add to SecondaryEdges list for proper boundary collision Testing.
				for(int j = 0;j<boundaryList.Count-1;j++){
					//Create Nodes@positions in boundaryList
					CiDyNode v0 = new CiDyNode("V"+j,boundaryList[j],j);
					CiDyNode v1 = new CiDyNode("V"+j+1,boundaryList[j+1],j+1);
					//Create cidyEdge and Add to SEcondaryEdges list
					secondaryEdges.Add(new CiDyEdge(v0,v1));
				}
				List<Vector3> nextList;
				//we need to make a connection piece from the current to the next and from the last to the first.
				if(i<edges.Count-1){
					nextList = edges[i+1];
				} else {
					nextList = edges[0];
				}
				CiDyNode v2 = new CiDyNode("BoundaryPoint",boundaryList[boundaryList.Count-1],0);
				CiDyNode v3 = new CiDyNode("BoundaryPoint",nextList[0],0);
				//Connect list with a CiDyEdge
				secondaryEdges.Add(new CiDyEdge(v2,v3));
			}*/
			/*//Iterate through points and connect them into secondaryEdges
			for(int i = 0;i<interiorPoints.Count;i++){
				CiDyNode v0 = new CiDyNode("V"+testCount,interiorPoints[i],i);
				testCount++;
				CiDyNode v1 = null;
				//We need to connect them cyclic
				if(i==interiorPoints.Count-1){
					//we are at the last number and need to connect to the first(0)
					v1 = new CiDyNode("V"+testCount,interiorPoints[0],i);
				} else {
					//We need to connect to the next in  line
					v1 = new CiDyNode("V"+testCount,interiorPoints[i+1],i);
				}
				testCount++;
				//Now add to secondary
				secondaryEdges.Add(new CiDyEdge(v0,v1));
			}*/
			//Store growth points in respective Road Data
			//Debug.Log("Longest Roads "+longestRoads.Count);
			//Now that we have our rds lets iterate through list and find there mid points and growth Directions.
			for (int i = 0; i < longestRoads.Length; i++)
			{
				//Set into Queue
				//GameObject.Find (nodeObject.name+"/Sphere").GetComponent<Renderer>().enabled = false;
				CiDyNode startNode = boundaryNodes.Find(x => x.position == longestRoads[i].growthPoint);//This Is should be a Boundary Node so the Road Mesh will properly end at it.
																										//NodesList.FindAll( ni => ni.nodeID > 5);
																										//GameObject.Find (nodeObject.name+"/Sphere").GetComponent<Renderer>().enabled = false;
				CiDyNode newNode = NewNode("S" + subCount, Vector3.zero);
				//newNode.subNode =true;
				//graph.masterGraph.Add(newNode);
				Vector3 newDir = longestRoads[i].growthDir.normalized;
				//This will always Be Placed as it is the first
				if (PlaceSegment(startNode, newDir, newNode))
				{
					//Debug.Log("Placed "+newNode.name);
					//Add new Point
					queue.Add(newNode);
					//Add new Direction
					queue.Add(newDir);
				}
			}
			//Now that we have placed the points.
		}

		//Create Bezier Curve out of Referenced Vector3 List
		List<Vector3> CreateBezier(List<Vector3> origPoints)
		{
			float t = 0.0f;
			int iterations = origPoints.Count;
			List<Vector3> newPoints = new List<Vector3>();
			List<Vector3> finalP = new List<Vector3>();
			Vector3 p = new Vector3(0, 0, 0);
			//Determine total distance between points
			float totalDist = FindTotalDistOfPoints(origPoints);
			//Have a Segment for every 4 meters in total distance
			float bSegments = Mathf.Round(totalDist / roadSegmentLength);
			//Iterate through and create the curve with as many bSegments as determined
			for (int j = 0; j <= bSegments; j++)
			{
				//Update T for this Iteration
				t = j / bSegments;
				//Test the GameData untouched raw
				for (int i = 0; i < iterations - 1; i++)
				{
					//Grab the New Points from the Total Points.
					p = (1 - t) * origPoints[i] + t * origPoints[i + 1];
					//Store New Points.
					newPoints.Add(p);
				}
				//Are there two points?
				if (newPoints.Count > 1)
				{
					//Make iterations until we have our true Position on the path.
					for (int h = 0; h < iterations; h++)
					{
						//Call a Function to find p.
						newPoints = FindP(newPoints, t);
						if (newPoints.Count == 1)
						{
							//Update P 
							p = newPoints[0];
							//End iterations
							break;
						}
					}
				}
				//Update BezierPath
				finalP.Add(p);
			}
			//Add last Point of Orig Points to Curve
			//finalP.Add (origPoints [iterations]);
			return finalP;
		}

		//Returns List<Vector3> from Segments from ordered List of Vector3s using linear Interpolation
		List<Vector3> FindP(List<Vector3> points, float t)
		{
			//Copy Points
			List<Vector3> secPoints = new List<Vector3>();
			//Clear for new Points
			//points = new List<Vector3>();
			//Iterate through clone array of old tmp control points.
			for (int i = 0; i < points.Count - 1; i++)
			{
				//Add new Points to List.
				Vector3 p = (1 - t) * points[i] + t * points[i + 1];
				secPoints.Add(p);
			}
			return secPoints;
		}

		public float FindTotalDistOfPoints(List<Vector3> points)
		{
			float totalDist = 0;
			//Iterate through array looking at two at a time totaling the distance.
			for (int i = 0; i < points.Count - 1; i++)
			{
				Vector3 a = points[i];
				Vector3 b = points[i + 1];
				float dist = Vector3.Distance(a, b);
				totalDist += dist;
			}
			return totalDist;
		}

		//USED For REFERENCE WHILE COMPARTMENTALIZING FILAMENT DETECTION INTO CIDY CELL CONTROL INSTEAD OF GRAPH.
		/*void AddFilamentToCycle(List<CiDyNode> cycle, List<CiDyNode> filament){
			//Debug.Log ("Add Filament To Cycle Fil.Cnt: " + filament.Count + " Cycle Cnt: " + cycle.Count);
			//Flip for the Root node at 0.
			filament.Reverse ();
			CiDyNode rootNode = FindMasterNode (filament [0]);
			CiDyNode curNode = null;
			//Find root node in cycle and begin entering the filament cycle nodes in the proper sequence. ;)
			for(int n = 0;n<cycle.Count;n++){
				//Find root node.
				if(cycle[n].name == rootNode.name){
					int cycleLoc = n+1;//This means if we call insert at cycleLoc it will push the current One and add a new one in this place.
					//Check for single connection scenario
					if(filament.Count == 2){
						//does the fwd node have more than 1 connection?
						if(filament[1].adjacentNodes.Count<=1){
							//This is a single Connection scenario.
							cycle.Insert(cycleLoc,filament[1]);
							cycleLoc++;
							cycle.Insert(cycleLoc,rootNode);
							cycleLoc++;
							//Debug.Log("Single Connection Inserted");
							//Debug.Log("Inserted "+filament[1].name);
							//Debug.Log("Inserted "+rootNode.name);
							break;
						}
					}
					//Yay we found the root node. run the Left finding algorithm starting at the root node and ending at the root Node. :).
					curNode = FindMasterNode(filament[1]);
					cycle.Insert(cycleLoc,curNode);
					cycleLoc++;
					//Debug.Log("Inserted "+curNode.name);
					//ShowCycle(cycle);
					CiDyNode nxtNode = GetLeftMost(rootNode,curNode);
					bool process = true;
					//Debug.Log("Start Process CurNode: "+curNode.name);
					while(process){
						if(nxtNode != null){
							//Debug.Log("Have Nxt Node :) "+nxtNode.name);
							if(nxtNode.name != rootNode.name){
								//Debug.Log("Nxt Node != rootNode");
								//Scenario where No outlet
								if(nxtNode.adjacentNodes.Count<=1){
									//Debug.Log("Nxt Node Dead End");
									//Dead End/Flip test
									cycle.Insert(cycleLoc,nxtNode);
									cycleLoc++;
									cycle.Insert(cycleLoc,curNode);
									cycleLoc++;
									//Debug.Log("Inserted "+nxtNode.name);
									//Debug.Log("Inserted "+curNode.name);
									nxtNode = GetLeftMost(nxtNode,curNode);
								} else {
									//Debug.Log("Nxt Node has Potentials");
									//Sceario with a potential nxtNode
									CiDyNode tmpNode = curNode;
									curNode = nxtNode;
									cycle.Insert(cycleLoc,curNode);
									cycleLoc++;
									//Debug.Log("Inserted "+curNode.name);
									//Run test using nxtNode & curNode
									nxtNode = GetLeftMost(tmpNode,curNode);
								}
							} else {
								//We are at the end of the iteration.:)
								process = false;
								cycle.Insert(cycleLoc,rootNode);
								cycleLoc++;
								//Debug.Log("End Process Inserted "+rootNode.name);
								return;
							}
						} else {
							//Debug.Log("Do Not Have NxtNode? :(");
							//Nothing returned
							if(curNode.adjacentNodes.Count<=1){
								//Dead End. :) Add curNode and re-test
								cycle.Insert(cycleLoc,curNode);
								cycleLoc++;
								nxtNode = GetLeftMost(nxtNode,curNode);
								//Debug.Log("Inserted "+curNode.name);
							} else {
								Debug.LogError("Cur Node Adj Count > 1 But we still have no nxtNode???");
								process = false;
								break;
							}
						}
					}
				}
			}
		}*/

		List<CiDyNode> CheckForFilaments()
		{
			//Debug.Log ("Check for Filaments");
			List<CiDyNode> finalSequence = new List<CiDyNode>(0);
			//To check for filaments we will edge are way around the interior of this cell starting at the cycleNode[0] and ending there. :)
			CiDyNode rootNode = cycleNodes[0];
			CiDyNode curNode = cycleNodes[1];
			//Debug.Log("Root Node: "+rootNode.name+" CurNode: "+curNode.name);
			finalSequence.Add(rootNode);
			finalSequence.Add(curNode);
			//ShowCycle(cycle);
			CiDyNode nxtNode = GetLeftMost(rootNode, curNode);
			bool process = true;
			bool rootFil = false;//Does the Root Node have a Filament?
								 //Debug.Log("Start Process CurNode: "+curNode.name);
			while (process)
			{
				if (nxtNode != null)
				{
					//Debug.Log("Nxt Node: "+nxtNode.name);
					if (nxtNode.name != rootNode.name)
					{
						//Debug.Log("Nxt Node != rootNode");
						//Scenario where No outlet
						if (nxtNode.adjacentNodes.Count <= 1)
						{
							//Dead End/Flip test
							//finalSequence.Add(curNode);
							finalSequence.Add(nxtNode);
							finalSequence.Add(curNode);
							//Debug.Log("Inserted "+nxtNode.name);
							//Debug.Log("Inserted "+curNode.name);
							//Debug.Log("Nxt Node Dead End rootNd Added: "+nxtNode.name+" fwdNd Added: "+curNode.name);
							nxtNode = GetLeftMost(nxtNode, curNode);
						}
						else
						{
							//Sceario with a potential nxtNode
							CiDyNode tmpNode = curNode;
							curNode = nxtNode;
							finalSequence.Add(curNode);
							//Debug.Log("Nxt Node has Potentials rootNode "+tmpNode.name+" fwdNode Added: "+curNode.name);
							//cycle.Insert(cycleLoc,curNode);
							//cycleLoc++;
							//Debug.Log("Inserted "+curNode.name);
							//Run test using nxtNode & curNode
							nxtNode = GetLeftMost(tmpNode, curNode);
						}
					}
					else
					{
						//This MIGHT BE THE END Make sure that the root node doesnt have more than 2 adj nodes.
						if (rootNode.adjacentNodes.Count > 2 && !rootFil)
						{
							//Debug.Log("Back To Root Node Added but this Node has More than 2 ADJ Nodes. Test it for Left");
							rootFil = true;
							//This is not the end. This root Node might have a Filament Coming from it.
							nxtNode = GetLeftMost(curNode, rootNode);
							if (nxtNode == null)
							{
								process = false;
								//cycle.Insert(cycleLoc,rootNode);
								//finalSequence.Add(rootNode);
								//cycleLoc++;
								//Debug.Log("End Process At "+rootNode.name+" finalSequcne Cnt: "+finalSequence.Count);
								return finalSequence;
							}
							else
							{
								//We are not done ad the root node again.
								finalSequence.Add(rootNode);
								curNode = rootNode;
							}
						}
						else
						{
							//We are at the end of the iteration.:)
							process = false;
							//cycle.Insert(cycleLoc,rootNode);
							//finalSequence.Add(rootNode);
							//cycleLoc++;
							//Debug.Log("End Process At "+rootNode.name+" finalSequcne Cnt: "+finalSequence.Count);
							return finalSequence;
						}
					}
				}
				else
				{
					Debug.Log("Do Not Have NxtNode? :(");
					//Nothing returned
					if (curNode.adjacentNodes.Count <= 1)
					{
						//Dead End. :) Add curNode and re-test
						//cycle.Insert(cycleLoc,curNode);
						//cycleLoc++;
						finalSequence.Add(curNode);
						nxtNode = GetLeftMost(nxtNode, curNode);
						//Debug.Log("Inserted "+curNode.name);
						Debug.Log("Dead End");
					}
					else
					{
						Debug.LogError("Cur Node Adj Count > 1 But we still have no nxtNode???");
						process = false;
						break;
					}
				}
			}
			Debug.LogWarning("Check for Filaments didnt find proper end on Cell: " + name);
			return finalSequence;
		}
		//For Filament Detection
		CiDyNode GetLeftMost(CiDyNode srcNode, CiDyNode fwdNode)
		{
			//Debug.Log ("SRCNode "+srcNode.name+" ADJ "+srcNode.adjacentNodes.Count+" FWDNode "+fwdNode.name+" ADJ "+fwdNode.adjacentNodes.Count);
			if (fwdNode.adjacentNodes.Count == 2)
			{
				if (fwdNode.adjacentNodes[0].name == srcNode.name)
				{
					return fwdNode.adjacentNodes[1];
				}
				else
				{
					return fwdNode.adjacentNodes[0];
				}
			}
			//CiDyNode startNode = new CiDyNode ("", Vector3.zero, 0);
			//CiDyNode startNode = ScriptableObject.CreateInstance<CiDyNode>().Init("",Vector3.zero,0);
			CiDyNode finalNode = null;
			//List<Vector3> points = new List<Vector3> (0);
			//Debug.Log ("running "+srcNode.name+" "+nxtNode.name);
			float currentDirection = Mathf.Infinity;
			int bestNode = -1;

			// the vector that we want to measure an angle from
			Vector3 referenceForward = (fwdNode.position - srcNode.position);// some vector that is not Vector3.Debug.Log (referenceForward);
																			 //referenceForward = nxtNode.position+referenceForward;
																			 //points.Add (referenceForward);
																			 // the vector perpendicular to referenceForward (90 degrees clockwise)
																			 // (used to determine if angle is positive or negative)
			Vector3 referenceRight = Vector3.Cross(Vector3.up, referenceForward);
			//referenceRight = srcNode.position+referenceRight*100;
			//points.Add (referenceRight);
			// the vector of interest
			List<CiDyNode> sortedNodes = new List<CiDyNode>();
			//Debug.Log ("NxtNode ADJ Cnt "+nxtNode.adjacentNodes.Count);
			if (fwdNode.adjacentNodes.Count != 1)
			{
				//Itearate through adjacent Nodes
				for (int i = 0; i < fwdNode.adjacentNodes.Count; i++)
				{
					CiDyNode tmpNode = fwdNode.adjacentNodes[i];
					//Debug.Log(nxtNode.name+ " Adj Node "+tmpNode.name+" Current Place "+i);
					//If the curNode we are checking is not equal to the node we came from (SRC)
					if (tmpNode.name != srcNode.name)
					{
						//Debug.Log(tmpNode.name);
						//Grab new Direction
						Vector3 newDirection = (tmpNode.position - fwdNode.position);// some vector that we're interested in
																					 //newDirection = srcNode.position+newDirection*100;
																					 //points.Add(newDirection);
																					 //Debug.Log("Added "+newDirection);
																					 // Get the angle in degrees between 0 and 180
						float angle = Vector3.Angle(newDirection, referenceForward);
						//Debug.Log(angle);
						// Determine if the degree value should be negative.  Here, a positive value
						// from the dot product means that our vector is on the right of the reference vector   
						// whereas a negative value means we're on the left.
						float sign = Mathf.Sign(Vector3.Dot(newDirection, referenceRight));
						//float finalAngle = angle*sign;
						float finalAngle = angle;
						//print ("Final Angle for "+tmpNode.name+" = "+finalAngle);
						//Catch scenario when adjacent node is directly behind us returning as a positive and make it a negative.
						/*if(finalAngle == 180){
							finalAngle = -180;
						}*/
						if (sign < 0)
						{
							finalAngle = (360 - finalAngle);
						}
						tmpNode.Angle = finalAngle;
						sortedNodes.Add(tmpNode);
						//Debug.Log("CounterClockWise "+tmpNode.name+" Angle = "+finalAngle);
						//finalAngle = Mathf.Round(finalAngle * 100f) / 100f;
						//finalAngle = (finalAngle<= 0) ? 360 + finalAngle : finalAngle;
						//Debug.Log(tmpNode.name+" "+finalAngle);
						//ClockWise Most (Highest/Positive)
						/*if(finalAngle < 180){
							//This cannot be on our left then.
							continue;
						}*/
						/*if(currentDirection > finalAngle){
							//The New angle is higher update CurrentDirection
							bestNode = i;
							currentDirection = finalAngle;
							Debug.Log("CurrentDir > finalAngle "+finalAngle);
							//Debug.Log("BestNode "+nxtNode.adjacentNodes[bestNode].name+" FinalAngle "+currentDirection);
						}*/
					}
				}
				//Now that we have a sorted list from lowest to highest based on Angle Determine the nxtNode.
				sortedNodes = sortedNodes.OrderBy(x => x.Angle).ToList();
				//iterate through them.
				sortedNodes.Reverse();
				//Debug.Log("Sorted Nodes[0] :"+sortedNodes[0].name+" sortedNodes[Last]: "+sortedNodes[sortedNodes.Count-1].name);
				bool isLeft = false;
				for (int i = 0; i < sortedNodes.Count; i++)
				{
					if (isLeft)
					{
						if (sortedNodes[i].Angle < 180f)
						{
							//Debug.Log("Angle < 180 No better choices left "+sortedNodes[i].Angle);
							break;
						}
					}
					if (sortedNodes[i].Angle < currentDirection)
					{
						//Debug.Log("Angle "+sortedNodes[i].Angle+" < currentDir "+currentDirection);
						currentDirection = sortedNodes[i].Angle;
						bestNode = i;
						if (currentDirection > 180f && !isLeft)
						{
							isLeft = true;
							//Debug.Log("Current Direction is Set above 180. This means nothing lower than 180 will be accepted");
						}
					}
				}
				//Did we find a new node?
				if (bestNode != -1)
				{
					//We have selected a Node
					finalNode = sortedNodes[bestNode];
					//points.Add(finalNode.position);
					//Debug.Log("CounterClockwise FinalNode "+finalNode.name+" Found For "+srcNode.name);
					//Debug.Log("Left Most = "+finalNode.name);
					return finalNode;
				}
			}
			Debug.Log("No Counter ClockWise Found for " + fwdNode.name);
			//Didn't Find a new Node return null so we know this may be a filament.
			return null;
		}

		//In our Cell Growth we only need 2D vector X,Z as all roads inside cells are within small y variance.
		//P is the point and EndA-EndB represent the Ends of the Line Segment.
		//Used for Node Proximity(Infinite Line)
		//CAll = DistanceToLine(p.position,endA.position,endB.position, ref distToP, ref r, ref s);
		/*public void DistanceToLine(Vector3 p, Vector3 endA, Vector3 endB, ref float dist, ref float r, ref float s, ref Vector3 segPoint){
			float a = p.x - endA.x;
			float b = p.z - endA.z;
			float c = endB.x - endA.x;
			float d = endB.z - endA.z;

			float dot = a * c + b * d;
			float len_sq = c * c + d * d;
			//Set R which represents between 0-1 float value (0 = at EndA),(1 = at EndB);
			r = dot/len_sq;
			r = Mathf.Round(r * 100f) / 100f;


			float xx;
			float zz;

			//try without Mathf.Approximatly as this is a more intense math calculation. the R value will be aprox as accurate and is already calculated
			//if(r < 0 || (Mathf.Approximately(endA.x,endB.x) && Mathf.Approximately(endA.z,endB.z))){
			if(r < 0 || (endA.x == endB.x && endA.z == endB.z)){
				//Debug.Log("r<0");
				xx = endA.x;
				zz = endA.z;
			} else if(r > 1){
				//Debug.Log("r>1");
				xx = endB.x;
				zz = endB.z;
			} else {
				//Debug.Log("else");
				xx = endA.x+r*c;
				zz = endA.z+r*d;
			}

			//float dx = p.x - xx;
			//float dz = p.y - zz;

			//Update Dist from point to Line Segment
			//dist = Mathf.Sqrt (dx * dx + dz * dz);
			dist = Vector2.Distance(new Vector2(p.x,p.z), new Vector2(xx,zz));
			//Update S which represents Left or right to Perpindicular Line a-b (>0 = right of line),(<0 = left of Line), (0 = colinear)
			s = ((endA.x-endB.x) *(p.z-endA.z)-(endA.z-endB.z)*(p.x-endA.x));
			s = Mathf.Round (s * 100f) / 100f;
			if(s<0){
				float t = Mathf.Abs(s);
				t = Mathf.Sqrt(t);
				s = -t;
			} else {
				s = Mathf.Sqrt(s);
			}
			segPoint = new Vector3 (xx, p.y, zz);
		}*/

		bool DuplicateEdge(CiDyEdge testEdge)
		{
			for (int i = 0; i < secondaryEdges.Count; i++)
			{
				if (testEdge.name == secondaryEdges[i].name)
				{
					//Debug.Log("Duplicate Edge");
					return true;
				}
			}
			return false;
		}

		bool DuplicateCloseNode(CiDyNode testNode, List<CiDyNode> testList)
		{
			for (int i = 0; i < testList.Count; i++)
			{
				if (testList[i].name == testNode.name)
				{
					return true;
				}
			}
			return false;
		}

		List<Bounds> visualBounds = new List<Bounds>();
		//Vector3 visualNode = Vector3.zero;
		//This Function will test a proposed segment A-B against a List of Segments and Determine All potential Intersecting Segments. :)
		List<CiDyEdge> BoundingBoxTest(ref CiDyEdge proposedEdge, ref List<CiDyEdge> testEdges, ref List<CiDyEdge> boxExclusions)
		{
			//List<CiDyEdge> finalList = new List<CiDyEdge> ();
			//Debug.Log ("BoundingBoxTest TestEdges Cnt: "+testEdges.Count);
			//Now create the bounding box for the proposed Segment. Incorporate SnapSize with Dimensions Width/Length(x,z) y= static 1.0f;
			/*Vector3 centerPoint = proposedEdge.v1.position;//sourceNode.position;
			centerPoint += ((proposedEdge.v2.position - proposedEdge.v1.position)*0.5f);*/
			//Set Initial Bounds Parameters
			//Set Initial Bounds Parameters
			Vector3 dir = ((proposedEdge.v2.position - proposedEdge.v1.position) * 0.5f).normalized;
			//Now lets move the sourcePoint(A)v1 away from dir by snapSize.
			Vector3 boundsMin = proposedEdge.v1.position + ((-dir) * nodeSpacing);
			//Now lets move the EndPoint(B)v2 further by snapSize.
			Vector3 boundsMax = proposedEdge.v2.position + (dir * nodeSpacing);
			//Calculate Center
			float length = Vector3.Distance(boundsMin, boundsMax);
			Vector3 center = boundsMin + (dir * (length / 2));
			//Create InitalBounds
			Bounds curBounds = new Bounds(center, Vector3.one);
			curBounds.SetMinMax(boundsMin, boundsMax);
			//Vector3 center = curBounds.center;
			float x = curBounds.size.x;
			float y = curBounds.size.y;
			float z = curBounds.size.z;

			//Inflate the box in the directions that are not (expansion+snapSize)
			if (Mathf.Abs(x) < (roadWidth + snapSize))
			{
				//Update the X Value so it is min (expansion+snapSize);
				x = (roadWidth + snapSize);
			}
			if (Mathf.Abs(y) < (roadWidth + snapSize))
			{
				y = (roadWidth + snapSize);
			}
			if (Mathf.Abs(z) < (roadWidth + snapSize))
			{
				z = (roadWidth + snapSize);
			}
			curBounds.size = new Vector3(x, y, z);
			//curBounds.center = center;
			//Debug.Log("Bounds x:"+curBounds.size.x+" y: "+curBounds.size.y+" z: "+curBounds.size.z);
			//Add to Visual
			visualBounds.Add(curBounds);
			//Determine bottom left and right points.
			//Bounds curBounds = new Bounds(center, new Vector3(roadWidth+snapSize, 1, paramLength+snapSize));
			//curBounds.Encapsulate(proposedEdge.v1.position);
			//curBounds.Encapsulate(proposedEdge.v2.position);

			//Test Bounding Boxes against proposed segment.
			for (int i = 0; i < testEdges.Count; i++)
			{
				dir = ((testEdges[i].v2.position - testEdges[i].v1.position) * 0.5f).normalized;
				/*//Calculate Center
				length = Vector3.Distance (testEdges[i].v2.position,testEdges[i].v1.position);
				center = testEdges[i].v1.position+(dir*(length/2));*/
				//Now lets move the sourcePoint(A)v1 away from dir by snapSize.
				boundsMin = testEdges[i].v1.position + ((-dir) * roadWidth);
				//Now lets move the EndPoint(B)v2 further by snapSize.
				boundsMax = testEdges[i].v2.position + (dir * roadWidth);
				//Calculate Center
				length = Vector3.Distance(boundsMin, boundsMax);
				center = boundsMin + (dir * (length / 2));
				//Create InitalBounds
				Bounds boundingBox = new Bounds(center, Vector3.one);
				//Now Size It Properly
				//boundingBox.SetMinMax(testEdges[i].v2.position, testEdges[i].v1.position);
				boundingBox.SetMinMax(boundsMin, boundsMax);
				x = boundingBox.size.x;
				y = boundingBox.size.y;
				z = boundingBox.size.z;
				//Inflate the box in the directions that are not (expansion+snapSize)
				if (Mathf.Abs(x) < roadWidth)
				{
					//We need to adjust this value to the proper amount.
					/*if(x<0){
						//Negative
						isNeg = true;
					}*/
					//Update the X Value so it is min (expansion+snapSize);
					x = (roadWidth);
				}
				if (Mathf.Abs(y) < roadWidth)
				{
					/*if(y<0){
						//Negative
						isNeg = true;
					}*/
					y = (roadWidth);
				}
				if (Mathf.Abs(z) < roadWidth)
				{
					/*if(z<0){
						//Negative
						isNeg = true;
					}*/
					z = (roadWidth);
				}
				//Settting Final Desired Size.
				boundingBox.size = new Vector3(x, y, z);
				//Add Box to list
				visualBounds.Add(boundingBox);
				bool collision = BoundsCollide(curBounds, boundingBox);
				//Test for box intersection
				if (collision)
				{
					//Store road for potential Snap Events
					boxExclusions.Add(testEdges[i]);
				}/* else {
				Debug.Log("No Interaction "+testEdges[i].name+" & "+proposedEdge.name);
			}*/
			}
			//Debug.Log ("Returned Total Box Collisions "+intersectingRds.Count);
			return boxExclusions;
		}

		bool BoundsCollide(Bounds a, Bounds b)
		{
			//Test if they collide
			//Calculate X Ranges for A/B
			if (Mathf.Max(a.min.x, a.max.x) >= Mathf.Min(b.min.x, b.max.x) &&
			   Mathf.Min(a.min.x, a.max.x) <= Mathf.Max(b.min.x, b.max.x) &&
			   Mathf.Max(a.min.z, a.max.z) >= Mathf.Min(b.min.z, b.max.z) &&
			   Mathf.Min(a.min.z, a.max.z) <= Mathf.Max(b.min.z, b.max.z) &&
			   Mathf.Max(a.min.y, a.max.y) >= Mathf.Min(b.min.y, b.max.y) &&
			   Mathf.Min(a.min.y, a.max.y) <= Mathf.Max(b.min.y, b.max.y))
			{
				return true;
			}
			//Debug.Log ("False");
			return false;
		}

		//returns -1 when to the left, 1 to the right, and 0 for forward/backward
		public float AngleDir(Vector3 fwd, Vector3 targetDir)
		{
			Vector3 perp = Vector3.Cross(fwd, targetDir);
			float dir = Vector3.Dot(perp, Vector3.up);

			if (dir > 0.0f)
			{
				//Debug.Log("Right of");
				return 1.0f;
			}
			else if (dir < 0.0f)
			{
				//Debug.Log("Left Of");
				return -1.0f;
			}
			else
			{
				//Debug.Log("Colinear");
				return 0.0f;
			}
		}

		public float cellTransparency = 0.25f;

		//Returns a random color
		public Color RandomColor()
		{
			Color newColor = Color.blue;
			//Effected by user Set Seed. :)
			int randomNmb = Random.Range(0, 8);
			if (randomNmb == 0)
			{
				newColor = Color.black;
			}
			else if (randomNmb == 1)
			{
				newColor = Color.yellow;
			}
			else if (randomNmb == 2)
			{
				newColor = Color.cyan;
			}
			else if (randomNmb == 3)
			{
				newColor = Color.gray;
			}
			else if (randomNmb == 4)
			{
				newColor = Color.green;
			}
			else if (randomNmb == 5)
			{
				newColor = Color.magenta;
			}
			else if (randomNmb == 6)
			{
				newColor = Color.red;
			}
			else if (randomNmb == 7)
			{
				newColor = Color.white;
			}
			// Set Transparency.
			//newColor.a = cellTransparency;
			//Return our final color
			return newColor;
		}

		//List<Color> colors = new List<Color>(0);

		/*private void OnDrawGizmos()
		{

			//Draw sidewalk Polygons
			//Origianl
			if (tmpPoints.Count > 0) {
				for (int i = 0; i < tmpPoints.Count; i++)
				{
					Vector3 thisPnt = tmpPoints[i];
					Vector3 secondPoint;
					if (i == tmpPoints.Count - 1)
					{
						secondPoint = tmpPoints[0];
					}
					else
					{
						secondPoint = tmpPoints[i+1];
					}
					Gizmos.color = Color.green;
					//Draw Line
					Gizmos.DrawLine(thisPnt, secondPoint);
					if (i == 0)
					{
						Gizmos.color = Color.blue;
					}
					else if (i == tmpPoints.Count - 1)
					{
						Gizmos.color = Color.red;
					}
					else {
						Gizmos.color = Color.green;
					}
					//Draw this Sphere
					Gizmos.DrawSphere(thisPnt, 0.6f);
				}
			}
			//Inset Polygon
			if (insetTmpPoints.Count > 0) {
				for (int i = 0; i < 1; i++) {
					for (int j = 0; j < insetTmpPoints[i].Count; j++) {
						Vector3 thisPnt = insetTmpPoints[i][j];
						Vector3 secondPoint;
						if (j == insetTmpPoints[i].Count - 1)
						{
							secondPoint = insetTmpPoints[i][0];
						}
						else {
							secondPoint = insetTmpPoints[i][j+1];
						}
						Gizmos.color = Color.white;
						//Draw Line
						Gizmos.DrawLine(thisPnt, secondPoint);

						if (j == 0)
						{
							Gizmos.color = Color.blue;
						}
						else if (j == insetTmpPoints[i].Count - 1)
						{
							Gizmos.color = Color.red;
						}
						else
						{
							Gizmos.color = Color.white;
						}
						if (j == 0 || j == insetTmpPoints[i].Count - 1)
						{
							//Draw this Sphere
							Gizmos.DrawSphere(thisPnt, 0.6f);
						}
					}
				}
			}
			//Show orignal Crossing Lines
			for (int i = 0; i < crossLines.Count; i++) {
				for (int j = 0; j < crossLines[i].Count-1; j++) {
					Vector3 thisPnt = crossLines[i][j];
					Vector3 secondPoint = crossLines[i][j + 1];

					Gizmos.color = Color.black;
					//Draw Line
					Gizmos.DrawLine(thisPnt, secondPoint);
					Gizmos.DrawSphere(thisPnt, 0.6f);
					if (j == crossLines[i].Count - 2) {
						Gizmos.DrawSphere(secondPoint, 0.6f);
					}
				}
			}
		}
		*/

		// Draw Bounding Box Exclusions
		/*void OnDrawGizmosSelected()
		{
			if (boundaries.Count > 0)
			{
				for (int i = 0; i < boundaries.Count; i++)
				{
					//Draw Boundary
					for (int j = 0; j < boundaries[i].Count; j++)
					{
						if (j == boundaries[i].Count - 1)
						{
							Debug.DrawLine(boundaries[i][j], boundaries[i][0], ReturnColor(i));
						}
						else
						{
							Debug.DrawLine(boundaries[i][j], boundaries[i][j + 1], ReturnColor(i));
						}
					}
				}
			}
		}

		//Returns Color based on Value
		Color ReturnColor(int number)
		{
			switch (number)
			{
				case 0:
					return Color.yellow;
				case 1:
					return Color.blue;
				case 2:
					return Color.red;
				case 3:
					return Color.magenta;
				case 4:
					return Color.black;
				case 5:
					return Color.green;
				default:
					return Color.white;
			}
		}*/
		//Draw Lot Lines
		/*if (lots.Count > 0) {
			for (int i = 0; i < lots.Count; i++) {
				//Draw Vector3 List
				for (int j = 0; j < lots[i].vectorList.Count; j++) {
					if (j == lots[i].vectorList.Count-1) {
						//Connect to beginning
						Debug.DrawLine(lots[i].vectorList[j], lots[i].vectorList[0], Color.red);
					} else {
						//Connect to next in line
						Debug.DrawLine(lots[i].vectorList[j], lots[i].vectorList[j + 1], Color.red);
					}
				}
			}
		}
		//Draw Green Lots
		if (greenLots.Count > 0)
		{
			//Iterate through a list of List of Vector3
			for (int i = 0; i < greenLots.Count; i++)
			{
				//Draw Vector3 List
				for (int j = 0; j < greenLots[i].Count - 1; j++)
				{
					if (j == greenLots[i].Count - 1)
					{
						//Connect to beginning
						Debug.DrawLine(greenLots[i][j], greenLots[i][0], Color.green);
					}
					else
					{
						//Connect to next in line
						Debug.DrawLine(greenLots[i][j], greenLots[i][j + 1], Color.green);
					}
				}
			}
		}*/
		/*Gizmos.color = Color.green;
		if(visualBounds.Count > 0){
			for(int i = 0;i<visualBounds.Count;i++){
				Gizmos.DrawWireCube (visualBounds[i].center, visualBounds[i].size);
			}
		}
		if(visualNode != Vector3.zero){
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(visualNode,nodeSpacing);
		}*/
		//}

		bool processing = false;
		List<GameObject> roadObjects = new List<GameObject>();
		/*//Debug Play GUI
		void OnGUI(){
			if(!processing){
				if (GUI.Button(new Rect(10, 150, 50, 30), "Regenerate"))
					StartCoroutine(CreateSecondaryRoads());
			}
		}*/
	}
}