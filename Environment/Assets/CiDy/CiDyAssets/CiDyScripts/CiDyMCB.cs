using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CiDy
{

	[System.Serializable]
	public class CiDyMCB
	{

		//Required List and Variables for MCB Calculations
		static List<List<CiDyNode>> cycles = new List<List<CiDyNode>>();//Cycles Found
		static List<List<CiDyNode>> cycleEdges = new List<List<CiDyNode>>();//Edges that are part of Cycles
		static List<List<CiDyNode>> filaments = new List<List<CiDyNode>>();
		static List<CiDyNode> filament = new List<CiDyNode>();
		static List<CiDyNode> untested = new List<CiDyNode>();//Nodes that are left for Primitive Testing
		static List<CiDyNode> markedList = new List<CiDyNode>();//Temporary Marked List of Nodes for Cycle Detection
		static CiDyNode startNode;//Cycle Start Node.
		static CiDyNode v1;
		static CiDyNode vprev;
		static CiDyNode vcurr;
		static CiDyNode vnext;
		static CiDyNode finalNode;

		//This function will Get the outside cycles.
		public static List<List<CiDyNode>> GetBoundaryCells(List<CiDyNode> newGraph, bool noCycles)
		{
			//Cloned list.
			List<CiDyNode> untested = new List<CiDyNode>(0);
			//Create all Nodes do this is O(n) then There adjacent Nodes in O(V+N)
			for (int i = 0; i < newGraph.Count; i++)
			{
				CiDyNode origNode = newGraph[i];
				//Create all New Nodes with deep copy.
				//CiDyNode tmpNode = new CiDyNode(origNode.name,origNode.position,(CiDyGraph)origNode.graph, origNode.nodeNumber);
				CiDyNode tmpNode = ScriptableObject.CreateInstance<CiDyNode>().Init(origNode.name, origNode.position, (CiDyGraph)origNode.Graph, origNode.nodeNumber);
				//add to list.
				untested.Add(tmpNode);
			}

			//Now to deep copy the adjacency List of the Original Nodes we will use there Unique tags(Name) to Properly Connect the New Nodes
			for (int i = 0; i < newGraph.Count; i++)
			{
				//Debug.Log("MCB Deep Copy");
				CiDyNode origNode = newGraph[i];
				//Iterate through adjacency List of Orignode matching adjacenNodes to the Cloned Node.
				CiDyNode newNode = untested.Find(x => x.name == origNode.name);
				for (int j = 0; j < origNode.adjacentNodes.Count; j++)
				{
					//Grab adjacent Node and find it in the CellGraph(cloned nodes)
					CiDyNode origAdjNode = origNode.adjacentNodes[j];
					CiDyNode newAdjNode = untested.Find(x => x.name == origAdjNode.name);
					//Flatten Y Axis as this MCB is assumed 2D x,z axis. :)
					newAdjNode.position.y = 0;
					//Add the cloned node to the proper adjacent List.
					newNode.adjacentNodes.Add(newAdjNode);
					//Debug.Log(newNode.name+" adj = "+newNode.adjacentNodes.Count);
				}
			}

			//Now run Cell Extraction on the cloned List and Return the Cells Found if Any.
			//Sort them by X,Z World Value
			//untested = untested.OrderBy(x => Mathf.Round(x.position.x*100f)/100f).ThenBy(x => Mathf.Round(x.position.z*100f)/100f).ToList();
			untested = untested.OrderBy(x => -x.position.x).ThenBy(x => x.position.z).ToList();
			//Lets see if there is a sequence here.
			List<List<CiDyNode>> finalSequence = new List<List<CiDyNode>>(0);
			List<CiDyNode> sequence = new List<CiDyNode>(0);
			CiDyNode v0;

			if (noCycles)
			{
				//Resort based on Adjacency
				untested = untested.OrderBy(x => x.adjacentNodes.Count).ToList();
				//There are no cycles. So we will be starting at end points only and tracing.
				while (untested.Count > 0)
				{
					//Get all the Sequences
					v0 = untested[0];
					sequence.Add(v0);//start Cycle
					v1 = GetClockWiseMost(v0);
					vprev = v0;//Start Node
					vcurr = v1;//Clockwise Most Node if not null.
							   //If we are not null and we are not the end of the cycle and we have not been involved in any other primitives.
					while (vcurr != null && vcurr.name != v0.name)
					{
						//Insert curr into sequence
						sequence.Add(vcurr);
						//Mark as visited
						vcurr.marked = true;
						markedList.Add(vcurr);
						if (vcurr.adjacentNodes.Count == 1)
						{
							//We have to go back
							vnext = vprev;//The point we must go back to
							vprev = vcurr;
							vcurr = vnext;
						}
						else
						{
							vnext = GetClockWiseMost(vprev, vcurr);
							vprev = vcurr;
							vcurr = vnext;
						}
					}
					if (vcurr != null && vcurr.name == v0.name)
					{
						Debug.Log("Completed Cycle");
						//Sequence is complete, Start Another one.
						finalSequence.Add(sequence);
						//Remove all these nodes.
						for (int i = 0; i < sequence.Count; i++)
						{
							untested.Remove(sequence[i]);
						}
						sequence = new List<CiDyNode>(0);
					}
				}
			}
			else
			{
				//Cycles Exist. Also Watch For Filaments.
				//Resort based on Adjacency
				//untested = untested.OrderBy(x => -x.adjacentNodes.Count).ToList();
				while (untested.Count > 0)
				{
					//Get all the Sequences
					v0 = untested[0];
					sequence.Add(v0);//start Cycle
					v1 = GetClockWiseMost(v0);
					vprev = v0;//Start Node
					vcurr = v1;//Clockwise Most Node if not null.
							   //If we are not null and we are not the end of the cycle and we have not been involved in any other primitives.
					while (vcurr != null && vcurr.name != v0.name)
					{
						//Insert curr into sequence
						sequence.Add(vcurr);
						//Mark as visited
						vcurr.marked = true;
						markedList.Add(vcurr);
						if (vcurr.adjacentNodes.Count == 1)
						{
							//We have to go back
							vnext = vprev;//The point we must go back to
							vprev = vcurr;
							vcurr = vnext;
						}
						else
						{
							vnext = GetClockWiseMost(vprev, vcurr);
							vprev = vcurr;
							vcurr = vnext;
						}
						Debug.Log("Vcurr: " + vprev.name + " VNext: " + vcurr.name);
					}
					if (vcurr != null && vcurr.name == v0.name)
					{
						Debug.Log("Completed Cycle");
						//Is this cycle Left Facing? or Right
						if (!Clockwise(sequence))
						{
							//Sequence is complete, Start Another one.
							finalSequence.Add(sequence);

						}
						//Remove all these nodes.
						for (int i = 0; i < sequence.Count; i++)
						{
							untested.Remove(sequence[i]);
						}
						sequence = new List<CiDyNode>(0);
					}
				}
			}

			return finalSequence;
		}

		public static bool Clockwise(List<CiDyNode> list)
		{
			Debug.Log("Clockwise Test: " + list.Count);
			//Check Angle Bisectors and determine if left or right facing.
			//Find the Bisector and Determine if Left or Right of Line Direction.
			for (int j = 0; j < list.Count; j++)
			{
				Vector3 prevNode;
				Vector3 curNode = list[j].position;
				Vector3 nxtNode;

				if (j == 0)
				{
					//We are at the begining, so the prev node points back to the end.
					prevNode = list[list.Count - 1].position;
					//Next node = Future
					nxtNode = list[j + 1].position;
				}
				else if (j == list.Count - 1)
				{
					//At End
					//Nxt Node points to Begining.
					nxtNode = list[0].position;
					//Prev node = before
					prevNode = list[j - 1].position;
				}
				else
				{
					//Prev node is before
					prevNode = list[j - 1].position;
					//Next node = Future
					nxtNode = list[j + 1].position;
				}
				prevNode.y = 0;
				curNode.y = 0;
				nxtNode.y = 0;
				//Get Angle Bisector
				Vector3 bisector = CiDyUtils.AngleBisector(prevNode, curNode, nxtNode);
				//Now project Point of Reference.
				//Vector3 refPoint = curNode + (bisector * 1000f);
				//Is refPoint left of direction Line or Right?
				Vector3 direction = (nxtNode - curNode);
				int angle = CiDyUtils.AngleDir(direction, bisector, Vector3.up);
				//If returns -1// Left, 0 = colliner, 1= right.
				Debug.Log(angle);
				//If left hand side then Accept. If not, Remove
				if (angle < 0)
				{
					return false;
				}
			}

			return true;
		}


		public static List<List<CiDyNode>> ExtractCells(List<CiDyNode> newGraph, ref List<List<CiDyNode>> finalFilaments)
		{//, ref List<List<CiDyNode>> finalEdges){
		 //Debug.Log ("Extract Cells");
			cycleEdges = new List<List<CiDyNode>>(0);
			//Debug.Log ("Extract Cells " + newGraph.Count);
			cycles = new List<List<CiDyNode>>(0);
			filament.Clear();
			filaments.Clear();
			//Deep Copy referenced List so we do not damage the Original Data
			//Cloned list.
			untested = new List<CiDyNode>(0);
			//Create all Nodes do this is O(n) then There adjacent Nodes in O(V+N)
			for (int i = 0; i < newGraph.Count; i++)
			{
				CiDyNode origNode = newGraph[i];
				//Create all New Nodes with deep copy.
				//CiDyNode tmpNode = new CiDyNode(origNode.name,origNode.position,(CiDyGraph)origNode.graph, origNode.nodeNumber);
				CiDyNode tmpNode = ScriptableObject.CreateInstance<CiDyNode>().Init(origNode.name, origNode.position, (CiDyGraph)origNode.Graph, origNode.nodeNumber);
				//add to list.
				untested.Add(tmpNode);
			}

			//Now to deep copy the adjacency List of the Original Nodes we will use there Unique tags(Name) to Properly Connect the New Nodes
			for (int i = 0; i < newGraph.Count; i++)
			{
				//Debug.Log("MCB Deep Copy");
				CiDyNode origNode = newGraph[i];
				//Iterate through adjacency List of Orignode matching adjacenNodes to the Cloned Node.
				CiDyNode newNode = untested.Find(x => x.name == origNode.name);
				for (int j = 0; j < origNode.adjacentNodes.Count; j++)
				{
					//Grab adjacent Node and find it in the CellGraph(cloned nodes)
					CiDyNode origAdjNode = origNode.adjacentNodes[j];
					CiDyNode newAdjNode = untested.Find(x => x.name == origAdjNode.name);
					//Flatten Y Axis as this MCB is assumed 2D x,z axis. :)
					newAdjNode.position.y = 0;
					//Add the cloned node to the proper adjacent List.
					newNode.adjacentNodes.Add(newAdjNode);
					//Debug.Log(newNode.name+" adj = "+newNode.adjacentNodes.Count);
				}
			}

			//Now run Cell Extraction on the cloned List and Return the Cells Found if Any.
			//Sort them by X,Z World Value
			//untested = untested.OrderBy(x => Mathf.Round(x.position.x*100f)/100f).ThenBy(x => Mathf.Round(x.position.z*100f)/100f).ToList();
			untested = untested.OrderBy(x => x.position.x).ThenBy(x => x.position.z).ToList();
			//Debug.Log ("untested= "+untested.Count);
			//Extract All Nodes in the Untested List.
			while (untested.Count > 0)
			{
				//We have nodes that need to be tested for Primitives
				startNode = untested[0];
				//Debug.Log("ExtractPrimitive "+startNode.name+" adjacents = "+startNode.adjacentNodes.Count);
				//Set Start Node for Cycle Detection
				//Extract Primitive for Testing Node.
				if (startNode.adjacentNodes.Count > 0)
				{
					ExtractPrimitive(startNode);
				}
				else
				{
					//Remove this isolated Node
					untested.RemoveAt(0);
				}

				if (filament.Count > 0)
				{
					//Debug.Log("Added to Filaments");
					filaments.Add(filament);
					filament = new List<CiDyNode>();
				}
			}

			if (filaments.Count > 0)
			{
				//This Graph has Filaments that need to be accounted for. :)
				//Current Filament list point to clones. Grab real Nodes and place into list.
				for (int i = 0; i < filaments.Count; i++)
				{
					for (int j = 0; j < filaments[i].Count; j++)
					{
						filaments[i][j] = newGraph.Find(x => x.name == filaments[i][j].name);
					}
				}
				finalFilaments = filaments;
				//finalEdges = cycleEdges;
				//Debug.Log("Filaments "+finalFilaments.Count);//+" CycleEdges "+finalEdges.Count);
			}
			//List<int> filamentCycles = new List<int> (0);
			//Grab True Nodes as the Current (cycles) List is Of Clones that do not have accurate Data Anymore.
			//List<List<CiDyNode>> finalCycles = new List<List<CiDyNode>>(0);
			for (int i = 0; i < cycles.Count; i++)
			{
				//List<CiDyNode> newCycle = new List<CiDyNode>(0);
				for (int j = 0; j < cycles[i].Count; j++)
				{
					string cycleName = cycles[i][j].name;
					cycles[i][j] = newGraph.Find(x => x.name == cycleName);
					//Do any filaments Root Nodes Start at This Node?
					//Update this Cycle with any interior Filaments.
					if (finalFilaments.Count > 0)
					{
						//bool endCheck = false;
						//Iterate through filaments ROOT Nodes and See if any root Nodes match the cycles nodes.
						for (int k = 0; k < finalFilaments.Count; k++)
						{
							int end = finalFilaments[k].Count - 1;
							CiDyNode rootNode = finalFilaments[k][end];
							CiDyNode fwdNode = finalFilaments[k][end - 1];
							if (cycleName == rootNode.name)
							{
								//This filament is rooted in this Cycle. Now we need to Determine if its on this cycles interior/exterior
								//To do that we will simple grab the two connected edges to this node in the cycle. and determine if the filamentRootNodes 1stAdjNode is Left of both Edges.
								Vector3 prePos;
								Vector3 curPos = cycles[i][j].position;
								Vector3 aftPos;

								if (j == 0)
								{
									//We need to grab the Last
									prePos = cycles[i][cycles[i].Count - 1].position;
									//Just grab the Next
									aftPos = cycles[i][j + 1].position;
								}
								else if (j == cycles[i].Count - 1)
								{
									//We are at the End of the List
									//Grab previous
									prePos = cycles[i][j - 1].position;
									//We are at the End so Grab the first
									aftPos = cycles[i][0].position; ;
								}
								else
								{
									//We are in the Middle
									prePos = cycles[i][j - 1].position; ;
									aftPos = cycles[i][j + 1].position; ;
								}
								//Setup Edge Directions
								Vector3 fwd = (curPos - prePos).normalized;
								Vector3 fwd2 = (aftPos - curPos).normalized;
								//Setup Filament Dir
								Vector3 filDir = (fwdNode.position - rootNode.position).normalized;
								//Now Determin AngleDir for Fwd
								int angleDir = CiDyUtils.AngleDir(fwd, filDir, Vector3.up);
								if (angleDir == -1)
								{
									//Now test second
									angleDir = CiDyUtils.AngleDir(fwd2, filDir, Vector3.up);
									if (angleDir == -1)
									{
										//This filament is inside this Cell. Add them in proper sequence and Remove it from Filaments List.
										//Debug.Log("Filament Root Node :"+rootNode.name+" fwdNode: "+fwdNode.name);
										//Now lets determine the Filaments Proper Sequence to be tied into this Cycle.
										List<CiDyNode> filamentSeq = new List<CiDyNode>(0);
										//Debug.Log("Added FwdNode "+fwdNode.name);
										filamentSeq.Add(fwdNode);
										if (fwdNode.adjacentNodes.Count > 1)
										{
											//More Nodes in filament to test.
											//We will tour the filament starting with rootNode-nxtNode
											CiDyNode nxtNode = GetLeftMost(rootNode, fwdNode);
											bool testing = true;
											while (testing)
											{
												if (nxtNode != null)
												{
													//Is this node the Root Node
													if (nxtNode.name == cycleName)
													{
														//Debug.Log("Found RootNode");
														//Yes this is the End of the Filament Touring.
														testing = false;
													}
													else
													{
														//Debug.Log("Nxt Node Added: "+nxtNode.name);
														//Add and update Sequence
														filamentSeq.Add(nxtNode);
														//Only test if potentials exist
														if (nxtNode.adjacentNodes.Count > 1)
														{
															//Store and Retest
															rootNode = fwdNode;
															fwdNode = nxtNode;
															//Debug.Log("RootNode: "+rootNode.name+" New FwdNode: "+fwdNode.name);
															//Test For Next Node.
															nxtNode = GetLeftMost(rootNode, fwdNode);
														}
														else
														{
															//Debug.Log("Nxt Node has no potentials. Reverse Test Add: "+fwdNode.name);
															filamentSeq.Add(fwdNode);
															//Fwd Node is a DeadEnd Turn around and Test Again.
															nxtNode = GetLeftMost(nxtNode, fwdNode);
														}
													}
												}
											}
										}
										//If here then we are done adding this filament to this cycle. Add Root to complete Filament
										filamentSeq.Add(finalFilaments[k][end]);
										//Lets store a pointer to this cycle for the filament we found.So later we can integrete the Sequence into the Proper cycles.
										//filamentCycles.Add(i);
										//Now Remove rootNode filament so we do not retest this filament against other cells.(it can only have one);
										finalFilaments.RemoveAt(k);
										k--;
										//Insert filament into cycle sequence
										int insertPoint;
										if (j != cycles[i].Count - 1)
										{
											insertPoint = j + 1;
										}
										else
										{
											insertPoint = 0;
										}
										cycles[i].InsertRange(insertPoint, filamentSeq);
									}
								}
							}
						}
					}
				}
				//finalCycles.Add(newCycle);
			}

			startNode = null;
			v1 = null;
			vprev = null;
			vcurr = null;
			vnext = null;
			finalNode = null;
			//Clear Untested Data
			cycleEdges.Clear();
			untested.Clear();
			//Clear Any Referenced Nodes
			Resources.UnloadUnusedAssets();
			return cycles;
		}

		static bool grabFil = false;
		//This will determine what type of primitive the referenced Node is apart of and update the graph.(Isolated,Filament or Minimum Cycle)
		public static void ExtractPrimitive(CiDyNode v0)
		{
			//Debug.Log ("New Test ");
			//Clear filament search
			grabFil = false;
			if (v0.adjacentNodes.Count == 1)
			{
				//We are a filament
				//Debug.Log("Filament Added 0 "+v0.name);
				//filament.Add(v0);
				grabFil = true;
			}
			//int filamnetsCnt = 0;
			//Debug.Log("Extracting "+v0.name+" Adj Count = "+v0.adjacentNodes.Count);
			List<CiDyNode> sequence = new List<CiDyNode>(0);
			sequence.Add(v0);//start Cycle
			v1 = GetClockWiseMost(v0);
			//Debug.Log(v0.name + " ClockWise Most ="+v1.name);
			/*if(v1 != null){
				//Debug.Log("Clockwise Node = "+v1.name);
			} else {
				//Debug.Log(v0.name+" has "+v0.adjacentNodes.Count+" Adj Nodes");
			}*/
			vprev = v0;//Start Node
			vcurr = v1;//Clockwise Most Node if not null.

			//If we are not null and we are not the end of the cycle and we have not been involved in any other primitives.
			while (vcurr != null && vcurr.name != v0.name && !vcurr.marked)
			{
				//Insert curr into sequence
				sequence.Add(vcurr);
				//Mark as visited
				vcurr.marked = true;
				markedList.Add(vcurr);
				//Debug.Log(vcurr.name+" (vCurr)Marked");
				//Test for the CounterClockwise Node using previous to current directional line.
				vnext = GetCounterClockWiseMost(vprev, vcurr);
				/*if(vnext != null){
					Debug.Log("Counter Clockwise to "+vcurr.name+" = "+vnext.name);
				} else {
					Debug.Log("Vnext is null");
					//Debug.Log(vcurr.name+" Only has "+vcurr.adjacentNodes.Count+" Adj Nodes");
					//filament.Add(vcurr);

				}*/
				vprev = vcurr;
				vcurr = vnext;
			}

			//Test for remaining scenarios
			if (vcurr == null)
			{
				//Debug.Log("Filement End Found ");
				//Debug.Log("VCurr is Null");
				// Filament found, not necessarily rooted at vprev.
				//Debug.Log("Filament Found "+vprev.name+" Adj[0] "+vprev.adjacentNodes[0].name);
				if (!grabFil)
				{
					grabFil = true;
				}
				if (filament.Count > 0)
				{
					//We have a filament that needs to be added to the total list.
					filaments.Add(filament);
					//Debug.Log("Filament Added to Full List "+filament[0].name+" FilamentsTotal:"+filaments.Count);
					//Clear for new Filament
					filament = new List<CiDyNode>();
				}
				//if(vprev.adjacentNodes.Count > 0)
				ExtractFilament(vprev, vprev.adjacentNodes[0]);
			}
			else if (vcurr.name == v0.name)
			{
				//Found Cycle
				//sequence.Add(vcurr);
				//Debug.Log("Found Cycle Starting At "+v0.name+" Sequence Count = "+sequence.Count);
				//Minimal Cycle Found
				//Add final sequence to Cycle list.
				cycles.Add(sequence);
				List<CiDyNode> edgeCycle = new List<CiDyNode>();
				//Set all edges as part of cycle
				for (int i = 0; i < sequence.Count; i++)
				{
					edgeCycle.Add(sequence[i]);
					//Debug.Log("Cycle Edge "+newEdge.name);
				}
				cycleEdges.Add(edgeCycle);
				//Debug.Log("Removed Edge "+v0.name+","+v1.name);
				//Remove Edge between v0-v1;
				v0.adjacentNodes.Remove(v1);
				v1.adjacentNodes.Remove(v0);

				//Did the removal of this cycle turn v0 into filament?
				if (v0.adjacentNodes.Count == 1)
				{
					if (!grabFil)
					{
						grabFil = true;
					}
					// Remove the filament rooted at v0.
					//Debug.Log(v0.name+" is now a Filament? ADJ : "+v0.adjacentNodes[0].name);
					ExtractFilament(v0, v0.adjacentNodes[0]);
				}
				//Did the removal of this cycle turn v1 into a filament?
				if (v1.adjacentNodes.Count == 1)
				{
					if (!grabFil)
					{
						grabFil = true;
					}
					// Remove the filament rooted at v1.
					//Debug.Log(v1.name+" is now a Filament? ADJ : "+v1.adjacentNodes[0].name);
					ExtractFilament(v1, v1.adjacentNodes[0]);
				}
			}
			else
			{
				if (filament.Count > 0)
				{
					//We have a filament that needs to be added to the total list.
					filaments.Add(filament);
					//Debug.Log("Filament Added to Full List "+filament[0].name+" FilamentsTotal:"+filaments.Count);
					//Clear for new Filament
					filament = new List<CiDyNode>();
				}
				//Debug.Log(vcurr.name+"  has been visited Potential Filament");
				//vCurr has been visited and may not be minimal cycle.
				// A cycle has been found, but is not guaranteed to be a minimal
				// cycle. This implies v0 is part of a filament. Locate the
				// starting point for the filament by traversing from v0 away
				// from the initial v1.
				//Debug.Log("Potential FIlament "+v0.name+" : "+v1.name);
				if (!grabFil)
				{
					grabFil = true;
				}
				while (v0.adjacentNodes.Count == 2)
				{
					//Make sure we set the nodes away from the inital v1.
					if (v0.adjacentNodes[0].name != v1.name)
					{
						//V1 is the second adjacent node so pick the first(0);
						v1 = v0;
						v0 = v0.adjacentNodes[0];
					}
					else
					{
						//V1 is the first adjacent so pick the Second(1);
						v1 = v0;
						v0 = v0.adjacentNodes[1];
					}
				}
				//Debug.Log("Filament? "+v0.name);
				//Run filament extraction on current set nodes from while loop.
				ExtractFilament(v0, v1);
			}
			//Clear all Nodes marked during search for next search.
			if (markedList.Count > 0)
			{
				////Debug.LogError("Resetting Marked");
				for (int i = 0; i < markedList.Count; i++)
				{
					markedList[i].marked = false;
				}
				//Clear List
				markedList = new List<CiDyNode>();
			}
		}

		//This will Remove Filaments from the graph.(Non-Cyclic Sequences)
		static void ExtractFilament(CiDyNode v0, CiDyNode v1)
		{
			//Debug.Log("ExtractingFilament "+v0.name+" to "+v1.name);
			CiDyEdge testEdge = new CiDyEdge(v0, v1);
			//Debug.Log(v0.name+" AdjNds Count = "+v0.adjacentNodes.Count);
			//Is this edge part of a found Cycle?
			if (IsCycleEdge(testEdge))
			{
				//Debug.Log(testEdge.name+" Is a cycle Edge");
				//If branch Node.
				if (v0.adjacentNodes.Count >= 3)
				{
					//Debug.Log(v0.name+" Is a Branch Node. Removing Edge between "+v0.name+","+v1.name);
					//Remove edge from graph.
					v0.adjacentNodes.Remove(v1);
					v1.adjacentNodes.Remove(v0);
					//Update v0 for next node in the filament
					v0 = v1;
					if (v0.adjacentNodes.Count == 1)
					{
						//We only have one adjacent node left to grab.
						v1 = v0.adjacentNodes[0];
					}
				}

				//Debug.Log("Test Edge = "+v0.name+","+v1.name);
				while (v0.adjacentNodes.Count == 1)
				{
					v1 = v0.adjacentNodes[0];
					//Update to the new edge
					testEdge = new CiDyEdge(v0, v1);
					if (IsCycleEdge(testEdge))
					{
						//Debug.Log(testEdge.name+" Is a cycle Edge. Removed edge and "+v0.name+" From Untested");
						//Remove this node from untested.
						untested.Remove(v0);
						//ExtractIsolatedNode(v0);
						//Remove edge
						v0.adjacentNodes.Remove(v1);
						v1.adjacentNodes.Remove(v0);
						//Update v0 for next test.
						v0 = v1;
					}
					else
					{
						//We have removed all of the cycle edges leave while loop.
						break;
					}
				}
				//Remove the newly isolated Node from the Graph.
				if (v0.adjacentNodes.Count == 0)
				{
					//Debug.Log(v0.name+" Is Isolated And Removed from untested");
					//Remove node from untesed
					untested.Remove(v0);
					//ExtractIsolatedNode(v0);
				}
			}
			else
			{
				//Debug.Log("Edge "+testEdge.name+" is not part of a cycle yet "+v0.name+" "+v0.adjacentNodes.Count);
				//If branch node and not cycle tagged edge.
				if (v0.adjacentNodes.Count >= 3)
				{
					//Debug.Log("Split Edge "+v0.name+" "+v1.name);
					if (grabFil)
					{
						//Debug.Log("Branch Node End Filament "+v0.name);
						grabFil = false;
					}
					//Debug.Log(v0.name+" Is a Branch Node. Removing Edge between "+v0.name+","+v1.name);
					//Remove edge
					v0.adjacentNodes.Remove(v1);
					v1.adjacentNodes.Remove(v0);
					//Update for next test.
					v0 = v1;
					//Is end point of filament?
					if (v0.adjacentNodes.Count == 1)
					{
						//Grab connected node.
						v1 = v0.adjacentNodes[0];
					}
				}
				//Iterate through filament nodes.
				while (v0.adjacentNodes.Count == 1)
				{
					//Debug.Log(v0.name+" Has only "+v0.adjacentNodes.Count+" Adj Nodes");
					/*if(grabFil){
						Debug.Log("Do we want this? "+v0.name);
					}*/
					//Update v1 to next connected node
					v1 = v0.adjacentNodes[0];
					//Remove v0 from untested
					untested.Remove(v0);
					//ExtractIsolatedNode(v0);
					//Debug.Log("Removed "+v0.name+" From Untested List");
					if (grabFil)
					{
						//Debug.Log("Testing "+v0.name);
						testEdge = new CiDyEdge(v0, v1);
						//Debug.Log("Testing cycle Edge name "+testEdge.name);
						if (!IsCycleEdge(testEdge))
						{
							//Debug.Log("Non Cycle Edge, Removed "+v0.name+"-"+v1.name);
							//Debug.Log(v1.name+" Count = "+v1.adjacentNodes.Count);
							//Debug.Log("Added "+v0.name+" Count for "+v1.name+":"+v1.adjacentNodes.Count);
							//Debug.Log("Added Filament "+v0.name);
							filament.Add(v0);
							if (v1.adjacentNodes.Count != 2)
							{
								//Debug.Log("Added2 "+v1.name);
								filament.Add(v1);
								//Debug.Log("Added filament2 "+v1.name);
							}
						}
						else
						{
							//End Grab Fil
							//Debug.Log("End Grab Fil Adding "+v0.name);
							//Debug.Log("Added filament Node: "+v0.name);
							filament.Add(v0);
							//Debug.Log(v0.name+" End Grab Fil");
							grabFil = false;
						}
					}
					//Remove edge
					v0.adjacentNodes.Remove(v1);
					v1.adjacentNodes.Remove(v0);
					//Updatee v0 for next Test.
					v0 = v1;
				}
				//Debug.Log("No longer 1 "+v0.adjacentNodes.Count);
				//If v0 is now isolated the last node of the filament
				if (v0.adjacentNodes.Count == 0)
				{
					//Debug.Log(v0.name+" Is an Isolated Node Removed it from Untested");
					//Remove from untested
					//ExtractIsolatedNode(v0);
					//Debug.Log("Isolated "+v0.name);
					untested.Remove(v0);
				}
			}
		}

		static CiDyNode GetClockWiseMost(CiDyNode srcNode)
		{
			float currentDirection = Mathf.NegativeInfinity;
			int bestNode = -1;

			// the vector that we want to measure an angle from
			Vector3 referenceForward = new Vector3(0, 0, -1);// some vector that is not Vector3.up

			// the vector perpendicular to referenceForward (90 degrees clockwise)
			// (used to determine if angle is positive or negative)
			Vector3 referenceRight = Vector3.Cross(Vector3.up, referenceForward);

			// the vector of interest
			//Itearate through adjacent Nodes
			for (int i = 0; i < srcNode.adjacentNodes.Count; i++)
			{
				CiDyNode tmpNode = srcNode.adjacentNodes[i];
				//Grab new Direction
				Vector3 newDirection = (tmpNode.position - srcNode.position).normalized;// some vector that we're interested in 
																						// Get the angle in degrees between 0 and 180
				float angle = Vector3.Angle(newDirection, referenceForward);
				// Determine if the degree value should be negative.  Here, a positive value
				// from the dot product means that our vector is on the right of the reference vector   
				// whereas a negative value means we're on the left.
				float sign = Mathf.Sign(Vector3.Dot(newDirection, referenceRight));
				float finalAngle = sign * angle;
				//print ("Final Angle for "+tmpNode.name+" = "+finalAngle);
				//Catch scenario when adjacent node is directly behind us returning as a positive and make it a negative.
				if (finalAngle == 180)
				{
					finalAngle = -180;
				}
				//ClockWise Most (Highest/Positive)
				if (currentDirection < finalAngle)
				{
					//The New angle is higher update CurrentDirection
					bestNode = i;
					currentDirection = finalAngle;
					//print ("Best Node updated");
				}
			}
			//Did we find a new node?
			if (bestNode != -1)
			{
				//We have selected a Node
				finalNode = srcNode.adjacentNodes[bestNode];
				return finalNode;
			}
			//Didn't Find a new Node return null so we know this may be a filament.
			return null;
		}

		public static List<Vector3> points = new List<Vector3>();
		//Find counterClockwise Most using prevNode direction to srcNode(curNode)
		public static CiDyNode GetCounterClockWiseMost(CiDyNode srcNode, CiDyNode nxtNode)
		{
			//startNode = new CiDyNode ("", Vector3.zero, 0);
			//startNode = ScriptableObject.CreateInstance<CiDyNode> ().Init ("", Vector3.zero, 0);
			points = new List<Vector3>(0);
			//Debug.Log ("running "+srcNode.name+" "+nxtNode.name);
			float currentDirection = Mathf.Infinity;
			int bestNode = -1;

			// the vector that we want to measure an angle from
			Vector3 referenceForward = (nxtNode.position - srcNode.position);// some vector that is not Vector3.Debug.Log (referenceForward);
																			 //referenceForward = nxtNode.position+referenceForward;
																			 //points.Add (referenceForward);
																			 // the vector perpendicular to referenceForward (90 degrees clockwise)
																			 // (used to determine if angle is positive or negative)
			Vector3 referenceRight = Vector3.Cross(Vector3.up, referenceForward);
			//referenceRight = srcNode.position+referenceRight*100;
			//points.Add (referenceRight);
			// the vector of interest
			//Debug.Log ("NxtNode ADJ Cnt "+nxtNode.adjacentNodes.Count);
			if (nxtNode.adjacentNodes.Count > 1)
			{
				//Itearate through adjacent Nodes
				//Debug.Log(nxtNode.name+" Count: "+nxtNode.adjacentNodes.Count);
				for (int i = 0; i < nxtNode.adjacentNodes.Count; i++)
				{
					CiDyNode tmpNode = nxtNode.adjacentNodes[i];
					//Debug.Log(nxtNode.name+" "+i+" Adj: "+tmpNode.name);
					//Debug.Log(nxtNode.name+ " Adj Node "+tmpNode.name+" Current Place "+i);
					//If the curNode we are checking is not equal to the node we came from (SRC)
					if (tmpNode.name != srcNode.name)
					{
						//Debug.Log(tmpNode.name);
						//Grab new Direction
						Vector3 newDirection = (tmpNode.position - nxtNode.position);// some vector that we're interested in
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
						float finalAngle = angle * sign;
						//print ("Final Angle for "+tmpNode.name+" = "+finalAngle);
						//Catch scenario when adjacent node is directly behind us returning as a positive and make it a negative.
						if (finalAngle == 180)
						{
							finalAngle = -180;
						}
						//finalAngle = Mathf.Round(finalAngle * 100f) / 100f;
						//finalAngle = (finalAngle<= 0) ? 360 + finalAngle : finalAngle;
						//Debug.Log(tmpNode.name+" "+finalAngle);
						//ClockWise Most (Highest/Positive)
						if (currentDirection > finalAngle)
						{
							//The New angle is higher update CurrentDirection
							bestNode = i;
							currentDirection = finalAngle;
							//Debug.Log("BestNode "+nxtNode.adjacentNodes[bestNode].name+" FinalAngle "+currentDirection);
						}
					}
				}
				//Did we find a new node?
				if (bestNode != -1)
				{
					//We have selected a Node
					finalNode = nxtNode.adjacentNodes[bestNode];
					//points.Add(finalNode.position);
					//Debug.Log("CounterClockwise FinalNode "+finalNode.name+" Found For "+srcNode.name);
					return finalNode;
				}
			}
			//Debug.Log("CounterClockwise FinalNode "+finalNode.name+" Found For "+srcNode.name);
			//Didn't Find a new Node return null so we know this may be a filament.
			return null;
		}

		//Find counterClockwise Most using prevNode direction to srcNode(curNode)
		public static CiDyNode GetClockWiseMost(CiDyNode srcNode, CiDyNode nxtNode)
		{
			//startNode = new CiDyNode ("", Vector3.zero, 0);
			//startNode = ScriptableObject.CreateInstance<CiDyNode> ().Init ("", Vector3.zero, 0);
			points = new List<Vector3>(0);
			//Debug.Log ("running "+srcNode.name+" "+nxtNode.name);
			float currentDirection = Mathf.NegativeInfinity;
			int bestNode = -1;

			// the vector that we want to measure an angle from
			Vector3 referenceForward = (nxtNode.position - srcNode.position);// some vector that is not Vector3.Debug.Log (referenceForward);
																			 //referenceForward = nxtNode.position+referenceForward;
																			 //points.Add (referenceForward);
																			 // the vector perpendicular to referenceForward (90 degrees clockwise)
																			 // (used to determine if angle is positive or negative)
			Vector3 referenceRight = Vector3.Cross(Vector3.up, referenceForward);
			//referenceRight = srcNode.position+referenceRight*100;
			//points.Add (referenceRight);
			// the vector of interest
			//Debug.Log ("NxtNode ADJ Cnt "+nxtNode.adjacentNodes.Count);
			if (nxtNode.adjacentNodes.Count > 1)
			{
				//Itearate through adjacent Nodes
				//Debug.Log(nxtNode.name+" Count: "+nxtNode.adjacentNodes.Count);
				for (int i = 0; i < nxtNode.adjacentNodes.Count; i++)
				{
					CiDyNode tmpNode = nxtNode.adjacentNodes[i];
					//Debug.Log(nxtNode.name+" "+i+" Adj: "+tmpNode.name);
					//Debug.Log(nxtNode.name+ " Adj Node "+tmpNode.name+" Current Place "+i);
					//If the curNode we are checking is not equal to the node we came from (SRC)
					if (tmpNode.name != srcNode.name)
					{
						//Debug.Log(tmpNode.name);
						//Grab new Direction
						Vector3 newDirection = (tmpNode.position - nxtNode.position);// some vector that we're interested in
																					 //newDirection = srcNode.position+newDirection*100;
																					 //points.Add(newDirection);
																					 //Debug.Log("Added "+newDirection);
																					 // Get the angle in degrees between 0 and 180
						float angle = Vector3.Angle(newDirection, referenceForward);
						// Determine if the degree value should be negative.  Here, a positive value
						// from the dot product means that our vector is on the right of the reference vector   
						// whereas a negative value means we're on the left.
						float sign = Mathf.Sign(Vector3.Dot(newDirection, referenceRight));
						float finalAngle = angle * sign;
						//Debug.Log("Final Angle for "+tmpNode.name+" = "+finalAngle);
						//Catch scenario when adjacent node is directly behind us returning as a positive and make it a negative.
						if (finalAngle == 180)
						{
							finalAngle = -180;
						}

						//finalAngle = Mathf.Round(finalAngle * 100f) / 100f;
						//finalAngle = (finalAngle<= 0) ? 360 + finalAngle : finalAngle;
						//Debug.Log(tmpNode.name+" "+finalAngle);
						//ClockWise Most (Highest/Positive)
						if (finalAngle > currentDirection)
						{
							//The New angle is higher update CurrentDirection
							bestNode = i;
							currentDirection = finalAngle;
							//Debug.Log("BestNode "+nxtNode.adjacentNodes[bestNode].name+" FinalAngle "+currentDirection);
						}
					}
				}
				//Did we find a new node?
				if (bestNode != -1)
				{
					//We have selected a Node
					finalNode = nxtNode.adjacentNodes[bestNode];
					//points.Add(finalNode.position);
					//Debug.Log("CounterClockwise FinalNode "+finalNode.name+" Found For "+srcNode.name);
					return finalNode;
				}
			}
			//Debug.Log("CounterClockwise FinalNode "+finalNode.name+" Found For "+srcNode.name);
			//Didn't Find a new Node return null so we know this may be a filament.
			return null;
		}

		//This will remove isolated Nodes from the Graph.(Nodes that have no connections)
		void ExtractIsolatedNode(CiDyNode v0)
		{
			//Test this nodes adjacency and make sure it has no connections
			//Remove this node from untested list. There is no need to test it.
			untested.Remove(v0);
			//isolatedNodes.Add(v0);
		}

		//Determine if this is apart of the found Cycles.
		static bool IsCycleEdge(CiDyEdge testEdge)
		{
			//Test against cycle edges
			for (int i = 0; i < cycleEdges.Count; i++)
			{
				List<CiDyNode> cycle = cycleEdges[i];
				for (int j = 0; j < cycle.Count; j++)
				{
					CiDyNode v1 = cycle[j];
					CiDyNode v2 = null;
					if (j == cycle.Count - 1)
					{
						//At end
						v2 = cycle[0];
					}
					else
					{
						v2 = cycle[j + 1];
					}
					CiDyEdge newEdge = new CiDyEdge(v1, v2);
					if (testEdge.name == newEdge.name)
					{
						return true;
					}
				}
			}
			return false;
		}

		//For Filament Detection
		static CiDyNode GetLeftMost(CiDyNode srcNode, CiDyNode fwdNode)
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
	}
}