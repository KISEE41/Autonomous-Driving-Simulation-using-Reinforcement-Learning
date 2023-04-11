using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CiDy
{
    //Mesh Class Needed for Mesh Extrusion
    public class MeshEdge
    {
        public int edgeIndex;
        public float dist;
        public Vector3 edgeDir;
        public Vector3 position;
        public bool front;//Front of mesh?

        public MeshEdge(int newEdgeIndex, float newDist, Vector3 newDir, Vector3 newPos)
        {
            edgeIndex = newEdgeIndex;
            dist = newDist;
            edgeDir = newDir;
            position = newPos;
        }
    }

    public static class GeneralUtils
    {
        #region MeshUtils
        public static Mesh ExtrudeMeshAlongSpline(GameObject holderObject, Mesh srcMesh, Mesh capMesh, Vector3[] path, Vector3 rotateSrcMesh)
        {
            Vector3 center = Vector3.zero;
            if (rotateSrcMesh != Vector3.zero)
            {
                //v = q * (v - center) + center;
                //We want to first rotate the Original Meshes, Vertices & Positions.
                Vector3[] verts = srcMesh.vertices;
                Transform turtle = new GameObject("TmpTurtle").transform;
                turtle.transform.rotation = Quaternion.Euler(rotateSrcMesh.x, rotateSrcMesh.y, rotateSrcMesh.z);
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = turtle.rotation * (verts[i] - center) + center;
                    //v = q * (v - center) + center;
                }
                //Set back to mesh
                Mesh newMesh = new Mesh();
                newMesh.vertices = verts;
                newMesh.uv = srcMesh.uv;
                newMesh.triangles = srcMesh.triangles;
                srcMesh = newMesh;
                //Destroy Turtle
#if UNITY_EDITOR
                GameObject.DestroyImmediate(turtle.gameObject);
#elif !UNITY_EDITOR
                    GameObject.Destroy(turtle.gameObject);
#endif
            }
            //Direction
            Vector3 extrusionDirection = Vector3.forward;//Forward Z Axis Extrusion of Mesh.
            //Get All Edges of Mesh.
            MeshExtrusion.Edge[] precomputedEdges = MeshExtrusion.BuildManifoldEdges(srcMesh);
            List<MeshExtrusion.Edge> outputEdges = new List<MeshExtrusion.Edge>(0);
            List<MeshEdge> meshEdges = new List<MeshEdge>(0);//initialize
            //Find Center of Mesh Bounding Box.
            Vector3 meshCenter = srcMesh.bounds.center;
            //Remove All Edges that are Parrallel to the Extrusion Direction.

            float longestEdge = 0;
            Bounds meshBounds = srcMesh.bounds;

            //Calculate Bounds of Mesh
            Vector3 bottomBackLeft = meshBounds.min;
            Vector3 topFrontRight = meshBounds.max;
            Vector3 bottomFrontLeft = new Vector3(bottomBackLeft.x, bottomBackLeft.y, topFrontRight.z);
            Vector3 topBackLeft = new Vector3(bottomBackLeft.x, topFrontRight.y, bottomBackLeft.z);
            Vector3 bottomBackRight = new Vector3(topFrontRight.x, bottomBackLeft.y, bottomBackLeft.z);
            Vector3 topFrontLeft = new Vector3(bottomBackLeft.x, topFrontRight.y, topFrontRight.z);
            Vector3 bottomFrontRight = new Vector3(topFrontRight.x, bottomBackLeft.y, topFrontRight.z);
            Vector3 topBackRight = new Vector3(topFrontRight.x, topFrontRight.y, bottomBackLeft.z);
            float meshWidth = meshBounds.extents.x;
            float meshLength = meshBounds.extents.z;
            float meshHeight = meshBounds.extents.y;
            Debug.Log("Mesh Width: " + meshWidth + " Length: " + meshLength + " Height: " + meshHeight);
            //What can you tell me about these edges?
            for (int i = 0; i < precomputedEdges.Length; i++)
            {
                if (precomputedEdges[i].vertexIndex.Length > 0)
                {
                    //There are two Index
                    int vertA = precomputedEdges[i].vertexIndex[0];
                    int vertB = precomputedEdges[i].vertexIndex[1];
                    Vector3 edgeCenter = (srcMesh.vertices[vertA] + srcMesh.vertices[vertB]) / 2;
                    //Calculate Distance Between Points.
                    float dist = Vector3.Distance(srcMesh.vertices[vertA], srcMesh.vertices[vertB]);
                    float edgeDistFromCenter = Mathf.RoundToInt(Vector3.Distance(edgeCenter, meshCenter));
                    if (edgeDistFromCenter > longestEdge)
                    {
                        longestEdge = edgeDistFromCenter;
                    }
                    //Get This Edge Forward Direction
                    Vector3 edgeDir = (srcMesh.vertices[vertB] - srcMesh.vertices[vertA]).normalized;

                    float indicator = Vector3.Dot(extrusionDirection.normalized, edgeDir.normalized);

                    // Is this the right axis?
                    if (indicator != -1.0f && indicator != 1.0f)
                    {
                        //Store as Mesh Edge
                        meshEdges.Add(new MeshEdge(i, edgeDistFromCenter, edgeDir, (srcMesh.vertices[vertB] + srcMesh.vertices[vertA]) / 2));
                        //outputEdges.Add(precomputedEdges[i]);
                    }
                }
            }
            Debug.Log("Farthest Distance from Center for an Edge: " + longestEdge);
            //Sort List by Edge DistFromCenter
            meshEdges = meshEdges.OrderBy(x => -x.position.x).ThenBy(x => x.position.z).ThenBy(x => x.position.y).ToList();

            //Now that we have our mesh edges. Put only the Matching Criteria for The Absolute Connecting Edges.
            for (int i = 0; i < meshEdges.Count; i++)
            {
#pragma warning disable CS0219 // The variable 'r' is assigned but its value is never used
                float r = 0;
#pragma warning restore CS0219 // The variable 'r' is assigned but its value is never used
#pragma warning disable CS0219 // The variable 's' is assigned but its value is never used
                float s = 0;
#pragma warning restore CS0219 // The variable 's' is assigned but its value is never used
                //Check Distance from this edge center to bounding box line.
                //Get Nearest Point on Line.
                Vector3 nearestPoint = NearestPointOnLine(bottomFrontLeft, meshEdges[i].edgeDir, meshEdges[i].position);

                float distanceToLine = Vector3.Distance(nearestPoint, meshEdges[i].position);
                //Check if this edge's vertices both are inside the desired Bounding Area
                Bounds frontEdgeBounds = new Bounds(topFrontRight, Vector3.zero);
                frontEdgeBounds.Encapsulate(topFrontRight + ((topFrontRight - bottomFrontLeft).normalized * 0.618f));
                frontEdgeBounds.Encapsulate(topFrontLeft + ((topFrontLeft - bottomFrontRight).normalized * 0.618f));
                frontEdgeBounds.Encapsulate(bottomFrontLeft + ((bottomFrontLeft - topFrontRight).normalized * 0.618f));
                frontEdgeBounds.Encapsulate(bottomFrontRight + ((bottomFrontRight - topFrontLeft).normalized * 0.618f));
                //Visualize Bounding Boxes
                Vector3[] frontBoundingBox = new Vector3[8];
                //Front Four
                frontBoundingBox[0] = frontEdgeBounds.center + frontEdgeBounds.extents;//topFrontRight
                frontBoundingBox[1] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(-1, 1, 1));//topFrontLeft
                frontBoundingBox[2] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(-1, -1, 1));//bottomFrontLeft
                frontBoundingBox[3] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(1, -1, 1));//bottomFrontRight
                                                                                                                             //Back Four
                frontBoundingBox[4] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(1, 1, -1));//topBackRight
                frontBoundingBox[5] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(-1, 1, -1));//topBackLeft
                frontBoundingBox[6] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(-1, -1, -1));//BottomBackLeft
                frontBoundingBox[7] = frontEdgeBounds.center + Vector3.Scale(frontEdgeBounds.extents, new Vector3(1, -1, -1));//BottomBackRight
                                                                                                                              //Debug.Log("Distance, FrontLine: "+distanceToLine+" R: "+r+" S: "+s+" Index: "+i);
                if (frontEdgeBounds.Contains(meshEdges[i].position))
                {
                    precomputedEdges[meshEdges[i].edgeIndex].isFrontEdge = true;
                    outputEdges.Add(precomputedEdges[meshEdges[i].edgeIndex]);
                }
                //Now mark the Backs
                r = 0;
                s = 0;
                //Check Distance from this edge center to bounding box line.
                //Get Nearest Point on Line.
                nearestPoint = NearestPointOnLine(bottomBackLeft, meshEdges[i].edgeDir, meshEdges[i].position);
                distanceToLine = Vector3.Distance(nearestPoint, meshEdges[i].position);
                //Debug.Log("Distance, Back: " + distanceToLine + " R: " + r + " S: " + s + " Index: " + i);
                //Check if this edge's vertices both are inside the desired Bounding Area
                Bounds backEdgeBounds = new Bounds(topBackLeft, Vector3.zero);
                backEdgeBounds.Encapsulate(topBackLeft + ((topBackLeft - bottomBackRight).normalized * 0.618f));
                backEdgeBounds.Encapsulate(topBackRight + ((topBackRight - bottomBackLeft).normalized * 0.618f));
                backEdgeBounds.Encapsulate(bottomBackRight + ((bottomBackRight - topBackLeft).normalized * 0.618f));
                backEdgeBounds.Encapsulate(bottomBackLeft + ((bottomBackLeft - topBackRight).normalized * 0.618f));

                //Visualize this Bounding Box
                Vector3[] backBoundingBox = new Vector3[8];
                //Front Four
                backBoundingBox[0] = backEdgeBounds.center + backEdgeBounds.extents;//topFrontRight
                backBoundingBox[1] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(-1, 1, 1));//topFrontLeft
                backBoundingBox[2] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(-1, -1, 1));//bottomFrontLeft
                backBoundingBox[3] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(1, -1, 1));//bottomFrontRight
                                                                                                                          //Back Four
                backBoundingBox[4] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(1, 1, -1));//topBackRight
                backBoundingBox[5] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(-1, 1, -1));//topBackLeft
                backBoundingBox[6] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(-1, -1, -1));//BottomBackLeft
                backBoundingBox[7] = backEdgeBounds.center + Vector3.Scale(backEdgeBounds.extents, new Vector3(1, -1, -1));//BottomBackRight
                                                                                                                           //Debug.Log("Distance, FrontLine: "+distanceToLine+" R: "+r+" S: "+s+" Index: "+i);
                if (backEdgeBounds.Contains(meshEdges[i].position))
                {
                    //Set Back Edges
                    precomputedEdges[meshEdges[i].edgeIndex].isFrontEdge = false;
                    outputEdges.Add(precomputedEdges[meshEdges[i].edgeIndex]);
                }
            }
            //Now Its time to Extrude the Mesh. (Only two for Test)
            if (path.Length > 2)
            {
                return CloneMesh(holderObject, path, srcMesh, capMesh, outputEdges, meshLength);
            }
            else
            {
                //Send Null to let them know it failed
                return null;
            }
        }

        //This is a test function. We will take a Spline as input and Plot a Mesh Along a Spline.
        public static Mesh CloneMesh(GameObject holderObject, Vector3[] lines, Mesh origMesh, Mesh meshCap, List<MeshExtrusion.Edge> frontBackEdges, float meshLength)
        {

            Bounds meshBounds = origMesh.bounds;

            //Calculate Bounds of Mesh
            Vector3 bottomBackLeft = meshBounds.min;
            Vector3 topFrontRight = meshBounds.max;
            Vector3 bottomFrontLeft = new Vector3(bottomBackLeft.x, bottomBackLeft.y, topFrontRight.z);
            Vector3 topBackLeft = new Vector3(bottomBackLeft.x, topFrontRight.y, bottomBackLeft.z);
            Vector3 bottomBackRight = new Vector3(topFrontRight.x, bottomBackLeft.y, bottomBackLeft.z);
            Vector3 topFrontLeft = new Vector3(bottomBackLeft.x, topFrontRight.y, topFrontRight.z);
            Vector3 bottomFrontRight = new Vector3(topFrontRight.x, bottomBackLeft.y, topFrontRight.z);
            Vector3 topBackRight = new Vector3(topFrontRight.x, topFrontRight.y, bottomBackLeft.z);
            //Three Things needed for a Basic Mesh.
            Vector3[] vertices = origMesh.vertices;
            int[] indicies = origMesh.GetIndices(0);
            Vector2[] uvs = origMesh.uv;
            //Dynamic List for Final Mesh Output.
            List<Vector3> newVertices = new List<Vector3>(0);
            List<int> newIndicies = new List<int>(0);
            List<Vector2> newUVs = new List<Vector2>(0);
            List<Mesh> meshes = new List<Mesh>(0);

            //Create Turtle that will be used to cacluclate rotaion and world matrix
            Transform turtle = new GameObject("Turtle").transform;

            CombineInstance[] combine = new CombineInstance[lines.Length - 1];
            Debug.Log("Combine Length: " + combine.Length);
            //Create Meshes before combining them.
            for (int i = 0; i < lines.Length - 1; i++)
            {
                Vector3 curPos = lines[i];
                Vector3 nxtPos = Vector3.zero;
                Vector3 lineDir = Vector3.zero;

                nxtPos = lines[i + 1];
                lineDir = (nxtPos - curPos).normalized;

                //Spawn our Prefab Mesh to fit between these two points.
                //float disttopoint = Vector3.Distance(meshBounds.extents,meshBounds.center);
                //Move Turtle to rotation and position
                turtle.position = curPos + (lineDir * meshLength);
                turtle.LookAt((turtle.position + (lineDir * 1.618f)));

                for (int j = 0; j < vertices.Length; j++)
                {
                    newVertices.Add(vertices[j]);
                }
                for (int j = 0; j < indicies.Length; j++)
                {
                    newIndicies.Add(indicies[j]);
                }
                for (int j = 0; j < uvs.Length; j++)
                {
                    newUVs.Add(uvs[j]);
                }
                //Now set this mesh to the new mesh
                Mesh newMesh = new Mesh();
                newMesh.vertices = newVertices.ToArray();//Set Vertices
                newMesh.SetIndices(newIndicies.ToArray(), MeshTopology.Triangles, 0, true);//Set Indicies
                newMesh.uv = newUVs.ToArray();//Set Uv's
                                              //Recalculate Mesh
                newMesh.RecalculateNormals();
                newMesh.RecalculateTangents();
                //Set to Combine Mesh Data
                combine[i].mesh = newMesh;
                combine[i].transform = turtle.localToWorldMatrix;
            }

            //Before we Finalize the Extruded Mesh, we want to match up the Connecting Ends.
            for (int i = 0; i < combine.Length - 1; i++)
            {
                //Compare this combine to the Next. Match This Front Edge Vertices to next Back Edge Vertices.
                //Grab Vertices of current Mesh.
                Vector3[] curVerts = new Vector3[combine[i].mesh.vertices.Length];
                for (int j = 0; j < combine[i].mesh.vertices.Length; j++)
                {
                    curVerts[j] = combine[i].mesh.vertices[j];
                }
                //Rotate These Vertices based on the Transform of the CombineMesh
                //Get Rotation from Matrix4x4, using turtle
                turtle.position = combine[i].transform.GetColumn(3);
                turtle.rotation = QuaternionFromMatrix(combine[i].transform);
                Vector3 curDir = (turtle.position + (turtle.forward * 1000) - turtle.position).normalized;
                Matrix4x4 localToWorld = turtle.localToWorldMatrix;

                for (int j = 0; j < curVerts.Length; j++)
                {
                    curVerts[j] = localToWorld.MultiplyPoint3x4(curVerts[j]);
                }
                Vector3[] nxtVerts = combine[i + 1].mesh.vertices;
                //Rotate These Vertices based on the Transform of the CombineMesh
                //Get Rotation from Matrix4x4, using turtle
                turtle.position = combine[i + 1].transform.GetColumn(3);
                turtle.rotation = QuaternionFromMatrix(combine[i + 1].transform);
                Vector3 nxtDir = (turtle.position + (turtle.forward * 1000) - turtle.position).normalized;

                localToWorld = turtle.localToWorldMatrix;

                for (int j = 0; j < nxtVerts.Length; j++)
                {
                    nxtVerts[j] = localToWorld.MultiplyPoint3x4(nxtVerts[j]);
                }

                List<int> curVertIndex = new List<int>(0);
                List<int> nxtVertIndex = new List<int>(0);
                //Now We Want to Find the CurVerts Front Edges and The NxtVerts Back Edges.
                //Now lets visualize there edges, referencing the Front/Back Edges List of the Original Mesh
                for (int j = 0; j < frontBackEdges.Count; j++)
                {
                    if (frontBackEdges[j].isFrontEdge)
                    {
                        curVertIndex.Add(frontBackEdges[j].vertexIndex[0]);//Add First Vertex
                        curVertIndex.Add(frontBackEdges[j].vertexIndex[1]);//Add First Vertex
                    }
                    else
                    {
                        nxtVertIndex.Add(frontBackEdges[j].vertexIndex[0]);//Add First Vertex
                        nxtVertIndex.Add(frontBackEdges[j].vertexIndex[1]);//Add First Vertex
                    }
                }

                //Lets make sure that the Front End Edges are Matched to there Back End (Mirrored Vertex)
                for (int j = 0; j < curVertIndex.Count - 1; j += 2)
                {
                    //CiDyUtils.MarkPoint(curVerts[curVertIndex[j]], j+1111);
                    //CiDyUtils.MarkPoint(nxtVerts[nxtVertIndex[j]], j + 2222);
                    //Compare This Edge to the Next Edge.
                    int curIndexA = curVertIndex[j];//Edge Vertex A
                    int curIndexB = curVertIndex[j + 1];//Edge Vertex B
                                                        //Next Edge
                    int nxtIndexA = nxtVertIndex[j];//Edge Vertex A
                    int nxtIndexB = nxtVertIndex[j + 1];//Edge Vertex B
                                                        //Move them to the middle of the difference between the two points.
                                                        // Extract new local position and Rotation of the Mesh based on Combine Matrix4x4
                    Vector3 posA = curVerts[curIndexA];
                    Vector3 posB = nxtVerts[nxtIndexB];
                    Vector3 intersectionPoint = Vector3.zero;

                    //Find there Intersection of the two direction lines.
                    if (CiDyUtils.LineIntersectionUnRounded(posA + (-curDir * meshLength), (posA + (-curDir * meshLength)) + (curDir * 100), posB + (nxtDir * meshLength), (posB + (nxtDir * meshLength)) + (-nxtDir * 100), ref intersectionPoint))
                    {
                        //Add Y Value
                        intersectionPoint.y = posA.y;
                        Debug.Log("Lines Do Intersect");
                        //CiDyUtils.MarkPoint(intersectionPoint, 8888);
                        //Since there is an intersection.//Perturb the Vertices to there New Positions.
                        curVerts[curIndexA] = intersectionPoint;
                        nxtVerts[nxtIndexB] = intersectionPoint;
                    }
                    //////Now we Must do the Same for the second Points.///////////////
                    //Move them to the middle of the difference between the two points.
                    // Extract new local position and Rotation of the Mesh based on Combine Matrix4x4
                    Vector3 posC = curVerts[curIndexB];
                    Vector3 posD = nxtVerts[nxtIndexA];
                    intersectionPoint = Vector3.zero;

                    //Find there Intersection of the two direction lines.
                    if (CiDyUtils.LineIntersectionUnRounded(posC + (-curDir * meshLength), (posC + (-curDir * meshLength)) + (curDir * 100), posD + (nxtDir * meshLength), (posD + (nxtDir * meshLength)) + (-nxtDir * 100), ref intersectionPoint))
                    {
                        intersectionPoint.y = posC.y;
                        Debug.Log("Lines Do Intersect");
                        //CiDyUtils.MarkPoint(intersectionPoint, 9999);
                        //Since there is an intersection.//Perturb the Vertices to there New Positions.
                        curVerts[curIndexB] = intersectionPoint;
                        nxtVerts[nxtIndexA] = intersectionPoint;
                    }

                }
                //Set Rotation back to Original Positions.
                turtle.position = combine[i].transform.GetColumn(3);
                turtle.rotation = QuaternionFromMatrix(combine[i].transform);
                Matrix4x4 worldToLocal = turtle.worldToLocalMatrix;

                for (int j = 0; j < curVerts.Length; j++)
                {
                    curVerts[j] = worldToLocal.MultiplyPoint3x4(curVerts[j]);
                }
                //Set Rotation back to Original Positions.
                turtle.position = combine[i + 1].transform.GetColumn(3);
                turtle.rotation = QuaternionFromMatrix(combine[i + 1].transform);
                worldToLocal = turtle.worldToLocalMatrix;

                for (int j = 0; j < nxtVerts.Length; j++)
                {
                    nxtVerts[j] = worldToLocal.MultiplyPoint3x4(nxtVerts[j]);
                }
                //Now set these back to there meshes.
                combine[i].mesh.vertices = curVerts;
                combine[i + 1].mesh.vertices = nxtVerts;
            }


            //Combine and Set Mesh Data
            Mesh extrudedMesh = new Mesh();
            extrudedMesh.CombineMeshes(combine);
            Mesh finalMesh = new Mesh();//Initialize

            if (meshCap != null)
            {
                //We have created the Core Extruded Mesh. But we have to add the Front and Back Cap Meshes.
                CombineInstance[] capCombine = new CombineInstance[3];
                //Now The First is Mesh Cap and Last is MeshCap, Middle is Extruded Mesh
                //Get First Direction Vector
                Vector3 curPos2 = lines[0];
                Vector3 nxtPos2 = lines[1];
                Vector3 lineDir2 = (nxtPos2 - curPos2).normalized;
                //Move Turtle to rotation and position
                turtle.position = curPos2;
                turtle.LookAt((turtle.position + (-lineDir2 * 1.618f)));
                //Set Mesh Cap A
                capCombine[0].mesh = meshCap;//Needs to Face Opposite Direction of First Line
                capCombine[0].transform = turtle.localToWorldMatrix;
                //Move Turtle to zero position again.
                turtle.position = holderObject.transform.position;
                turtle.rotation = Quaternion.identity;
                capCombine[1].mesh = extrudedMesh;//Extruded Center Mesh
                capCombine[1].transform = turtle.localToWorldMatrix;
                //Get First Direction Vector
                curPos2 = lines[lines.Length - 2];
                nxtPos2 = lines[lines.Length - 1];
                lineDir2 = (nxtPos2 - curPos2).normalized;
                //Move Turtle to rotation and position
                turtle.position = curPos2 + (lineDir2 * (meshLength * 2));
                turtle.LookAt((turtle.position + (lineDir2 * 1.618f)));
                capCombine[2].mesh = meshCap;//Needs to Face Opposite Direction of First Line
                capCombine[2].transform = turtle.localToWorldMatrix;

                finalMesh.CombineMeshes(capCombine);
            }
            else
            {
                finalMesh = extrudedMesh;
            }
            //Now set mesh to MeshFilter
            return finalMesh;
            /* mFilter.mesh = finalMesh;
             mRenderer.material = meshMat;*/
        }

        public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
        {
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }
        //linePnt - point the line passes through
        //lineDir - unit vector in direction of line, either direction works
        //pnt - the point to find nearest on line for
        public static Vector3 NearestPointOnLine(Vector3 linePnt, Vector3 lineDir, Vector3 pnt)
        {
            lineDir.Normalize();//this needs to be a unit vector
            var v = pnt - linePnt;
            var d = Vector3.Dot(v, lineDir);
            return linePnt + lineDir * d;
        }

#endregion
        //This function will Destroy an Array of GameObject's
        public static void DestroySpawned(GameObject[] objectArray)
        {
            if (objectArray != null && objectArray.Length > 0)
            {
                int length = objectArray.Length;
                for (int i = 0; i < length; i++)
                {
                    //Destroy
                    Object.Destroy(objectArray[i]);
                }
            }
        }

        //Check if Array of Transforms are Clockwise.
        public static bool IsClockwise(Transform[] vertices)
        {

            float sum = 0.0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v1 = vertices[i].position;
                Vector3 v2 = vertices[(i + 1) % vertices.Length].position;
                sum += (v2.x - v1.x) * (v2.z + v1.z);
            }
            return sum > 0.0f;
        }

        //Check if List of Vectors are Clockwise
        public static bool IsClockwise(List<Vector3> vertices)
        {

            float sum = 0.0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v1 = vertices[i];
                Vector3 v2 = vertices[(i + 1) % vertices.Count];
                sum += (v2.x - v1.x) * (v2.z + v1.z);
            }
            return sum > 0.0f;
        }
    }
}
