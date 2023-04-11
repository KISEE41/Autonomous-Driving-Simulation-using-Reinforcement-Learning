using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//clipperLib
using ClipperLib;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using StraightSkeletonNet;
using StraightSkeletonNet.Primitives;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Reflection;
#if VEGETATION_STUDIO_PRO || VEGETATION_STUDIO
using AwesomeTechnologies.Utility;
#endif

namespace CiDy
{
    public static class CiDyUtils
    {
        static Texture2D _whiteTexture;
        public static Texture2D WhiteTexture
        {
            get
            {
                if (_whiteTexture == null)
                {
                    _whiteTexture = new Texture2D(1, 1);
                    _whiteTexture.SetPixel(0, 0, Color.white);
                    _whiteTexture.Apply();
                }

                return _whiteTexture;
            }
        }

        public static void DrawScreenRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, WhiteTexture);
            GUI.color = Color.white;
        }

        public static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
        {
            // Top
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            // Left
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            // Right
            DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
            // Bottom
            DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        }

        public static Rect GetScreenRect(Vector3 screenPosition1, Vector3 screenPosition2)
        {
            // Move origin from bottom left to top left
            screenPosition1.y = Screen.height - screenPosition1.y;
            screenPosition2.y = Screen.height - screenPosition2.y;
            // Calculate corners
            var topLeft = Vector3.Min(screenPosition1, screenPosition2);
            var bottomRight = Vector3.Max(screenPosition1, screenPosition2);
            // Create Rect
            return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
        }

        public static Bounds GetViewportBounds(Camera camera, Vector3 screenPosition1, Vector3 screenPosition2)
        {
            var v1 = Camera.main.ScreenToViewportPoint(screenPosition1);
            var v2 = Camera.main.ScreenToViewportPoint(screenPosition2);
            var min = Vector3.Min(v1, v2);
            var max = Vector3.Max(v1, v2);
            min.z = camera.nearClipPlane;
            max.z = camera.farClipPlane;

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public static List<int> TriangulatePolygon(List<Vector3> polygon)
        {
            Debug.Log("TriangulatePolygon Vector3 Count: " + polygon.Count);
            List<int> tris = new List<int>(0);
            List<CiDyEdge> hullEdges = new List<CiDyEdge>(0);
            List<CiDyEdge> proposedEdges = new List<CiDyEdge>(0);

            int polyCount = polygon.Count;
            float bestAngle = 0f;//This will be the triangle for this edge with the largest(smallestAngle)
            int bestP2 = -1;//This will be set to the best p2 triangle.
                            //Setup Edges for Collision Testing
            for (int i = 0; i < polyCount; i++)
            {
                //Create an Edge list of all current Polygon Connections
                Vector3 p0 = polygon[i];
                Vector3 p1;
                if (i != polyCount - 1)
                {
                    //We are at the Beginning or middle
                    p1 = polygon[i + 1];
                }
                else
                {
                    //I is at the End
                    p1 = polygon[0];
                }
                //Create an Edge and Add to Hull.
                hullEdges.Add(new CiDyEdge(p0, p1));
            }
            //Lets iterate through the list and Calculate the Polygon.
            //Iterate through one edge at a time and test all possible triangles with this edge to another polygon point.
            for (int i = 0; i < polyCount; i++)
            {
                bestAngle = 0f;
                bestP2 = -1;//This will be set to the best p2 triangle.
                            //Grab points.
                int prevPoint = -1;//This tells us what point we just tested.
                int curPoint = i;//The current Point: P0 of test edge (P0-P1).
                int nxtPoint = -1;//The next Point: P1 of test edge (P0-P1).
                int origP2 = -1;//This will be set once per edge Test Cycle.
                int dynamicPoint = -1;//This will be a dynamic point we use to test multiple Triangulations for testEdge.

                //Set points based on current I Location in list.
                if (i == 0)
                {
                    //i is @ the Start of the list.
                    prevPoint = polyCount - 1;
                    nxtPoint = i + 1;
                }
                else if (i != 0 && i != polyCount - 1)
                {
                    //i is in the middle.
                    prevPoint = i - 1;
                    nxtPoint = i + 1;
                }
                else if (i == polyCount - 1)
                {
                    //i is @ the End of the list.
                    prevPoint = i - 1;
                    nxtPoint = 0;
                }
                origP2 = nxtPoint;
                origP2 = FindNextInList(origP2, polyCount);
                //Debug.Log("OrigP2: "+origP2);
                //Now that we know what p1 is we can determine the dynamic point
                dynamicPoint = nxtPoint;//Initial set
                                        //Now lets find the first potential Triangle for the curEdge(curPoint-nxtPoint).
                Vector3 p0 = polygon[curPoint];//Set P0
                Vector3 p1 = polygon[nxtPoint];//Set p1
                Vector3 p2 = Vector3.zero;//Set Initial DynamicPoint
                                          //Debug.Log("CurPoint: "+curPoint+" NxtPoint: "+nxtPoint+" Initial DynamicPoint: "+dynamicPoint);
                                          //Now that we have our points Lets test all Potential Triangles for this edge.
                while (p2 == Vector3.zero)
                {
                    //yield return new WaitForSeconds(delay);
                    //Debug.Log("p2 == Vector3.Zero Finding New p2");
                    dynamicPoint = FindNextInList(dynamicPoint, polyCount);//This will return the next place in the list weather i+1 or 0.
                                                                           //Debug.Log("Picked Dynamic: "+dynamicPoint);
                                                                           //Make sure we move on to the next edge when we return the prevPoint.
                    if (dynamicPoint == prevPoint)
                    {
                        //Debug.LogError("Returned to PrevPoint Move to Next Edge");
                        //If BestP2 is found. :)
                        if (bestP2 != -1)
                        {
                            //Add these Edges. :)
                            //Back Edge
                            //Debug.Log("Adding Back Edge "+curPoint+" - "+bestP2);
                            CiDyEdge edgeA = new CiDyEdge(p0, polygon[bestP2]);
                            //AddEdge(edgeA);
                            proposedEdges.Add(edgeA);
                            hullEdges.Add(edgeA);
                            //If we are not equal to the origP2 then add the Front Edge As Well. :)
                            if (bestP2 != origP2)
                            {
                                //Debug.Log("Add Front Edge "+nxtPoint+" - "+bestP2);
                                //Front Edge
                                CiDyEdge edgeB = new CiDyEdge(p1, polygon[bestP2]);
                                //AddEdge(edgeB);
                                proposedEdges.Add(edgeB);
                                hullEdges.Add(edgeB);
                            }
                            else
                            {
                                //No back edge also means this triangle is apart of the next edge as well.
                                i++;
                            }
                            /*tris.Add(curPoint);
                            tris.Add(nxtPoint);
                            tris.Add(bestP2);*/
                            tris.Add(bestP2);
                            tris.Add(nxtPoint);
                            tris.Add(curPoint);
                        }
                        //There are no more triangles to test against
                        break;
                    }
                    //Make sure dynamic Point is not reflex
                    Vector3 fwd = (p1 - p0);
                    Vector3 targetDir = (polygon[dynamicPoint] - p0);
                    int angleDir = AngleDir(fwd, targetDir, Vector3.up);
                    //Debug.Log("AngleDir: "+angleDir);
                    if (angleDir != -1)
                    {
                        //Debug.Log("Clear P2 to find another one");
                        //This is not a usable Point/Triangle find another point
                        p2 = Vector3.zero;
                    }
                    else
                    {
                        //Debug.Log("Set Triangle P2: "+dynamicPoint);
                        //We also want to track the lowestAngle per Triangle
                        float lowestAngle = Mathf.Infinity;//Start infinite so anything first found will be less.
                                                           //Set p2
                        p2 = polygon[dynamicPoint];
                        //Make sure that Proposed Point has line of sight to both P0 & P1.
                        if (!IntersectsList(p0, p2, hullEdges) && !IntersectsList(p1, p2, hullEdges))
                        {
                            //Now that p2 is set. Test Triangle (p0-p1-p2);
                            //Find the most proportianate Angles.
                            //Find Anlge For corner p0.
                            int lastAngle = 180;//Initial used to find third
                            targetDir = (p2 - p0);
                            fwd = (p1 - p0);
                            int curAngle = Mathf.RoundToInt(Vector3.Angle(targetDir, fwd));
                            if (curAngle < lowestAngle)
                            {
                                //This is now the lowest Angle
                                lowestAngle = curAngle;
                            }
                            lastAngle -= curAngle;
                            //Debug.Log("P0 Angle "+curAngle);
                            //Now determine secondAngle
                            targetDir = (p2 - p1);
                            fwd = (p0 - p1);
                            curAngle = Mathf.RoundToInt(Vector3.Angle(targetDir, fwd));
                            if (curAngle < lowestAngle)
                            {
                                //This is now the lowest Angle
                                lowestAngle = curAngle;
                            }
                            //Debug.Log("P1 Angle "+curAngle);
                            //Now determine last angle from 180-the first two
                            lastAngle -= curAngle;//Now last Angle should be the final
                                                  //Debug.Log("P2 Angle "+lastAngle);
                            if (lastAngle < lowestAngle)
                            {
                                //This is now the lowest Angle
                                lowestAngle = lastAngle;
                            }
                            //Debug.Log("LowestAngle: "+lowestAngle+" BestAngle: "+bestAngle+" BestP2: "+bestP2);
                            if (lowestAngle > bestAngle)
                            {
                                //Debug.Log("Dynamic: "+dynamicPoint+" ");
                                //This triangles smallest Angle is larger than the currentBest. Update it. :)
                                bestAngle = lowestAngle;
                                bestP2 = dynamicPoint;
                            }
                        }
                        //Test Next Triangle now.
                        p2 = Vector3.zero;
                    }
                    //yield return new WaitForSeconds(delay);
                }
                //
            }
            return tris;
        }

        //This will determine what the next point in the list would be based on the starting point.
        static int FindNextInList(int startPoint, int LastPoint)
        {
            int newPoint = -1;//Default is -1;
                              //Determine if the startpoint is equal to lastPoint
            if (startPoint == LastPoint - 1)
            {
                //Return 0
                newPoint = 0;
            }
            else
            {
                //Grab next in line
                newPoint = startPoint + 1;
            }

            return newPoint;
        }

        static bool IntersectsList(Vector3 p0, Vector3 p1, List<CiDyEdge> edges)
        {
            Vector3 intersection = Vector3.zero;
            //visualObjects.Add(CiDyUtils.MarkPoint(p0,200));
            //visualObjects.Add(CiDyUtils.MarkPoint(p1,300));
            //Make sure that this line p0-p1 doesnt intersect any line in points list.
            for (int i = 0; i < edges.Count; i++)
            {
                Vector3 n0 = edges[i].pos1;
                Vector3 n1 = edges[i].pos2;
                //visualObjects.Add(CiDyUtils.MarkPoint(n0,0+i));
                //visualObjects.Add(CiDyUtils.MarkPoint(n1,1+i));
                //Now that we have our line lets test against the p0-p1 line.
                if (LineIntersection(p0, p1, n0, n1, ref intersection))
                {
                    //visualObjects.Add(CiDyUtils.MarkPoint(intersection,500+i));
                    //Make sure the intersection is not equal to any of the four points
                    if (!SameVector3s(intersection, p0) && !SameVector3s(intersection, p1) && !SameVector3s(intersection, n0) && !SameVector3s(intersection, n1))
                    {
                        //visualObjects.Add(CiDyUtils.MarkPoint(intersection, 999+i));
                        //This does intersect
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IntersectsList(Vector3 p0, Vector3 p1, List<Vector3> polygon, ref Vector3 intersection, bool loopEnd)
        {
            //CiDyUtils.MarkPoint(p0,200);
            //CiDyUtils.MarkPoint(p1,300);
            //Make sure that this line p0-p1 doesnt intersect any line in points list.
            for (int i = 0; i < polygon.Count; i++)
            {
                if (!loopEnd && i == polygon.Count - 1)
                {
                    //End
                    continue;
                }
                Vector3 n0 = polygon[i];
                Vector3 n1;
                if (i == polygon.Count - 1)
                {
                    n1 = polygon[0];
                }
                else
                {
                    n1 = polygon[i + 1];
                }
                //CiDyUtils.MarkPoint(n0,0+i);
                //CiDyUtils.MarkPoint(n1,1+i);
                //Now that we have our line lets test against the p0-p1 line.
                if (LineIntersectionUnRounded(p0, p1, n0, n1, ref intersection))
                {
                    //visualObjects.Add(CiDyUtils.MarkPoint(intersection,500+i));
                    //Make sure the intersection is not equal to any of the four points
                    //if(!SameVector3s(intersection,p0) && !SameVector3s(intersection,p1) && !SameVector3s(intersection,n0) && !SameVector3s(intersection,n1)){
                    //visualObjects.Add(CiDyUtils.MarkPoint(intersection, 999+i));
                    //This does intersect
                    return true;
                    //}
                }
            }
            return false;
        }

        public static bool IntersectsList(int proposedRouteId, int maxRouteId, Vector3 p0, Vector3 p1, List<CiDyRoute> route, ref Vector3 intersection)
        {
            //Make sure that this line p0-p1 doesnt intersect any line in points list.
            for (int j = 0; j < route.Count; j++)
            {
                //Current Id?
                int activeId = route[j].routeId;
                //Debug.Log(proposedRouteId+"_Testing Intersection Against: " + activeId);
                if (proposedRouteId == maxRouteId || proposedRouteId == activeId) {
                    //Debug.Log("Skip Route: " + proposedRouteId);
                    //Skip as this is the Same id, and we do not care if they intersect. Or its the Last Lane, Which means all lanes are lower and dont care if we cross.
                    continue;
                }
                //Special Case of maxRouteId = 2
                if (maxRouteId == 2 && proposedRouteId > activeId) {
                    //Degen event for 3 lane Middle route heading into two lane
                    continue;
                }
                for (int i = 0; i < route[j].waypoints.Count-1; i++)
                {
                    Vector3 n0 = route[j].waypoints[i];
                    Vector3 n1 = route[j].waypoints[i+1];
                    //CiDyUtils.MarkPoint(n0,0+i);
                    //CiDyUtils.MarkPoint(n1,1+i);
                    //Now that we have our line lets test against the p0-p1 line.
                    if (LineIntersectionUnRounded(p0, p1, n0, n1, ref intersection))
                    {
                        //This does intersect
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool FindInterpolatedPointInList(Vector3 p0, Vector3 p1, Vector3[] polygon, ref Vector3 intersection)
        {
            //CiDyUtils.MarkPoint(p0,200);
            //CiDyUtils.MarkPoint(p1,300);
            //Make sure that this line p0-p1 doesnt intersect any line in points list.
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector3 n0 = polygon[i];
                Vector3 n1;
                if (i == polygon.Length - 1)
                {
                    n1 = polygon[0];
                }
                else
                {
                    n1 = polygon[i + 1];
                }
                //CiDyUtils.MarkPoint(n0,0+i);
                //CiDyUtils.MarkPoint(n1,1+i);
                //Now that we have our line lets test against the p0-p1 line.
                if (LineIntersection(p0, p1, n0, n1, ref intersection))
                {
                    /*float xValue = Mathf.InverseLerp(n0.x, n1.x, intersection.x);
                    float zValue = Mathf.InverseLerp(n0.z, n1.z, intersection.z);
                    xValue = Mathf.Round(xValue * 100) / 100;
                    zValue = Mathf.Round(zValue * 100) / 100;
                    float interpolate = Mathf.Max(xValue, zValue);
                    intersection = Vector3.Lerp(n0, n1, interpolate);
                    intersection.y = Mathf.Min(n0.y, n1.y);*/
                    //Now that I have the Intersection. I want to know its distance in relation to an end point.
                    intersection.y = n0.y;
                    float totalDist = Vector3.Distance(n0, n1);
                    float dist = Vector3.Distance(intersection, n0);
                    float interpolate = dist / totalDist;
                    intersection = Vector3.Lerp(n0, n1, interpolate);
                    //Use Dist
                    //Do narrow down
                    //This does intersect
                    return true;
                }
            }
            return false;
        }

        //This Function will Create a 2D Bounds around any Polyline(Closed || Open)
        public static Bounds Create2DBoundsFromPolyLine(List<Vector3> polyline)
        {
            //Determine the Highest (x,z) Combo and Lowest(x,z) Combo
            //Iterate through list and find Highest point.
            //List<Vector3> poly = new List<Vector3> (0);
            Vector3 lowestX = new Vector3(999, 0, 999);
            Vector3 highestX = new Vector3(-999, 0, -999);
            Vector3 lowestZ = new Vector3(999, 0, 999);
            Vector3 highestZ = new Vector3(-999, 0, -999);
            for (int i = 0; i < polyline.Count; i++)
            {
                Vector3 v0 = polyline[i];
                if (v0.x < lowestX.x)
                {
                    lowestX = v0;
                }
                if (v0.x > highestX.x)
                {
                    highestX = v0;
                }
                if (v0.z < lowestZ.z)
                {
                    lowestZ = v0;
                }
                if (v0.z > highestZ.z)
                {
                    highestZ = v0;
                }
            }
            Vector3 center = FindCentroid(polyline);
            Bounds newBounds = new Bounds(center, Vector3.zero);
            newBounds.SetMinMax(lowestX, highestZ);
            newBounds.Encapsulate(lowestZ);
            newBounds.Encapsulate(highestX);
            //Return Created Bounds
            return newBounds;
        }

        //Returns true if any intersection between the two cyclic arrays occur( The arrays cannot share any points otherwise will always return true)
        public static bool BoundsIntersect(Vector3[] extBoundary, Vector3[] interiorBound)
        {
            //Vector3 intersectionVector = Vector3.zero;
            for (int i = 0; i < extBoundary.Length; i++)
            {
                Vector3 p0 = extBoundary[i];
                Vector3 p1;
                if (i == extBoundary.Length - 1)
                {
                    p1 = extBoundary[0];
                }
                else
                {
                    p1 = extBoundary[i + 1];
                }
                for (int j = 0; j < interiorBound.Length; j++)
                {
                    //Now that we have a line. Lets test this line against the Interior Points. if any interior points are on the right side of this line it is outside the bounds and we must return true
                    float r = 0;//Center Line R Value
                    float s = 0;//Center Line S Value
                    CiDyUtils.DistanceToLineR(interiorBound[j], p0, p1, ref r, ref s);
                    //Inside Line Area.
                    if (s > 0)
                    {
                        //Point is outside of bondary. The two polygons must either intersection or the Interior is larget than the Exterior boundary
                        return true;
                    }
                }
            }
            //No Intersection Detected
            return false;
        }

        public static bool BoundsIntersectOverload(Vector3[] extBoundary, Vector3[] interiorBounds)
        {
            //SubDivide Interior Bounds to Account for the Middle of there Straight Lines.
            List<Vector3> subDividedBound = new List<Vector3>(0);

            Vector3 newPoint = Vector3.zero;
            //Sub Divide Bound list
            for (int i = 0; i < interiorBounds.Length; i++)
            {
                //Add Current
                subDividedBound.Add(interiorBounds[i]);
                //Add Nxt
                if (i == interiorBounds.Length - 1)
                {
                    //End Loop
                    newPoint = (interiorBounds[i] + interiorBounds[0]) / 2;
                    subDividedBound.Add(newPoint);
                }
                else
                {
                    //Middle , grab Nxt
                    newPoint = (interiorBounds[i] + interiorBounds[i + 1]) / 2;
                    subDividedBound.Add(newPoint);
                }
            }
            //Check if they overlap anywhere
            for (int i = 0; i < subDividedBound.Count; i++)
            {
                if (!CiDyUtils.PointInsideOrOnLinePolygon(extBoundary, subDividedBound[i]))
                {
                    return true;
                }
            }

            return false;
        }

        //Returns true if any intersection between the two cyclic arrays occur( The arrays cannot share any points otherwise will always return true)
        public static bool BoundsIntersect(Vector3[] bounds, GameObject[] buildings)
        {
            Vector3 intersectionVector = Vector3.zero;
            //Iterate through All Buildings and Calculate there Bounds. Then Test there bounds List against ours
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null)
                {
                    continue;
                }
                //To Get proper Rotated bounds we need to Set the Building to Zero and Zero Rotation then back.
                Vector3 storedPos = buildings[i].transform.position;
                Quaternion storedRot = buildings[i].transform.rotation;
                //Reset back to Zero
                buildings[i].transform.position = Vector3.zero;
                buildings[i].transform.rotation = Quaternion.identity;
                //Prefab Bounds of All Sub Mesh Renderers
                var combinedBounds = buildings[i].GetComponentInChildren<Renderer>().bounds;
                var renderers = buildings[i].GetComponentsInChildren<Renderer>();
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
                //Position and Rotate Back
                buildings[i].transform.position = storedPos;
                buildings[i].transform.rotation = storedRot;
                //Extract bounds
                Vector3[] boundFootPrint = new Vector3[4];
                boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
                boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
                boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left
                boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
                                                                                                  //Translate based on Transform Directions
                for (int j = 0; j < boundFootPrint.Length; j++)
                {
                    boundFootPrint[j] = buildings[i].transform.TransformPoint(boundFootPrint[j]);
                }
                //Now that we have the Bounds. Lets Test if they intesrsect at All.
                if (SATRectangleRectangle(bounds, boundFootPrint))
                {
                    return true;
                }
            }
            //No Intersection Detected
            return false;
        }

        //Find out if 2 rectangles with orientation are intersecting by using the SAT algorithm
        public static bool SATRectangleRectangle(Vector3[] r1, Vector3[] r2)
        {
            bool isIntersecting = false;

            //We have just 4 normals because the other 4 normals are the same but in another direction
            //So we only need a maximum of 4 tests if we have rectangles
            //It is enough if one side is not overlapping, if so we know the rectangles are not intersecting

            //Test 1
            Vector3 normal1 = GetNormal(r1[1], r1[2]);

            if (!IsOverlapping(normal1, r1, r2))
            {
                //No intersection is possible!
                return isIntersecting;
            }

            //Test 2
            Vector3 normal2 = GetNormal(r1[0], r1[1]);

            if (!IsOverlapping(normal2, r1, r2))
            {
                return isIntersecting;
            }

            //Test 3
            Vector3 normal3 = GetNormal(r2[1], r2[2]);

            if (!IsOverlapping(normal3, r1, r2))
            {
                return isIntersecting;
            }

            //Test 4
            Vector3 normal4 = GetNormal(r2[0], r2[1]);

            if (!IsOverlapping(normal4, r1, r2))
            {
                return isIntersecting;
            }

            //If we have come this far, then we know all sides are overlapping
            //So the rectangles are intersecting!
            isIntersecting = true;

            return isIntersecting;
        }

        //Is this side overlapping?
        private static bool IsOverlapping(Vector3 normal, Vector3[] r1, Vector3[] r2)
        {
            bool isOverlapping = false;

            //Project the corners of rectangle 1 onto the normal
            float dot1 = DotProduct(normal, r1[1]);
            float dot2 = DotProduct(normal, r1[0]);
            float dot3 = DotProduct(normal, r1[2]);
            float dot4 = DotProduct(normal, r1[3]);

            //Find the range
            float min1 = Mathf.Min(dot1, Mathf.Min(dot2, Mathf.Min(dot3, dot4)));
            float max1 = Mathf.Max(dot1, Mathf.Max(dot2, Mathf.Max(dot3, dot4)));


            //Project the corners of rectangle 2 onto the normal
            float dot5 = DotProduct(normal, r2[1]);
            float dot6 = DotProduct(normal, r2[0]);
            float dot7 = DotProduct(normal, r2[2]);
            float dot8 = DotProduct(normal, r2[3]);

            //Find the range
            float min2 = Mathf.Min(dot5, Mathf.Min(dot6, Mathf.Min(dot7, dot8)));
            float max2 = Mathf.Max(dot5, Mathf.Max(dot6, Mathf.Max(dot7, dot8)));


            //Are the ranges overlapping?
            if (min1 <= max2 && min2 <= max1)
            {
                isOverlapping = true;
            }

            return isOverlapping;
        }

        //Get the normal from 2 points. This normal is pointing left in the direction start -> end
        //But it doesn't matter in which direction the normal is pointing as long as you have the same
        //algorithm for all edges
        private static Vector3 GetNormal(Vector3 startPos, Vector3 endPos)
        {
            //The direction
            Vector3 dir = endPos - startPos;

            //The normal, just flip x and z and make one negative (don't need to normalize it)
            Vector3 normal = new Vector3(-dir.z, dir.y, dir.x);

            //Draw the normal from the center of the rectangle's side
            //Debug.DrawRay(startPos + (dir * 0.5f), normal.normalized * 2f, Color.red);

            return normal;
        }

        //Get the dot product
        //p - the vector we want to project
        //u - the unit vector p is being projected on
        //proj_p_on_u = Vector3.Dot(p, u) * u;
        //But we only need to project a point, so just Vector3.Dot(p, u)
        private static float DotProduct(Vector3 v1, Vector3 v2)
        {
            //2d space
            float dotProduct = v1.x * v2.x + v1.z * v2.z;

            return dotProduct;
        }
        //Special Concat function for curves. Skip first point in next list as this is duplicat of last of current List.
        //This will simply take a list and add a list to the end of the first list
        public static List<Vector3> ConcatCurve(List<Vector3> list1, List<Vector3> list2)
        {
            for (int i = 1; i < list2.Count; i++)
            {
                list1.Add(list2[i]);
            }
            return list1;
        }

        //Create B-Spline out of Referenced Vector3 List
        public static List<Vector3> CreateBSpline(List<Vector3> knots, int segmentLength)
        {
            //SegmentLength The lenght we will split the segments into (Meters)
            //Make sure we have at least two points.0th degree.
            if (knots.Count <= 1)
            {
                Debug.LogError("Cannot Create a B-Spline out of <2 points!");
                return new List<Vector3>();
            }
            //float t = 0.0f;
            //int iterations = knots.Count;
            //Vector3 p = new Vector3(0,0,0);
            //We want to find the control points for these Knot Points.
            //To do this we run a special Middle point algorithm on the Knots and superimopse the scaled point placement in relation to knots to create
            //perfect control points for a close curve to the knots polygon pattern.
            //Iterate through the list two points at a time. To determine there control points.
            List<Vector3> cp = new List<Vector3>();
            List<Vector3> aList = new List<Vector3>();
            if (knots.Count == 2)
            {
                return knots;
            }
            //Setup List with proper control Points for the Knots
            for (int i = 0; i < knots.Count - 1; i++)
            {
                //Find middle point between this knot and the next
                Vector3 m0 = ((knots[i] + knots[i + 1]) / 2);
                aList.Add(m0);
                //Check for when we are at our control desired control points.
                if (aList.Count == 2)
                {
                    //There is a control point here. Detemine the Middle Point.
                    Vector3 m1 = ((aList[0] + aList[1]) / 2);
                    //Find direction from m1-knots[i];
                    Vector3 dir = (knots[i] - m1);
                    //Now move cp Points in direction.
                    //Add control points to cp List
                    if (i == 1)
                    {
                        //	Debug.Log("Added "+knots[i-1]);
                        cp.Add(knots[i - 1]);
                    }
                    cp.Add(aList[0] + dir);
                    cp.Add(knots[i]);
                    cp.Add(aList[1] + dir);
                    if (i == knots.Count - 2)
                    {
                        //Add the End
                        cp.Add(knots[i + 1]);
                    }
                    //Remove the first from the Alist and contiue process if paired again.
                    aList.RemoveAt(0);
                }
            }
            //Add Open End Null Duplicates
            //We need to duplicate the end points for closure. :)
            cp.Insert(0, knots[0]);
            cp.Add(knots[knots.Count - 1]);
            //Now that we have our Bezier Points lets run the bezier algorithm on the needed points.
            //Iterate through the List four points at a time creating and storing the bezier points returned into a Bezier Spline.
            List<Vector3> finalP = new List<Vector3>();//The Full BSpline Vector3 List
            /*for(int i = 0;i<cp.Count-3;i+=3){
                //Create curve out of every four points.
                List<Vector3> newCurve = new List<Vector3>();
                //Debug.Log("Adding "+cp[i]+" : "+cp[i+1]+" : "+cp[i+2]+" : "+cp[i+3]);
                newCurve.Add(cp[i]);
                newCurve.Add(cp[i+1]);
                newCurve.Add(cp[i+2]);
                newCurve.Add(cp[i+3]);
                //Create Curve and add it to final List
                newCurve = CiDyUtils.CreateBezier(newCurve, segmentLength);
                finalP = CiDyUtils.ConcatCurve(finalP,newCurve);
            }*/
            finalP = CreateBezier(cp, segmentLength);
            //Debug.Log ("Returned BSpline Cnt: " + finalP.Count);
            return finalP;
        }

        //Create Bezier Curve out of Referenced Vector3 List
        public static List<Vector3> CreateBezier(List<Vector3> origPoints, int segments)
        {
            float t = 0.0f;
            int iterations = origPoints.Count - 1;
            List<Vector3> newPoints = new List<Vector3>();
            List<Vector3> finalP = new List<Vector3>();
            Vector3 p = new Vector3(0, 0, 0);
            //Determine total distance between points
            float totalDist = FindTotalDistOfPoints(origPoints);
            //Have a Segment for every 4 meters in total distance
            float bSegments = Mathf.Round(totalDist / segments);
            //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = FindP(origPoints, t);
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
            //Debug.Log ("Bezier Curve Returned " + finalP.Count);
            //Add last Point of Orig Points to Curve
            //finalP.Add (origPoints [iterations]);
            return finalP;
        }

        ////BEZIER(Optimized?? )
        //Create Bezier Curve out of Referenced Vector3 List
        public static Vector3[] CreateBezier(Vector3[] origPoints, float segments)
        {
            //Check for Degen event where there is less than 3 points.
            if (origPoints.Length <= 2)
            {
                return origPoints;
            }
            float t = 0.0f;
            int iterations = origPoints.Length - 1;
            Vector3 p = new Vector3(0, 0, 0);
            Vector3[] newPoints;
            //Determine total distance between points
            float totalDist = FindTotalDistOfPoints(origPoints);
            //Have a Segment for every segmentLength in unity meters in total distance
            float bSegments = Mathf.Round(totalDist / segments);
            Vector3[] finalP = new Vector3[(int)bSegments + 1];
            //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = new Vector3[origPoints.Length];
                newPoints = FindP(t, origPoints);
                //Are there two points?
                if (newPoints.Length > 1)
                {
                    //Make iterations until we have our true Position on the path.
                    for (int h = 0; h < iterations; h++)
                    {
                        //Call a Function to find p.
                        newPoints = FindP(t, newPoints);
                        if (newPoints.Length == 1)
                        {
                            //Update P 
                            p = newPoints[0];
                            //End iterations
                            break;
                        }
                    }
                }
                //Update BezierPath
                finalP[j] = p;
            }
            //Return final
            return finalP;
        }

        //Create Bezier Curve out of Referenced Vector3 List
        public static Vector3[] CreateBezier(Vector3[] origPoints)
        {
            float t = 0.0f;
            int iterations = origPoints.Length - 1;
            List<Vector3> newPoints = new List<Vector3>();
            List<Vector3> finalP = new List<Vector3>();
            Vector3 p = new Vector3(0, 0, 0);
            //Determine total distance between points
            //float totalDist = FindTotalDistOfPoints(origPoints);
            //Have a Segment for every 6 meters in total distance
            float bSegments = 4;// Mathf.Round(totalDist / 6.8f);
                                //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = FindP(origPoints, t);
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
            //Debug.Log ("Bezier Curve Returned " + finalP.Count);
            //Add last Point of Orig Points to Curve
            //finalP.Add (origPoints [iterations]);
            return finalP.ToArray();
        }

        //Overload Method
        //Create Bezier Curve out of Referenced Vector3 List
        public static Vector3[] CreateBezier(Vector3[] origPoints, int segments, int maxSegments)
        {
            float t = 0.0f;
            int iterations = origPoints.Length - 1;
            List<Vector3> newPoints = new List<Vector3>();
            List<Vector3> finalP = new List<Vector3>();
            Vector3 p = new Vector3(0, 0, 0);
            //Determine total distance between points
            float totalDist = FindTotalDistOfPoints(origPoints);
            /*if (segments > maxSegments) {
                segments = maxSegments;
            }*/
            //Have a Segment for every 4 meters in total distance
            float bSegments = Mathf.Round(totalDist / 6);
            /*if (bSegments > maxSegments) {
                bSegments = Mathf.Round(maxSegments);
            }*/
            //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = FindP(origPoints, t);
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
            //Debug.Log ("Bezier Curve Returned " + finalP.Count);
            //Add last Point of Orig Points to Curve
            //finalP.Add (origPoints [iterations]);
            return finalP.ToArray();
        }

        //Overload(intput Vecotr3[] returned List<Vector3>
        //Create Bezier Curve out of Referenced Vector3 List
        public static List<Vector3> CreateBezier(float segments, Vector3[] origPoints)
        {
            float t = 0.0f;
            int iterations = origPoints.Length - 1;
            List<Vector3> newPoints = new List<Vector3>();
            List<Vector3> finalP = new List<Vector3>();
            Vector3 p = new Vector3(0, 0, 0);
            //Determine total distance between points
            float totalDist = FindTotalDistOfPoints(origPoints);
            //Have a Segment for every 4 meters in total distance
            float bSegments = Mathf.Round(totalDist / segments);
            //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = FindP(origPoints, t);
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
            //Debug.Log ("Bezier Curve Returned " + finalP.Count);
            //Add last Point of Orig Points to Curve
            //finalP.Add (origPoints [iterations]);
            return finalP;
        }

        //CatMull-Rom Spline
        //This Function will Create A CatMullRom
        public static Vector3[] CreateCatMullSpline(Vector3[] origPoints)
        {
            //Iterate through Array and Determine CatMull-Rom Splines.
            //To Determine how many points will be in the output from the Input. 10 for the first 4 points and 10 for any additional point after.
            int splineInt = 10 * (origPoints.Length - 4) + 11;//(First Four Points will create 10 points. Any Additional Point after will create 10.)
            Vector3[] spline = new Vector3[splineInt];
            //Reuse CurveInt for tracking what point we are adding.
            splineInt = 0;
            //Iterate through Array and Calculate CatMullRom Point
            for (int i = 0; i < origPoints.Length; i++)
            {
                //Cant draw between the endpoints
                //Neither do we need to draw from the second to the last endpoint
                //...if we are not making a looping line
                if (i == 0 || i == origPoints.Length - 2 || i == origPoints.Length - 1)
                {
                    //Add Last Point at End of Line
                    if (i == origPoints.Length - 1)
                    {
                        spline[splineInt] = origPoints[origPoints.Length - 2];
                    }
                    continue;
                }
                //Calculate CatMull-Rom using four Vector3 points.
                //Clamp to allow looping
                Vector3 p0 = origPoints[i - 1];
                Vector3 p1 = origPoints[i];
                Vector3 p2 = origPoints[i + 1];
                Vector3 p3 = origPoints[i + 2];

                //t is always between 0 and 1 and determines the resolution of the spline
                //0 is always at p1
                for (float t = 0; t < 1; t += 0.1f)
                {
                    //Find the coordinates between the control points with a Catmull-Rom spline
                    Vector3 newPos = ReturnCatmullRom(t, p0, p1, p2, p3);
                    spline[splineInt] = newPos;
                    splineInt++;
                }
            }
            //Debug.Log("Curve Amount Returned: " + spline.Length + " Input Count: " + origPoints.Length);
            return spline;
        }

        //Returns a position between 4 Vector3 with Catmull-Rom Spline algorithm
        //http://www.iquilezles.org/www/articles/minispline/minispline.htm
        static Vector3 ReturnCatmullRom(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 a = 0.5f * (2f * p1);
            Vector3 b = 0.5f * (p2 - p0);
            Vector3 c = 0.5f * (2f * p0 - 5f * p1 + 4f * p2 - p3);
            Vector3 d = 0.5f * (-p0 + 3f * p1 - 3f * p2 + p3);

            Vector3 pos = a + (b * t) + (c * t * t) + (d * t * t * t);

            return pos;
        }

        //This does not work on a Looped Spline, unless the last point is the first point.
        public static Vector3[] CreateTrafficWaypoints(Vector3[] path, float waypointDist, bool skipFirstPoint = false, bool skipLastPoint = true) {
            if (path == null || path.Length == 0)
            {
                Debug.LogWarning("CreateTrafficWaypoints---Path is Empty?");
                return path;
            }
            //Create Tmp array
            List<Vector3> tmpObjectArray = new List<Vector3>(0);
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
                    //Place Point
                    actualLastPoint = lastLightPoint;
                    if (!skipFirstPoint)
                    {
                        tmpObjectArray.Add(lastLightPoint);
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
                        if (lightsCurDist >= waypointDist)
                        {
                            //Place Point
                            //Place Light nxtToCurb End. Reuse GameObject Memory
                            tmpObjectArray.Add(lastLightPoint);
                            actualLastPoint = lastLightPoint;
                            //Reset Distance Moved.
                            lightsCurDist = 0f;
                        }
                    }
                }
                //Set Last one if its at least 2/3 the Desired Distance.
                if (j == path.Length - 2)
                {
                    //Calculate Distance Between Cur and P1
                    moveDist = Vector3.Distance(p1, actualLastPoint);
                    if (moveDist >= (waypointDist * 0.5f))
                    {
                        //Always Place at End Point.
                        lastLightPoint = p1;
                        //Place Point
                        //Place Light nxtToCurb End. Reuse GameObject Memory
                        tmpObjectArray.Add(lastLightPoint);
                        actualLastPoint = lastLightPoint;
                    }
                }
            }
            //Add Last Point
            if (!skipLastPoint)
            {
                tmpObjectArray.Add(path[path.Length - 1]);
            }
            //Set Back to Stored ObjectArray
            return tmpObjectArray.ToArray();
        }

        public static float FindTotalDistOfPoints(List<Vector3> points)
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
        //OverLoad 
        public static float FindTotalDistOfPoints(Vector3[] points)
        {
            float totalDist = 0;
            //Iterate through array looking at two at a time totaling the distance.
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                float dist = Vector3.Distance(a, b);
                totalDist += dist;
            }
            return totalDist;
        }

        //New Line Match Logic. (Visualize Sphere Moving Along Line and Creating new Points as it goes.
        public static List<Vector3> MatchLine(List<Vector3> lineA, List<Vector3> lineB)
        {
            //Debug.Log ("MatchLine LineACnt: " + lineA.Count+" LineBCnt: "+lineB.Count+" SegmentLength: "+segmentLength);
            //int desiredAmount = lineB.Count;
            List<Vector3> finalLine = new List<Vector3>(0);
            Vector3 intersection = Vector3.zero;

            float distTraveled = 0;
            int desiredSegment = -1;

            //float thisLength = FindTotalDistOfPoints(lineA);
            //Debug.Log("Total Length: "+ thisLength);
            for (int i = 0; i < lineA.Count - 1; i++)
            {
                distTraveled = 0;
                //Start and End Position
                Vector3 startPosition = lineA[i];
                Vector3 endPosition = lineA[i + 1];
                //Total Length
                float totalLength = Vector3.Distance(startPosition, endPosition);
                //Store Last Position for Tracking
                Vector3 lastPos = startPosition;
                //Start First point
                finalLine.Add(lineA[i]);
                //Debug.Log("Added Point: "+i);

                float lastSegLength = 0;

                while (true)
                {
                    //How far do we want to move to match lineB Segment
                    desiredSegment++;
                    //Debug.Log("New Segment Picked: "+desiredSegment);
                    if (desiredSegment >= lineB.Count - 1)
                    {
                        //We are at the End of Line B end Loops
                        //Debug.Log("End of Line B");
                        break;
                    }
                    //If here then there is still room in line B. Lets move our Point up if there is space on this segment.
                    Vector3 bA = lineB[desiredSegment];
                    Vector3 bB = lineB[desiredSegment + 1];
                    float segLength = Vector3.Distance(bA, bB);
                    //Debug.Log("Desired Segment Length: "+segLength);
                    if ((totalLength - distTraveled) > segLength)
                    {
                        //We have the room to create a new point.
                        lastSegLength += segLength;
                        float fracJourney = lastSegLength / totalLength;
                        Vector3 curPos = Vector3.Lerp(startPosition, endPosition, fracJourney);
                        finalLine.Add(curPos);
                        //Expensive Calculation but returns Accurate reading
                        distTraveled += Vector3.Distance(lastPos, curPos);
                        //Update Last Distance Traveled
                        lastPos = curPos;
                        //Debug.Log("Created New Point: "+distTraveled);
                    }
                    else
                    {
                        //We do not have enough room to cut out a segment.
                        //Debug.Log("We do not have enough room left, Remaining Length: "+(totalLength-distTraveled) + " DistTraveled: "+distTraveled+" SegLength: "+segLength);
                        break;
                    }
                }
            }
            //Add Last Point.
            finalLine.Add(lineA[lineA.Count - 1]);
            //Return Final Line
            return finalLine;
        }

        //Returns List<Vector3> from Segments from ordered List of Vector3s using linear Interpolation
        public static List<Vector3> FindP(List<Vector3> points, float t)
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
        //Overload
        public static List<Vector3> FindP(Vector3[] points, float t)
        {
            //Copy Points
            List<Vector3> secPoints = new List<Vector3>();
            //Clear for new Points
            //points = new List<Vector3>();
            //Iterate through clone array of old tmp control points.
            for (int i = 0; i < points.Length - 1; i++)
            {
                //Add new Points to List.
                Vector3 p = (1 - t) * points[i] + t * points[i + 1];
                secPoints.Add(p);
            }
            return secPoints;
        }

        //Optimized??
        public static Vector3[] FindP(float t, Vector3[] points)
        {
            //Copy Points
            Vector3[] secPoints = new Vector3[points.Length - 1];
            //Iterate through clone array of old tmp control points.
            for (int i = 0; i < points.Length - 1; i++)
            {
                //Add new Points to List.
                Vector3 p = (1 - t) * points[i] + t * points[i + 1];
                //secPoints.Add(p);
                secPoints[i] = p;
            }
            return secPoints;
        }

        //Create Bezier Curve out of Referenced Vector3 List
        /*public static Vector3[] CreateBezier(Vector3[] origPoints, int segments)
        {
            float t = 0.0f;
            int iterations = origPoints.Length - 1;
            List<Vector3> newPoints = new List<Vector3>();
            List<Vector3> finalP = new List<Vector3>();
            Vector3 p = new Vector3(0, 0, 0);
            //Determine total distance between points
            float totalDist = FindTotalDistOfPoints(origPoints);
            //Have a Segment for every 4 meters in total distance
            float bSegments = Mathf.Round(totalDist / segments);
            //Iterate through and create the curve with as many bSegments as determined
            for (int j = 0; j <= bSegments; j++)
            {
                //Update T for this Iteration
                t = j / bSegments;
                //Test the GameData untouched raw
                newPoints = FindP(origPoints, t);
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
            //Debug.Log ("Bezier Curve Returned " + finalP.Count);
            //Add last Point of Orig Points to Curve
            //finalP.Add (origPoints [iterations]);
            return finalP.ToArray();
        }*/

        public static List<Vector3> SplitLine(Vector3[] line, float spacing)
        {

            List<Vector3> finalLine = new List<Vector3>(0)
        {
            line[0]
        };
            //Liner Interpolate along Lines by Desired Distance/s
            for (int i = 0; i < line.Length - 1; i++)
            {
                if (i != 0)
                {
                    finalLine.Add(line[i]);
                }
                //Find This Lines Length
                float lineDist = Vector3.Distance(line[i], line[i + 1]);
                //Determine Linear Interpolation Distance needed to hit spacing.
                float t = spacing / lineDist;//0.5(half way Etc)
                int totalFittingPoints = Mathf.RoundToInt(1 / t);
                for (int j = 1; j < totalFittingPoints; j++)
                {
                    //Move until we are out of space to move.
                    Vector3 p = Vector3.Lerp(line[i], line[i + 1], t * j);
                    finalLine.Add(p);
                }
            }
            finalLine.Add(line[line.Length - 1]);
            return finalLine;
        }

        //Plots the Vector3s around a point in World space.(CounterClockwise)
        public static List<Vector3> PlotCircle(Vector3 newPos, float radius, int segments)
        {
            List<Vector3> circle = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 pos = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                pos = pos + newPos;
                //Debug.Log("i: "+i+" Angle("+angle+") Pos: "+pos);
                //Add Pos to circlePPath
                circle.Add(pos);
            }
            return circle;
        }

        //Overload (Takes Direction) Plots the Vector3s around a point in World space.(CounterClockwise)
        public static List<Vector3> PlotCircle(Vector3 newPos, float radius, int segments, Vector3 worldDir)
        {
            float dirAngle = Vector3.Angle(worldDir, Vector3.forward);
            float fwdAngle = CiDyUtils.AngleDir2(Vector3.forward, worldDir, Vector3.up);
            //float rightAngle = CiDyUtils.AngleDir2(Vector3.right, worldDir, Vector3.up);
            float offset = 0;

            if (fwdAngle == 0)
            {
                offset = 180;
            }
            else
            {
                offset = dirAngle * fwdAngle;
            }
            //Is it to the Right of foward?
            /*if (fwdAngle > 0)
            {
                //Right positive side
                if (rightAngle < 0)
                {
                    offset = dirAngle;
                }
                else if (rightAngle > 0)
                {
                    //Negative Z axis
                    offset = dirAngle - 360;
                }
            }
            else if (fwdAngle < 0)
            {
                //If less than zero
                //Negative Left Side
                if (rightAngle > 0)
                {
                    //Netaive BAck
                    offset = dirAngle - 360;
                }
                else if(rightAngle < 0) {
                    offset = dirAngle - 360;
                }
            }
            else {
                //Collinear is it reversed?
                if (dirAngle == 180)
                {
                    //Yes it is
                    offset = 360 - 180;
                }
            }*/
            Debug.Log(offset);
            List<Vector3> circle = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float angle = ((i * Mathf.PI * 2 / segments)) + offset;
                Vector3 pos = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                pos = (pos + newPos);
                //Debug.Log("i: "+i+" Angle("+angle+") Pos: "+pos);
                //Add Pos to circlePPath
                circle.Add(pos);
            }
            return circle;
        }

        //Plots the Vector3s into a grid.
        public static List<Vector3> PlotGrid(Vector3 newPos, int gridX, int gridY, float spacing)
        {
            List<Vector3> grid = new List<Vector3>();
            for (int y = 0; y < gridY; y++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    Vector3 pos = new Vector3(x, 0, y) * spacing;
                    pos = newPos + pos;
                    //Add pos to Grid
                    grid.Add(pos);
                }
            }
            return grid;
        }

        //For Mesh Creation
        //Plots the Vector3s around a point in World space.(CounterClockwise)
        public static List<Vector3> PlotSegAroundPoint(Vector3 newPos, float radius, int segments)
        {
            List<Vector3> circle = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                //Vector3 pos = new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0)*radius;
                Vector3 pos = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * radius;
                pos = pos + newPos;
                //Debug.Log("i: "+i+" Angle("+angle+") Pos: "+pos);
                //Add Pos to circlePPath
                circle.Add(pos);
            }
            return circle;
        }

        //Check if the lines are interesecting in 2d space
        public static bool IsIntersecting(Vector3 p0, Vector3 p1, Vector3 n0, Vector3 n1, ref Vector3 intersection)
        {
            bool isIntersecting = false;

            //3d -> 2d
            Vector2 l1_start = new Vector2(p0.x, p0.z);
            Vector2 l1_end = new Vector2(p1.x, p1.z);

            Vector2 l2_start = new Vector2(n0.x, n0.z);
            Vector2 l2_end = new Vector2(n1.x, n1.z);

            //Direction of the lines
            Vector2 l1_dir = (l1_end - l1_start).normalized;
            Vector2 l2_dir = (l2_end - l2_start).normalized;

            //If we know the direction we can get the normal vector to each line
            Vector2 l1_normal = new Vector2(-l1_dir.y, l1_dir.x);
            Vector2 l2_normal = new Vector2(-l2_dir.y, l2_dir.x);


            //Step 1: Rewrite the lines to a general form: Ax + By = k1 and Cx + Dy = k2
            //The normal vector is the A, B
            float A = l1_normal.x;
            float B = l1_normal.y;

            float C = l2_normal.x;
            float D = l2_normal.y;

            //To get k we just use one point on the line
            float k1 = (A * l1_start.x) + (B * l1_start.y);
            float k2 = (C * l2_start.x) + (D * l2_start.y);


            //Step 2: are the lines parallel? -> no solutions
            if (IsParallel(l1_normal, l2_normal))
            {
                Debug.Log("The lines are parallel so no solutions!");

                return isIntersecting;
            }


            //Step 3: are the lines the same line? -> infinite amount of solutions
            //Pick one point on each line and test if the vector between the points is orthogonal to one of the normals
            if (IsOrthogonal(l1_start - l2_start, l1_normal))
            {
                Debug.Log("Same line so infinite amount of solutions!");

                //Return false anyway
                return isIntersecting;
            }


            //Step 4: calculate the intersection point -> one solution
            float x_intersect = (D * k1 - B * k2) / (A * D - B * C);
            float y_intersect = (-C * k1 + A * k2) / (A * D - B * C);

            intersection = new Vector3(x_intersect, 0, y_intersect);


            //Step 5: but we have line segments so we have to check if the intersection point is within the segment
            if (IsBetween(l1_start, l1_end, intersection) && IsBetween(l2_start, l2_end, intersection))
            {
                Debug.Log("We have an intersection point!");

                isIntersecting = true;
            }

            return isIntersecting;
        }

        //Are 2 vectors parallel?
        public static bool IsParallel(Vector2 v1, Vector2 v2)
        {
            //2 vectors are parallel if the angle between the vectors are 0 or 180 degrees
            if (Vector2.Angle(v1, v2) == 0f || Vector2.Angle(v1, v2) == 180f)
            {
                return true;
            }

            return false;
        }

        //Are 2 vectors orthogonal?
        public static bool IsOrthogonal(Vector2 v1, Vector2 v2)
        {
            //2 vectors are orthogonal is the dot product is 0
            //We have to check if close to 0 because of floating numbers
            if (Mathf.Abs(Vector2.Dot(v1, v2)) < 0.000001f)
            {
                return true;
            }

            return false;
        }

        //Is a point c between 2 other points a and b?
        public static bool IsBetween(Vector2 a, Vector2 b, Vector2 c)
        {
            bool isBetween = false;

            //Entire line segment
            Vector2 ab = b - a;
            //The intersection and the first point
            Vector2 ac = c - a;

            //Need to check 2 things: 
            //1. If the vectors are pointing in the same direction = if the dot product is positive
            //2. If the length of the vector between the intersection and the first point is smaller than the entire line
            if (Vector2.Dot(ab, ac) > 0f && ab.sqrMagnitude >= ac.sqrMagnitude)
            {
                isBetween = true;
            }

            return isBetween;
        }

        //Line intersection Test Algorithm only X/Z
        public static bool LineIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, ref Vector3 intersection)
        {
            //Because of Floating Point issues. The Values need to Rounded Properly.
            p1 = new Vector3(Mathf.Round(p1.x * 100f) / 100f, Mathf.Round(p1.y * 100f) / 100f, Mathf.Round(p1.z * 100f) / 100f);
            p2 = new Vector3(Mathf.Round(p2.x * 100f) / 100f, Mathf.Round(p2.y * 100f) / 100f, Mathf.Round(p2.z * 100f) / 100f);
            p3 = new Vector3(Mathf.Round(p3.x * 100f) / 100f, Mathf.Round(p3.y * 100f) / 100f, Mathf.Round(p3.z * 100f) / 100f);
            p4 = new Vector3(Mathf.Round(p4.x * 100f) / 100f, Mathf.Round(p4.y * 100f) / 100f, Mathf.Round(p4.z * 100f) / 100f);
            /*GameObject sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p1;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p1";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p2;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p2";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p3;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p3";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p4;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p4";*/
            //Debug.Log ("Testing Intesrections");
            float Ax, Bx, Cx, Az, Bz, Cz, d, e, f, num/*,offset*/;
            float x1lo, x1hi, y1lo, y1hi;

            Ax = (p2.x - p1.x);
            Bx = (p3.x - p4.x);
            //Debug.Log ("Ax: " + Ax + " Bx: " + Bx);
            // X bound box test/
            if (Ax < 0)
            {
                x1lo = p2.x; x1hi = p1.x;
            }
            else
            {
                x1hi = p2.x; x1lo = p1.x;
            }

            if (Bx > 0)
            {
                if (x1hi < p4.x || p3.x < x1lo) return false;
            }
            else
            {
                if (x1hi < p3.x || p4.x < x1lo) return false;
            }

            Az = (p2.z - p1.z);
            //Ay = (p2.y-p1.y);
            Bz = (p3.z - p4.z);
            //By = (p3.y-p4.y);
            //Debug.Log ("Ay: " + Ay + " By: " + By);
            // Y bound box test//
            if (Az < 0)
            {
                y1lo = p2.z; y1hi = p1.z;
            }
            else
            {
                y1hi = p2.z; y1lo = p1.z;
            }

            if (Bz > 0)
            {
                if (y1hi < p4.z || p3.z < y1lo) return false;
            }
            else
            {
                if (y1hi < p3.z || p4.z < y1lo) return false;
            }
            //Debug.Log ("Passed X & Y Bound Test");
            Cx = (p1.x - p3.x);
            Cz = (p1.z - p3.z);
            //Cy = (p1.y-p3.y);
            //Debug.Log ("Cx: " + Cx + " Cy: " + Cy);
            d = Bz * Cx - Bx * Cz;  // alpha numerator//
            f = Az * Bx - Ax * Bz;  // both denominator//

            // alpha tests//
            if (f > 0)
            {
                if (d < 0 || d > f) return false;
            }
            else
            {
                if (d > 0 || d < f) return false;
            }

            //Debug.Log ("Passed Alpha Tests F: "+f);
            e = Ax * Cz - Az * Cx;  // beta numerator//

            // beta tests //
            if (f > 0)
            {
                if (e < 0 || e > f) return false;
            }
            else
            {
                if (e > 0 || e < f) return false;
            }
            //Debug.Log ("Passed Beta Tests E: "+e);
            // check if they are parallel
            if (f == 0) return false;
            //Debug.Log ("Passed Parallel Test");
            // compute intersection coordinates //
            num = d * Ax; // numerator //
                          //    offset = same_sign(num,f) ? f*0.5f : -f*0.5f;   // round direction //
                          //    intersection.x = p1.x + (num+offset) / f;
            intersection.x = p1.x + num / f;

            num = d * Az;
            //    offset = same_sign(num,f) ? f*0.5f : -f*0.5f;
            //    intersection.z = p1.z + (num+offset) / f;
            intersection.z = p1.z + num / f;
            //Calculate Y Value
            /*d = By*Cx - Bx*Cy;  // alpha numerator//
            f = Ay*Bx - Ax*By;  // both denominator//
            num = d*Ay;
            //Debug.Log ("P1y: "+p1.y+" Num: "+num+" f: "+f);
            float yValue =  (p1.y + num) / f;
            if(float.IsNaN(yValue)){
                //Debug.LogError("Y is NaN");
                intersection.y = 0;
            } else {
                intersection.y = yValue;
            }*/
            //Debug.Log (intersection.y);
            /*sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = intersection;
            sphere.name = "intersection";*/
            return true;
        }

        //Line intersection Test Algorithm only X/Z
        public static bool LineIntersectionUnRounded(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, ref Vector3 intersection)
        {
            //Because of Floating Point issues. The Values need to Rounded Properly.
            /*p1 = new Vector3(Mathf.Round(p1.x * 100f) / 100f, Mathf.Round(p1.y * 100f) / 100f, Mathf.Round(p1.z * 100f) / 100f);
            p2 = new Vector3(Mathf.Round(p2.x * 100f) / 100f, Mathf.Round(p2.y * 100f) / 100f, Mathf.Round(p2.z * 100f) / 100f);
            p3 = new Vector3(Mathf.Round(p3.x * 100f) / 100f, Mathf.Round(p3.y * 100f) / 100f, Mathf.Round(p3.z * 100f) / 100f);
            p4 = new Vector3(Mathf.Round(p4.x * 100f) / 100f, Mathf.Round(p4.y * 100f) / 100f, Mathf.Round(p4.z * 100f) / 100f);*/
            /*GameObject sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p1;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p1";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p2;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p2";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p3;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p3";
            sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = p4;
            sphere.transform.localScale = new Vector3 (0.005f, 0.005f, 0.005f);
            sphere.name = "p4";*/
            //Debug.Log ("Testing Intesrections");
            float Ax, Bx, Cx, Az, Bz, Cz, d, e, f, num/*,offset*/;
            float x1lo, x1hi, y1lo, y1hi;

            Ax = (p2.x - p1.x);
            Bx = (p3.x - p4.x);
            //Debug.Log ("Ax: " + Ax + " Bx: " + Bx);
            // X bound box test/
            if (Ax < 0)
            {
                x1lo = p2.x; x1hi = p1.x;
            }
            else
            {
                x1hi = p2.x; x1lo = p1.x;
            }

            if (Bx > 0)
            {
                if (x1hi < p4.x || p3.x < x1lo) return false;
            }
            else
            {
                if (x1hi < p3.x || p4.x < x1lo) return false;
            }

            Az = (p2.z - p1.z);
            //Ay = (p2.y-p1.y);
            Bz = (p3.z - p4.z);
            //By = (p3.y-p4.y);
            //Debug.Log ("Ay: " + Ay + " By: " + By);
            // Y bound box test//
            if (Az < 0)
            {
                y1lo = p2.z; y1hi = p1.z;
            }
            else
            {
                y1hi = p2.z; y1lo = p1.z;
            }

            if (Bz > 0)
            {
                if (y1hi < p4.z || p3.z < y1lo) return false;
            }
            else
            {
                if (y1hi < p3.z || p4.z < y1lo) return false;
            }
            //Debug.Log ("Passed X & Y Bound Test");
            Cx = (p1.x - p3.x);
            Cz = (p1.z - p3.z);
            //Cy = (p1.y-p3.y);
            //Debug.Log ("Cx: " + Cx + " Cy: " + Cy);
            d = Bz * Cx - Bx * Cz;  // alpha numerator//
            f = Az * Bx - Ax * Bz;  // both denominator//

            // alpha tests//
            if (f > 0)
            {
                if (d < 0 || d > f) return false;
            }
            else
            {
                if (d > 0 || d < f) return false;
            }

            //Debug.Log ("Passed Alpha Tests F: "+f);
            e = Ax * Cz - Az * Cx;  // beta numerator//

            // beta tests //
            if (f > 0)
            {
                if (e < 0 || e > f) return false;
            }
            else
            {
                if (e > 0 || e < f) return false;
            }
            //Debug.Log ("Passed Beta Tests E: "+e);
            // check if they are parallel
            if (f == 0)
            {
                return false;
            }
            //Debug.Log ("Passed Parallel Test");
            // compute intersection coordinates //
            num = d * Ax; // numerator //
                          //    offset = same_sign(num,f) ? f*0.5f : -f*0.5f;   // round direction //
                          //    intersection.x = p1.x + (num+offset) / f;
            intersection.x = p1.x + num / f;

            num = d * Az;
            //    offset = same_sign(num,f) ? f*0.5f : -f*0.5f;
            //    intersection.z = p1.z + (num+offset) / f;
            intersection.z = p1.z + num / f;
            //Calculate Y Value
            /*d = By*Cx - Bx*Cy;  // alpha numerator//
            f = Ay*Bx - Ax*By;  // both denominator//
            num = d*Ay;
            //Debug.Log ("P1y: "+p1.y+" Num: "+num+" f: "+f);
            float yValue =  (p1.y + num) / f;
            if(float.IsNaN(yValue)){
                //Debug.LogError("Y is NaN");
                intersection.y = 0;
            } else {
                intersection.y = yValue;
            }*/
            //Debug.Log (intersection.y);
            /*sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            sphere.transform.position = intersection;
            sphere.name = "intersection";*/
            return true;
        }

        //This will simulate a circulur clockwise intersection test on a line and will return the first intersection found.
        public static bool CircleIntersectsLine(Vector3 ourPos, float radius, int segments, Vector3 p0, Vector3 p1, ref Vector3 intersection)
        {
            //4 is default on segments.
            //Circle Test Rd Lines store points as well.
            //Create Circle for Intersection testing as well
            //create Four points relative to us fwd/bkwd/left/rght
            //Plot a Circle Around our position (Vector3 p,float r, int segments)
            List<Vector3> circlePath = PlotCircle(ourPos, radius, segments);
            //Iterate through circle line segments.
            for (int n = 0; n < circlePath.Count; n++)
            {
                //Test against line and store finding.
                Vector3 circleStr = circlePath[n];
                Vector3 circleEnd;
                if (n == circlePath.Count - 1)
                {
                    circleEnd = circlePath[0];
                }
                else
                {
                    circleEnd = circlePath[n + 1];
                }
                if (LineIntersectionUnRounded(circleStr, circleEnd, p0, p1, ref intersection))
                {
                    return true;
                }
            }
            //no Intersection found
            return false;
        }

        //Returns all Hits on Circle to Line
        public static List<Vector3> CircleLineTest(Vector3 ourPos, float radius, int segments, Vector3 p0, Vector3 p1, ref Vector3 intersection)
        {
            //4 is default on segments.
            //Circle Test Rd Lines store points as well.
            //Create Circle for Intersection testing as well
            //create Four points relative to us fwd/bkwd/left/rght
            //Plot a Circle Around our position (Vector3 p,float r, int segments)
            List<Vector3> circlePath = PlotCircle(ourPos, radius, segments);
            //Now create a Circle
            List<Vector3> hits = new List<Vector3>();
            //Iterate through circle line segments.
            for (int n = 0; n < circlePath.Count - 1; n++)
            {
                //Test against line and store finding.
                Vector3 circleStr = circlePath[n];
                Vector3 circleEnd = circlePath[n + 1];
                if (LineIntersection(circleStr, circleEnd, p0, p1, ref intersection))
                {
                    hits.Add(intersection);
                }
            }
            return hits;
        }

        //Find Centroid Of Polygon List
        public static Vector3 FindCentroid(Vector3[] vertices)
        {
            Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
            float signedArea = 0.0f;
            float x0 = 0.0f; // Current vertex X
            float z0 = 0.0f; // Current vertex Y
            float x1 = 0.0f; // Next vertex X
            float z1 = 0.0f; // Next vertex Y
            float a = 0.0f;  // Partial signed area

            // For all vertices except last
            int i = 0;
            for (i = 0; i < vertices.Length - 1; ++i)
            {
                x0 = vertices[i].x;
                z0 = vertices[i].z;
                x1 = vertices[i + 1].x;
                z1 = vertices[i + 1].z;
                a = x0 * z1 - x1 * z0;
                signedArea += a;
                centroid.x += (x0 + x1) * a;
                centroid.z += (z0 + z1) * a;
            }

            // Do last vertex
            x0 = vertices[i].x;
            z0 = vertices[i].z;
            x1 = vertices[0].x;
            z1 = vertices[0].z;
            a = x0 * z1 - x1 * z0;
            signedArea += a;
            centroid.x += (x0 + x1) * a;
            centroid.z += (z0 + z1) * a;

            signedArea *= 0.5f;
            centroid.x /= (6 * signedArea);
            centroid.z /= (6 * signedArea);

            return centroid;
        }

        //Find Centroid Of Polygon List(Overloaded Version for List)
        public static Vector3 FindCentroid(List<Vector3> vertices)
        {
            Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
            float signedArea = 0.0f;
            float x0 = 0.0f; // Current vertex X
            float z0 = 0.0f; // Current vertex Y
            float x1 = 0.0f; // Next vertex X
            float z1 = 0.0f; // Next vertex Y
            float a = 0.0f;  // Partial signed area

            // For all vertices except last
            int i = 0;
            for (i = 0; i < vertices.Count - 1; ++i)
            {
                x0 = vertices[i].x;
                z0 = vertices[i].z;
                x1 = vertices[i + 1].x;
                z1 = vertices[i + 1].z;
                a = x0 * z1 - x1 * z0;
                signedArea += a;
                centroid.x += (x0 + x1) * a;
                centroid.z += (z0 + z1) * a;
            }

            // Do last vertex
            x0 = vertices[i].x;
            z0 = vertices[i].z;
            x1 = vertices[0].x;
            z1 = vertices[0].z;
            a = x0 * z1 - x1 * z0;
            signedArea += a;
            centroid.x += (x0 + x1) * a;
            centroid.z += (z0 + z1) * a;

            signedArea *= 0.5f;
            centroid.x /= (6 * signedArea);
            centroid.z /= (6 * signedArea);

            return centroid;
        }

        //Find Centroid Of Polygon List(Overloaded Version for List)//Use averege expects 0,1, or 2 (0 no Avearge,1 true Aver,2 Lowest point, 3 Highests point
        public static Vector3 FindCentroid(List<Vector3> vertices, int useAverageY)
        {
            Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
            float signedArea = 0.0f;
            float x0 = 0.0f; // Current vertex X
            float z0 = 0.0f; // Current vertex Y
            float x1 = 0.0f; // Next vertex X
            float z1 = 0.0f; // Next vertex Y
            float a = 0.0f;  // Partial signed area
            float curY = 0;
            // For all vertices except last
            int i = 0;
            float lowest = Mathf.Infinity;
            float highest = 0;

            for (i = 0; i < vertices.Count - 1; ++i)
            {
                x0 = vertices[i].x;
                z0 = vertices[i].z;
                x1 = vertices[i + 1].x;
                z1 = vertices[i + 1].z;
                a = x0 * z1 - x1 * z0;
                signedArea += a;
                centroid.x += (x0 + x1) * a;
                centroid.z += (z0 + z1) * a;
                switch (useAverageY)
                {
                    case 1:
                        curY += vertices[i].y;
                        break;
                    case 2:
                        //Lowest
                        if (vertices[i].y < lowest)
                        {
                            lowest = vertices[i].y;
                            curY = lowest;
                        }
                        break;
                    case 3:
                        //Highest
                        if (vertices[i].y > highest)
                        {
                            highest = vertices[i].y;
                            curY = highest;
                        }
                        break;
                }
            }
            if (useAverageY == 1)
            {
                curY += vertices[vertices.Count - 1].y;
                curY = curY / vertices.Count;
            }

            // Do last vertex
            x0 = vertices[i].x;
            z0 = vertices[i].z;
            x1 = vertices[0].x;
            z1 = vertices[0].z;
            a = x0 * z1 - x1 * z0;
            signedArea += a;
            centroid.x += (x0 + x1) * a;
            centroid.z += (z0 + z1) * a;

            signedArea *= 0.5f;
            centroid.x /= (6 * signedArea);
            centroid.z /= (6 * signedArea);
            if (useAverageY != 0)
            {
                centroid.y = curY;
            }
            return centroid;
        }

        //Find Centroid Of Polygon List(Overloaded Version for List)
        public static Vector3 FindCentroid(List<CiDyNode> vertices)
        {
            Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
            float signedArea = 0.0f;
            float x0 = 0.0f; // Current vertex X
            float z0 = 0.0f; // Current vertex Y
            float x1 = 0.0f; // Next vertex X
            float z1 = 0.0f; // Next vertex Y
            float a = 0.0f;  // Partial signed area

            // For all vertices except last
            int i = 0;
            for (i = 0; i < vertices.Count - 1; ++i)
            {
                x0 = vertices[i].position.x;
                z0 = vertices[i].position.z;
                x1 = vertices[i + 1].position.x;
                z1 = vertices[i + 1].position.z;
                a = x0 * z1 - x1 * z0;
                signedArea += a;
                centroid.x += (x0 + x1) * a;
                centroid.z += (z0 + z1) * a;
            }

            // Do last vertex
            x0 = vertices[i].position.x;
            z0 = vertices[i].position.z;
            x1 = vertices[0].position.x;
            z1 = vertices[0].position.z;
            a = x0 * z1 - x1 * z0;
            signedArea += a;
            centroid.x += (x0 + x1) * a;
            centroid.z += (z0 + z1) * a;

            signedArea *= 0.5f;
            centroid.x /= (6 * signedArea);
            centroid.z /= (6 * signedArea);

            return centroid;
        }

        //Average y
        //Find Centroid Of Polygon List(Overloaded Version for List)
        public static Vector3 FindCentroid(List<CiDyNode> vertices, bool useAverageY)
        {

            Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
            float signedArea = 0.0f;
            float x0 = 0.0f; // Current vertex X
            float z0 = 0.0f; // Current vertex Y
            float x1 = 0.0f; // Next vertex X
            float z1 = 0.0f; // Next vertex Y
            float a = 0.0f;  // Partial signed area
            float curY = 0;
            // For all vertices except last
            int i = 0;
            for (i = 0; i < vertices.Count - 1; ++i)
            {
                x0 = vertices[i].position.x;
                z0 = vertices[i].position.z;
                x1 = vertices[i + 1].position.x;
                z1 = vertices[i + 1].position.z;
                a = x0 * z1 - x1 * z0;
                signedArea += a;
                centroid.x += (x0 + x1) * a;
                centroid.z += (z0 + z1) * a;
                curY += vertices[i].position.y;
            }
            curY += vertices[vertices.Count - 1].position.y;
            curY = curY / vertices.Count;

            // Do last vertex
            x0 = vertices[i].position.x;
            z0 = vertices[i].position.z;
            x1 = vertices[0].position.x;
            z1 = vertices[0].position.z;
            a = x0 * z1 - x1 * z0;
            signedArea += a;
            centroid.x += (x0 + x1) * a;
            centroid.z += (z0 + z1) * a;

            signedArea *= 0.5f;
            centroid.x /= (6 * signedArea);
            centroid.z /= (6 * signedArea);
            if (useAverageY)
            {
                centroid.y = curY;
            }
            return centroid;
        }


        //Used for Insetting CiDy Cells.
        /*public static List<CiDyNode> PolygonInset(CiDyVector3[] origCycles, float insetAmount)
        {
            //Determine normal of Poly
            Vector3 perpNormal = Vector3.Cross(origCycles[1].pos - origCycles[0].pos, origCycles[2].pos - origCycles[0].pos).normalized;

            //vectorLines = new List<ColoredLines>();
            //Debug.Log("Post Process Outline, " + origCycles.Count + " Inset: " + insetAmount);
            List<CiDyNode> walkableNodes = new List<CiDyNode>();
            //Post Process the Outline and move Visualized Points to tileEdge Along Angle Bisector
            if (origCycles.Length > 1)
            {
                //Iterate through and project points.
                for (int i = 0; i < origCycles.Length; i++)
                {
                    //Grab Pred Node
                    Vector3 predPos;
                    //Grab Cur Node
                    Vector3 curPos = origCycles[i].pos;
                    //Grab Succ Node
                    Vector3 succPos;
                    //Determine Previous and Successor Positions
                    if (i == 0)
                    {
                        //At Beginning
                        predPos = origCycles[origCycles.Length - 1].pos;
                        succPos = origCycles[i + 1].pos;
                    }
                    else if (i == origCycles.Length - 1)
                    {
                        //At End
                        predPos = origCycles[i - 1].pos;
                        succPos = origCycles[0].pos;
                    }
                    else
                    {
                        //In Middle
                        predPos = origCycles[i - 1].pos;
                        succPos = origCycles[i + 1].pos;
                    }

                    //Determine AngleBisector
                    Vector3 bisector = CiDyUtils.AngleBisector(predPos, curPos, succPos);

                    //Check that bisector is on the desired Side of the Line(clockwise = Right Side)
                    Vector3 fwd = (curPos - predPos).normalized;
                    Vector3 right = Vector3.Cross(perpNormal, fwd).normalized;
                    Vector3 targetDir = ((curPos + bisector) - curPos).normalized;
                    float angleDir = CiDyUtils.AngleDirection(fwd, targetDir, perpNormal);
                    //Debug.Log("AngleDir: " + angleDir + " For: " + i);
                    if (angleDir == -1)
                    {
                        //We are on the left so reverse bisector value
                        bisector = -bisector;
                    }

                    //Shared Float(HAlf of TileSize)
                    float sideA = insetAmount / 2;
                    if (predPos != succPos)
                    {
                        //Do not add Any straight line points.
                        if (angleDir != 0)
                        {
                            //Tell me what the Three Angles Are of this Triangle.
                            //Get angle Between Right to Bisector
                            //float angleA = Vector3.Angle()
                            float angleB = Mathf.RoundToInt(Vector3.Angle(-targetDir, right));//Mathf.Round(Vector3.Angle(-targetDir, right) * 100) / 100;
                            float angleC = Mathf.RoundToInt(90 - angleB);//Mathf.Round((90 - angleB)*100)/100;
                                                                         //float sinA = Mathf.Sin(90 * Mathf.Deg2Rad);
                            float sinB = Mathf.Sin(angleB * Mathf.Deg2Rad);
                            float sinC = Mathf.Sin(angleC * Mathf.Deg2Rad);
                            //b / sin B = c / sin C
                            //b / sin(83°B) = 7 / sin(62°C)
                            //b = (7 × sin(83°B))/ sin(62°C)
                            float sideB = ((sideA * sinB) / sinC);
                            //Calculate Hypotenuse Length
                            float hypotenuse = CiDyUtils.HypotenuseLength(sideB, sideA);
                            //Catch Degeneracy Event
                            if (hypotenuse == Mathf.Infinity || hypotenuse == 0)
                            {
                                //Debug.LogError("Point: " + i + " Is == Infinity, Moved Manually");
                                bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                                //Move relative to World Position
                                bisector = curPos + (bisector * insetAmount / 2);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);

                            }
                            else
                            {
                                //Move relative to World Position
                                bisector = curPos + (bisector * hypotenuse);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                            }
                        }
                        else if (angleDir == 0)
                        {
                            //Debug.Log(i+" Moved Right Manually");
                            bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                            //Move relative to World Position
                            bisector = curPos + (bisector * insetAmount / 2);
                            //Add to Stored Reference List
                            walkableNodes.Add(bisector);
                        }
                    }
                    else
                    {
                        //Special Degeneracy logic for Single Point Connections.(Dead Ends)
                        //Check for Single Point connection.
                        //Create Right Vector3 and Left Vector3 of Fwd Direction
                        Vector3 nR = curPos + (fwd * sideA) + (right * sideA);
                        Vector3 nL = curPos + (fwd * sideA) + (-right * sideA);
                        walkableNodes.Add(nR);
                        walkableNodes.Add(nL);
                    }
                }
            }

            //Return final List
            return walkableNodes.ToArray();
        }*/

        public static List<Vector3> PolygonInset(List<Vector3> origCycles, float insetAmount)
        {
            //Debug.Log("Post Process Outline, " + origCycles.Count + " Inset: " + insetAmount);
            List<Vector3> walkableNodes = new List<Vector3>();
            //Post Process the Outline and move Visualized Points to tileEdge Along Angle Bisector
            if (origCycles.Count > 1)
            {
                //Iterate through and project points.
                for (int i = 0; i < origCycles.Count; i++)
                {
                    //Grab Pred Node
                    Vector3 predPos;
                    //Grab Cur Node
                    Vector3 curPos = (Vector3)origCycles[i];
                    //Grab Succ Node
                    Vector3 succPos;
                    //Determine Previous and Successor Positions
                    if (i == 0)
                    {
                        //At Beginning
                        predPos = (Vector3)origCycles[origCycles.Count - 1];
                        succPos = (Vector3)origCycles[i + 1];
                    }
                    else if (i == origCycles.Count - 1)
                    {
                        //At End
                        predPos = (Vector3)origCycles[i - 1];
                        succPos = (Vector3)origCycles[0];
                    }
                    else
                    {
                        //In Middle
                        predPos = (Vector3)origCycles[i - 1];
                        succPos = (Vector3)origCycles[i + 1];
                    }

                    //Determine AngleBisector
                    Vector3 bisector = CiDyUtils.AngleBisector(predPos, curPos, succPos);
                    //Check that bisector is on the desired Side of the Line(clockwise = Right Side)
                    Vector3 fwd = (curPos - predPos).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 targetDir = ((curPos + bisector) - curPos).normalized;
                    float angleDir = CiDyUtils.AngleDirection(fwd, targetDir, Vector3.up);
                    //Debug.Log("AngleDir: " + angleDir + " For: " + i);
                    if (angleDir == -1)
                    {
                        //We are on the left so reverse bisector value
                        bisector = -bisector;
                    }

                    //Shared Float(HAlf of TileSize)
                    float sideA = insetAmount / 2;
                    if (predPos != succPos)
                    {
                        //Do not add Any straight line points.
                        if (angleDir != 0)
                        {
                            //Tell me what the Three Angles Are of this Triangle.
                            //Get angle Between Right to Bisector
                            //float angleA = Vector3.Angle()
                            float angleB = Mathf.RoundToInt(Vector3.Angle(-targetDir, right));//Mathf.Round(Vector3.Angle(-targetDir, right) * 100) / 100;
                            float angleC = Mathf.RoundToInt(90 - angleB);//Mathf.Round((90 - angleB)*100)/100;
                                                                         //float sinA = Mathf.Sin(90 * Mathf.Deg2Rad);
                            float sinB = Mathf.Sin(angleB * Mathf.Deg2Rad);
                            float sinC = Mathf.Sin(angleC * Mathf.Deg2Rad);
                            //b / sin B = c / sin C
                            //b / sin(83°B) = 7 / sin(62°C)
                            //b = (7 × sin(83°B))/ sin(62°C)
                            float sideB = ((sideA * sinB) / sinC);
                            //Calculate Hypotenuse Length
                            float hypotenuse = CiDyUtils.HypotenuseLength(sideB, sideA);
                            //Catch Degeneracy Event
                            if (hypotenuse == Mathf.Infinity || hypotenuse == 0)
                            {
                                //Debug.LogError("Point: " + i + " Is == Infinity, Moved Manually");
                                bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                                //Move relative to World Position
                                bisector = curPos + (bisector * insetAmount / 2);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Store VectorLines
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/

                            }
                            else
                            {
                                //Move relative to World Position
                                bisector = curPos + (bisector * hypotenuse);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Debug.Log("Sins = SinA: " + sinA + " SinB: " + sinB + " SinC: " + sinC);
                                //Debug.Log("AngleA = 90, " + "AngleB: " + angleB + " AngleC: " + angleC + " = " + (90 + (angleB + angleC)));
                                //Debug.Log("Hypotenuse: " + hypotenuse);
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                            }
                        }
                        else if (angleDir == 0)
                        {
                            //Debug.Log(i+" Moved Right Manually");
                            bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                            //Move relative to World Position
                            bisector = curPos + (bisector * insetAmount / 2);
                            //Add to Stored Reference List
                            walkableNodes.Add(bisector);
                            //Store VectorLines
                            /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                            vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                            vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                        }
                    }
                    else
                    {
                        //Special Degeneracy logic for Single Point Connections.(Dead Ends)
                        //Check for Single Point connection.
                        //Create Right Vector3 and Left Vector3 of Fwd Direction
                        Vector3 nR = curPos + (fwd * sideA) + (right * sideA);
                        Vector3 nL = curPos + (fwd * sideA) + (-right * sideA);
                        walkableNodes.Add(nR);
                        walkableNodes.Add(nL);
                        //Store VectorLines
                        /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                        vectorLines.Add(new ColoredLines(curPos, nR, Color.white));
                        vectorLines.Add(new ColoredLines(curPos, nL, Color.white));*/
                    }
                }
            }

            //Return final List
            return walkableNodes;
        }

        public static Vector3[] PolygonInset(Vector3[] origCycles, float insetAmount)
        {
            //Determine normal of Poly
            Vector3 perpNormal = Vector3.Cross(origCycles[1] - origCycles[0], origCycles[2] - origCycles[0]).normalized;

            //vectorLines = new List<ColoredLines>();
            //Debug.Log("Post Process Outline, " + origCycles.Count + " Inset: " + insetAmount);
            List<Vector3> walkableNodes = new List<Vector3>();
            //Post Process the Outline and move Visualized Points to tileEdge Along Angle Bisector
            if (origCycles.Length > 1)
            {
                //Iterate through and project points.
                for (int i = 0; i < origCycles.Length; i++)
                {
                    //Grab Pred Node
                    Vector3 predPos;
                    //Grab Cur Node
                    Vector3 curPos = (Vector3)origCycles[i];
                    //Grab Succ Node
                    Vector3 succPos;
                    //Determine Previous and Successor Positions
                    if (i == 0)
                    {
                        //At Beginning
                        predPos = (Vector3)origCycles[origCycles.Length - 1];
                        succPos = (Vector3)origCycles[i + 1];
                    }
                    else if (i == origCycles.Length - 1)
                    {
                        //At End
                        predPos = (Vector3)origCycles[i - 1];
                        succPos = (Vector3)origCycles[0];
                    }
                    else
                    {
                        //In Middle
                        predPos = (Vector3)origCycles[i - 1];
                        succPos = (Vector3)origCycles[i + 1];
                    }

                    //Determine AngleBisector
                    Vector3 bisector = CiDyUtils.AngleBisector(predPos, curPos, succPos);

                    //Check that bisector is on the desired Side of the Line(clockwise = Right Side)
                    Vector3 fwd = (curPos - predPos).normalized;
                    Vector3 right = Vector3.Cross(perpNormal, fwd).normalized;
                    Vector3 targetDir = ((curPos + bisector) - curPos).normalized;
                    float angleDir = CiDyUtils.AngleDirection(fwd, targetDir, perpNormal);
                    //Debug.Log("AngleDir: " + angleDir + " For: " + i);
                    if (angleDir == -1)
                    {
                        //We are on the left so reverse bisector value
                        bisector = -bisector;
                    }

                    //Shared Float(HAlf of TileSize)
                    float sideA = insetAmount / 2;
                    if (predPos != succPos)
                    {
                        //Do not add Any straight line points.
                        if (angleDir != 0)
                        {
                            //Tell me what the Three Angles Are of this Triangle.
                            //Get angle Between Right to Bisector
                            //float angleA = Vector3.Angle()
                            float angleB = Mathf.RoundToInt(Vector3.Angle(-targetDir, right));//Mathf.Round(Vector3.Angle(-targetDir, right) * 100) / 100;
                            float angleC = Mathf.RoundToInt(90 - angleB);//Mathf.Round((90 - angleB)*100)/100;
                                                                         //float sinA = Mathf.Sin(90 * Mathf.Deg2Rad);
                            float sinB = Mathf.Sin(angleB * Mathf.Deg2Rad);
                            float sinC = Mathf.Sin(angleC * Mathf.Deg2Rad);
                            //b / sin B = c / sin C
                            //b / sin(83°B) = 7 / sin(62°C)
                            //b = (7 × sin(83°B))/ sin(62°C)
                            float sideB = ((sideA * sinB) / sinC);
                            //Calculate Hypotenuse Length
                            float hypotenuse = CiDyUtils.HypotenuseLength(sideB, sideA);
                            //Catch Degeneracy Event
                            if (hypotenuse == Mathf.Infinity || hypotenuse == 0)
                            {
                                //Debug.LogError("Point: " + i + " Is == Infinity, Moved Manually");
                                bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                                //Move relative to World Position
                                bisector = curPos + (bisector * insetAmount / 2);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Store VectorLines
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/

                            }
                            else
                            {
                                //Move relative to World Position
                                bisector = curPos + (bisector * hypotenuse);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Debug.Log("Sins = SinA: " + sinA + " SinB: " + sinB + " SinC: " + sinC);
                                //Debug.Log("AngleA = 90, " + "AngleB: " + angleB + " AngleC: " + angleC + " = " + (90 + (angleB + angleC)));
                                //Debug.Log("Hypotenuse: " + hypotenuse);
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                            }
                        }
                        else if (angleDir == 0)
                        {
                            //Debug.Log(i+" Moved Right Manually");
                            bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                            //Move relative to World Position
                            bisector = curPos + (bisector * insetAmount / 2);
                            //Add to Stored Reference List
                            walkableNodes.Add(bisector);
                            //Store VectorLines
                            /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                            vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                            vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                        }
                    }
                    else
                    {
                        //Special Degeneracy logic for Single Point Connections.(Dead Ends)
                        //Check for Single Point connection.
                        //Create Right Vector3 and Left Vector3 of Fwd Direction
                        Vector3 nR = curPos + (fwd * sideA) + (right * sideA);
                        Vector3 nL = curPos + (fwd * sideA) + (-right * sideA);
                        walkableNodes.Add(nR);
                        walkableNodes.Add(nL);
                        //Store VectorLines
                        /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                        vectorLines.Add(new ColoredLines(curPos, nR, Color.white));
                        vectorLines.Add(new ColoredLines(curPos, nL, Color.white));*/
                    }
                }
            }

            //Return final List
            return walkableNodes.ToArray();
        }

        //OVERLOAD takes List<CiDyNode> and returns List<Vector3>
        public static List<Vector3> PolygonInset(float insetAmount, CiDyNode[] origCycles)
        {
            //Debug.Log("Post Process Outline, " + origCycles.Count + " Inset: " + insetAmount);
            List<Vector3> walkableNodes = new List<Vector3>();
            //Post Process the Outline and move Visualized Points to tileEdge Along Angle Bisector
            if (origCycles.Length > 1)
            {
                //Iterate through and project points.
                for (int i = 0; i < origCycles.Length; i++)
                {
                    //Grab Pred Node
                    Vector3 predPos;
                    //Grab Cur Node
                    Vector3 curPos = (Vector3)origCycles[i].position;
                    //Grab Succ Node
                    Vector3 succPos;
                    //Determine Previous and Successor Positions
                    if (i == 0)
                    {
                        //At Beginning
                        predPos = (Vector3)origCycles[origCycles.Length - 1].position;
                        succPos = (Vector3)origCycles[i + 1].position;
                    }
                    else if (i == origCycles.Length - 1)
                    {
                        //At End
                        predPos = (Vector3)origCycles[i - 1].position;
                        succPos = (Vector3)origCycles[0].position;
                    }
                    else
                    {
                        //In Middle
                        predPos = (Vector3)origCycles[i - 1].position;
                        succPos = (Vector3)origCycles[i + 1].position;
                    }

                    //Determine AngleBisector
                    Vector3 bisector = CiDyUtils.AngleBisector(predPos, curPos, succPos);
                    //Check that bisector is on the desired Side of the Line(clockwise = Right Side)
                    Vector3 fwd = (curPos - predPos).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 targetDir = ((curPos + bisector) - curPos).normalized;
                    float angleDir = CiDyUtils.AngleDirection(fwd, targetDir, Vector3.up);
                    //Debug.Log("AngleDir: " + angleDir + " For: " + i);
                    if (angleDir == -1)
                    {
                        //We are on the left so reverse bisector value
                        bisector = -bisector;
                    }

                    //Shared Float(HAlf of TileSize)
                    float sideA = insetAmount / 2;
                    if (predPos != succPos)
                    {
                        //Do not add Any straight line points.
                        if (angleDir != 0)
                        {
                            //Tell me what the Three Angles Are of this Triangle.
                            //Get angle Between Right to Bisector
                            //float angleA = Vector3.Angle()
                            float angleB = Mathf.RoundToInt(Vector3.Angle(-targetDir, right));//Mathf.Round(Vector3.Angle(-targetDir, right) * 100) / 100;
                            float angleC = Mathf.RoundToInt(90 - angleB);//Mathf.Round((90 - angleB)*100)/100;
                                                                         //float sinA = Mathf.Sin(90 * Mathf.Deg2Rad);
                            float sinB = Mathf.Sin(angleB * Mathf.Deg2Rad);
                            float sinC = Mathf.Sin(angleC * Mathf.Deg2Rad);
                            //b / sin B = c / sin C
                            //b / sin(83°B) = 7 / sin(62°C)
                            //b = (7 × sin(83°B))/ sin(62°C)
                            float sideB = ((sideA * sinB) / sinC);
                            //Calculate Hypotenuse Length
                            float hypotenuse = CiDyUtils.HypotenuseLength(sideB, sideA);
                            //Catch Degeneracy Event
                            if (hypotenuse == Mathf.Infinity || hypotenuse == 0)
                            {
                                //Debug.LogError("Point: " + i + " Is == Infinity, Moved Manually");
                                bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                                //Move relative to World Position
                                bisector = curPos + (bisector * insetAmount / 2);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Store VectorLines
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/

                            }
                            else
                            {
                                //Move relative to World Position
                                bisector = curPos + (bisector * hypotenuse);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Debug.Log("Sins = SinA: " + sinA + " SinB: " + sinB + " SinC: " + sinC);
                                //Debug.Log("AngleA = 90, " + "AngleB: " + angleB + " AngleC: " + angleC + " = " + (90 + (angleB + angleC)));
                                //Debug.Log("Hypotenuse: " + hypotenuse);
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                            }
                        }
                        else if (angleDir == 0)
                        {
                            //Debug.Log(i+" Moved Right Manually");
                            bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                            //Move relative to World Position
                            bisector = curPos + (bisector * insetAmount / 2);
                            //Add to Stored Reference List
                            walkableNodes.Add(bisector);
                            //Store VectorLines
                            /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                            vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                            vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                        }
                    }
                    else
                    {
                        //Special Degeneracy logic for Single Point Connections.(Dead Ends)
                        //Check for Single Point connection.
                        //Create Right Vector3 and Left Vector3 of Fwd Direction
                        Vector3 nR = curPos + (fwd * sideA) + (right * sideA);
                        Vector3 nL = curPos + (fwd * sideA) + (-right * sideA);
                        walkableNodes.Add(nR);
                        walkableNodes.Add(nL);
                        //Store VectorLines
                        /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                        vectorLines.Add(new ColoredLines(curPos, nR, Color.white));
                        vectorLines.Add(new ColoredLines(curPos, nL, Color.white));*/
                    }
                }
            }

            //Return final List
            return walkableNodes;
        }

        //Overload Return List<List<CiDyNode>> takes List<CiDyVector3>
        public static List<List<CiDyNode>> PolygonInset(List<CiDyVector3> origCycles, float insetAmount)
        {
            //Translate into a LAV for Insetting.
            List<CiDyNode> LAV = CreateLav(origCycles, true);
            //Debug.Log("Post Process Outline, " + origCycles.Count + " Inset: " + insetAmount);
            List<Vector3> walkableNodes = new List<Vector3>();
            //Post Process the Outline and move Visualized Points to tileEdge Along Angle Bisector
            if (origCycles.Count > 1)
            {
                //Iterate through and project points.
                for (int i = 0; i < origCycles.Count; i++)
                {
                    //Grab Pred Node
                    Vector3 predPos;
                    //Grab Cur Node
                    Vector3 curPos = (Vector3)origCycles[i].pos;
                    //Grab Succ Node
                    Vector3 succPos;
                    //Determine Previous and Successor Positions
                    if (i == 0)
                    {
                        //At Beginning
                        predPos = (Vector3)origCycles[origCycles.Count - 1].pos;
                        succPos = (Vector3)origCycles[i + 1].pos;
                    }
                    else if (i == origCycles.Count - 1)
                    {
                        //At End
                        predPos = (Vector3)origCycles[i - 1].pos;
                        succPos = (Vector3)origCycles[0].pos;
                    }
                    else
                    {
                        //In Middle
                        predPos = (Vector3)origCycles[i - 1].pos;
                        succPos = (Vector3)origCycles[i + 1].pos;
                    }

                    //Determine AngleBisector
                    Vector3 bisector = CiDyUtils.AngleBisector(predPos, curPos, succPos);
                    //Check that bisector is on the desired Side of the Line(clockwise = Right Side)
                    Vector3 fwd = (curPos - predPos).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 targetDir = ((curPos + bisector) - curPos).normalized;
                    float angleDir = CiDyUtils.AngleDirection(fwd, targetDir, Vector3.up);
                    //Debug.Log("AngleDir: " + angleDir + " For: " + i);
                    if (angleDir == -1)
                    {
                        //We are on the left so reverse bisector value
                        bisector = -bisector;
                    }

                    //Shared Float(HAlf of TileSize)
                    float sideA = insetAmount / 2;
                    if (predPos != succPos)
                    {
                        //Do not add Any straight line points.
                        if (angleDir != 0)
                        {
                            //Tell me what the Three Angles Are of this Triangle.
                            //Get angle Between Right to Bisector
                            //float angleA = Vector3.Angle()
                            float angleB = Mathf.RoundToInt(Vector3.Angle(-targetDir, right));//Mathf.Round(Vector3.Angle(-targetDir, right) * 100) / 100;
                            float angleC = Mathf.RoundToInt(90 - angleB);//Mathf.Round((90 - angleB)*100)/100;
                                                                         //float sinA = Mathf.Sin(90 * Mathf.Deg2Rad);
                            float sinB = Mathf.Sin(angleB * Mathf.Deg2Rad);
                            float sinC = Mathf.Sin(angleC * Mathf.Deg2Rad);
                            //b / sin B = c / sin C
                            //b / sin(83°B) = 7 / sin(62°C)
                            //b = (7 × sin(83°B))/ sin(62°C)
                            float sideB = ((sideA * sinB) / sinC);
                            //Calculate Hypotenuse Length
                            float hypotenuse = CiDyUtils.HypotenuseLength(sideB, sideA);
                            //Catch Degeneracy Event
                            if (hypotenuse == Mathf.Infinity || hypotenuse == 0)
                            {
                                //Debug.LogError("Point: " + i + " Is == Infinity, Moved Manually");
                                bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                                //Move relative to World Position
                                bisector = curPos + (bisector * insetAmount / 2);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Store VectorLines
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/

                            }
                            else
                            {
                                //Move relative to World Position
                                bisector = curPos + (bisector * hypotenuse);
                                //Add to Stored Reference List
                                walkableNodes.Add(bisector);
                                //Debug.Log("Sins = SinA: " + sinA + " SinB: " + sinB + " SinC: " + sinC);
                                //Debug.Log("AngleA = 90, " + "AngleB: " + angleB + " AngleC: " + angleC + " = " + (90 + (angleB + angleC)));
                                //Debug.Log("Hypotenuse: " + hypotenuse);
                                /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                                vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                                vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                            }
                        }
                        else if (angleDir == 0)
                        {
                            //Debug.Log(i+" Moved Right Manually");
                            bisector = ((curPos + (right * insetAmount)) - curPos).normalized;
                            //Move relative to World Position
                            bisector = curPos + (bisector * insetAmount / 2);
                            //Add to Stored Reference List
                            walkableNodes.Add(bisector);
                            //Store VectorLines
                            /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                            vectorLines.Add(new ColoredLines(curPos, bisector, Color.white));
                            vectorLines.Add(new ColoredLines(curPos, curPos + (right * 1.38f), Color.yellow));*/
                        }
                    }
                    else
                    {
                        //Special Degeneracy logic for Single Point Connections.(Dead Ends)
                        //Check for Single Point connection.
                        //Create Right Vector3 and Left Vector3 of Fwd Direction
                        Vector3 nR = curPos + (fwd * sideA) + (right * sideA);
                        Vector3 nL = curPos + (fwd * sideA) + (-right * sideA);
                        walkableNodes.Add(nR);
                        walkableNodes.Add(nL);
                        //Store VectorLines
                        /*vectorLines.Add(new ColoredLines(curPos, curPos + (fwd * 1.38f), Color.blue));
                        vectorLines.Add(new ColoredLines(curPos, nR, Color.white));
                        vectorLines.Add(new ColoredLines(curPos, nL, Color.white));*/
                    }
                }
            }

            List<List<CiDyNode>> nodes = new List<List<CiDyNode>>();

            walkableNodes.Reverse();
            LAV.Reverse();
            for (int i = 0; i < walkableNodes.Count; i++)
            {
                LAV[i].position = walkableNodes[i];
            }
            nodes.Add(LAV);
            //Return final List
            return nodes;
        }

        //Return HypotenuseLength = A(sqr)+B(sqr) = C(sqr)
        public static float HypotenuseLength(float sideALength, float sideBLength)
        {
            return Mathf.Sqrt(sideALength * sideALength + sideBLength * sideBLength);
        }

        // AngleDir Function From HigherScriptingAuthority on Unity Community
        public static int AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up)
        {
            int finalDir = 0;//Default
            Vector3 perp = Vector3.Cross(fwd, targetDir);
            perp = new Vector3(Mathf.Round(perp.x * 100) / 100, Mathf.Round(perp.y * 100) / 100, Mathf.Round(perp.z * 100) / 100);
            float dir = Vector3.Dot(perp, up);

            if (dir > 0f)
            {
                //Right
                finalDir = 1;
            }
            else if (dir < 0f)
            {
                //Left
                finalDir = -1;
            }
            else
            {
                //Collinear
                finalDir = 0;
            }
            return finalDir;
        }

        // AngleDir Function From HigherScriptingAuthority on Unity Community
        public static float AngleDir2(Vector3 fwd, Vector3 targetDir, Vector3 up)
        {
            Vector3 perp = Vector3.Cross(fwd, targetDir);
            perp = new Vector3(Mathf.Round(perp.x * 100) / 100, Mathf.Round(perp.y * 100) / 100, Mathf.Round(perp.z * 100) / 100);
            float dir = Vector3.Dot(perp, up);

            return dir;
        }

        //Determine if TargetDir is Left/Right of Forward Direction Vector
        public static int AngleDirection(Vector3 fwd, Vector3 targetDir, Vector3 up)
        {
            Vector3 perp = Vector3.Cross(fwd, targetDir);
            float dir = Vector3.Dot(perp, up);
            //Debug.Log("Dir: " + dir);
            if (dir > 0.0f)
            {
                //Right
                return 1;
            }
            else if (dir < 0.0f)
            {
                //Left
                return -1;
            }
            else
            {
                //Parallel
                return 0;
            }
        }

        //Marching Squares Polygon Mesh Creator. (Flat shapes only produced)
        public static Mesh CreateMeshFromPoly(Vector3[] polygon, int resolution, ref CiDyVoxelSquare[,] squares)
        {
            //Take a Polygon Outline(Flattend y dimension(2D)). And Place Points into its interior using Marching Sqaures to create a Mesh.
            Vector3 centroid = CiDyUtils.FindCentroid(polygon.ToList());
            //Convert Original Polygon into CiDyVoxels for later use.
            CiDyVoxel[] origPoly = new CiDyVoxel[polygon.Length];
            //Get Updated Polygon to Users Points.
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector3 flatPos = polygon[i];
                flatPos.y = centroid.y;
                //polygon[i] = flatPos;
                origPoly[i] = new CiDyVoxel(flatPos, 0.5f);
                origPoly[i].polyIndex = i;
            }

            //Place CiDyVoxels in the OBB Area of the Polygon.
            //float farthest = 0f;
            //int farthestIndex = 0;
            //Create Bounds at Centroid
            Bounds bounds = new Bounds(centroid, Vector3.one);
            for (int i = 0; i < polygon.Length; i++)
            {
                //Encapsulate All Points for a proper Sized OBB Collider
                bounds.Encapsulate(polygon[i]);
            }
            bounds.size = bounds.size * 1.618f;
            //CreateBoundsList for Intersection Testing
            Vector3 boundsCntr = bounds.center;
            Vector3 boundPoint1 = bounds.min;//LowerLeft
            Vector3 boundMax = bounds.max;//Needed To Calculate other Points.
            Vector3 boundPoint2 = new Vector3(boundPoint1.x, boundPoint1.y, boundMax.z);//UpperLeft
            Vector3 boundPoint3 = new Vector3(boundMax.x, boundPoint1.y, boundMax.z);//UpperRight
                                                                                     //Vector3 boundPoint4 = new Vector3(boundMax.x, boundPoint1.y, boundPoint1.z);//LowerRight
                                                                                     //Area = L*W;//Calculate Area of Bounding Box
            float width = Vector3.Distance(boundPoint2, boundPoint3);//Width
            float length = Vector3.Distance(boundPoint2, boundPoint1);//Length
                                                                      //float area = length * width;
            Vector3 boundSize = bounds.size;
            Vector3 extents = bounds.extents;

            //Get Current Box Position
            Vector3[] boundFootPrint = new Vector3[4];
            //boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, extents.y, extents.z);//Top Front Right
            //boundFootPrint[1] = (boundsCntr) + new Vector3(extents.x, extents.y, -extents.z);//Top Back Right
            boundFootPrint[0] = (boundsCntr) + new Vector3(extents.x, -extents.y, extents.z);//Bottom Front Right
            boundFootPrint[3] = (boundsCntr) + new Vector3(extents.x, -extents.y, -extents.z);//Bottom Back Right
                                                                                              //boundFootPrint[4] = (boundsCntr) + new Vector3(-extents.x, extents.y, extents.z);//Top Front Left
                                                                                              //boundFootPrint[5] = (boundsCntr) + new Vector3(-extents.x, extents.y, -extents.z);//Top Back Left
            boundFootPrint[1] = (boundsCntr) + new Vector3(-extents.x, -extents.y, extents.z);//Bottom Front Left
            boundFootPrint[2] = (boundsCntr) + new Vector3(-extents.x, -extents.y, -extents.z);//Bottom Back Left



            float xRes = (width / resolution);
            //Determine how many X value points we want. 
            float zRes = (length / resolution);
            //Debug.Log("XRes: "+xRes+" YRes: "+zRes+" Width: "+width+" Length: "+length);

            int j = 0;
            int count = (resolution + 1);
            CiDyVoxel[,] voxels = new CiDyVoxel[count, count];

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++, j++)
                {

                    float xSpacing = boundFootPrint[2].x + xRes * x;
                    float zSpacing = boundFootPrint[2].z + zRes * z;

                    //voxels[x * xRes + z] = new Vector3(boundFootPrint[2].x * x, 0, boundFootPrint[2].z * z);
                    //GameObject sphere = CiDyUtils.MarkPoint(new Vector3(xSpacing, 0, zSpacing), j);
                    //Vector3 newPos = new Vector2(xSpacing, zSpacing);
                    voxels[x, z] = new CiDyVoxel(new Vector3(xSpacing, 0, zSpacing), 0.5f);
                    //Check if Position is inside Polygon
                    //if (CiDyUtils.PointInsideOrOnLinePolygon(polygon.ToList(), new Vector3(xSpacing, 0, zSpacing)))
                    if (CiDyUtils.PointInsideOrOnLinePolygon(polygon.ToList(), new Vector3(xSpacing, 0, zSpacing)))
                    {
                        voxels[x, z].state = true;
                    }
                    else
                    {
                        voxels[x, z].state = false;
                    }
                }
            }
            //Create CiDyVoxel Squares
            //CiDyVoxelSquare[,] squares = new CiDyVoxelSquare[resolution, resolution];
            squares = new CiDyVoxelSquare[resolution, resolution];
            //Iterate through CiDyVoxels and Create Squares that hold the connections to the CiDyVoxels and there States.
            for (int z = 0; z <= resolution - 1; z++)
            {
                for (int x = 0; x <= resolution - 1; x++, j++)
                {
                    //We will need all four Points. Upper, Right and top Right.
                    CiDyVoxel upper = null;
                    CiDyVoxel right = null;
                    CiDyVoxel topRight = null;
                    //Add Upper
                    upper = voxels[x, z + 1];
                    //Right
                    right = voxels[x + 1, z];
                    //TopRight
                    topRight = voxels[x + 1, z + 1];
                    //Create and Store Reference to Square
                    CiDyVoxelSquare square = new CiDyVoxelSquare(upper, topRight, right, voxels[x, z]);
                    squares[x, z] = square;
                }
            }

            //Perform Interpolation On Edge Squares
            //Iterate through the Squares and Calculate there Edges to the Polygons Actual Shape.
            for (int x = 0; x < squares.GetLength(0); x++)
            {
                for (int z = 0; z < squares.GetLength(1); z++)
                {
                    CiDyVoxelSquare square = squares[x, z];
                    //We are only interseted in CiDyVoxels that are not Fully Engulfed in the Shape.
                    if (square.configuration == 15)
                    {
                        //This is a full voxel. No Edge cases possible. Skip
                        continue;
                    }
                    //Check for Degeneracy Event. Where a Polygon Point is inside a Square's Shape.
                    for (int i = 0; i < polygon.Length; i++)
                    {
                        if (CiDyUtils.PointInsideOrOnLinePolygon(square.SquareOutline().ToList(), polygon[i]))
                        {
                            //Debug.Log("Degeneracy Event: ");
                            //For these special Degeneracy Events we need to add the polygon Points into the Square for its triangulation step.
                            squares[x, z].AddExteriorPoint(origPoly[i]);
                        }
                    }
                    //If here then there is a potential edge case that needs accounted for.
                    //Determine if case is for centreTop,CentreRight,CentreLeft or CentreBottom
                    //Is bottomLeft Active?
                    if (square.bottomLeft.state)
                    {
                        //Check Top Left and Bottom Right States
                        if (!square.topLeft.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.bottomLeft.position, square.topLeft.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreLeft.position = intersection;
                            }
                        }
                        if (!square.bottomRight.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.bottomLeft.position, square.bottomRight.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreBottom.position = intersection;
                            }
                        }
                    }
                    if (square.bottomRight.state)
                    {
                        //Check for Bottom Right Cases
                        //Check Top Left and Bottom Right States
                        if (!square.topRight.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.bottomRight.position, square.topRight.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreRight.position = intersection;
                            }
                        }
                        if (!square.bottomLeft.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.bottomRight.position, square.bottomLeft.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreBottom.position = intersection;
                            }
                        }
                    }
                    if (square.topLeft.state)
                    {
                        //Top Left
                        //Check Top Left and Bottom Right States
                        if (!square.bottomLeft.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.topLeft.position, square.bottomLeft.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreLeft.position = intersection;
                            }
                        }
                        if (!square.topRight.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.topLeft.position, square.topRight.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreTop.position = intersection;
                            }
                        }
                    }
                    if (square.topRight.state)
                    {
                        //Check Top Left and Bottom Right States
                        if (!square.topLeft.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.topRight.position, square.topLeft.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreTop.position = intersection;
                            }
                        }
                        if (!square.bottomRight.state)
                        {
                            //Get Linear Interpolation for this point against the Polygon.
                            Vector3 intersection = Vector3.zero;

                            if (CiDyUtils.IntersectsList(square.topRight.position, square.bottomRight.position, polygon.ToList(), ref intersection, true))
                            {
                                //We have the Intersection. So the Middle Point needs adjusted to the new intersection.
                                squares[x, z].centreRight.position = intersection;
                            }
                        }
                    }
                }
            }
            //Get Reference to Filter and Renderer
            //Create Mesh
            Mesh marchingSquareMesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>(0);
            List<int> triangles = new List<int>(0);
            //Now we must triangulate the Mesh from the CiDyVoxels.
            marchingSquareMesh.name = "Marching Square Mesh";

            for (int x = 0; x < squares.GetLength(0); x++)
            {
                for (int y = 0; y < squares.GetLength(1); y++)
                {
                    TriangulateSquare(squares[x, y], ref triangles, ref vertices);
                }
            }

            marchingSquareMesh.vertices = vertices.ToArray();
            marchingSquareMesh.triangles = triangles.ToArray();
            //Create UVS from Positions x,z of Points in mesh.
            Vector2[] newUVs = new Vector2[marchingSquareMesh.vertices.Length];
            for (int i = 0; i < marchingSquareMesh.vertices.Length; i++)
            {
                newUVs[i] = new Vector2(marchingSquareMesh.vertices[i].x, marchingSquareMesh.vertices[i].z);
            }
            marchingSquareMesh.uv = newUVs;
            //Now Recalculate Normals
            marchingSquareMesh.RecalculateNormals();
            marchingSquareMesh.RecalculateBounds();
            return marchingSquareMesh;
        }

        //Marching Squares Code
        static void TriangulateSquare(CiDyVoxelSquare square, ref List<int> triangles, ref List<Vector3> vertices)
        {
            //Sort by the x Value then by Z value
            //square.exteriorPoints = square.exteriorPoints.OrderBy(x => x.position.x).ThenBy(x => x.position.z).ToArray();

            switch (square.configuration)
            {
                case 0:
                    break;
                //1 Points
                case 1:
                    //Bottom Left Corner |-< Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        //Degeneracy Event.
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                //Visualize Extra Points
                                //GameObject sphere = CiDyUtils.MarkPoint(square.exteriorPoints[i], i);
                                //sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                                //Iterate through Points and Create Triangles connecting to Bottom Left Corner.
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.centreBottom);
                                    }
                                    else
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.exteriorPoints[i], square.bottomLeft);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomLeft);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomLeft);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only One Point
                            //Connect to Bottom and Left Edge
                            MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centreLeft, square.exteriorPoints[0]);
                            //Last point connects to Bottom Edge and Center Bottom
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreBottom, square.bottomLeft);
                        }
                    }
                    else
                    {
                        //Standard Bottom Left Corner Interpolation
                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.bottomLeft, square.centreLeft);
                    }
                    break;
                case 2:
                    //Bottom Right Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        //Degeneracy Event.
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                //Visualize Extra Points
                                //GameObject sphere = CiDyUtils.MarkPoint(square.exteriorPoints[i], i);
                                //sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                                //Iterate through Points and Create Triangles connecting to Bottom Left Corner.
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.exteriorPoints[i], square.centreRight);
                                    }
                                    else
                                    {
                                        //Connect to 
                                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.exteriorPoints[i], square.bottomRight);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.exteriorPoints[i], square.centreRight);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomRight);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomRight);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only One Point
                            //Connect to Bottom and Left Edge
                            MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.exteriorPoints[0]);
                            //Last point connects to Bottom Edge and Center Bottom
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomRight, square.centreBottom);
                        }
                    }
                    else
                    {
                        //Standard Bottom Right Corner
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.centreBottom);
                    }
                    break;
                case 4:
                    //Top Right Corner -> Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        //Degeneracy Event.
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);
                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                //Visualize Extra Points
                                //GameObject sphere = CiDyUtils.MarkPoint(square.exteriorPoints[i], i);
                                //sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                                //Iterate through Points and Create Triangles connecting to Bottom Left Corner.
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.exteriorPoints[i], square.topRight);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topRight, square.exteriorPoints[i]);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.centreRight);
                                    }
                                    else
                                    {

                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.exteriorPoints[i - 1]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreTop, square.topRight);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topRight, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.exteriorPoints[i - 1]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only One Point
                            //Connect to Bottom and Left Edge
                            MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[0]);
                            //Last point connects to Bottom Edge and Center Bottom
                            MeshFromPoints(ref triangles, ref vertices, square.topRight, square.exteriorPoints[0], square.centreTop);
                        }
                    }
                    else
                    {
                        //Create Single Mesh Piece
                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.centreRight);
                    }
                    break;
                case 8:
                    //Top Left Corner <- Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        //Degeneracy Event.
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                //Visualize Extra Points
                                //GameObject sphere = CiDyUtils.MarkPoint(square.exteriorPoints[i], i);
                                //sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                                //Iterate through Points and Create Triangles connecting to Bottom Left Corner.
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.exteriorPoints[i], square.centreLeft);
                                    }
                                    else
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topLeft, square.exteriorPoints[i]);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.exteriorPoints[i - 1]);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreLeft, square.topLeft);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topLeft, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.exteriorPoints[i - 1]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only One Point
                            MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[0]);
                            //Last point connects to Bottom Edge and Center Bottom
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreLeft, square.topLeft);
                        }
                    }
                    else
                    {
                        //Create Single Mesh Piece
                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centreLeft);
                    }
                    break;
                //2 Pionts
                case 3:
                    //Bottom Two Points Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centreLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.exteriorPoints[i], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.exteriorPoints[i], square.centreRight);
                                        //Connect Final
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreBottom);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreBottom);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreBottom);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreBottom);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point
                            MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centreLeft, square.exteriorPoints[0]);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreBottom, square.bottomLeft);
                            //Two other Points
                            MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.exteriorPoints[0], square.bottomRight);
                            MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.exteriorPoints[0], square.centreRight);
                            //Connect Bottom Edge to Center Bottom.
                            //MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centreBottom, square.bottomRight);
                        }
                    }
                    else
                    {
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.bottomLeft, square.centreLeft);
                    }
                    break;
                case 6:
                    //Right Two Points Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.left, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.centreBottom, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreRight, square.bottomRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[i]);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.exteriorPoints[i], square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.exteriorPoints[i], square.centreTop);
                                        //Connect Final
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.centreBottom, square.exteriorPoints[i]);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreRight);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point
                            MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.centreBottom, square.exteriorPoints[0]);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreRight, square.bottomRight);
                            //Two other Points
                            MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.exteriorPoints[0], square.topRight);
                            MeshFromPoints(ref triangles, ref vertices, square.topRight, square.exteriorPoints[0], square.centreTop);
                        }
                    }
                    else
                    {
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.centreBottom);
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.centreTop, square.topRight);
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.centreBottom, square.centreTop);
                        //Original
                        //MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.bottomRight, square.centreBottom);
                    }
                    break;
                case 9:
                    //Left Two Points Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Determine Angle Direction
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.right, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        ////Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.exteriorPoints[i], square.topLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreLeft, square.topLeft);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.centreBottom);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.exteriorPoints[i], square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.centreBottom);
                                        //Connect Final
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreLeft);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreLeft, square.topLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.centreTop);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreLeft);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreLeft);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point
                            MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[0]);
                            MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.exteriorPoints[0], square.centreLeft);
                            //Two other Points
                            MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.exteriorPoints[0], square.bottomLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[0], square.centreBottom);
                        }
                    }
                    else
                    {
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.centreTop);
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.centreBottom, square.bottomLeft);
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.centreBottom, square.centreLeft);
                        //Original Two Triangles
                        //MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centreBottom, square.bottomLeft);
                    }
                    break;
                case 12:
                    //Top Two Points Active
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.forward, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Bottom and Left Edge
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreTop, square.topRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.centreTop);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.exteriorPoints[i], square.topLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.exteriorPoints[i], square.centreLeft);
                                        //Connect Final
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreTop);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreTop);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreTop, square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.centreRight);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.centreTop);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.centreTop);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point
                            MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.exteriorPoints[0], square.centreLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[0]);
                            //Two other Points
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreTop, square.topRight);
                            MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[0]);
                        }
                    }
                    else
                    {
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.centreTop);
                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.centreRight);
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.centreTop, square.centreRight);
                        //original
                        //MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.topRight, square.centreRight, square.centreLeft);
                    }
                    break;
                //Diagonal 2 Points
                case 5:
                    MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft, square.centreLeft);
                    break;
                case 10:
                    MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.centreBottom, square.centreLeft);
                    break;
                //Three Point Cases
                case 7:
                    //Three Right Points are active. Top Left is Inactive.
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.left, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Bottom Left Triangles
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.exteriorPoints[i], square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.exteriorPoints[i]);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == 1)
                                    {
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.exteriorPoints[i]);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreRight, square.bottomRight);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                    }
                                }
                                else
                                {
                                    if (angleDir == 1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.bottomRight);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.bottomRight);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point, There are 6 triangles
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreTop, square.topRight);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topRight, square.centreRight);
                            //Second Trinagle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreRight, square.bottomRight);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomRight, square.centreBottom);
                            //Thrid Triangle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreBottom, square.bottomLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomLeft, square.centreLeft);
                        }
                    }
                    else
                    {
                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.bottomRight, square.bottomLeft, square.centreLeft);
                    }
                    break;
                case 11:
                    //Left three Points, Top Right is inactive
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.right, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Only one Point, There are 6 triangles
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreRight, square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                    }
                                    else
                                    {
                                        //Only one Point, There are 6 triangles
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.exteriorPoints[i], square.centreLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.exteriorPoints[i - 1]);
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i - 1], square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.centreBottom);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.exteriorPoints[i], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreRight, square.bottomRight);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i], square.exteriorPoints[i - 1]);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.exteriorPoints[i - 1], square.exteriorPoints[i]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point, There are 6 triangles
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topLeft, square.centreTop);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreLeft, square.topLeft);
                            //Second Trinagle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomLeft, square.centreLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreBottom, square.bottomLeft);
                            //Thrid Triangle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomRight, square.centreBottom);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreRight, square.bottomRight);
                        }
                    }
                    else
                    {
                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centre, square.centreLeft);
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.centre, square.bottomLeft);
                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centre, square.centreBottom);
                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.centre, square.bottomRight);
                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.centre, square.centreRight);
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.centre, square.centreTop);
                        //Original
                        //MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.bottomLeft);
                    }
                    break;
                case 13:
                    //Bottom Right is inactive. Other three are. Topleft,TopRight,BottomLeft
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.right, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Only one Point, There are 6 triangles
                                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.bottomLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centreLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.exteriorPoints[i], square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.exteriorPoints[i], square.centreTop);
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topLeft, square.exteriorPoints[i]);
                                        //Last point connects to Bottom Edge and Center Bottom as well as to last point
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.centreTop);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreTop, square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.centreRight);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.exteriorPoints[i - 1]);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreLeft, square.topLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomLeft, square.centreLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreBottom, square.bottomLeft);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.topLeft, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topLeft, square.exteriorPoints[i - 1]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point, There are 6 triangles
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreTop, square.topRight);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topLeft, square.centreTop);
                            //Second Trinagle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreLeft, square.topLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomLeft, square.centreLeft);
                            //Thrid Triangle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreBottom, square.bottomLeft);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topRight, square.centreRight);
                        }
                    }
                    else
                    {
                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft);
                    }
                    break;
                case 14:
                    //Bottome Left is inactive
                    if (square.exteriorPoints.Length != 0)
                    {
                        if (square.exteriorPoints.Length > 1)
                        {
                            //Create Direction Vector
                            Vector3 dir = (square.exteriorPoints[1].position - square.exteriorPoints[0].position).normalized;
                            //Check direction angle
                            int angleDir = AngleDir(Vector3.left, dir, Vector3.up);

                            for (int i = 0; i < square.exteriorPoints.Length; i++)
                            {
                                if (i == 0)
                                {
                                    if (angleDir == -1)
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.exteriorPoints[i]);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.exteriorPoints[i]);
                                    }
                                    else
                                    {
                                        //Only one Point, There are 6 triangles
                                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.exteriorPoints[i], square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreRight, square.bottomRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.centreRight);
                                    }
                                }
                                else if (i == square.exteriorPoints.Length - 1)
                                {
                                    if (angleDir == -1)
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.topRight, square.centreRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.exteriorPoints[i], square.centreRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.bottomRight, square.centreBottom);
                                    }
                                    else
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.centreTop, square.topRight);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.exteriorPoints[i], square.topLeft);
                                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.exteriorPoints[i]);
                                    }
                                }
                                else
                                {
                                    if (angleDir == -1)
                                    {
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i], square.exteriorPoints[i - 1], square.topRight);
                                    }
                                    else
                                    {
                                        //Connect to Middle Triangles of points.
                                        MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[i - 1], square.exteriorPoints[i], square.topRight);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Only one Point, There are 6 triangles
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topLeft, square.centreTop);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreTop, square.topRight);
                            //Second Trinagle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.topRight, square.centreRight);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreLeft, square.topLeft);
                            //Thrid Triangle Set
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.bottomRight, square.centreBottom);
                            MeshFromPoints(ref triangles, ref vertices, square.exteriorPoints[0], square.centreRight, square.bottomRight);
                        }
                    }
                    else
                    {
                        //
                        MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.centreTop, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.topRight, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.bottomRight, square.centreBottom, square.centre);
                        MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.centreLeft, square.centre);

                        //Original
                        //MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.topRight, square.bottomRight, square.centreBottom, square.centreLeft);
                    }
                    break;
                //4 Point Case
                case 15:
                    //Bottom Center two triangles
                    MeshFromPoints(ref triangles, ref vertices, square.bottomLeft, square.centre, square.centreBottom);
                    MeshFromPoints(ref triangles, ref vertices, square.centreBottom, square.centre, square.bottomRight);
                    //
                    MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.centre, square.bottomLeft);
                    MeshFromPoints(ref triangles, ref vertices, square.centreRight, square.bottomRight, square.centre);
                    //
                    MeshFromPoints(ref triangles, ref vertices, square.centre, square.centreTop, square.topRight);
                    MeshFromPoints(ref triangles, ref vertices, square.topRight, square.centreRight, square.centre);
                    //
                    MeshFromPoints(ref triangles, ref vertices, square.centreLeft, square.topLeft, square.centre);
                    MeshFromPoints(ref triangles, ref vertices, square.centreTop, square.centre, square.topLeft);
                    //MeshFromPoints(ref triangles, ref vertices, square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                    break;
            }
        }

        static void MeshFromPoints(ref List<int> triangles, ref List<Vector3> vertices, params CiDyVoxel[] points)
        {
            AssignVertices(ref vertices, points);

            if (points.Length >= 3)
            {
                CreateTriangle(points[0], points[1], points[2], ref triangles);
            }
            if (points.Length >= 4)
            {
                CreateTriangle(points[0], points[2], points[3], ref triangles);
            }
            if (points.Length >= 5)
            {
                CreateTriangle(points[0], points[3], points[4], ref triangles);
            }
            if (points.Length >= 6)
            {
                CreateTriangle(points[0], points[4], points[5], ref triangles);
            }
        }

        static void AssignVertices(ref List<Vector3> vertices, params CiDyVoxel[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].vertexIndex == -1)
                {
                    points[i].vertexIndex = vertices.Count;
                    vertices.Add(points[i].position);
                }
            }
        }

        static void CreateTriangle(CiDyVoxel a, CiDyVoxel b, CiDyVoxel c, ref List<int> triangles)
        {
            triangles.Add(a.vertexIndex);
            triangles.Add(b.vertexIndex);
            triangles.Add(c.vertexIndex);
        }

        // Clone a mesh
        public static Mesh CloneMesh(Mesh mesh)
        {
            Mesh clone = new Mesh();
            clone.vertices = mesh.vertices;
            clone.normals = mesh.normals;
            clone.tangents = mesh.tangents;
            clone.triangles = mesh.triangles;
            clone.uv = mesh.uv;
            clone.uv2 = mesh.uv2;
            clone.bindposes = mesh.bindposes;
            clone.boneWeights = mesh.boneWeights;
            clone.bounds = mesh.bounds;
            clone.colors = mesh.colors;
            clone.name = mesh.name;
            //TODO : Are we missing anything?
            return clone;
        }

        public static Mesh ConformMeshToTerrain(Mesh mesh, Transform meshTransform, Terrain terrain, float offsetMultiplier)
        {

            RaycastHit hit;

            Mesh newMesh = mesh;

            int layerMask = (1 << Terrain.activeTerrain.gameObject.layer);

            Vector3[] verts = new Vector3[mesh.vertices.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = mesh.vertices[i];//Grab Original

                //CiDyUtils.MarkPoint(verts[i]+transform.position, i);
                //Iterate through Vertices and move them down to the Terrain raycast hit point.
                if (Physics.Raycast((verts[i] + meshTransform.position) + (Vector3.up * 500), Vector3.down, out hit, 1000, layerMask))
                {
                    verts[i] = (hit.point - meshTransform.position) + (Vector3.up * offsetMultiplier);
                }
            }

            //feed back to mesh
            newMesh.vertices = verts;
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();
            //Return the Final Mesh
            return newMesh;
        }
        // Finds a set of adjacent vertices for a given vertex
        // Note the success of this routine expects only the set of neighboring faces to eacn contain one vertex corresponding
        // to the vertex in question
        public static List<Vector3> findAdjacentNeighbors(Vector3[] v, int[] t, Vector3 vertex)
        {
            List<Vector3> adjacentV = new List<Vector3>();
            List<int> facemarker = new List<int>();
            int facecount = 0;

            // Find matching vertices
            for (int i = 0; i < v.Length; i++)
                if (Mathf.Approximately(vertex.x, v[i].x) &&
                    Mathf.Approximately(vertex.y, v[i].y) &&
                    Mathf.Approximately(vertex.z, v[i].z))
                {
                    int v1 = 0;
                    int v2 = 0;
                    bool marker = false;

                    // Find vertex indices from the triangle array
                    for (int k = 0; k < t.Length; k = k + 3)
                        if (facemarker.Contains(k) == false)
                        {
                            v1 = 0;
                            v2 = 0;
                            marker = false;

                            if (i == t[k])
                            {
                                v1 = t[k + 1];
                                v2 = t[k + 2];
                                marker = true;
                            }

                            if (i == t[k + 1])
                            {
                                v1 = t[k];
                                v2 = t[k + 2];
                                marker = true;
                            }

                            if (i == t[k + 2])
                            {
                                v1 = t[k];
                                v2 = t[k + 1];
                                marker = true;
                            }

                            facecount++;
                            if (marker)
                            {
                                // Once face has been used mark it so it does not get used again
                                facemarker.Add(k);

                                // Add non duplicate vertices to the list
                                if (isVertexExist(adjacentV, v[v1]) == false)
                                {
                                    adjacentV.Add(v[v1]);
                                    //Debug.Log("Adjacent vertex index = " + v1);
                                }

                                if (isVertexExist(adjacentV, v[v2]) == false)
                                {
                                    adjacentV.Add(v[v2]);
                                    //Debug.Log("Adjacent vertex index = " + v2);
                                }
                                marker = false;
                            }
                        }
                }

            //Debug.Log("Faces Found = " + facecount);

            return adjacentV;
        }


        // Finds a set of adjacent vertices indexes for a given vertex
        // Note the success of this routine expects only the set of neighboring faces to eacn contain one vertex corresponding
        // to the vertex in question
        public static List<int> findAdjacentNeighborIndexes(Vector3[] v, int[] t, Vector3 vertex)
        {
            List<int> adjacentIndexes = new List<int>();
            List<Vector3> adjacentV = new List<Vector3>();
            List<int> facemarker = new List<int>();
            int facecount = 0;

            // Find matching vertices
            for (int i = 0; i < v.Length; i++)
                if (Mathf.Approximately(vertex.x, v[i].x) &&
                    Mathf.Approximately(vertex.y, v[i].y) &&
                    Mathf.Approximately(vertex.z, v[i].z))
                {
                    int v1 = 0;
                    int v2 = 0;
                    bool marker = false;

                    // Find vertex indices from the triangle array
                    for (int k = 0; k < t.Length; k = k + 3)
                        if (facemarker.Contains(k) == false)
                        {
                            v1 = 0;
                            v2 = 0;
                            marker = false;

                            if (i == t[k])
                            {
                                v1 = t[k + 1];
                                v2 = t[k + 2];
                                marker = true;
                            }

                            if (i == t[k + 1])
                            {
                                v1 = t[k];
                                v2 = t[k + 2];
                                marker = true;
                            }

                            if (i == t[k + 2])
                            {
                                v1 = t[k];
                                v2 = t[k + 1];
                                marker = true;
                            }

                            facecount++;
                            if (marker)
                            {
                                // Once face has been used mark it so it does not get used again
                                facemarker.Add(k);

                                // Add non duplicate vertices to the list
                                if (isVertexExist(adjacentV, v[v1]) == false)
                                {
                                    adjacentV.Add(v[v1]);
                                    adjacentIndexes.Add(v1);
                                    //Debug.Log("Adjacent vertex index = " + v1);
                                }

                                if (isVertexExist(adjacentV, v[v2]) == false)
                                {
                                    adjacentV.Add(v[v2]);
                                    adjacentIndexes.Add(v2);
                                    //Debug.Log("Adjacent vertex index = " + v2);
                                }
                                marker = false;
                            }
                        }
                }

            //Debug.Log("Faces Found = " + facecount);

            return adjacentIndexes;
        }

        // Does the vertex v exist in the list of vertices
        static bool isVertexExist(List<Vector3> adjacentV, Vector3 v)
        {
            bool marker = false;
            foreach (Vector3 vec in adjacentV)
                if (Mathf.Approximately(vec.x, v.x) && Mathf.Approximately(vec.y, v.y) && Mathf.Approximately(vec.z, v.z))
                {
                    marker = true;
                    break;
                }

            return marker;
        }

        //Will go along a Path and Find the Points that are Raised off "Terrain" Layer for Ground Support Creation.
        public static List<List<Vector3>> FindRaisedPoints(Vector3[] spline, int mask = -1)
        {
            if (mask == -1)
            {
                mask = 1 << LayerMask.NameToLayer("Terrain");
            }
            //We may have multiple Lists
            List<List<Vector3>> allRaisedPoints = new List<List<Vector3>>(0);
            List<Vector3> raisedPoints = new List<Vector3>(0);//Current List.
            bool hasStarted = false;
            float minHeight = 0.25f;

            //Iterate through source Path and Check Heights
            for (int i = 0; i < spline.Length; i++)
            {
                //Check Height
                Vector3 rayOrig = spline[i] + (Vector3.up * 1000);
                //Shoot Raycast downward
                RaycastHit hit;
                if (Physics.Raycast(rayOrig, Vector3.down, out hit, Mathf.Infinity, mask))
                {
                    //Check Its Downward
                    if (hit.point.y < spline[i].y)
                    {
                        //Now Check Distance
                        float dist = Vector3.Distance(hit.point, spline[i]);
                        //Now determine if we are beginning the list or ending it.
                        if (!hasStarted)
                        {
                            //Check for Beginning
                            if (dist >= minHeight)
                            {
                                hasStarted = true;
                                //Okay. Lets start the List and Grab previous point(If applicable)
                                if (i != 0)
                                {
                                    raisedPoints.Add(spline[i - 1]);
                                }
                                raisedPoints.Add(spline[i]);
                            }
                        }
                        else
                        {
                            //Checking for Ending.
                            if (dist < minHeight)
                            {
                                //This is an Ending
                                raisedPoints.Add(spline[i]);
                                if (i != spline.Length - 1)
                                {
                                    raisedPoints.Add(spline[i + 1]);
                                }
                                List<Vector3> newList = new List<Vector3>(raisedPoints);
                                allRaisedPoints.Add(newList);
                                raisedPoints = new List<Vector3>(0);
                                hasStarted = false;
                            }
                            else
                            {
                                //Not an Ending. Just add point
                                raisedPoints.Add(spline[i]);
                            }
                        }
                    }
                    else
                    {
                        if (hasStarted)
                        {
                            //This is an End Point
                            //This is an Ending
                            raisedPoints.Add(spline[i]);
                            if (i != spline.Length - 1)
                            {
                                raisedPoints.Add(spline[i + 1]);
                            }
                            List<Vector3> newList = new List<Vector3>(raisedPoints);
                            allRaisedPoints.Add(newList);
                            raisedPoints = new List<Vector3>(0);
                            hasStarted = false;
                        }
                    }
                }
            }
            //If we have points remaining then we may have never hit the ground again.
            if (raisedPoints.Count > 0)
            {
                allRaisedPoints.Add(raisedPoints);
            }
            return allRaisedPoints;
        }

        public static Vector3[] CalculatePointsAlongPath(Vector3[] path, float spacing)
        {
            if (path == null || path.Length == 0)
            {
                throw new System.ArgumentException("Parameter cannot be null", "Vector3[] path");
            }
            List<Vector3> generatedPoints = new List<Vector3>(0);

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
                    actualLastPoint = lastLightPoint;
                    generatedPoints.Add(lastLightPoint);
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
                        //Place Point
                        if (lightsCurDist >= spacing)
                        {
                            generatedPoints.Add(lastLightPoint);
                            actualLastPoint = lastLightPoint;
                            //Reset Distance Moved.
                            lightsCurDist = 0f;
                        }
                    }
                }
                //Set Last one if its at least 3/4 the Desired Distance.
                if (j == path.Length - 2)
                {
                    //Calculate Distance Between Cur and P1
                    moveDist = Vector3.Distance(p1, actualLastPoint);
                    if (moveDist >= (spacing * 0.75f))
                    {
                        lastLightPoint = p1;
                        //Place Point
                        //Place Light nxtToCurb End. Reuse GameObject Memory
                        generatedPoints.Add(lastLightPoint);
                        actualLastPoint = lastLightPoint;
                    }
                }
            }

            //Return Final List
            return generatedPoints.ToArray();
        }

        //This function will Generate the Procedural Support Meshes based on Sampling the Roads Height from its Terrain.
        public static GameObject GenerateRoadSupport(Vector3[] spline, float baseWidth, float sideHeight, float sideWidth, float beamBaseWidth, float beamBaseHeight, float beamSpacing, Transform parentTransform, Material supportBaseMat = null)
        {
            //Grab Material
            if (supportBaseMat == null)
            {
                supportBaseMat = Resources.Load("CiDyResources/Concrete", typeof(Material)) as Material;
                if (supportBaseMat == null)
                {
                    Debug.LogWarning("No Concrete Material found in Resources Folder, CiDyUtils.cs Line 4472 GenerateRoadSupport()");
                }
            }
            //Offset Spline by SupportSideHeight/2
            for (int i = 0; i < spline.Length; i++)
            {
                spline[i].y -= (sideHeight / 2) + 0.1f;
            }
            //We Need to Calculate where the Support Beams are going to be based on User Desired Spacing and Calculate there Ground Points.
            Vector3[] supportPoints = CiDyUtils.CalculatePointsAlongPath(spline, beamSpacing);
            Vector3[] groundPoints = new Vector3[supportPoints.Length];
            for (int i = 0; i < supportPoints.Length; i++)
            {
                groundPoints[i] = supportPoints[i];
                //Find Terrain Hit Points and Determine the Distance from Source Point to Terrain.
                //Run a Raycast below this point.
                Vector3 rayOrig = groundPoints[i] + (Vector3.up * 1000);
                //Shoot Raycast downward
                RaycastHit hit;
                if (Physics.Raycast(rayOrig, Vector3.down, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Terrain")))
                {
                    //Return Height
                    groundPoints[i].y = (hit.point.y - 0.1f);
                }
            }
            //Create Base Mesh
            Vector3 center = Vector3.zero;
            Vector3 left = Vector3.left;
            Vector3[] shape = new Vector3[8];
            shape[0] = center + (left * ((baseWidth / 2) + sideWidth));
            shape[1] = shape[0] + (Vector3.up * sideHeight);
            shape[2] = shape[1] + ((-left) * sideWidth);
            shape[3] = shape[2] + (-Vector3.up * (sideHeight / 2));
            shape[4] = shape[3] + ((-left) * baseWidth);
            shape[5] = shape[4] + Vector3.up * (sideHeight / 2);
            shape[6] = shape[5] + ((-left) * sideWidth);
            shape[7] = shape[6] + (-Vector3.up) * sideHeight;

            System.Array.Reverse(shape);
            GameObject supportBase = new GameObject("SupportBase");
            supportBase.transform.position = parentTransform.position;
            supportBase.transform.SetParent(parentTransform);
            MeshRenderer sRenderer = supportBase.AddComponent<MeshRenderer>();
            MeshFilter sFilter = supportBase.AddComponent<MeshFilter>();
            sRenderer.sharedMaterial = supportBaseMat;
            sFilter.sharedMesh = CiDyUtils.ExtrudeRail(shape, spline, parentTransform);

            //Create Support Mesh with Height
            for (int i = 0; i < groundPoints.Length - 1; i++)
            {
                //Check and Make sure Beam is Not going to Hit a Road Mesh.
                RaycastHit hit;
                float colliderWidth = (beamBaseWidth / 2);
                if (Physics.BoxCast(supportPoints[i] + (Vector3.up * 1000), new Vector3(colliderWidth, colliderWidth, colliderWidth), Vector3.down, out hit, Quaternion.identity, Mathf.Infinity, 1 << LayerMask.NameToLayer("Road")))// (groundPoints[i]+(Vector3.up*1000), Vector3.down, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Road")))
                {
                    //We Hit a road, Skip It.
                    continue;
                }
                Vector3 curPos = supportPoints[i];
                Vector3 nxtPos = supportPoints[i + 1];
                Vector3 fwd = (nxtPos - curPos).normalized;
                fwd.y = 0;
                Vector3 middlePos = (nxtPos + curPos) / 2;
                //Create Box Mesh the Same Width as Road Width
                GameObject supportBeam = new GameObject("SupportBeam");
                supportBeam.transform.position = supportPoints[i] + (Vector3.down * (beamBaseHeight / 2));
                supportBeam.transform.SetParent(supportBase.transform);
                MeshRenderer beamRenderer = supportBeam.AddComponent<MeshRenderer>();
                MeshFilter beamFilter = supportBeam.AddComponent<MeshFilter>();
                beamRenderer.sharedMaterial = supportBaseMat;
                Mesh boxMesh = CiDyUtils.CreateBoxMesh(supportBeam.transform, baseWidth, beamBaseHeight, beamBaseWidth * 2, false);
                fwd.y = 0;
                //Rotate
                supportBeam.transform.LookAt(middlePos + (fwd * 1.618f));
                //Calculate This Beams Height
                float beamHeight = Vector3.Distance(groundPoints[i], supportPoints[i]);
                Mesh beamMesh = CiDyUtils.CreateBoxMesh(supportBase.transform, baseWidth, beamHeight, beamBaseWidth, false);
                Vector3[] verts = beamMesh.vertices;
                for (int j = 0; j < verts.Length; j++)
                {
                    verts[j] += (Vector3.down * ((beamHeight / 2) + (beamBaseHeight / 2)));
                }
                beamMesh.vertices = verts;
                //combine Meshes
                CombineInstance[] combine = new CombineInstance[2];
                combine[0].mesh = boxMesh;
                combine[1].mesh = beamMesh;
                combine[0].transform = combine[0].transform = supportBase.transform.localToWorldMatrix;
                combine[1].transform = combine[0].transform = supportBase.transform.localToWorldMatrix;
                //Initialize Doors
                Mesh finalMesh = new Mesh();
                //Combine
                finalMesh.CombineMeshes(combine, true, true);
                //Set Mesh
                beamFilter.sharedMesh = finalMesh;
            }

            return supportBase;
        }

        /*//Rotate Shape points
        Vector3 dir = Vector3.right;
        float angle = Vector3.Angle(Vector3.forward, dir);
        Vector3 rotatingAxis = Vector3.up;
        if (CiDyUtils.AngleDir(Vector3.forward, dir, rotatingAxis) == -1)
        {
            angle = -angle;
        }
        shape[0] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[0];*/

        //Request a preset shape
        public static Vector3[] ReturnPresetShape(int shapeInt)
        {

            Vector3[] shape = new Vector3[0];

            switch (shapeInt)
            {
                case 0:
                    //Train Rail
                    shape = GenerateTrainRailShape();
                    break;
                case 1:
                    //Highway Guard Rail
                    shape = GenerateGuardRailShape();
                    break;
                case 2:
                    shape = GenerateGuardPostShape();
                    break;
                default:
                    //Return Rail
                    shape = GenerateTrainRailShape();
                    break;
            }

            //Return final Shape
            return shape;
        }

        //Highway Guard Rail
        static float beamHeight = 0.73421875f;
        static float beamWidth = 0.2f;
        static float beamDepth = 0.2032f;
        static float beamEdgeDepth = 0.01905f;
        static float railHeight = 0.3683f;
        //static float railWidth = 0.0238125f;
        static float railDepth = 0.32385f;
        static float railSeamWidth = 0.028575f;

        static Vector3[] GenerateGuardPostShape()
        {
            //Left and Right Bottom
            Vector3 strtPoint = Vector3.zero;
            Vector3 left = Vector3.left;
            Vector3 fwd = Vector3.forward;
            //Calculate Shape based on dimensions
            Vector3[] guardRailPoints = new Vector3[12];
            //Bottom Left Point
            guardRailPoints[0] = strtPoint + (left * (beamWidth / 2));
            guardRailPoints[1] = guardRailPoints[0] + (-left * beamEdgeDepth);
            guardRailPoints[2] = guardRailPoints[1] + fwd * ((beamDepth / 2) - (beamEdgeDepth / 2));
            guardRailPoints[3] = guardRailPoints[2] + -left * (beamWidth - (beamEdgeDepth * 2));
            guardRailPoints[4] = guardRailPoints[0] + (-left * (beamWidth - beamEdgeDepth));
            guardRailPoints[5] = guardRailPoints[4] + (-left * beamEdgeDepth);
            guardRailPoints[6] = guardRailPoints[5] + (fwd * beamDepth);
            guardRailPoints[7] = guardRailPoints[6] + (left * beamEdgeDepth);
            guardRailPoints[8] = guardRailPoints[7] + -fwd * ((beamDepth / 2) - (beamEdgeDepth / 2));
            guardRailPoints[9] = guardRailPoints[8] + left * (beamWidth - (beamEdgeDepth * 2));
            guardRailPoints[10] = guardRailPoints[9] + fwd * ((beamDepth / 2) - (beamEdgeDepth / 2));
            guardRailPoints[11] = guardRailPoints[10] + (left * beamEdgeDepth);

            return guardRailPoints;
        }

        static Vector3[] GenerateGuardRailShape()
        {
            //Left and Right Bottom
            Vector3 strtPoint = Vector3.zero;
            Vector3 left = Vector3.left;
            //Calculate line and show me in the Shape Points array
            //Base Point
            //Plot bottom two points.
            Vector3[] guardRailPoints = new Vector3[8];
            //Pre calculate Rail Curve Height
            float railCurveHeight = (railHeight / 2) - (railSeamWidth / 2);
            guardRailPoints[0] = strtPoint + (left * (beamWidth / 2) + (Vector3.up * (beamHeight - railHeight)));
            guardRailPoints[1] = guardRailPoints[0] + (left * railDepth) + (Vector3.up * (railCurveHeight / 2));
            guardRailPoints[2] = guardRailPoints[0] + (Vector3.up * railCurveHeight);
            guardRailPoints[3] = guardRailPoints[2] + (Vector3.up * (railSeamWidth / 3));
            guardRailPoints[4] = guardRailPoints[2] + (Vector3.up * ((railSeamWidth / 3) * 2));
            guardRailPoints[5] = guardRailPoints[2] + (Vector3.up * railSeamWidth);
            guardRailPoints[6] = guardRailPoints[3] + (left * railDepth) + (Vector3.up * (railCurveHeight / 2));
            guardRailPoints[7] = guardRailPoints[3] + (Vector3.up * railCurveHeight);
            //Convert to Curved line
            Vector3[] railCurve = CiDyUtils.CreateBezier(guardRailPoints, 0.06f);
            //Now reverse and add to the back side.
            Vector3[] tmpCurve = CiDyUtils.CreateBezier(guardRailPoints, 0.06f);
            System.Array.Reverse(tmpCurve);
            guardRailPoints = new Vector3[railCurve.Length + tmpCurve.Length];
            int count = 0;
            for (int i = 0; i < railCurve.Length; i++)
            {
                guardRailPoints[count] = railCurve[i];
                count++;
            }
            for (int i = 0; i < tmpCurve.Length; i++)
            {
                guardRailPoints[count] = tmpCurve[i];
                count++;
            }

            //Return Shape
            return guardRailPoints;
        }

        //Train Rail
        static float baseWidth = 0.2794f;
        static float baseHeight = 0.0047625f;
        static float headWidth = 0.06985f;
        static float headDepth = 0.0381f;
        static float depth = 0.1524f;
        static float neutralAxis = 0.0142875f;

        static Vector3[] GenerateTrainRailShape()
        {

            Vector3 strtPoint = Vector3.zero;
            Vector3 left = Vector3.left;

            //Create Rail Shape
            //Plot bottom two points.
            Vector3[] points = new Vector3[12];
            //Left and Right Bottom
            points[0] = left * (baseWidth / 2);
            points[1] = -left * (baseWidth / 2);
            //left and Right Extruded Up by baseHeight
            points[2] = points[0] + (Vector3.up * baseHeight);
            points[3] = points[1] + (Vector3.up * baseHeight);
            //Move in the Left and right Points to create the Inset/Neutral Axis area
            points[4] = points[2] + (-left * ((baseWidth / 2) - (neutralAxis / 2)));
            points[5] = points[3] + (left * ((baseWidth / 2) - (neutralAxis / 2)));
            //Neutral Axis Extruded by Depth
            points[6] = points[4] + (Vector3.up * ((depth - baseHeight) - headDepth));
            points[7] = points[5] + (Vector3.up * ((depth - baseHeight) - headDepth));
            //bottom points of Head
            points[8] = points[6] + (left * (headWidth / 2));
            points[9] = points[7] + (-left * (headWidth / 2));
            //top Points of Head
            points[10] = points[8] + (Vector3.up * headDepth);
            points[11] = points[9] + (Vector3.up * headDepth);

            //Organize Shape Points in proper counter clockwise order
            List<Vector3> shape = new List<Vector3>(0);

            shape.Add(points[0]);
            shape.Add(points[1]);
            //left and Right Extruded Up by baseHeight
            shape.Add(points[3]);
            shape.Add(points[5]);
            //Move in the Left and right Points to create the Inset/Neutral Axis area
            shape.Add(points[7]);
            shape.Add(points[9]);
            //Neutral Axis Extruded by Depth
            //Curve these
            //Top Right First
            Vector3 strt = points[11] + (Vector3.down * (headDepth / 4));
            Vector3 middle = points[11];
            Vector3 end = points[11] + (left * (headDepth / 4));
            Vector3[] curve = new Vector3[3];
            curve[0] = strt;
            curve[1] = middle;
            curve[2] = end;

            curve = CiDyUtils.CreateBezier(curve, 0.01f);
            for (int i = 0; i < curve.Length; i++)
            {
                shape.Add(curve[i]);
            }
            //Now Top Left Curve
            strt = points[10] + (Vector3.down * (headDepth / 4));
            middle = points[10];
            end = points[10] + (-left * (headDepth / 4));
            curve = new Vector3[3];
            curve[0] = strt;
            curve[1] = middle;
            curve[2] = end;

            curve = CiDyUtils.CreateBezier(curve, 0.01f);
            System.Array.Reverse(curve);

            for (int i = 0; i < curve.Length; i++)
            {
                shape.Add(curve[i]);
            }
            //bottom points of Head
            shape.Add(points[8]);
            shape.Add(points[6]);
            //top Points of Head
            shape.Add(points[4]);
            shape.Add(points[2]);

            return shape.ToArray();
        }

        public static GameObject GenerateGuardRailandPost(Vector3[] line, bool leftFacing, Material railMat, Material postMat, bool addCollider = false, Vector3 startDir = default, Vector3 endDir = default)
        {
            //Copy the list.
            Vector3[] points = new Vector3[line.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = line[i];
            }
            line = points;
            //Flip Facing
            if (leftFacing)
            {
                System.Array.Reverse(line);
            }
            //Generate Guard Rail and Post
            //This holds the Entire Gameobject
            GameObject newObject = new GameObject("RailGuard");
            Transform ourTrans = newObject.transform;
            //Get Guard Rail Shape
            Vector3[] shapePoints = CiDyUtils.ReturnPresetShape(2);
            //Extrude this shape upward at a point.
            Mesh postMesh = CiDyUtils.ExtrudePrint(shapePoints, beamHeight, ourTrans, false);
            //Call Extrude along a line
            Mesh railMesh = CiDyUtils.ExtrudeRail(CiDyUtils.ReturnPresetShape(1), line, ourTrans, startDir, endDir);
            //Iterate along the Line and Extrude Post Mesh at the Points.
            Mesh[] postMeshes = new Mesh[line.Length];//Post for every point along the Line.
                                                      //Now Combine for Final Mesh
                                                      //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[postMeshes.Length];
            for (int i = 0; i < line.Length; i++)
            {
                postMeshes[i] = CiDyUtils.CloneMesh(postMesh);
                //Move vertices to new location
                Vector3[] verts = postMeshes[i].vertices;
                Vector3 curPos = line[i];
                Vector3 nxtPos = line[0];
                if (i < line.Length - 1)
                {
                    nxtPos = line[i + 1];
                }
                Vector3 fwdDir = (nxtPos - curPos).normalized;

                if (i == line.Length - 1)
                {
                    fwdDir = (line[i] - line[i - 1]).normalized;
                }
                Vector3 fwdDir2 = Vector3.zero;
                if (i < line.Length - 2)
                {
                    //There is room to grab the next
                    fwdDir2 = (line[i + 2] - nxtPos).normalized;
                }
                else
                {
                    //Keep Direction
                    fwdDir2 = fwdDir;
                }

                //Rotate Shape for First Position
                Vector3[] rotatedShape = new Vector3[verts.Length];
                for (int j = 0; j < verts.Length; j++)
                {
                    //Convert from Current Forward (Z) to new Fwd Dir
                    //Rotate Shape points
                    float angle = Vector3.Angle(Vector3.forward, fwdDir);
                    Vector3 rotatingAxis = Vector3.up;
                    if (CiDyUtils.AngleDir(Vector3.forward, fwdDir, rotatingAxis) == -1)
                    {
                        angle = -angle;
                    }
                    rotatedShape[j] = Quaternion.AngleAxis(angle, rotatingAxis) * verts[j];
                    if (i == line.Length - 1)
                    {
                        rotatedShape[j] += (curPos + (-fwdDir * (beamWidth * 2)));
                    }
                    else
                    {
                        rotatedShape[j] += curPos;
                    }
                }
                postMeshes[i].vertices = rotatedShape;
                postMeshes[i].RecalculateBounds();
                postMeshes[i].RecalculateNormals();
                //Move this Mesh based on 
                combine[i].mesh = postMeshes[i];
                combine[i].transform = ourTrans.localToWorldMatrix;
            }

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            //This holds the Post Mesh
            //GameObject newObject = new GameObject("RailGuard");
            newObject.AddComponent<MeshFilter>().sharedMesh = finalMesh;
            if (postMat != null)
            {
                newObject.AddComponent<MeshRenderer>().sharedMaterial = postMat;
            }
            else
            {
                newObject.AddComponent<MeshRenderer>();
            }

            //Create Sub Transform and Mesh
            GameObject rail = new GameObject("Rail");
            //This holds the Rail Mesh
            rail.AddComponent<MeshFilter>().sharedMesh = railMesh;
            //Add Collider
            if (addCollider)
            {
                MeshCollider mCollider = rail.AddComponent<MeshCollider>();
                mCollider.sharedMesh = railMesh;
            }
            if (railMat != null)
            {
                rail.AddComponent<MeshRenderer>().sharedMaterial = railMat;
            }
            else
            {
                rail.AddComponent<MeshRenderer>();
            }

            rail.transform.parent = newObject.transform;

            return newObject;
        }

        public static Mesh ExtrudeSideWalk(List<Vector3> outsideLine, List<Vector3> insideLine, float sideWalkHeight, float sideWalkWidth, float sideWalkEdgeWidth, Transform ourTrans, Transform newParent, Material edgeMaterial = null)
        {
            //SideWalkEdgeWidth cannot be more than half of the Width
            if (sideWalkEdgeWidth > (sideWalkWidth / 2))
            {
                Debug.Log("Correcting Edge Width: " + sideWalkEdgeWidth + " Half Width: " + (sideWalkWidth / 2));
                sideWalkEdgeWidth = (sideWalkWidth / 2);
            }
            List<Vector3> insetLine = new List<Vector3>(0);
            //Move points to relative of transform
            for (int i = 0; i < outsideLine.Count; i++)
            {
                //Need forward Direction so we can move relative to the right
                Vector3 pos = outsideLine[i];
                Vector3 pos2;
                if (i == outsideLine.Count - 1)
                {
                    //Cycle
                    pos2 = outsideLine[0];
                }
                else
                {
                    pos2 = outsideLine[i + 1];
                }
                //Move  up
                pos += Vector3.up * sideWalkHeight;
                pos -= ourTrans.position;
                outsideLine[i] = pos;
                //Now set Inset Line
                //Get Direction 
                Vector3 dir = (pos2 - pos).normalized;
                Vector3 right = Vector3.Cross(dir, Vector3.up);
                if (i == outsideLine.Count - 1)
                {
                    pos = insetLine[0];
                }
                else
                {
                    pos += right * sideWalkEdgeWidth;
                }
                insetLine.Add(pos);
            }
            //Interior Line
            for (int i = 0; i < insideLine.Count; i++)
            {
                Vector3 pos = insideLine[i];
                pos += Vector3.up * sideWalkHeight;
                pos -= ourTrans.position;
                insideLine[i] = pos;
            }

            Mesh sideMeshA = CiDyUtils.ExtrudeWall(outsideLine.ToArray(), -sideWalkHeight, true, true);
            Mesh sideMeshB = CiDyUtils.ExtrudeWall(insideLine.ToArray(), -sideWalkHeight, false, true);
            //Triangulate Top Mesh Tris
            List<Vector3> verts = new List<Vector3>(0);
            List<int> tris = new List<int>(0);
            List<List<Vector3>> allHoles = new List<List<Vector3>>(0);
            allHoles.Add(insetLine);

            //Move OutsideLine inward by Brick Inset
            Traingulation.TriangulatePolygon(outsideLine, allHoles, out tris, out verts);

            Mesh edgeMesh = new Mesh();
            edgeMesh.vertices = verts.ToArray();
            edgeMesh.triangles = tris.ToArray();
            //Calculate UVs
            Vector2[] uvs = new Vector2[edgeMesh.vertices.Length];
            for (int i = 0; i < verts.Count; i++)
            {
                uvs[i] = new Vector2(verts[i].x, verts[i].z);
            }
            //Set Uvs
            edgeMesh.uv = uvs;
            edgeMesh.RecalculateBounds();
            edgeMesh.RecalculateNormals();

            //Calculate Remaining Top Mesh/////////////////////////////////
            //Triangulate Top Mesh Tris
            verts = new List<Vector3>(0);
            tris = new List<int>(0);
            allHoles = new List<List<Vector3>>(0);
            allHoles.Add(insideLine);

            //Move OutsideLine inward by Brick Inset
            Traingulation.TriangulatePolygon(insetLine, allHoles, out tris, out verts);

            Mesh topMesh = new Mesh();
            topMesh.vertices = verts.ToArray();
            topMesh.triangles = tris.ToArray();
            //Calculate UVs
            uvs = new Vector2[topMesh.vertices.Length];

            for (int i = 0; i < verts.Count; i++)
            {
                uvs[i] = new Vector2(verts[i].x, verts[i].z);
            }
            //Set Uvs
            topMesh.uv = uvs;
            topMesh.RecalculateBounds();
            topMesh.RecalculateNormals();
            //Create Holder for Edge Mesh
            GameObject sideWalkEdge = new GameObject("SideWalkEdge");
            sideWalkEdge.transform.parent = newParent.transform;
            MeshFilter mFilter = sideWalkEdge.AddComponent<MeshFilter>();
            MeshRenderer mRenderer = sideWalkEdge.AddComponent<MeshRenderer>();
            //Create Final Mesh and Set to Sub Edge
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = edgeMesh;//Top Mesh
            combine[1].mesh = sideMeshA;//Left Side(Inside)
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            mFilter.sharedMesh = finalMesh;
            if (edgeMaterial == null)
            {
                mRenderer.sharedMaterial = (Material)Resources.Load("CiDyResources/SideWalkEdge");
            }
            else
            {
                mRenderer.sharedMaterial = edgeMaterial;
            }
            //Now Combine for Final Mesh
            //Combine PolyMesh (Extruded and Side Meshes)
            combine = new CombineInstance[2];
            combine[0].mesh = topMesh;//Top Mesh
            combine[1].mesh = sideMeshB;//Left Side(Inside)
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static Mesh ExtrudeSideWalk(List<Vector3> outsideLine, List<Vector3> insideLine, float sideWalkHeight, Transform ourTrans)
        {

            //Move points to relative of transform
            for (int i = 0; i < outsideLine.Count; i++)
            {
                Vector3 pos = outsideLine[i];
                pos += Vector3.up * sideWalkHeight;
                pos -= ourTrans.position;
                outsideLine[i] = pos;
            }
            for (int i = 0; i < insideLine.Count; i++)
            {
                Vector3 pos = insideLine[i];
                pos += Vector3.up * sideWalkHeight;
                pos -= ourTrans.position;
                insideLine[i] = pos;
            }

            Mesh sideMeshA = CiDyUtils.ExtrudeWall(outsideLine.ToArray(), -sideWalkHeight, true, true);
            Mesh sideMeshB = CiDyUtils.ExtrudeWall(insideLine.ToArray(), -sideWalkHeight, false, true);
            List<Vector3> verts = new List<Vector3>(0);
            List<int> tris = new List<int>(0);
            List<List<Vector3>> allHoles = new List<List<Vector3>>(0);
            allHoles.Add(insideLine);
            //Debug.Log("Triangulate Polygon, OutsideLine: " + outsideLine.Count +" Holes: "+allHoles.Count);
            /*for (int i = 0; i < outsideLine.Count; i++) {
                CiDyUtils.MarkPoint(outsideLine[i]+ourTrans.position, i);
            }*/
            Traingulation.TriangulatePolygon(outsideLine, allHoles, out tris, out verts);

            Mesh topMesh = new Mesh();
            topMesh.vertices = verts.ToArray();
            topMesh.triangles = tris.ToArray();
            //Calculate UVs
            Vector2[] uvs = new Vector2[topMesh.vertices.Length];
            for (int i = 0; i < verts.Count; i++)
            {
                uvs[i] = new Vector2(verts[i].x, verts[i].z);
            }
            //Set Uvs
            topMesh.uv = uvs;
            topMesh.RecalculateBounds();
            topMesh.RecalculateNormals();
            //Now Combine for Final Mesh
            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[3];
            combine[0].mesh = topMesh;//Top Mesh
            combine[1].mesh = sideMeshA;//Right Side(outside)
            combine[2].mesh = sideMeshB;//Left Side(Inside)
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;
            combine[2].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static Mesh ExtrudeDetailedSideWalk(Vector3[] spline, bool leftFacing, float sideWalkHeight, float sideWalkWidth, float sideWalkEdgeWidth, float sideWalkEdgeHeight, Transform srcTrans, Transform subTrans, Material edgeMaterial = null, bool addMeshColliderToEdge = false, Vector3 startDir = default, Vector3 endDir = default)
        {
            //Flip Facing
            if (leftFacing)
            {
                System.Array.Reverse(spline);
                //Modify Start and End Dir
                Vector3 tmpSrtDir = startDir;
                startDir = endDir;
                endDir = tmpSrtDir;
            }
            Vector3[] offsetPoints = new Vector3[spline.Length];
            //Offset Spline by Width?
            for (int i = 0; i < spline.Length; i++)
            {
                offsetPoints[i] = spline[i] - srcTrans.position;
            }
            spline = offsetPoints;
            float WidthHeight45 = 0.1618f;
            //SideWalkEdgeWidth cannot be more than half of the Width
            if (sideWalkEdgeWidth > (sideWalkWidth / 2))
            {
                sideWalkEdgeWidth = (sideWalkWidth / 2);
            }
            //Correct WidthHeight45 no more than half the height
            if (WidthHeight45 > (sideWalkHeight / 2))
            {
                WidthHeight45 = sideWalkHeight / 2;
            }
            //Calculate Top Width (Used when Generating Vertices for Top of Mesh of Sidewalk)
            //Set Verts for Top Mesh
            Vector3[] verts = new Vector3[spline.Length * 2];
            int count = 0;
            Vector3[] leftEdgeLine = new Vector3[spline.Length];
            Vector3[] edgeLine1 = new Vector3[spline.Length];
            //We have a Source Spline. Lets Create Needed Vertices
            for (int i = 0; i < spline.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = spline[i] + (Vector3.up * sideWalkHeight);
                Vector3 vector2;//Next Point
                                //Dir based on next in line.And Direction Based on Second Line.
                Vector3 vectorDir;//Direction from cur to Next
                if (i == spline.Length - 1)
                {
                    //At End
                    vector2 = spline[i - 1] + (Vector3.up * sideWalkHeight);
                    vectorDir = (vector - vector2).normalized;
                }
                else
                {
                    //Beginning and Middle
                    vector2 = spline[i + 1] + (Vector3.up * sideWalkHeight);
                    vectorDir = (vector2 - vector).normalized;
                }
                vectorDir.y = 0;
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                if (i == 0 && startDir != default)
                {
                    cross = startDir;
                }
                else if (i == spline.Length - 1 && endDir != default)
                {
                    cross = -endDir;
                }
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector + ((-cross) * (sideWalkWidth / 2));
                Vector3 rightVector = vector + ((cross * ((sideWalkWidth / 2) - sideWalkEdgeWidth)));

                edgeLine1[i] = rightVector;
                verts[count] = leftVector;
                leftEdgeLine[i] = leftVector;
                count++;
                verts[count] = rightVector;
                count++;
            }
            //Convert To Mesh
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            //Look at four points at a time
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                tris.Add(i);//0
                tris.Add(i + 2);//2
                tris.Add(i + 1);//1

                tris.Add(i + 1);//1
                tris.Add(i + 2);//2
                tris.Add(i + 3);//3
            }
            //Setup UVs
            float uvDist = 0;
            float zDist = 0;
            float xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideWalkMesh = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideWalkMesh.RecalculateBounds();
            sideWalkMesh.RecalculateNormals();
            count = 0;
            //////////////////////////////////////////////////Calculate Top Lip Edge of Sidewalk Edge
            Vector3[] raisedEdgeLine = new Vector3[edgeLine1.Length];
            for (int i = 0; i < edgeLine1.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = edgeLine1[i];
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector;
                Vector3 rightVector = vector + (Vector3.up * sideWalkEdgeHeight);
                verts[count] = leftVector;
                count++;
                verts[count] = rightVector;
                raisedEdgeLine[i] = rightVector;//Store new Edge Line
                count++;
            }

            uvs = new List<Vector2>();
            //Setup UVs
            uvDist = 0;
            zDist = 0;
            xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideWalkEdgeLipMesh = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideWalkEdgeLipMesh.RecalculateBounds();
            sideWalkEdgeLipMesh.RecalculateNormals();
            //////////////////////////////////Calculate Top of SideWalk Edge
            //Store Refernece to Right Edge.
            Vector3[] edgeLine = new Vector3[spline.Length];
            count = 0;
            for (int i = 0; i < raisedEdgeLine.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = raisedEdgeLine[i];
                Vector3 vector2;//Next Point
                                //Dir based on next in line.And Direction Based on Second Line.
                Vector3 vectorDir;//Direction from cur to Next
                if (i == raisedEdgeLine.Length - 1)
                {
                    //At End
                    vector2 = raisedEdgeLine[i - 1];
                    vectorDir = (vector - vector2).normalized;
                }
                else
                {
                    //Beginning or Middle
                    vector2 = raisedEdgeLine[i + 1];
                    vectorDir = (vector2 - vector).normalized;
                }
                vectorDir.y = 0;
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                if (i == 0 && startDir != default)
                {
                    cross = startDir;
                }
                else if (i == spline.Length - 1 && endDir != default)
                {
                    cross = -endDir;
                }
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector;
                Vector3 rightVector = vector + cross * (sideWalkEdgeWidth - WidthHeight45);
                verts[count] = leftVector;
                count++;
                verts[count] = rightVector;
                edgeLine[i] = rightVector;//Store new Edge Line
                count++;
            }
            uvs = new List<Vector2>();
            //Setup UVs
            uvDist = 0;
            zDist = 0;
            xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideWalkEdgeMesh = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideWalkEdgeMesh.RecalculateBounds();
            sideWalkEdgeMesh.RecalculateNormals();
            if (edgeMaterial == null)
            {
                edgeMaterial = (Material)Resources.Load("CiDyResources/SideWalkEdge");
            }
            //Create SideWalkEdge Mesha and Nest it into this Transform.
            GameObject newEdge = new GameObject("SideWalkEdge");
            //Nest it
            newEdge.transform.SetParent(subTrans);
            //Create Rendere and Filter
            MeshRenderer mRenderer = newEdge.AddComponent<MeshRenderer>();
            MeshFilter mFilter = newEdge.AddComponent<MeshFilter>();
            //Set Material
            mRenderer.sharedMaterial = edgeMaterial;
            /////////////////////////////////////////Create 45 Cut Edge Mesh
            count = 0;
            Vector3[] edgeLine45 = new Vector3[edgeLine.Length];
            for (int i = 0; i < edgeLine.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = edgeLine[i];
                Vector3 vector2;//Next Point
                                //Dir based on next in line.And Direction Based on Second Line.
                Vector3 vectorDir;//Direction from cur to Next
                if (i == edgeLine.Length - 1)
                {
                    //At End
                    vector2 = edgeLine[i - 1];
                    vectorDir = (vector - vector2).normalized;
                }
                else
                {
                    //Beginning or Middle
                    vector2 = edgeLine[i + 1];
                    vectorDir = (vector2 - vector).normalized;
                }
                vectorDir.y = 0;
                //Calculate Cross Product and place points.
                Vector3 cross = Vector3.Cross(Vector3.up, vectorDir).normalized;
                if (i == 0 && startDir != default)
                {
                    cross = startDir;
                }
                else if (i == spline.Length - 1 && endDir != default)
                {
                    cross = -endDir;
                }
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector;
                Vector3 rightVector = vector + (cross * WidthHeight45) + (Vector3.down * WidthHeight45);
                verts[count] = leftVector;
                count++;
                verts[count] = rightVector;
                edgeLine45[i] = rightVector;
                count++;
            }
            uvs = new List<Vector2>();
            //Setup UVs
            uvDist = 0;
            zDist = 0;
            xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideWalk45Mesh = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideWalk45Mesh.RecalculateBounds();
            sideWalk45Mesh.RecalculateNormals();
            //Extrude Right Wall that sits under the 45 Edge.
            //Mesh sideMeshA = CiDyUtils.ExtrudeWall(edgeLine45, (-(sideWalkHeight-WidthHeight45)), true, true);
            count = 0;
            for (int i = 0; i < edgeLine45.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = edgeLine45[i];
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector;
                Vector3 rightVector = vector + (Vector3.down * ((sideWalkHeight - WidthHeight45) + sideWalkEdgeHeight));
                verts[count] = leftVector;
                count++;
                verts[count] = rightVector;
                count++;
            }
            uvs = new List<Vector2>();
            //Setup UVs
            uvDist = 0;
            zDist = 0;
            xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideMeshA = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideMeshA.RecalculateBounds();
            sideMeshA.RecalculateNormals();
            //////////////////////Create Left Edge Wall///////////////
            count = 0;
            for (int i = 0; i < leftEdgeLine.Length; i++)
            {
                //Test Left of Line.//Determine Pos
                Vector3 vector = leftEdgeLine[i];
                //Calculate Four Points creating left Line and Right Line.
                Vector3 leftVector = vector;
                Vector3 rightVector = vector + (Vector3.down * sideWalkHeight);
                verts[count] = leftVector;
                count++;
                verts[count] = rightVector;
                count++;
            }
            tris.Reverse();
            uvs = new List<Vector2>();
            //Setup UVs
            uvDist = 0;
            zDist = 0;
            xDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Length - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                if (i == verts.Length - 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = verts[i + 1];
                    Vector3 midPointB = verts[i + 3];
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
                else
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(xDist, uvDist));
                    //We are at the Beginning//Get Vertical Distance
                    //Vector2 midPointA = (new Vector2(newVerts[i].x, newVerts[i].z) + new Vector2(newVerts[i + 1].x, newVerts[i + 1].z)) / 2;
                    //Vector2 midPointB = (new Vector2(newVerts[i + 2].x, newVerts[i + 2].z) + new Vector2(newVerts[i + 3].x, newVerts[i + 3].z)) / 2;
                    //Get Vertical Distance
                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = Mathf.RoundToInt(Vector3.Distance(midPointA, midPointB));
                    uvDist += zDist;
                }
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));
            //Debug.Log("Verts: " + newVerts.Count+" UVs: "+uvs.Count+" Road: "+name);
            //Set Triangles and 
            Mesh sideWalkLeftWall = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            sideWalkLeftWall.RecalculateBounds();
            sideWalkLeftWall.RecalculateNormals();
            //Create First Closed Wall///////////////////////////////////////////
            Vector3 forwardDirection = (spline[1] - spline[0]).normalized;
            forwardDirection.y = 0;
            //Calculate Cross Product and place points.
            Vector3 cross2 = Vector3.Cross(Vector3.up, forwardDirection).normalized;
            if (startDir != default)
            {
                cross2 = startDir;
            }
            //Calculate Four Points creating left Line and Right Line.
            Vector3 bottomLeftWalk = spline[0] + (-cross2) * (sideWalkWidth / 2);
            Vector3 bottomRightWalk = spline[0] + cross2 * ((sideWalkWidth / 2) - sideWalkEdgeWidth);
            Vector3 topLeft = bottomLeftWalk + (Vector3.up * sideWalkHeight);
            Vector3 topRight = bottomRightWalk + (Vector3.up * sideWalkHeight);
            Vector3 edgeLeftTop = topRight + (Vector3.up * sideWalkEdgeHeight);
            Vector3 edgeRightTop = edgeLeftTop + (cross2 * (sideWalkEdgeWidth - WidthHeight45));
            Vector3 bottomRight = bottomRightWalk + (cross2 * sideWalkEdgeWidth);
            Vector3 edgeTop45Right = bottomRight + (Vector3.up * ((sideWalkHeight + sideWalkEdgeHeight) - WidthHeight45));
            Vector3[] capWallVerts = new Vector3[8];
            capWallVerts[0] = bottomLeftWalk;
            capWallVerts[1] = topLeft;
            capWallVerts[2] = topRight;
            capWallVerts[3] = edgeLeftTop;
            capWallVerts[4] = edgeRightTop;
            capWallVerts[5] = edgeTop45Right;
            capWallVerts[6] = bottomRight;
            capWallVerts[7] = bottomRightWalk;
            int[] capTris = new int[18];
            //Triangulate Manually
            capTris[0] = 0;
            capTris[1] = 1;
            capTris[2] = 2;

            capTris[3] = 0;
            capTris[4] = 2;
            capTris[5] = 7;

            capTris[6] = 2;
            capTris[7] = 3;
            capTris[8] = 4;

            capTris[9] = 2;
            capTris[10] = 4;
            capTris[11] = 5;

            capTris[12] = 7;
            capTris[13] = 2;
            capTris[14] = 5;

            capTris[15] = 7;
            capTris[16] = 5;
            capTris[17] = 6;

            Vector2[] capUvs = new Vector2[8];
            //Set UVS Manually
            capUvs[0] = new Vector2(0, 0);
            capUvs[1] = new Vector2(0, sideWalkHeight);
            capUvs[2] = new Vector2(sideWalkWidth - (sideWalkEdgeWidth + WidthHeight45), sideWalkHeight);
            capUvs[3] = new Vector2(sideWalkWidth - (sideWalkEdgeWidth + WidthHeight45), sideWalkHeight + sideWalkEdgeHeight);
            capUvs[4] = new Vector2(sideWalkWidth - WidthHeight45, sideWalkHeight + (sideWalkEdgeHeight));
            capUvs[5] = new Vector2(sideWalkWidth, sideWalkHeight - WidthHeight45);
            capUvs[6] = new Vector2(sideWalkWidth, 0);
            capUvs[7] = new Vector2(sideWalkWidth - sideWalkEdgeWidth, 0);

            Mesh capAMesh = new Mesh
            {
                vertices = capWallVerts,
                triangles = capTris,
                uv = capUvs
            };

            capAMesh.RecalculateBounds();
            capAMesh.RecalculateNormals();
            //Create End Cap
            //Create First Closed Wall///////////////////////////////////////////
            forwardDirection = (spline[spline.Length - 1] - spline[spline.Length - 2]).normalized;
            forwardDirection.y = 0;
            //Calculate Cross Product and place points.
            cross2 = Vector3.Cross(Vector3.up, forwardDirection).normalized;
            if (endDir != default)
            {
                cross2 = -endDir;
            }
            //Calculate Four Points creating left Line and Right Line.
            bottomLeftWalk = spline[spline.Length - 1] + (-cross2) * (sideWalkWidth / 2);
            bottomRightWalk = spline[spline.Length - 1] + cross2 * ((sideWalkWidth / 2) - sideWalkEdgeWidth);
            topLeft = bottomLeftWalk + (Vector3.up * sideWalkHeight);
            topRight = bottomRightWalk + (Vector3.up * sideWalkHeight);
            edgeLeftTop = topRight + (Vector3.up * sideWalkEdgeHeight);
            edgeRightTop = edgeLeftTop + (cross2 * (sideWalkEdgeWidth - WidthHeight45));
            bottomRight = bottomRightWalk + (cross2 * sideWalkEdgeWidth);
            edgeTop45Right = bottomRight + (Vector3.up * ((sideWalkHeight + sideWalkEdgeHeight) - WidthHeight45));
            capWallVerts = new Vector3[8];
            capWallVerts[0] = bottomLeftWalk;
            capWallVerts[1] = topLeft;
            capWallVerts[2] = topRight;
            capWallVerts[3] = edgeLeftTop;
            capWallVerts[4] = edgeRightTop;
            capWallVerts[5] = edgeTop45Right;
            capWallVerts[6] = bottomRight;
            capWallVerts[7] = bottomRightWalk;
            capTris = new int[18];
            //Triangulate Manually
            capTris[0] = 0;
            capTris[1] = 1;
            capTris[2] = 2;

            capTris[3] = 0;
            capTris[4] = 2;
            capTris[5] = 7;

            capTris[6] = 2;
            capTris[7] = 3;
            capTris[8] = 4;

            capTris[9] = 2;
            capTris[10] = 4;
            capTris[11] = 5;

            capTris[12] = 7;
            capTris[13] = 2;
            capTris[14] = 5;

            capTris[15] = 7;
            capTris[16] = 5;
            capTris[17] = 6;

            capUvs = new Vector2[8];
            //Set UVS Manually
            capUvs[0] = new Vector2(0, 0);
            capUvs[1] = new Vector2(0, sideWalkHeight);
            capUvs[2] = new Vector2(sideWalkWidth - (sideWalkEdgeWidth + WidthHeight45), sideWalkHeight);
            capUvs[3] = new Vector2(sideWalkWidth - (sideWalkEdgeWidth + WidthHeight45), sideWalkHeight + sideWalkEdgeHeight);
            capUvs[4] = new Vector2(sideWalkWidth - WidthHeight45, sideWalkHeight + (sideWalkEdgeHeight));
            capUvs[5] = new Vector2(sideWalkWidth, sideWalkHeight - WidthHeight45);
            capUvs[6] = new Vector2(sideWalkWidth, 0);
            capUvs[7] = new Vector2(sideWalkWidth - sideWalkEdgeWidth, 0);

            System.Array.Reverse(capTris);

            Mesh capBMesh = new Mesh
            {
                vertices = capWallVerts,
                triangles = capTris,
                uv = capUvs
            };

            capAMesh.RecalculateBounds();
            capAMesh.RecalculateNormals();

            //Combine SideWalkEdgeMesh and SideWalk45Mesh
            //Now Combine for Final Mesh
            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[4];
            combine[0].mesh = sideWalkEdgeMesh;//Top Mesh
            combine[1].mesh = sideWalk45Mesh;//Left Side(Inside)
            combine[2].mesh = sideMeshA;
            combine[3].mesh = sideWalkEdgeLipMesh;
            combine[0].transform = srcTrans.localToWorldMatrix;
            combine[1].transform = srcTrans.localToWorldMatrix;
            combine[2].transform = srcTrans.localToWorldMatrix;
            combine[3].transform = srcTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);
            //Set Mesh
            mFilter.sharedMesh = finalMesh;
            if (addMeshColliderToEdge)
            {
                //Add collider
                newEdge.AddComponent<MeshCollider>().sharedMesh = finalMesh;
            }
            //Combine SideWalkMesh and its Left Wall and Return it.
            combine = new CombineInstance[4];
            combine[0].mesh = sideWalkMesh;//Top Mesh
            combine[1].mesh = sideWalkLeftWall;//Left Side(Inside)
            combine[2].mesh = capAMesh;
            combine[3].mesh = capBMesh;
            combine[0].transform = srcTrans.localToWorldMatrix;
            combine[1].transform = srcTrans.localToWorldMatrix;
            combine[2].transform = srcTrans.localToWorldMatrix;
            combine[3].transform = srcTrans.localToWorldMatrix;
            //Initialize Doors
            finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);
            //Return Product
            return finalMesh;
        }

        //Override for Polygon input,Line input to extrude polygon along and Transform
        public static Mesh ExtrudeRail(Vector3[] shape, Vector3[] line, Transform ourTrans, Vector3 startDir = default, Vector3 endDir = default)
        {
            //We will calculate the Rail Mesh and the End Caps as two seperate meshes then combine them into one mesh.
            //Store Verts and Triangles
            List<Vector3> verts = new List<Vector3>(0);
            List<int> tris = new List<int>(0);
            //Go down Line
            for (int l = 0; l < line.Length - 1; l++)
            {
                Vector3 curPos = line[l];
                Vector3 nxtPos = line[l + 1];
                Vector3 fwdDir = (nxtPos - curPos).normalized;
                Vector3 fwdDir2 = Vector3.zero;

                if (l < line.Length - 2)
                {
                    //There is room to grab the next
                    fwdDir2 = (line[l + 2] - nxtPos).normalized;
                }
                else
                {
                    //Keep Direction
                    fwdDir2 = fwdDir;
                }

                //Translate based on Transform Directions
                //curPos = placedBuilding.transform.TransformPoint(boundFootPrint[j]);
                //Add shape points by two
                for (int i = 0; i < shape.Length; i++)
                {
                    //Rotate Shape for First Position
                    Vector3[] rotatedShape = new Vector3[shape.Length];
                    for (int j = 0; j < shape.Length; j++)
                    {
                        //Convert from Current Forward (Z) to new Fwd Dir
                        //Rotate Shape points
                        float angle = Vector3.Angle(Vector3.forward, fwdDir);
                        Vector3 rotatingAxis = Vector3.up;
                        if (CiDyUtils.AngleDir(Vector3.forward, fwdDir, rotatingAxis) == -1)
                        {
                            angle = -angle;
                        }
                        rotatedShape[j] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[j];
                    }
                    //Get Proper Next Int location
                    int nextInt = i + 1;
                    if (i == shape.Length - 1)
                    {
                        nextInt = 0;
                    }
                    //Extrude polygon 
                    verts.Add((curPos + rotatedShape[i]));
                    verts.Add(curPos + rotatedShape[nextInt]);
                    //Rotate Shape for Second Position
                    for (int j = 0; j < shape.Length; j++)
                    {
                        //Convert from Current Forward (Z) to new Fwd Dir
                        //Rotate Shape points
                        float angle = Vector3.Angle(Vector3.forward, fwdDir2);
                        Vector3 rotatingAxis = Vector3.up;
                        if (CiDyUtils.AngleDir(Vector3.forward, fwdDir2, rotatingAxis) == -1)
                        {
                            angle = -angle;
                        }
                        rotatedShape[j] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[j];
                    }

                    //Add Extruded next line points
                    verts.Add(nxtPos + rotatedShape[i]);
                    verts.Add(nxtPos + rotatedShape[nextInt]);
                    //Add Triangles
                    tris.Add(verts.Count - 3);
                    tris.Add(verts.Count - 2);
                    tris.Add(verts.Count - 4);
                    //Second Triangle
                    tris.Add(verts.Count - 3);
                    tris.Add(verts.Count - 1);
                    tris.Add(verts.Count - 2);
                }
            }

            //Calculate Verts
            List<Vector2> uvs = new List<Vector2>(0);
            float xDist = 0;
            float uvDist = 0;
            float zDist = 0;
            //Set up UVs for Three Segments and Up.
            for (int i = 0; i < verts.Count - 2; i += 2)
            {
                xDist = Vector3.Distance(verts[i], verts[i + 1]);
                //Handle All Four Points of UV with mounting Values.
                uvs.Add(new Vector2(0, uvDist));
                uvs.Add(new Vector2(xDist, uvDist));
                Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                //Get Vertical Distance
                zDist = Vector3.Distance(midPointA, midPointB);
                uvDist += zDist;
            }
            //Add Last Two Points
            uvs.Add(new Vector2(0, uvDist));
            uvs.Add(new Vector2(xDist, uvDist));

            //Now we want to Calculate the Cap ends triangles. To Do this we want to Flatten the Shape
            //Now store Cap Mesh Data
            List<Vector3> capAVerts = new List<Vector3>(0);
            List<Vector3> capBVerts = new List<Vector3>(0);
            Mesh capAMesh = new Mesh();
            Mesh capBMesh = new Mesh();
            //Flatten orignal Shape to Face Up
            Vector3[] rotated = new Vector3[shape.Length];
            //Rotate Shape for Second Position
            for (int j = 0; j < shape.Length; j++)
            {
                //Convert from Current Forward (Z) to new Fwd Dir
                //Rotate Shape points
                float angle = Vector3.Angle(Vector3.forward, -Vector3.up);
                Vector3 rotatingAxis = Vector3.right;
                if (CiDyUtils.AngleDir(Vector3.forward, -Vector3.up, rotatingAxis) == -1)
                {
                    angle = -angle;
                }
                rotated[j] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[j];
            }
            //Now that the Shape is flat we can calculate the triangles
            //Calculate Triangles for Mesh Cap
            Vector2[] flatShape = new Vector2[rotated.Length];
            //Calculate First Cap
            for (int j = 0; j < rotated.Length; j++)
            {
                Vector3 capPos = line[0] + rotated[j];
                flatShape[j] = new Vector2(capPos.x, capPos.z);
            }

            // Use the triangulator to get indices for creating triangles
            CiDyTriangulator tr = new CiDyTriangulator(flatShape);
            int[] indices = tr.Triangulate();
            //Now that we have the triangles for the Caps lets project the First Cap End.
            Vector3 fwd = (line[1] - line[0]).normalized;
            //Now that we have the Desired Triangles. Project the Cap Verts to the Proper Orientation
            //Rotate Shape for First Position
            for (int j = 0; j < shape.Length; j++)
            {
                //Convert from Current Forward (Z) to new Fwd Dir
                //Rotate Shape points
                float angle = Vector3.Angle(Vector3.forward, fwd);
                Vector3 rotatingAxis = Vector3.up;
                if (CiDyUtils.AngleDir(Vector3.forward, fwd, rotatingAxis) == -1)
                {
                    angle = -angle;
                }
                rotated[j] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[j];
                //Store Point
                capAVerts.Add(line[0] + rotated[j]);
            }

            capAMesh.vertices = capAVerts.ToArray();
            capAMesh.triangles = indices;
            capAMesh.RecalculateBounds();
            capAMesh.RecalculateNormals();
            //Now Project End Cap
            //Now that we have the triangles for the Caps lets project the First Cap End.
            fwd = (line[line.Length - 1] - line[line.Length - 2]).normalized;
            //Now that we have the Desired Triangles. Project the Cap Verts to the Proper Orientation
            //Rotate Shape for First Position
            for (int j = 0; j < shape.Length; j++)
            {
                //Convert from Current Forward (Z) to new Fwd Dir
                //Rotate Shape points
                float angle = Vector3.Angle(Vector3.forward, fwd);
                Vector3 rotatingAxis = Vector3.up;
                if (CiDyUtils.AngleDir(Vector3.forward, fwd, rotatingAxis) == -1)
                {
                    angle = -angle;
                }
                rotated[j] = Quaternion.AngleAxis(angle, rotatingAxis) * shape[j];
                //Store Point
                capBVerts.Add(line[line.Length - 1] + rotated[j]);
            }

            //Reverse Indices for Opposite CapB
            System.Array.Reverse(indices);

            capBMesh.vertices = capBVerts.ToArray();
            capBMesh.triangles = indices;
            capBMesh.RecalculateBounds();
            capBMesh.RecalculateNormals();

            //Create New Mesh
            Mesh railMesh = new Mesh();
            railMesh.vertices = verts.ToArray();
            railMesh.triangles = tris.ToArray();
            railMesh.uv = uvs.ToArray();
            railMesh.RecalculateNormals();
            railMesh.RecalculateTangents();
            railMesh.RecalculateBounds();

            //Now Combine for Final Mesh
            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[3];
            combine[0].mesh = railMesh;
            combine[1].mesh = capAMesh;
            combine[2].mesh = capBMesh;
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;
            combine[2].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static Mesh ExtrudeWall(Vector3[] line, float depth, bool reversePolyFace = false, bool isCycle = false)
        {
            List<Vector3> verts = new List<Vector3>(0);
            List<int> tris = new List<int>(0);
            //Extrude Wall by depth
            for (int i = 0; i < line.Length; i++)
            {
                verts.Add(line[i]);
                verts.Add(line[i] + (Vector3.up * depth));
                if (isCycle && i == line.Length - 1)
                {
                    //Add first back to it
                    verts.Add(line[0]);
                    verts.Add(line[0] + (Vector3.up * depth));
                }
            }
            //Iterate through by fours and calculate triangles
            for (int i = 0; i < verts.Count - 4; i += 2)
            {
                if (!reversePolyFace)
                {
                    tris.Add(i);
                    tris.Add(i + 1);
                    tris.Add(i + 3);
                    //SEcond Triangles
                    tris.Add(i);
                    tris.Add(i + 3);
                    tris.Add(i + 2);
                }
                else
                {
                    tris.Add(i + 3);
                    tris.Add(i + 1);
                    tris.Add(i);
                    //SEcond Triangles
                    tris.Add(i + 2);
                    tris.Add(i + 3);
                    tris.Add(i);
                }
            }
            //Update Mesh
            Mesh newMesh = new Mesh();
            newMesh.vertices = verts.ToArray();
            newMesh.triangles = tris.ToArray();
            Vector2[] uvs = new Vector2[verts.Count];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(verts[i].x, verts[i].y);
            }
            //add Uvs
            newMesh.uv = uvs;
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();

            return newMesh;
        }

        public static Mesh ExtrudeRail(Vector3[] leftLine, Vector3[] rightLine, float depth, Transform ourTrans)
        {
            //Taking Left Line and Right Line as input lets create the outline (Counter Clockwise) for side Extrusion
            Vector3[] origSide = new Vector3[leftLine.Length + rightLine.Length];
            int count = 0;
            for (int i = 0; i < leftLine.Length; i++)
            {
                origSide[count] = leftLine[i];
                count++;
            }
            for (int i = 0; i < rightLine.Length; i++)
            {
                origSide[count] = rightLine[i];
                count++;
            }
            System.Array.Reverse(origSide);
            System.Array.Reverse(rightLine);

            Vector3[] origPolygon = new Vector3[leftLine.Length * 2];
            count = 0;
            for (int i = 0; i < leftLine.Length; i++)
            {
                origPolygon[count] = leftLine[i];
                count++;
                origPolygon[count] = rightLine[i];
                count++;
            }
            //System.Array.Reverse(rightLine);
            //System.Array.Reverse(leftLine);
            //We need to convert poly for extrusion and triangulation based on Perpendicular direction
            //Determine normal of Poly
            Vector3 perpNormal;
            perpNormal = Vector3.up;//-Vector3.Cross(origPolygon[1] - origPolygon[0], origPolygon[2] - origPolygon[0]).normalized;

            int uvDirection = 1;//1 = Y
            Vector3 u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
            Vector3 v = Vector3.Cross(u, perpNormal).normalized;

            List<int> tris = new List<int>(0);
            //Look at four points at a time
            for (int i = 0; i < origPolygon.Length - 2; i += 2)
            {
                tris.Add(i);//0
                tris.Add(i + 2);//2
                tris.Add(i + 1);//1

                tris.Add(i + 1);//1
                tris.Add(i + 2);//2
                tris.Add(i + 3);//3
            }

            //int[] tris = EarClipper.Triangulate(poly);//Triangulate Polygon
            Mesh polyMesh = new Mesh();
            Mesh sideMesh = new Mesh();

            Vector3[] vertices = new Vector3[origPolygon.Length];
            Vector2[] polyUvs = new Vector2[origPolygon.Length];
            //Place Vertices of extruded poly
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //Extrude these vertices
                vertices[i] = (origPolygon[i] + (perpNormal * depth)) - ourTrans.position;
                switch (uvDirection)
                {
                    case 0:
                        //X
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].z, origPolygon[i].y);
                        break;
                    case 1:
                        //Y
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].z);
                        break;
                    case 2:
                        //Z
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].y);
                        break;
                }
            }
            //Update Extruded Face of Mesh
            polyMesh.vertices = vertices;
            polyMesh.triangles = tris.ToArray();
            polyMesh.uv = polyUvs;
            polyMesh.RecalculateNormals();
            polyMesh.RecalculateBounds();

            List<Vector3> newVerts = new List<Vector3>(0);
            List<int> triangles = new List<int>(0);
            List<Vector2> uvs = new List<Vector2>(0);
            //Now Lets generate the Side Walls of the Extruded Poly
            for (int i = 0; i < origPolygon.Length; i++)
            {

                if (i == origPolygon.Length - 1)
                {
                    //Last Points
                    //Loop connection   
                    //First Line
                    //Duplicate the originals positions as this is the base connection points.
                    newVerts.Add(origSide[i] - ourTrans.position);
                    newVerts.Add(origSide[i] + (perpNormal * depth) - ourTrans.position);
                    //SecondLine
                    newVerts.Add(origSide[0] - ourTrans.position);
                    newVerts.Add(origSide[0] + (perpNormal * depth) - ourTrans.position);
                }
                else
                {
                    //First Line
                    //Duplicate the originals positions as this is the base connection points.
                    newVerts.Add(origSide[i] - ourTrans.position);
                    newVerts.Add(origSide[i] + (perpNormal * depth) - ourTrans.position);
                    //SecondLine
                    newVerts.Add(origSide[i + 1] - ourTrans.position);
                    newVerts.Add(origSide[i + 1] + (perpNormal * depth) - ourTrans.position);
                }
                float xDist = Vector3.Distance(newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2]);
                float yDist = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 2]);

                float xDist2 = Vector3.Distance(newVerts[newVerts.Count - 2], newVerts[newVerts.Count - 1]);
                float yDist2 = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 3]);

                //Calculate Normal of these Two polygons
                //Determine normal of Poly
                Vector3 polygonNormal = Vector3.Cross(newVerts[newVerts.Count - 3] - newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2] - newVerts[newVerts.Count - 4]).normalized;

                //Determine this polygons Normal Direction
                if (Mathf.Abs(Vector3.Dot(Vector3.up, polygonNormal)) < 0.2f)
                {
                    //Project to Y
                    //Debug.Log("Y Plane");
                    uvDirection = 1;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.forward, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("Z Plane");
                    uvDirection = 0;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.right, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("X Plane");
                    uvDirection = 2;
                }

                switch (uvDirection)
                {
                    case 0:
                        uvs.Add(new Vector2(0, 0));
                        uvs.Add(new Vector2(0, yDist));
                        //
                        uvs.Add(new Vector2(xDist, 0));
                        uvs.Add(new Vector2(xDist, yDist));
                        break;
                    case 1:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                    case 2:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                }
                /*//Add UVS now that we have added four Verts.
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, yDist));
                //
                uvs.Add(new Vector2(xDist, 0));
                uvs.Add(new Vector2(xDist, yDist));*/
            }

            //Calculate Triangles for Vertices
            for (int i = 0; i < newVerts.Count; i += 4)
            {
                //First Triangle
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 3);
                //SecondTriangles
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
            }

            //Update Side Walls of Mesh
            sideMesh.vertices = newVerts.ToArray();
            sideMesh.triangles = triangles.ToArray();
            sideMesh.uv = uvs.ToArray();
            sideMesh.RecalculateNormals();
            sideMesh.RecalculateBounds();

            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = polyMesh;
            combine[1].mesh = sideMesh;
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }
        //Used  in conjuction with Corner SideWalk
        public static bool IsBetween(int testValue, int bound1, int bound2)
        {
            return (testValue >= Mathf.Min(bound1, bound2) && testValue <= Mathf.Max(bound1, bound2));
        }

        //We Assume. Curve is First then Last point is Rear Point. (Must be this way as we flatten the curve in the Front) (PA->PB->PC) Assumed Direction for Reflex Detection
        public static Mesh ExtrudeCornerSideWalk(ref Vector3 connectorVector, Vector3 pA, Vector3 pB, Vector3 pC, float sideWalkWidth, float sideWalkHeight, Transform ourTrans, Transform subTransform = null, float curveSegements = 0.618f, Material intersectionMat = null)
        {
            /*pA = pA - ourTrans.position;
            pB = pB - ourTrans.position;
            pC = pC - ourTrans.position;*/
            //Clamp Curve Segments between min(0.1f) & Max(SideWalkWidth)
            curveSegements = Mathf.Clamp(curveSegements, 0.1f, sideWalkWidth);
            //Clamp Height to min(0.01f) & Max(SideWalkWidth / 3)
            sideWalkHeight = Mathf.Clamp(sideWalkHeight, 0.01f, sideWalkWidth / 3);
            //Get Directions pb-pa
            Vector3 lineDirection = (pB - pA).normalized;
            //Calculate Bisector from Total Angle
            Vector3 bisector = CiDyUtils.AngleBisector(pA, pB, pC);
            //Is Reflex?
            int angleDir = AngleDirection(lineDirection, bisector, Vector3.up);
            if (angleDir > 0.0f)
            {
                //This is a reflex Vector
                bisector = -bisector;
                //Debug.Log("Angle Dir is Reflex: " + angleDir);
            }
            //Get Direction Vectors from Bisector Center to Both Directions (Opposite and Adjacent)
            Vector3 oppositeSideDir = (pA - pB).normalized;
            Vector3 adjacentSideDir = (pC - pB).normalized;
            //Calculate Theta Angle by Taking Total Angle and Dividing, Creating two Triangles.
            float totalAngle = Vector3.Angle(oppositeSideDir, adjacentSideDir);
            //Debug.Log("Total Angle: " +totalAngle+" Theta: "+totalAngle/2);
            //Set Theta
            float theta = totalAngle / 2;//Theta
                                         //Set Opposite Side to Desired SideWalkWidth
            float oppositeSide = sideWalkWidth;
            //Determine Hypotenuse using Pythagorm Therom from Opposite Side and Theta (Radians Angle)
            float hypotenuse = oppositeSide / Mathf.Sin(theta * Mathf.Deg2Rad);
            //Debug.Log("Hypotenuse Length calculated from opposited(SideWalkWidth & Theta Rad Angle = " + hypotenuse);
            //Update Hypotenuse
            float bisectorLength = hypotenuse;
            //Hypoto = length
            Vector3 bisectorPoint = pB + (bisector * bisectorLength);
            connectorVector = bisectorPoint;
            //Determine Final Angle
            float finalAngle = ((180 - 90) - theta);
            //Debug.Log("Final Angle: "+finalAngle);
            Vector3 dirA = (Quaternion.AngleAxis(finalAngle, Vector3.up) * -bisector).normalized;
            Vector3 rightEnd = bisectorPoint + (dirA * sideWalkWidth);
            Vector3 dirB = (Quaternion.AngleAxis(-finalAngle, Vector3.up) * -bisector).normalized;
            Vector3 leftEnd = bisectorPoint + (dirB * sideWalkWidth);

            //Convert to SideWalk Corner Mesh
            //We Need our Boundary Points. (RightEnd)101,(Center)pB,(LeftEnd)102,100(Bisector)
            Vector3[] origCurve = new Vector3[3];
            origCurve[0] = rightEnd;
            origCurve[1] = pB;
            origCurve[2] = leftEnd;
            //Create Curved End
            Vector3[] curve = CiDyUtils.CreateBezier(origCurve, curveSegements);
            if (curve.Length < 3)
            {
                Debug.LogError("Failed to Create Corner SideWalk, Corner of Cell is not Viable, Please Adjust");
                return null;
            }
            List<Vector3> print = new List<Vector3>(0);
            for (int i = 0; i < curve.Length; i++)
            {
                print.Add(curve[i]);
            }
            //Add Final Point
            print.Add(bisectorPoint);

            Vector3[] origPolygon = print.ToArray();
            //We need to convert poly for extrusion and triangulation based on Perpendicular direction
            //Determine normal of Poly
            Vector3 perpNormal;

            perpNormal = -Vector3.Cross(origPolygon[1] - origPolygon[0], origPolygon[2] - origPolygon[0]).normalized;

            int uvDirection = 0;//0 = X, 1 = Y, 2 = Z 

            Vector3 u = Vector3.zero;

            if (Vector3.Dot(Vector3.up, perpNormal) == 1.0f)
            {
                //Project to Y
                //Debug.Log("Projecting to Y Plane");
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
                uvDirection = 1;
            }
            else if (Vector3.Dot(Vector3.right, perpNormal) == 1.0f)
            {
                //Debug.Log("Projecting to X Plane");
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
                uvDirection = 0;
            }
            else if (Vector3.Dot(Vector3.forward, perpNormal) == 1.0f)
            {
                //Debug.Log("Projecting to Z Plane");
                u = Vector3.ProjectOnPlane(Vector3.up, perpNormal);
                uvDirection = 2;
            }

            Vector3 v = Vector3.Cross(u, perpNormal).normalized;

            Vector2[] poly = new Vector2[origPolygon.Length];
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //Project 3D Vectors to a 2D Plane
                poly[i] = new Vector2(Vector3.Dot(origPolygon[i], u), Vector3.Dot(origPolygon[i], v));
            }

            //int[] tris = EarClipper.Triangulate(poly);//Triangulate Polygon
            // Use the triangulator to get indices for creating triangles
            CiDyTriangulator tr = new CiDyTriangulator(poly);
            int[] tris = tr.Triangulate();

            Mesh polyMesh = new Mesh();
            Mesh sideMesh = new Mesh();

            Vector3[] vertices = new Vector3[poly.Length];
            Vector2[] polyUvs = new Vector2[poly.Length];
            int middle = (origPolygon.Length - 1) / 2;
            int end = origPolygon.Length - 1;
            float minHeight = origPolygon[0].y;//Minimum Height
            float maxHeight = sideWalkHeight;//Max Height
            int offset = end / 5;
            //Place Vertices of extruded poly
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //If we are before middle. We are heading to zero
                //If we are after Middle we are heading to 1
                float t;
                float curHeight;
                if (i <= (middle - offset))
                {
                    t = (float)i / (float)(middle - offset);
                    curHeight = Mathf.Lerp(maxHeight, minHeight, t);
                }
                else
                {
                    t = (float)(i - (middle + offset)) / (float)(end - (middle + offset));
                    curHeight = Mathf.Lerp(minHeight, maxHeight, t);
                }
                if (i == 0 || i == origPolygon.Length - 1 || i == origPolygon.Length - 2)
                {
                    //Full Height
                    //Extrude these vertices
                    vertices[i] = (origPolygon[i] + (perpNormal * sideWalkHeight)) - ourTrans.position;
                }
                else if (IsBetween(i, middle - offset, middle + offset))
                {
                    //These are Flattended
                    vertices[i] = (origPolygon[i] + (perpNormal * minHeight)) - ourTrans.position;
                }
                else
                {
                    //These are Lerped on there Y Axis between Max height and Original Height
                    vertices[i] = (origPolygon[i] + (perpNormal * curHeight)) - ourTrans.position;
                }

                switch (uvDirection)
                {
                    case 0:
                        //X
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].z, origPolygon[i].y);
                        break;
                    case 1:
                        //Y
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].z);
                        break;
                    case 2:
                        //Z
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].y);
                        break;
                }
            }
            //Update Extruded Face of Mesh
            polyMesh.vertices = vertices;
            polyMesh.triangles = tris;
            polyMesh.uv = polyUvs;
            polyMesh.RecalculateNormals();
            polyMesh.RecalculateBounds();

            List<Vector3> newVerts = new List<Vector3>(0);
            List<int> triangles = new List<int>(0);
            List<Vector2> uvs = new List<Vector2>(0);
            //Now Lets generate the Side Walls of the Extruded Poly
            for (int i = 0; i < poly.Length; i++)
            {
                //If we are before middle. We are heading to zero
                //If we are after Middle we are heading to 1
                float t;
                float curHeight;
                if (i <= (middle - offset))
                {
                    t = (float)i / (float)(middle - offset);
                    curHeight = Mathf.Lerp(maxHeight, minHeight, t);
                }
                else
                {
                    t = (float)(i - (middle + offset)) / (float)(end - (middle + offset));
                    curHeight = Mathf.Lerp(minHeight, maxHeight, t);
                }
                if (i == 0 || i == origPolygon.Length - 1 || i == origPolygon.Length - 2)
                {
                    //Full Height
                    //Extrude these vertices
                    curHeight = sideWalkHeight;
                }
                else if (IsBetween(i, middle - offset, middle + offset))
                {
                    //These are Flattended
                    curHeight = minHeight;
                }

                if (i == poly.Length - 1)
                {
                    //Last Points
                    //Loop connection   
                    //First Line
                    //Duplicate the originals positions as this is the base connection points.
                    newVerts.Add(origPolygon[i] - ourTrans.position);
                    newVerts.Add(origPolygon[i] + (perpNormal * curHeight) - ourTrans.position);
                    //SecondLine
                    newVerts.Add(origPolygon[0] - ourTrans.position);
                    newVerts.Add(origPolygon[0] + (perpNormal * curHeight) - ourTrans.position);
                }
                else
                {
                    if (i == (middle + offset))
                    {
                        //First Line
                        //Duplicate the originals positions as this is the base connection points.
                        newVerts.Add(origPolygon[i] - ourTrans.position);
                        newVerts.Add(origPolygon[i] + (perpNormal * curHeight) - ourTrans.position);
                        //SecondLine
                        newVerts.Add(origPolygon[i + 1] - ourTrans.position);
                        newVerts.Add(origPolygon[i + 1] + (perpNormal * sideWalkHeight) - ourTrans.position);
                    }
                    else
                    {
                        //First Line
                        //Duplicate the originals positions as this is the base connection points.
                        newVerts.Add(origPolygon[i] - ourTrans.position);
                        newVerts.Add(origPolygon[i] + (perpNormal * curHeight) - ourTrans.position);
                        //SecondLine
                        newVerts.Add(origPolygon[i + 1] - ourTrans.position);
                        newVerts.Add(origPolygon[i + 1] + (perpNormal * curHeight) - ourTrans.position);
                        if (i != 0)
                        {
                            newVerts[newVerts.Count - 5] = origPolygon[i] + (perpNormal * curHeight) - ourTrans.position;
                        }
                    }
                }
                //UVS
                float xDist = Vector3.Distance(newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2]);
                float yDist = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 2]);

                float xDist2 = Vector3.Distance(newVerts[newVerts.Count - 2], newVerts[newVerts.Count - 1]);
                float yDist2 = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 3]);

                //Calculate Normal of these Two polygons
                //Determine normal of Poly
                Vector3 polygonNormal = Vector3.Cross(newVerts[newVerts.Count - 3] - newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2] - newVerts[newVerts.Count - 4]).normalized;

                //Determine this polygons Normal Direction
                if (Mathf.Abs(Vector3.Dot(Vector3.up, polygonNormal)) < 0.2f)
                {
                    //Project to Y
                    //Debug.Log("Y Plane");
                    uvDirection = 1;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.forward, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("Z Plane");
                    uvDirection = 0;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.right, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("X Plane");
                    uvDirection = 2;
                }

                switch (uvDirection)
                {
                    case 0:
                        uvs.Add(new Vector2(0, 0));
                        uvs.Add(new Vector2(0, yDist));
                        //
                        uvs.Add(new Vector2(xDist, 0));
                        uvs.Add(new Vector2(xDist, yDist));
                        break;
                    case 1:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                    case 2:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                }
            }

            //Calculate Triangles for Vertices
            for (int i = 0; i < newVerts.Count; i += 4)
            {
                //First Triangle
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 3);
                //SecondTriangles
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
            }

            //Update Side Walls of Mesh
            sideMesh.vertices = newVerts.ToArray();
            sideMesh.triangles = triangles.ToArray();
            sideMesh.uv = uvs.ToArray();
            sideMesh.RecalculateNormals();
            sideMesh.RecalculateBounds();

            Vector3[] flatCurve = new Vector3[curve.Length + 1];
            for (int i = 0; i < curve.Length; i++)
            {
                flatCurve[i] = curve[i];
            }
            flatCurve[flatCurve.Length - 1] = pB;
            //Create Flat Connector Mesh.
            Mesh flatMesh = ExtrudePrint(flatCurve, 0, ourTrans, false);
            GameObject flatObject = new GameObject("FlatMesh");
            Vector3 curPos = subTransform.position;
            flatObject.transform.position = new Vector3(curPos.x, curPos.y - 0.001f, curPos.z);
            //Set Material
            if (intersectionMat == null)
            {
                intersectionMat = (Material)Resources.Load("CiDyResources/Intersection");
            }
            //Set Mesh Renderer
            MeshRenderer flatRenderer = flatObject.AddComponent<MeshRenderer>();
            MeshFilter flatFilter = flatObject.AddComponent<MeshFilter>();
            flatRenderer.sharedMaterial = intersectionMat;//Set Material
            flatFilter.sharedMesh = flatMesh;
            //Set Parent
            flatObject.transform.parent = subTransform;
            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = polyMesh;
            combine[1].mesh = sideMesh;
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static Mesh ExtrudePrint(Vector3[] origPolygon, float depth, Transform ourTrans, bool reversePoly)
        {
            //We need to convert poly for extrusion and triangulation based on Perpendicular direction
            //Determine normal of Poly
            Vector3 perpNormal;
            if (reversePoly)
            {
                perpNormal = -Vector3.Cross(origPolygon[0] - origPolygon[1], origPolygon[0] - origPolygon[2]).normalized;
            }
            else
            {
                perpNormal = -Vector3.Cross(origPolygon[1] - origPolygon[0], origPolygon[2] - origPolygon[0]).normalized;
            }

            int uvDirection = 0;//0 = X, 1 = Y, 2 = Z 

            Vector3 u = Vector3.zero;

            if (Vector3.Dot(Vector3.up, perpNormal) == 1.0f)
            {
                //Project to Y
                //Debug.Log("Projecting to Y Plane");
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
                uvDirection = 1;
            }
            else if (Vector3.Dot(Vector3.right, perpNormal) == 1.0f)
            {
                //Debug.Log("Projecting to X Plane");
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
                uvDirection = 0;
            }
            else if (Vector3.Dot(Vector3.forward, perpNormal) == 1.0f)
            {
                //Debug.Log("Projecting to Z Plane");
                u = Vector3.ProjectOnPlane(Vector3.up, perpNormal);
                uvDirection = 2;
            }

            Vector3 v = Vector3.Cross(u, perpNormal).normalized;

            Vector2[] poly = new Vector2[origPolygon.Length];
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //Project 3D Vectors to a 2D Plane
                poly[i] = new Vector2(Vector3.Dot(origPolygon[i], u), Vector3.Dot(origPolygon[i], v));
            }

            //int[] tris = EarClipper.Triangulate(poly);//Triangulate Polygon
            // Use the triangulator to get indices for creating triangles
            CiDyTriangulator tr = new CiDyTriangulator(poly);
            int[] tris = tr.Triangulate();

            Mesh polyMesh = new Mesh();
            Mesh sideMesh = new Mesh();

            Vector3[] vertices = new Vector3[poly.Length];
            Vector2[] polyUvs = new Vector2[poly.Length];
            //Place Vertices of extruded poly
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //Extrude these vertices
                vertices[i] = (origPolygon[i] + (perpNormal * depth)) - ourTrans.position;
                switch (uvDirection)
                {
                    case 0:
                        //X
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].z, origPolygon[i].y);
                        break;
                    case 1:
                        //Y
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].z);
                        break;
                    case 2:
                        //Z
                        //Update UVS
                        polyUvs[i] = new Vector2(origPolygon[i].x, origPolygon[i].y);
                        break;
                }
            }
            //Update Extruded Face of Mesh
            polyMesh.vertices = vertices;
            polyMesh.triangles = tris;
            polyMesh.uv = polyUvs;
            polyMesh.RecalculateNormals();
            polyMesh.RecalculateBounds();

            List<Vector3> newVerts = new List<Vector3>(0);
            List<int> triangles = new List<int>(0);
            List<Vector2> uvs = new List<Vector2>(0);
            //Now Lets generate the Side Walls of the Extruded Poly
            for (int i = 0; i < poly.Length; i++)
            {

                if (i == poly.Length - 1)
                {
                    //Last Points
                    //Loop connection   
                    //First Line
                    //Duplicate the originals positions as this is the base connection points.
                    newVerts.Add(origPolygon[i] - ourTrans.position);
                    newVerts.Add(origPolygon[i] + (perpNormal * depth) - ourTrans.position);
                    //SecondLine
                    newVerts.Add(origPolygon[0] - ourTrans.position);
                    newVerts.Add(origPolygon[0] + (perpNormal * depth) - ourTrans.position);
                }
                else
                {
                    //First Line
                    //Duplicate the originals positions as this is the base connection points.
                    newVerts.Add(origPolygon[i] - ourTrans.position);
                    newVerts.Add(origPolygon[i] + (perpNormal * depth) - ourTrans.position);
                    //SecondLine
                    newVerts.Add(origPolygon[i + 1] - ourTrans.position);
                    newVerts.Add(origPolygon[i + 1] + (perpNormal * depth) - ourTrans.position);
                }
                float xDist = Vector3.Distance(newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2]);
                float yDist = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 2]);

                float xDist2 = Vector3.Distance(newVerts[newVerts.Count - 2], newVerts[newVerts.Count - 1]);
                float yDist2 = Vector3.Distance(newVerts[newVerts.Count - 1], newVerts[newVerts.Count - 3]);

                //Calculate Normal of these Two polygons
                //Determine normal of Poly
                Vector3 polygonNormal = Vector3.Cross(newVerts[newVerts.Count - 3] - newVerts[newVerts.Count - 4], newVerts[newVerts.Count - 2] - newVerts[newVerts.Count - 4]).normalized;

                //Determine this polygons Normal Direction
                if (Mathf.Abs(Vector3.Dot(Vector3.up, polygonNormal)) < 0.2f)
                {
                    //Project to Y
                    //Debug.Log("Y Plane");
                    uvDirection = 1;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.forward, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("Z Plane");
                    uvDirection = 0;
                }
                else if (Mathf.Abs(Vector3.Dot(Vector3.right, polygonNormal)) < 0.2f)
                {
                    //Debug.Log("X Plane");
                    uvDirection = 2;
                }

                switch (uvDirection)
                {
                    case 0:
                        uvs.Add(new Vector2(0, 0));
                        uvs.Add(new Vector2(0, yDist));
                        //
                        uvs.Add(new Vector2(xDist, 0));
                        uvs.Add(new Vector2(xDist, yDist));
                        break;
                    case 1:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                    case 2:
                        uvs.Add(new Vector2(xDist2, 0));
                        uvs.Add(new Vector2(0, 0));
                        //
                        uvs.Add(new Vector2(xDist2, yDist2));
                        uvs.Add(new Vector2(0, yDist2));
                        break;
                }
                /*// Add UVS now that we have added four Verts.
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, yDist));
                //
                uvs.Add(new Vector2(xDist, 0));
                uvs.Add(new Vector2(xDist, yDist));*/
            }

            //Calculate Triangles for Vertices
            for (int i = 0; i < newVerts.Count; i += 4)
            {
                //First Triangle
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 3);
                //SecondTriangles
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
            }

            //Update Side Walls of Mesh
            sideMesh.vertices = newVerts.ToArray();
            sideMesh.triangles = triangles.ToArray();
            sideMesh.uv = uvs.ToArray();
            sideMesh.RecalculateNormals();
            sideMesh.RecalculateBounds();

            //Combine PolyMesh (Extruded and Side Meshes)
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = polyMesh;
            combine[1].mesh = sideMesh;
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static Mesh ExtrudeInset(Vector3[] origPolygon, float inset, float depth, Transform ourTrans)
        {
            //Create Mesh Holder for Inset Mesh and Side Mesh of Extruded Sides
            Mesh polyMesh = new Mesh();
            Mesh extWallMesh = new Mesh();
            Mesh interWallMesh = new Mesh();
            //We need to convert poly for extrusion and triangulation based on Perpendicular direction
            //Determine normal of Poly
            Vector3 perpNormal = Vector3.Cross(origPolygon[1] - origPolygon[0], origPolygon[2] - origPolygon[0]).normalized;

            /*Vector3 u;
            if (Mathf.Abs(Vector3.Dot(Vector3.forward, perpNormal)) < 0.2f)
            {
                //Debug.Log("Projecting to X Plane: "+Mathf.Abs(Vector3.Dot(Vector3.forward,perpNormal)));
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
            }
            else if (Mathf.Abs(Vector3.Dot(Vector3.right, perpNormal)) < 0.2f)
            {
                //Debug.Log("Projecting to Z Plane");
                u = Vector3.ProjectOnPlane(Vector3.up, perpNormal);
            }
            else
            {
                //Project to Y
                //Debug.Log("Projecting to Y Plane");
                u = Vector3.ProjectOnPlane(Vector3.forward, perpNormal);
            }

            Vector3 v = Vector3.Cross(u, perpNormal).normalized;*/

            System.Array.Reverse(origPolygon);//Reverse counter clockwise to clockwise for Inset Algorithm
                                              //Vector3[] poly = new Vector3[origPolygon.Length];
            /*for (int i = 0; i < origPolygon.Length; i++)
            {
                //Project 3D Vectors to a 2D Plane
                poly[i] = new Vector3(Vector3.Dot(origPolygon[i], u), 0, Vector3.Dot(origPolygon[i], v));
            }*/

            Vector3[] insetPoly = CiDyUtils.PolygonInset(origPolygon, inset);
            //Reverse Original and Poly
            //System.Array.Reverse(insetPoly);
            //System.Array.Reverse(origPolygon);
            Vector3[] verts = new Vector3[origPolygon.Length * 2];
            Vector2[] uvs = new Vector2[verts.Length];
            int currentPoint = 0;
            //Interweave the points. (Orig then inset etc)
            for (int i = 0; i < origPolygon.Length; i++)
            {
                //Clone orig
                verts[currentPoint] = origPolygon[i] - ourTrans.position + (perpNormal * depth);
                uvs[currentPoint] = (Vector2)origPolygon[i];
                currentPoint++;
                //Clone Inset
                verts[currentPoint] = (insetPoly[i] - ourTrans.position) + (perpNormal * depth);
                uvs[currentPoint] = (Vector2)insetPoly[i];
                //Update UVS
                currentPoint++;
            }
            //Now that we have the meshes verts. Lets calculate the Tris
            int[] tris = new int[(verts.Length * 3)];
            int triCount = 0;
            for (int i = 0; i < verts.Length; i += 2)
            {

                if (i == verts.Length - 2)
                {
                    //If at end of verts. Make sure we reference the first points.
                    //First Triangle
                    tris[triCount] = i;
                    triCount++;
                    tris[triCount] = i + 1;
                    triCount++;
                    tris[triCount] = 0;
                    triCount++;
                    //Second Triangle
                    tris[triCount] = 0;
                    triCount++;
                    tris[triCount] = i + 1;
                    triCount++;
                    tris[triCount] = 1;
                    triCount++;
                }
                else
                {
                    //First Triangle
                    tris[triCount] = i;
                    triCount++;
                    tris[triCount] = i + 1;
                    triCount++;
                    tris[triCount] = i + 2;
                    triCount++;
                    //Second Triangle
                    tris[triCount] = i + 2;
                    triCount++;
                    tris[triCount] = i + 1;
                    triCount++;
                    tris[triCount] = i + 3;
                    triCount++;
                }
            }

            polyMesh.vertices = verts;
            polyMesh.triangles = tris;
            polyMesh.uv = uvs;
            polyMesh.RecalculateBounds();
            polyMesh.RecalculateNormals();

            //Calcualte Extrior Side Wall Meshs
            List<Vector3> extPolySide = new List<Vector3>(0);
            List<Vector2> extUVS = new List<Vector2>(0);
            //Add to duplicates to end
            //Create Side Wall Verts for Extruded Exterior walls
            for (int i = 0; i < origPolygon.Length + 1; i++)
            {

                if (i != 0 && i != origPolygon.Length)
                {
                    //Add To Depth Poly
                    extPolySide.Add((origPolygon[i] - ourTrans.position));//Original
                    extPolySide.Add((origPolygon[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(origPolygon[i]);
                    extUVS.Add(origPolygon[i] + (perpNormal * depth));
                    //Add the Duplicates for all middle Points
                    extPolySide.Add(origPolygon[i] - ourTrans.position);//Original
                    extPolySide.Add((origPolygon[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(origPolygon[i]);
                    extUVS.Add(origPolygon[i] + (perpNormal * depth));
                }
                else if (i == origPolygon.Length)
                {

                    //Add To Depth Poly
                    extPolySide.Add((origPolygon[0] - ourTrans.position));//Original
                    extPolySide.Add((origPolygon[0] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(origPolygon[0]);
                    extUVS.Add(origPolygon[0] + (perpNormal * depth));
                }
                else
                {
                    //Add To Depth Poly
                    extPolySide.Add((origPolygon[i] - ourTrans.position));//Original
                    extPolySide.Add((origPolygon[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(origPolygon[i]);
                    extUVS.Add(origPolygon[i] + (perpNormal * depth));
                }
            }

            //Now Calculate the Tris
            int[] extTris = new int[extPolySide.Count * 3];
            triCount = 0;
            for (int i = 0; i < extPolySide.Count; i += 4)
            {
                //First Triangle
                extTris[triCount] = i;
                triCount++;
                extTris[triCount] = i + 1;
                triCount++;
                extTris[triCount] = i + 3;
                triCount++;
                //Second Triangle
                extTris[triCount] = i;
                triCount++;
                extTris[triCount] = i + 3;
                triCount++;
                extTris[triCount] = i + 2;
                triCount++;
            }

            extWallMesh.vertices = extPolySide.ToArray();
            extWallMesh.triangles = extTris;
            extWallMesh.uv = extUVS.ToArray();

            //Now Calculate the Interior Side Walls
            //Calcualte Extrior Side Wall Meshs
            List<Vector3> interPolySide = new List<Vector3>(0);
            List<Vector2> interUVS = new List<Vector2>(0);
            System.Array.Reverse(insetPoly);
            //Add to duplicates to end
            //Create Side Wall Verts for Extruded Exterior walls
            for (int i = 0; i < insetPoly.Length + 1; i++)
            {

                if (i != 0 && i != insetPoly.Length)
                {
                    //Add To Depth Poly
                    interPolySide.Add((insetPoly[i] - ourTrans.position));//Original
                    interPolySide.Add((insetPoly[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(insetPoly[i]);
                    extUVS.Add(insetPoly[i] + (perpNormal * depth));
                    //Add the Duplicates for all middle Points
                    interPolySide.Add(insetPoly[i] - ourTrans.position);//Original
                    interPolySide.Add((insetPoly[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(insetPoly[i]);
                    extUVS.Add(insetPoly[i] + (perpNormal * depth));
                }
                else if (i == insetPoly.Length)
                {

                    //Add To Depth Poly
                    interPolySide.Add((insetPoly[0] - ourTrans.position));//Original
                    interPolySide.Add((insetPoly[0] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(insetPoly[0]);
                    extUVS.Add(insetPoly[0] + (perpNormal * depth));
                }
                else
                {
                    //Add To Depth Poly
                    interPolySide.Add((insetPoly[i] - ourTrans.position));//Original
                    interPolySide.Add((insetPoly[i] - ourTrans.position) + (perpNormal * depth));//Depth
                    extUVS.Add(insetPoly[i]);
                    extUVS.Add(insetPoly[i] + (perpNormal * depth));
                }
            }

            //Now Calculate the Tris
            int[] interTris = new int[interPolySide.Count * 3];
            triCount = 0;
            for (int i = 0; i < interPolySide.Count; i += 4)
            {
                //First Triangle
                interTris[triCount] = i;
                triCount++;
                interTris[triCount] = i + 1;
                triCount++;
                interTris[triCount] = i + 3;
                triCount++;
                //Second Triangle
                interTris[triCount] = i;
                triCount++;
                interTris[triCount] = i + 3;
                triCount++;
                interTris[triCount] = i + 2;
                triCount++;
            }

            interWallMesh.vertices = interPolySide.ToArray();
            interWallMesh.triangles = interTris;
            interWallMesh.uv = interUVS.ToArray();

            //Combine PolySide, ExtWallMesh & InterWallMesh
            CombineInstance[] combine = new CombineInstance[3];
            combine[0].mesh = polyMesh;
            combine[1].mesh = extWallMesh;
            combine[2].mesh = interWallMesh;
            combine[0].transform = ourTrans.localToWorldMatrix;
            combine[1].transform = ourTrans.localToWorldMatrix;
            combine[2].transform = ourTrans.localToWorldMatrix;

            //Initialize Doors
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);

            return finalMesh;
        }

        public static float RoundToMidPoint(float value)
        {
            if (value % 0.5f == 0)
                return Mathf.Ceil(value);
            else
                return Mathf.Floor(value);
        }

        //To Compute the Angle Bisector Direction we need the predecessor pos, cur pos and the successor pos.
        public static Vector3 AngleBisector(Vector3 predPos, Vector3 curPos, Vector3 succPos)
        {
            //Now that we have our pred. and succes. Lets calculate the angle using there position from ours normalized to 1 unit vector.
            //to find the bisector we need to  find the angle of bisectors's adjacents(b-1,b+1)
            Vector3 v1 = (predPos - curPos).normalized;
            Vector3 v2 = (succPos - curPos).normalized;
            Vector3 bisector = (v1 + v2).normalized;
            return bisector;
        }
        //To Compute the Angle Bisector Direction we need the predecessor pos, cur pos and the successor pos.
        public static Vector3 AngleBisector(Vector3 predPos, Vector3 curPos1, Vector3 curPos2, Vector3 succPos)
        {
            /*predPos = new Vector3 (Mathf.Round(predPos.x*10)/10, 0, Mathf.Round(predPos.z*10)/10);
            curPos = new Vector3 (Mathf.Round(curPos.x*10)/10, 0, Mathf.Round(curPos.z*10)/10);
            succPos = new Vector3 (Mathf.Round(succPos.x*10)/10, 0, Mathf.Round(succPos.z*10)/10);*/
            //Now that we have our pred. and succes. Lets calculate the angle using there position from ours normalized to 1 unit vector.
            //to find the bisector we need to  find the angle of bisectors's adjacents(b-1,b+1)
            Vector3 v1 = (predPos - curPos1).normalized;
            Vector3 v2 = (succPos - curPos2).normalized;
            Vector3 bisector = (v1 + v2).normalized;
            return bisector;
        }

        public static float DistanceToLine(Vector3 p, Vector3 endA, Vector3 endB)
        {
            /*p = new Vector3 (Mathf.Round (p.x*10)/10, Mathf.Round (p.y*10)/10, Mathf.Round (p.z*10)/10);
            endA = new Vector3 (Mathf.Round (endA.x*10)/10, Mathf.Round (endA.y*10)/10, Mathf.Round (endA.z*10)/10);
            endB = new Vector3 (Mathf.Round (endB.x*10)/10, Mathf.Round (endB.y*10)/10, Mathf.Round (endB.z*10)/10);*/
            //float a = p.x - endA.x;
            //float b = p.z - endA.z;
            //float c = endB.x - endA.x;
            //float d = endB.z - endA.z;

            //float dot = a * c + b * d;
            //float len_sq = c * c + d * d;
            //Set R which represents between 0-1 float value (0 = at EndA),(1 = at EndB);
            //float r = dot/len_sq;
            //r = Mathf.Round(r * 10f) / 10f;


            /*float xx;
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
            }*/

            //float dx = p.x - xx;
            //float dz = p.y - zz;

            //Update Dist from point to Line Segment
            //dist = Mathf.Sqrt (dx * dx + dz * dz);
            float dist = Mathf.Abs((endB.x - endA.x) * (endA.z - p.z) - (endA.x - p.x) * (endB.z - endA.z)) / Mathf.Sqrt(Mathf.Pow(endB.x - endA.x, 2) + Mathf.Pow(endB.z - endA.z, 2));
            dist = Mathf.Round(dist * 10f) / 10f;
            //Update S which represents Left or right to Perpindicular Line a-b (>0 = right of line),(<0 = left of Line), (0 = colinear)
            //s = ((endA.x-endB.x) *(p.z-endA.z)-(endA.z-endB.z)*(p.x-endA.x));
            //s = Mathf.Round (s * 100f) / 100f;
            /*if(s<0){
                float t = Mathf.Abs(s);
                t = Mathf.Sqrt(t);
                s = -t;
            } else {
                s = Mathf.Sqrt(s);
            }*/
            return dist;
        }

        //Overload Method
        public static float DistanceToLine(Vector3 p, Vector3 endA, Vector3 endB, ref float r, ref float s)
        {
            float a = p.x - endA.x;
            float b = p.z - endA.z;
            float c = endB.x - endA.x;
            float d = endB.z - endA.z;

            float dot = a * c + b * d;
            float len_sq = c * c + d * d;
            //Set R which represents between 0-1 float value (0 = at EndA),(1 = at EndB);
            r = dot / len_sq;
            r = Mathf.Round(r * 100f) / 100f;
            if (r < 0)
            {
                r = 9999;
            }
            //Update S which represents Left or right to Perpindicular Line a-b (>0 = right of line),(<0 = left of Line), (0 = colinear)
            float length = Vector2.Distance(new Vector2(endA.x, endA.z), new Vector2(endB.x, endB.z));
            s = ((endA.x - endB.x) * (p.z - endA.z) - (endA.z - endB.z) * (p.x - endA.x)) / length;
            s = Mathf.Round(s * 100f) / 100f;

            return Mathf.Abs(s);
        }

        //Overload Method No Modifictaion of R
        public static float DistanceToLineR(Vector3 p, Vector3 endA, Vector3 endB, ref float r, ref float s)
        {
            float a = p.x - endA.x;
            float b = p.z - endA.z;
            float c = endB.x - endA.x;
            float d = endB.z - endA.z;

            float dot = a * c + b * d;
            float len_sq = c * c + d * d;
            //Set R which represents between 0-1 float value (0 = at EndA),(1 = at EndB);
            r = dot / len_sq;
            //r = Mathf.Round(r * 100f) / 100f;
            //Update S which represents Left or right to Perpindicular Line a-b (>0 = right of line),(<0 = left of Line), (0 = colinear)
            float length = Vector2.Distance(new Vector2(endA.x, endA.z), new Vector2(endB.x, endB.z));
            s = ((endA.x - endB.x) * (p.z - endA.z) - (endA.z - endB.z) * (p.x - endA.x)) / length;
            //s = Mathf.Round(s * 100f) / 100f;

            return Mathf.Abs(s);
        }

        //Overload Method
        public static float DistanceToLine(Vector3 p, Vector3 endA, Vector3 endB, ref float r, ref float s, ref Vector3 segPoint)
        {
            float a = p.x - endA.x;
            float b = p.z - endA.z;
            float c = endB.x - endA.x;
            float d = endB.z - endA.z;

            float dot = a * c + b * d;
            float len_sq = c * c + d * d;
            //Set R which represents between 0-1 float value (0 = at EndA),(1 = at EndB);
            r = dot / len_sq;
            r = Mathf.Round(r * 100f) / 100f;


            float xx;
            float zz;

            //try without Mathf.Approximatly as this is a more intense math calculation. the R value will be aprox as accurate and is already calculated
            //if(r < 0 || (Mathf.Approximately(endA.x,endB.x) && Mathf.Approximately(endA.z,endB.z))){
            if (r < 0 || (endA.x == endB.x && endA.z == endB.z))
            {
                //Debug.Log("r<0");
                xx = endA.x;
                zz = endA.z;
            }
            else if (r > 1)
            {
                //Debug.Log("r>1");
                xx = endB.x;
                zz = endB.z;
            }
            else
            {
                //Debug.Log("else");
                xx = endA.x + r * c;
                zz = endA.z + r * d;
            }

            //float dx = p.x - xx;
            //float dz = p.y - zz;

            //Update Dist from point to Line Segment
            //dist = Mathf.Sqrt (dx * dx + dz * dz);
            float dist = Mathf.Abs((endB.x - endA.x) * (endA.z - p.z) - (endA.x - p.x) * (endB.z - endA.z)) / Mathf.Sqrt(Mathf.Pow(endB.x - endA.x, 2) + Mathf.Pow(endB.z - endA.z, 2));
            dist = Mathf.Round(dist * 100f) / 100f;
            //Update S which represents Left or right to Perpindicular Line a-b (>0 = right of line),(<0 = left of Line), (0 = colinear)
            s = ((endA.x - endB.x) * (p.z - endA.z) - (endA.z - endB.z) * (p.x - endA.x));
            if (s < 0)
            {
                float t = Mathf.Abs(s);
                t = Mathf.Sqrt(t);
                s = -t;
            }
            else
            {
                s = Mathf.Sqrt(s);
            }
            s = Mathf.Round(s * 100f) / 100f;
            segPoint = new Vector3(xx, p.y, zz);
            return dist;
        }

        public static float PointLineDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            b.Normalize();
            Vector3 AP = p - a;
            return Vector3.Distance(AP, Vector3.Dot(AP, b) * b);
        }

        //This will return a -1(inside),0(On Line),1(outside) Tells us the Relation of Point to the Line.
        public static int PointToCircle(Vector3 p, Vector3 center, float radius)
        {
            int placement = 1;//Default is outside.
                              //Now test this point against our Circle. :)
            float PCx = Mathf.Round(Mathf.Pow((p.x - center.x), 2) * 10f / 10f);
            float PCz = Mathf.Round(Mathf.Pow((p.z - center.z), 2) * 10f / 10f);
            float radiusSq = Mathf.Round(Mathf.Pow(radius, 2) * 10f / 10f);
            float curValue = Mathf.Round((PCx + PCz) * 10f / 10f);
            //Only except the points that do not rest on the line or outside of it.
            if (curValue < radiusSq)
            {
                //Debug.Log (curValue+" < "+radiusSq);
                placement = -1;
            }
            else if (NearlyEqual(curValue, radiusSq))
            {
                //Debug.Log (curValue+" == "+radiusSq);
                placement = 0;
            }
            else if (curValue > radiusSq)
            {
                //Debug.Log (curValue+" > "+radiusSq);
                placement = 1;
            }

            //Debug.Log ("Return Placement "+placement);
            return placement;
        }

        public static bool NearlyEqual(float a, float b)
        {
            float absA = Mathf.Abs(a);
            float absB = Mathf.Abs(b);
            float diff = Mathf.Abs(a - b);

            if (a == b)
            { // shortcut, handles infinities
                return true;
            }
            else if (a == 0 || b == 0 || diff < float.MinValue)
            {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < (Mathf.Epsilon * float.MinValue);
            }
            else
            { // use relative error
                return diff / (absA + absB) < Mathf.Epsilon;
            }
        }

        //Test if Point is Inside Poly List(List is assumed CounterClockwise);
        public static bool PointInsidePolygon(List<Vector3> poly, Vector3 pnt)
        {
            int i, j;
            int nvert = poly.Count;
            bool c = false;
            for (i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                if (((poly[i].z > pnt.z) != (poly[j].z > pnt.z)) &&
                    (pnt.x < (poly[j].x - poly[i].x) * (pnt.z - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
                    c = !c;
            }
            return c;
        }

        //Overload for Straight Skeleton Inset Algorithm
        //Test if Point is Inside Poly List(List is assumed CounterClockwise);
        public static bool PointInsidePolygon(List<Vector2d> poly, Vector3 pnt)
        {
            int i, j;
            int nvert = poly.Count;
            bool c = false;
            for (i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                if (((poly[i].Y > pnt.z) != (poly[j].Y > pnt.z)) &&
                    (pnt.x < (poly[j].X - poly[i].X) * (pnt.z - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    c = !c;
            }
            return c;
        }

        //Test if Point is Inside Poly List(List is assumed CounterClockwise);
        public static bool PointInsideOrOnLinePolygon(List<Vector3> poly, Vector3 pnt)
        {
            int i, j;
            int nvert = poly.Count;
            bool c = false;
            for (i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                if (((poly[i].z >= pnt.z) != (poly[j].z >= pnt.z)) &&
                    (pnt.x <= (poly[j].x - poly[i].x) * (pnt.z - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
                    c = !c;
            }
            return c;
        }

        public static bool PointInsideOrOnLinePolygon(Vector3[] poly, Vector3 pnt)
        {
            int i, j;
            int nvert = poly.Length;
            bool c = false;
            for (i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                if (((poly[i].z >= pnt.z) != (poly[j].z >= pnt.z)) &&
                    (pnt.x <= (poly[j].x - poly[i].x) * (pnt.z - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
                    c = !c;
            }
            return c;
        }

        //XZ axis PointToCircle test This will Return True or False. If the Point is On the Circles perimiter it is Consider outside.
        public static bool PointInsideCircle(Vector3 p, Vector3 center, float radius)
        {
            bool inside = false;
            //Now test this point against our Circle. :)
            float PCx = Mathf.Pow(Mathf.Round((p.x - center.x) * 100f) / 100f, 2);
            float PCz = Mathf.Pow(Mathf.Round((p.z - center.z) * 100f) / 100f, 2);
            float radiusSq = Mathf.Round(radius * radius);

            //Only except the points that do not rest on the line or outside of it.
            if ((PCx + PCz) < radiusSq)
            {
                inside = true;
            }

            return inside;
        }
        //Calculates the Center and Radius of circumCircle of Triangle (ABC)
        //In general, x and y must satisfy (x - center_x)^2 + (y - center_y)^2 < radius^2.(< means inside and == means on Circle and > means outSide of Circle)
        //Take the User Placed Points (3) and find the Circumcircle through them.
        public static Vector3 FindCircumCircle(Vector3 a, Vector3 b, Vector3 c, ref float radius)
        {
            //Debug.Log ("Find Circum Circle a: " + a + " b: " + b + " C: " + c);
            Vector3 center = Vector3.zero;
            //Create Perpendicular Bisectors of A-B and B-C
            //Find perp Direction
            Vector3 AB = (b - a);
            Vector3 BC = (c - b);
            //Draw Point in Middle Of AB Line.
            Vector3 perpAB = Vector3.Cross(AB, Vector3.up).normalized;
            Vector3 perpBC = Vector3.Cross(BC, Vector3.up).normalized;
            //Place Point half way on line.
            Vector3 halfAB = a + AB * 0.5f;
            Vector3 halfBC = b + BC * 0.5f;
            //Set End Points of Both Perp Lines for Line Intersection Testing.
            Vector3 endAB = (halfAB + (perpAB * 2000));
            Vector3 endAB2 = (halfAB + (-perpAB * 2000));
            Vector3 endBC = (halfBC + (perpBC * 2000));
            Vector3 endBC2 = (halfBC + (-perpBC * 2000));
            // find intersection => center point of incircle
            if (!LineIntersection(endAB, endAB2, endBC, endBC2, ref center))
            {
                //We cant find the CircleCenter so no circle
                //Debug.LogWarning("No Intersection Found?");
                return Vector3.zero;//Null
            }
            //Calculate Radius by flattening Center and A
            Vector3 tmpA = a;
            Vector3 tmpCntr = center;
            tmpCntr.y = 0;
            tmpA.y = 0;
            // We must have a center so Calculate the Radius :)
            radius = Vector3.Distance(tmpA, tmpCntr);
            //Debug.Log ("Radius to A: "+Vector3.Distance(center,a)+" Radius to B: " + Vector3.Distance (center, b) + " Radius to C: " + Vector3.Distance (center, c));
            return center;
        }

        //This will return true or false if any points in the sent list exist inside the circumcircle of the sent triangle.
        public static bool AnyPointsInsideCircumCircle(Vector3 p0, Vector3 p1, Vector3 p2, List<Vector3> pointSet)
        {
            //Create the CircumCircle for point testing for this triangle
            float radius = 0f;
            Vector3 center = FindCircumCircle(p0, p1, p2, ref radius);
            //if we do not have a center then return as if there are points. 
            if (center == Vector3.zero)
            {
                Debug.Log("Same As Zero: " + center);
                return true;//This is based on how i use this bool function.
            }
            else
            {
                Debug.Log("Not the Same: " + center);
            }
            //Test point set
            for (int i = 0; i < pointSet.Count; i++)
            {
                //Test if this point p4 is inside of the circle. :)
                Vector3 p4 = pointSet[i];
                if (PointInsideCircle(p4, center, radius))
                {
                    //There is a point inside this circum Circle. This return true;
                    return true;
                }
            }
            //We didn't find any points inside of the circle.
            return false;
        }

        public static bool CompareVectors(Vector3 a, Vector3 b)
        {
            //Debug.Log ("Compare Vectors A: "+a+" B: "+b);
            //if they aren't the same length, don't bother checking the rest.
            if (!Mathf.Approximately(a.magnitude, b.magnitude))
            {
                Debug.Log("Length is Different They Cannot be the Same");
                //If length is different then they cannot be the same.
                return false;
            }
            float cosAngleError = Mathf.Cos(Mathf.Epsilon * Mathf.Deg2Rad);
            //A value between -1 and 1 corresponding to the angle.
            float cosAngle = Vector3.Dot(a.normalized, b.normalized);
            //Debug.Log ("cosAngleError: " + cosAngleError+" CosAngle: "+cosAngle);
            //The dot product of normalized Vectors is equal to the cosine of the angle between them.
            //So the closer they are, the closer the value will be to 1.  Opposite Vectors will be -1
            //and orthogonal Vectors will be 0.
            if (cosAngle >= cosAngleError)
            {
                //Debug.Log("Same Vectors cosAngle "+cosAngle+" >= cosAngleError "+cosAngleError);
                //If angle is greater, that means that the angle between the two vectors is less than the error allowed.
                return true;
            }
            //Debug.Log("Different Vectors cosAngle "+cosAngle+" < cosAngleError "+cosAngleError);
            return false;
        }

        //This Function will generate a Multi-Lane Road
        public static float GenerateDetailedRoad(Vector3[] roadPoints, int laneCount, float laneWidth, CiDyRoad.LaneType _laneType, float leftShoulderWidth, float rightShoulderWidth, float centerSpacing, Transform parentTransform, CiDyRoad parentRoad = null, Material roadMat = null, Material centerMat = null, Material shoulderMat = null, bool createMarkings = true, Vector3 strtDir = default(Vector3), Vector3 endDir = default(Vector3), bool leftOffRamp = false, bool rightOffRamp = false)
        {
            //Handle OneWay Logic
            if (laneCount == 1)
            {
                centerSpacing = 0;
            }
            //Clear Previous Marking spawners
            if (parentRoad != null)
            {
                parentRoad.ClearMarkingSpawners();
            }
            bool seperateShoulderMat = true;
            //Grab Road Material
            if (roadMat == null)
            {
                roadMat = Resources.Load("CiDyResources/Road", typeof(Material)) as Material;
                if (roadMat == null)
                {
                    Debug.LogWarning("No Road Material found in Resources Folder, CiDyUtils.cs Line 7686 GenerateLane()");
                }
            }
            //Set Defaults.
            if (shoulderMat == null || shoulderMat == roadMat)
            {
                shoulderMat = roadMat;
                seperateShoulderMat = false;
            }
            //We want both Directions to have there own Lanes and Shoulders.
            float centerWidth = laneWidth;
            if (laneCount > 1)
            {
                //Split down the Middle to equalize multi lanes
                centerWidth = (laneCount / 2) * laneWidth;
            }
            List<Mesh> meshes = new List<Mesh>(0);//List of Meshes that should be combined (Road Material)
            List<Mesh> shoulderMeshes = new List<Mesh>(0);
            if (strtDir == default(Vector3))
            {
                strtDir = Vector3.zero;
            }
            if (endDir == default(Vector3))
            {
                endDir = Vector3.zero;
            }
            Mesh rightRoadMesh = new Mesh();//Null
            Vector3[] rightSideLane = new Vector3[0];

            if (laneCount > 1)
            {
                //Right Side
                //Offset Path
                rightSideLane = CiDyUtils.OffsetPath(roadPoints, (centerSpacing / 2) + (centerWidth / 2), ref strtDir, ref endDir);
                //Create Right Side Road Mesh
                //We have a Lane Count and Lane Width. Lane Width * Lane Count = Total Width.
                rightRoadMesh = CreateRoad(rightSideLane, centerWidth, strtDir, endDir);
                meshes.Add(rightRoadMesh);
            }
            Vector3[] verts = rightRoadMesh.vertices;
            int[] tris = rightRoadMesh.triangles;
            Vector2[] uvs = rightRoadMesh.uv;
            //Create Spawner
            if (laneCount > 1 && createMarkings)
            {
                //Add Spawner Spline
                GameObject spawnerSpline = new GameObject("CiDySpawnerSpline");
                spawnerSpline.transform.SetParent(parentTransform);
                //Add Component
                CiDySpawner spawner = spawnerSpline.AddComponent<CiDySpawner>();
                //Set Path to Spawner
                spawner.SetPath(roadPoints, centerWidth, strtDir, endDir);
                //Set first one
                spawner.spawnType = CiDySpawner.SpawnerType.RoadMarkings;
                switch (_laneType)
                {
                    case CiDyRoad.LaneType.SixLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.SixLane;
                        break;
                    case CiDyRoad.LaneType.FourLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.FourLane;
                        break;
                    case CiDyRoad.LaneType.TwoLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.TwoLane;
                        break;
                    case CiDyRoad.LaneType.OneLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.OneWayLine;
                        break;
                }
                spawner.pathOffset = (centerSpacing / 2) + (centerWidth / 2);//One Lane is 0 offset for Marking.
                spawner.Generate();
                if (parentRoad)
                    parentRoad.AddMarkingSpawner(spawner, 0);
            }
            ////////////Left Side Of Road
            Mesh leftRoadMesh = new Mesh();
            Vector3[] leftSideLane = new Vector3[0];
            if (laneCount > 1)
            {
                leftSideLane = CiDyUtils.OffsetPath(roadPoints, -((centerSpacing / 2) + (centerWidth / 2)), ref strtDir, ref endDir);
                //Create Left Side Road Mesh
                //We have a Lane Count and Lane Width. Lane Width * Lane Count = Total Width.
                leftRoadMesh = CreateRoad(leftSideLane, centerWidth, strtDir, endDir);
                meshes.Add(leftRoadMesh);
            }
            //This also Creates Markings for One Way
            if (createMarkings)
            {
                //Create Spawner
                //Add Spawner Spline
                GameObject spawnerSpline = new GameObject("CiDySpawnerSpline");
                spawnerSpline.transform.SetParent(parentTransform);
                //Add Component
                CiDySpawner spawner = spawnerSpline.AddComponent<CiDySpawner>();
                //Set Path to Spawner
                spawner.SetPath(roadPoints, centerWidth, strtDir, endDir, false, true);
                //Set first one
                spawner.spawnType = CiDySpawner.SpawnerType.RoadMarkings;
                switch (_laneType)
                {
                    case CiDyRoad.LaneType.SixLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.SixLane;
                        break;
                    case CiDyRoad.LaneType.FourLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.FourLane;
                        break;
                    case CiDyRoad.LaneType.TwoLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.TwoLane;
                        break;
                    case CiDyRoad.LaneType.OneLane:
                        spawner.roadMarkings = CiDySpawner.RoadMarkings.OneWayLine;
                        break;
                }
                if (laneCount > 1)
                {
                    spawner.pathOffset = -((centerSpacing / 2) + (centerWidth / 2));//One Lane is 0 offset for Marking.
                }
                spawner.Generate();
                if (parentRoad)
                    parentRoad.AddMarkingSpawner(spawner, 1);
            }
            if (laneCount > 1 && centerMat && centerSpacing > 0f)
            {
                //Define Center Mesh and Material and Add to Sub Objects.
                GameObject dividerLane = new GameObject("DividerLane");
                dividerLane.transform.position = parentTransform.position;
                dividerLane.transform.SetParent(parentTransform);
                MeshRenderer dRenderer = dividerLane.AddComponent<MeshRenderer>();
                dRenderer.sharedMaterial = centerMat;
                MeshFilter dFilter = dividerLane.AddComponent<MeshFilter>();
                Mesh dividerMesh = CiDyUtils.CreateRoad(roadPoints, centerSpacing, strtDir, endDir);
                //meshes.Add(dividerMesh);
                dFilter.sharedMesh = dividerMesh;//Set Mesh On Filter
                                                 //Set Collider
                MeshCollider dCollider = dividerLane.AddComponent<MeshCollider>();
                dCollider.sharedMesh = dividerMesh;
                dCollider.gameObject.tag = "Road";
                dCollider.gameObject.layer = LayerMask.NameToLayer("Road");
            }
            else if (laneCount == 1)
            {
                //Create OneWayRoad.
                Mesh oneWayMesh = CreateRoad(roadPoints, laneWidth, strtDir, endDir);
                meshes.Add(oneWayMesh);
                //Debug.Log("Created One Way Road Mesh");
            }
            Vector3[] offsetPath = null;
            //Determine OffRamps and Shoulders
            if (rightOffRamp)
            {
                //Generate Merger Lane
                //We Calculate Offset for Left Edge of MergerLane
                offsetPath = CiDyUtils.OffsetPath(rightSideLane, (centerWidth / 2), ref strtDir, ref endDir);
                //Calculate Normalized 
                Vector3[] rightPath = new Vector3[offsetPath.Length];
                int length = offsetPath.Length;
                float incrementDist = laneWidth / length;
                for (int i = 0; i < length; i++)
                {
                    Vector3 curPos = offsetPath[i];
                    Vector3 nxtPos;
                    Vector3 vectorDir;
                    if (i == offsetPath.Length - 1)
                    {
                        //End
                        nxtPos = offsetPath[i - 1];
                        vectorDir = (curPos - nxtPos).normalized;
                    }
                    else
                    {
                        //Beginning or Middle
                        nxtPos = offsetPath[i + 1];
                        vectorDir = (nxtPos - curPos).normalized;
                    }
                    vectorDir.y = 0;
                    Vector3 rightDir = Vector3.Cross(Vector3.up, vectorDir).normalized;
                    //Calculate Distance offset lerped to Max Offset.
                    float offset = (i * incrementDist);
                    if (offset > laneWidth)
                    {
                        offset = laneWidth;
                    }
                    if (i == 0)
                    {
                        rightPath[i] = offsetPath[i] + (rightDir * 0.1f);
                    }
                    else
                    {
                        rightPath[i] = offsetPath[i] + (rightDir * (offset));
                    }
                }
                //Create a Polygon from the Two Lists
                List<Vector3> vertsList = new List<Vector3>(0);
                List<Vector2> uvsList = new List<Vector2>(0);
                List<int> trisList = new List<int>(0);
                //System.Array.Reverse(offsetPath);
                for (int i = 0; i < length; i++)
                {
                    vertsList.Add(offsetPath[i]);
                    vertsList.Add(rightPath[i]);
                }
                int n = vertsList.Count;

                //Look at four points
                for (int i = 0; i < n - 2; i += 2)
                {
                    trisList.Add(i);//0
                    trisList.Add(i + 2);//2
                    trisList.Add(i + 3);//3

                    trisList.Add(i + 3);//3
                    trisList.Add(i + 1);//1
                    trisList.Add(i);//0
                }
                //Setup UVs
                float uvDist = 0;
                float zDist = 0;
                float xDist = 0;
                //Set up UVs for Three Segments and Up.
                for (int i = 0; i < n - 2; i += 2)
                {
                    xDist = Vector3.Distance(verts[i], verts[i + 1]) / 10;
                    //Handle All Four Points of UV with mounting Values.
                    uvsList.Add(new Vector2(1 - xDist, uvDist));
                    uvsList.Add(new Vector2(1, uvDist));

                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = (Vector3.Distance(midPointA, midPointB)) / 60;
                    uvDist += zDist;
                    xDist = Vector3.Distance(verts[i + 2], verts[i + 3]) / 10;
                }
                //Add Last Two Points
                uvsList.Add(new Vector2(1 - xDist, uvDist));
                uvsList.Add(new Vector2(1, uvDist));

                Mesh polyMesh = new Mesh
                {
                    vertices = vertsList.ToArray(),
                    triangles = trisList.ToArray(),
                    uv = uvsList.ToArray()
                };
                polyMesh.RecalculateBounds();
                polyMesh.RecalculateNormals();
                GameObject mergerLane = new GameObject("MergerLane");
                mergerLane.transform.SetParent(parentTransform);
                MeshRenderer mergerRenderer = mergerLane.AddComponent<MeshRenderer>();
                MeshFilter mergerFilter = mergerLane.AddComponent<MeshFilter>();
                mergerRenderer.sharedMaterial = roadMat;
                mergerFilter.sharedMesh = polyMesh;
                //Update Offset Path
                offsetPath = rightPath;
            }
            if (offsetPath != null && offsetPath.Length > 0)
            {
                //Right Shoulder Mesh
                Mesh rightShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(offsetPath, (rightShoulderWidth / 2), ref strtDir, ref endDir), rightShoulderWidth, strtDir, endDir);
                if (!seperateShoulderMat)
                {
                    meshes.Add(rightShoulderMesh);
                }
                else
                {
                    shoulderMeshes.Add(rightShoulderMesh);
                }
            }
            else
            {
                //Right Shoulder Mesh
                Mesh rightShoulderMesh = new Mesh();
                if (laneCount > 1)
                {
                    rightShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(rightSideLane, (centerWidth / 2) + (rightShoulderWidth / 2), ref strtDir, ref endDir), rightShoulderWidth, strtDir, endDir);
                }
                else if (laneCount == 1)
                {
                    //Debug.Log("Right Shoulder Mesh for Single One Way Road");
                    rightShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(roadPoints, (centerWidth / 2) + (rightShoulderWidth / 2), ref strtDir, ref endDir), rightShoulderWidth, strtDir, endDir);
                }
                if (!seperateShoulderMat)
                {
                    meshes.Add(rightShoulderMesh);
                }
                else
                {
                    shoulderMeshes.Add(rightShoulderMesh);
                }
            }
            offsetPath = null;//Reset
            if (leftOffRamp)
            {
                //Generate Merger Lane
                //We Calculate Offset for Left Edge of MergerLane
                offsetPath = CiDyUtils.OffsetPath(leftSideLane, -(centerWidth / 2), ref strtDir, ref endDir);
                //Calculate Normalized 
                Vector3[] leftPath = new Vector3[offsetPath.Length];
                int length = offsetPath.Length;
                float incrementDist = laneWidth / length;
                for (int i = 0; i < length; i++)
                {
                    Vector3 curPos = offsetPath[i];
                    Vector3 nxtPos;
                    Vector3 vectorDir;
                    if (i == offsetPath.Length - 1)
                    {
                        //End
                        nxtPos = offsetPath[i - 1];
                        vectorDir = (curPos - nxtPos).normalized;
                    }
                    else
                    {
                        //Beginning or Middle
                        nxtPos = offsetPath[i + 1];
                        vectorDir = (nxtPos - curPos).normalized;
                    }
                    vectorDir.y = 0;
                    Vector3 leftDir = -Vector3.Cross(Vector3.up, vectorDir).normalized;
                    //Calculate Distance offset lerped to Max Offset.
                    float offset = (i * incrementDist);
                    if (offset > laneWidth)
                    {
                        offset = laneWidth;
                    }
                    if (i == 0)
                    {
                        leftPath[i] = offsetPath[i] + (leftDir * 0.1f);
                    }
                    else
                    {
                        leftPath[i] = offsetPath[i] + (leftDir * (offset));
                    }
                }
                //Create a Polygon from the Two Lists
                List<Vector3> vertsList = new List<Vector3>(0);
                List<Vector2> uvsList = new List<Vector2>(0);
                List<int> trisList = new List<int>(0);
                //System.Array.Reverse(offsetPath);
                for (int i = 0; i < length; i++)
                {
                    vertsList.Add(offsetPath[i]);
                    vertsList.Add(leftPath[i]);
                }
                int n = vertsList.Count;

                //Look at four points
                for (int i = 0; i < n - 2; i += 2)
                {
                    trisList.Add(i);//0
                    trisList.Add(i + 2);//2
                    trisList.Add(i + 3);//3

                    trisList.Add(i + 3);//3
                    trisList.Add(i + 1);//1
                    trisList.Add(i);//0
                }
                //Setup UVs
                float uvDist = 0;
                float zDist = 0;
                float xDist = 0;
                //Set up UVs for Three Segments and Up.
                for (int i = 0; i < n - 2; i += 2)
                {
                    xDist = Vector3.Distance(verts[i], verts[i + 1]) / 10;
                    //Handle All Four Points of UV with mounting Values.
                    uvsList.Add(new Vector2(1 - xDist, uvDist));
                    uvsList.Add(new Vector2(1, uvDist));

                    Vector3 midPointA = (verts[i] + verts[i + 1]) / 2;
                    Vector3 midPointB = (verts[i + 2] + verts[i + 3]) / 2;
                    //Get Vertical Distance
                    zDist = (Vector3.Distance(midPointA, midPointB)) / 60;
                    uvDist += zDist;
                    xDist = Vector3.Distance(verts[i + 2], verts[i + 3]) / 10;
                }
                //Add Last Two Points
                uvsList.Add(new Vector2(1 - xDist, uvDist));
                uvsList.Add(new Vector2(1, uvDist));

                trisList.Reverse();
                Mesh polyMesh = new Mesh
                {
                    vertices = vertsList.ToArray(),
                    triangles = trisList.ToArray(),
                    uv = uvsList.ToArray()
                };
                polyMesh.RecalculateBounds();
                polyMesh.RecalculateNormals();
                GameObject mergerLane = new GameObject("MergerLane");
                mergerLane.transform.position = parentTransform.position;
                mergerLane.transform.SetParent(parentTransform);
                MeshRenderer mergerRenderer = mergerLane.AddComponent<MeshRenderer>();
                MeshFilter mergerFilter = mergerLane.AddComponent<MeshFilter>();
                mergerRenderer.sharedMaterial = roadMat;
                mergerFilter.sharedMesh = polyMesh;
                //Update Offset Path
                offsetPath = leftPath;
            }
            if (offsetPath != null && offsetPath.Length > 0)
            {
                //Left Shoulder Mesh
                Mesh leftShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(offsetPath, -(leftShoulderWidth / 2), ref strtDir, ref endDir), rightShoulderWidth, strtDir, endDir);
                if (!seperateShoulderMat)
                {
                    meshes.Add(leftShoulderMesh);
                }
                else
                {
                    shoulderMeshes.Add(leftShoulderMesh);
                }
            }
            else
            {
                //Left Shoulder Mesh
                Mesh leftShoulderMesh = new Mesh();
                if (laneCount > 1)
                {
                    leftShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(leftSideLane, -((centerWidth / 2) + (leftShoulderWidth / 2)), ref strtDir, ref endDir), leftShoulderWidth, strtDir, endDir);
                }
                else if (laneCount == 1)
                {
                    //Debug.Log("Left Shoulder Mesh for Single One Way Road");
                    leftShoulderMesh = CiDyUtils.CreateRoad(CiDyUtils.OffsetPath(roadPoints, -((centerWidth / 2) + (leftShoulderWidth / 2)), ref strtDir, ref endDir), leftShoulderWidth, strtDir, endDir);
                }
                if (!seperateShoulderMat)
                {
                    meshes.Add(leftShoulderMesh);
                }
                else
                {
                    shoulderMeshes.Add(leftShoulderMesh);
                }
            }
            ///////////////////////////Combine Meshes/////////////////////////
            //Combine Center Road and Shoulders to Minimize Shader Draw Calls.
            CombineInstance[] combine = new CombineInstance[meshes.Count];
            for (int i = 0; i < combine.Length; i++)
            {
                combine[i].mesh = meshes[i];
                combine[i].transform = parentTransform.localToWorldMatrix;
            }
            //Combine All Desired Meshes.
            Mesh finalMesh = new Mesh();
            //Combine
            finalMesh.CombineMeshes(combine, true, true);
            //Set Main Mesh Renderer
            MeshRenderer mRenderer = parentTransform.GetComponent<MeshRenderer>();
            if (mRenderer == null)
            {
                mRenderer = parentTransform.gameObject.AddComponent<MeshRenderer>();
            }
            MeshFilter mFilter = parentTransform.GetComponent<MeshFilter>();
            if (mFilter == null)
            {
                mFilter = parentTransform.gameObject.AddComponent<MeshFilter>();
            }
            MeshCollider mCollider = parentTransform.GetComponent<MeshCollider>();
            if (mCollider == null)
            {
                mCollider = parentTransform.gameObject.AddComponent<MeshCollider>();
            }
            mFilter.sharedMesh = finalMesh;
            mRenderer.sharedMaterial = roadMat;
            mCollider.sharedMesh = finalMesh;
            //Handle Seperate Shoulder Meshes if they are defined as different Materials.
            if (seperateShoulderMat)
            {
                combine = new CombineInstance[shoulderMeshes.Count];
                for (int i = 0; i < combine.Length; i++)
                {
                    combine[i].mesh = shoulderMeshes[i];
                    combine[i].transform = parentTransform.localToWorldMatrix;
                }
                //Combine All Desired Meshes.
                finalMesh = new Mesh();
                //Combine
                finalMesh.CombineMeshes(combine, true, true);
                //Now Create a Holder for Shoulder Mesh.
                //Define Center Mesh and Material and Add to Sub Objects.
                GameObject Shoulders = new GameObject("Shoulders");
                Shoulders.transform.position = parentTransform.position;
                Shoulders.transform.SetParent(parentTransform);
                MeshRenderer shoulderRenderer = Shoulders.AddComponent<MeshRenderer>();
                shoulderRenderer.sharedMaterial = shoulderMat;
                MeshFilter shoulderFilter = Shoulders.AddComponent<MeshFilter>();
                shoulderFilter.sharedMesh = finalMesh;//Set Mesh on Renderer
                                                      //Set Collider
                MeshCollider shoulderCollider = Shoulders.AddComponent<MeshCollider>();
                shoulderCollider.sharedMesh = finalMesh;
                shoulderCollider.gameObject.tag = "Road";
                shoulderCollider.gameObject.layer = LayerMask.NameToLayer("Road");
            }
            //Return This Roads Calculated Width
            return (centerSpacing + ((laneCount * laneWidth) + (leftShoulderWidth + rightShoulderWidth)));
        }

        public static Vector3[] OffsetPath(Vector3[] spline, float offset, ref Vector3 startDir, ref Vector3 endDir, bool reverseFacing = false, bool placeOnGround = false, bool placeOnRoad = false)
        {

            //Calculate New path with Offset Value
            Vector3[] offsetPath = new Vector3[spline.Length];
            //int mask = ~(1 >> groundLayer.value);
            //Offset Path
            for (int i = 0; i < spline.Length; i++)
            {
                Vector3 curPos = spline[i];
                Vector3 nxtPos;//
                Vector3 dir;//
                if (i == spline.Length - 1)
                {
                    nxtPos = spline[i - 1];
                    dir = (curPos - nxtPos).normalized;
                }
                else
                {
                    nxtPos = spline[i + 1];
                    dir = (nxtPos - curPos).normalized;
                }
                //Flatten Y
                dir.y = 0;
                //Get Right
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;//Right by Default
                if (i == 0 && startDir != default)
                {
                    right = startDir;
                }
                else if (i == spline.Length - 1 && endDir != default)
                {
                    right = -endDir;
                }
                if (offset < 0)
                {
                    //Left
                    offsetPath[i] = spline[i] + ((-right) * (-offset));
                }
                else
                {
                    //Finalize Path with Offset
                    offsetPath[i] = spline[i] + (right * offset);
                }

                //Run a Raycast below this point.
                if (placeOnGround)
                {
                    Vector3 rayOrig = offsetPath[i] + (Vector3.up * 1000);
                    //Shoot Raycast downward
                    RaycastHit hit;
                    if (Physics.Raycast(rayOrig, Vector3.down, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Terrain")))
                    {
                        //Return Height
                        offsetPath[i].y = (hit.point.y - 0.1f);
                    }
                }
                if (placeOnRoad)
                {
                    Vector3 rayOrig = offsetPath[i] + (Vector3.up * 1000);
                    //Shoot Raycast downward
                    RaycastHit hit;
                    if (Physics.Raycast(rayOrig, Vector3.down, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Road")))
                    {
                        //Return Height
                        offsetPath[i].y = (hit.point.y + 0.5f);
                    }
                }
            }

            if (reverseFacing)
            {
                System.Array.Reverse(offsetPath);
                Vector3 tmpDir = endDir;
                endDir = startDir;
                startDir = tmpDir;
            }

            //Return Offset Path
            return offsetPath;
        }

        //This turns points lines list into a road mesh. // bool for rotation of texture if needed.
        public static Mesh CreateRoad(Vector3[] points, float width, Vector3 startDir = default, Vector3 endDir = default, bool isHorizontal = false)
        {
            // Create and set the mesh
            Mesh mesh = new Mesh();
            //Store Points iteration count
            int n = points.Length - 1;
            //Debug.Log (n+1);
            Vector3 fwd = Vector3.forward;
            Vector3 right = Vector3.right;
            List<Vector3> vs = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            //Iterate through the Points List
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 curPoint = points[i];
                Vector3 nxtPoint;
                if (i == points.Length - 1)
                {
                    //At End
                    nxtPoint = points[i - 1];
                    //Last Must reverse Fwd
                    fwd = (curPoint - nxtPoint).normalized;
                }
                else
                {
                    //Standard Fwd
                    nxtPoint = points[i + 1];
                    //Last Must reverse Fwd
                    fwd = (nxtPoint - curPoint).normalized;
                }
                // Except for the last point
                // Now assume no banking
                fwd.y = 0f;
                // Get left
                right = Vector3.Cross(Vector3.up, fwd).normalized;
                if (i == 0 && startDir != default)
                {
                    right = startDir;
                }
                else if (i == points.Length - 1 && endDir != default)
                {
                    right = -endDir;
                }
                //Debug.Log("Found LEfT");
                //Second point to the left of first point Multiplied by road width
                curPoint = points[i] + ((-right) * (width / 2));//Left
                vs.Add(curPoint);
                curPoint = points[i] + (right * (width / 2));//Right
                vs.Add(curPoint);
            }
            n = vs.Count;

            //Look at four points
            for (int i = 0; i < n - 2; i += 2)
            {
                tris.Add(i);//0
                tris.Add(i + 2);//2
                tris.Add(i + 3);//3

                tris.Add(i + 3);//3
                tris.Add(i + 1);//1
                tris.Add(i);//0
            }
            //Setup UVs
            if (!isHorizontal)
            {
                float uvDist = 0;
                float zDist = 0;
                //Set up UVs for Three Segments and Up.
                for (int i = 0; i < n - 2; i += 2)
                {
                    //Handle All Four Points of UV with mounting Values.
                    uvs.Add(new Vector2(0, uvDist));
                    uvs.Add(new Vector2(1, uvDist));

                    Vector3 midPointA = (vs[i] + vs[i + 1]) / 2;
                    Vector3 midPointB = (vs[i + 2] + vs[i + 3]) / 2;
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
                for (int i = 0; i < n; i++)
                {
                    uvs.Add(new Vector2(vs[i].x, vs[i].z));
                }
            }

            //Update the Mesh with the new variables
            mesh.vertices = (vs.ToArray());
            mesh.triangles = (tris.ToArray());
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        //Found On Unity Community
        private static void TangentSolver(Mesh theMesh)
        {
            int vertexCount = theMesh.vertexCount;
            Vector3[] vertices = theMesh.vertices;
            Vector3[] normals = theMesh.normals;
            Vector2[] texcoords = theMesh.uv;
            int[] triangles = theMesh.triangles;
            int triangleCount = triangles.Length / 3;
            Vector4[] tangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            int tri = 0;
            for (int i = 0; i < (triangleCount); i++)
            {
                int i1 = triangles[tri];
                int i2 = triangles[tri + 1];
                int i3 = triangles[tri + 2];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector2 w1 = texcoords[i1];
                Vector2 w2 = texcoords[i2];
                Vector2 w3 = texcoords[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float r = 1.0f / (s1 * t2 - s2 * t1);
                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
                tri += 3;
            }

            for (int i = 0; i < (vertexCount); i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan1[i];

                // Gram-Schmidt orthogonalize
                Vector3.OrthoNormalize(ref n, ref t);

                tangents[i].x = t.x;
                tangents[i].y = t.y;
                tangents[i].z = t.z;
                // Calculate handedness
                tangents[i].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0) ? -1.0f : 1.0f;
            }
            theMesh.tangents = tangents;
        }

        public static GameObject MarkPoint(Vector3 newPos, int count)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = newPos;
            sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            sphere.name = ("M" + count);
            sphere.GetComponent<SphereCollider>().enabled = false;
            return sphere;
        }

        public static GameObject MarkPoint(Vector3 newPos, string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = newPos;
            sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            sphere.name = name;
            sphere.GetComponent<SphereCollider>().enabled = false;
            return sphere;
        }


        public static bool IsEqual(float a, float b)
        {
            if (a >= b - Mathf.Epsilon && a <= b + Mathf.Epsilon)
                return true;
            else
                return false;
        }

        public static bool SameSign(float num1, float num2)
        {
            //Debug.Log(num1+" , "+num2);

            if (num1 >= 0 && num2 < 0)
            {
                //Debug.Log("Different");
                //Different Sign
                return false;
            }
            else if (num1 < 0 && num2 >= 0)
            {
                //Debug.Log("Different");
                //Different Sign
                return false;
            }
            else
            {
                //Debug.Log("Same Sign");
                //Same Sign
                return true;
            }
        }


        //Compares if the vectors are the same or so close they might as well be.
        public static bool SameVector3s(Vector3 v1, Vector3 v2)
        {
            //X Values
            float xA = Mathf.Round(v1.x * 10f) / 10f;
            float xB = Mathf.Round(v2.x * 10f) / 10f;
            //Y Values
            float yA = Mathf.Round(v1.y * 10f) / 10f;
            float yB = Mathf.Round(v2.y * 10f) / 10f;
            //Z Values
            float zA = Mathf.Round(v1.z * 10f) / 10f;
            float zB = Mathf.Round(v2.z * 10f) / 10f;
            //Debug.Log ("Same Vector3s A: x "+xA+" y "+yA+" z "+zA+" B: x "+xB+" y "+yB+" z "+zB);
            float diff = Mathf.Abs(xA - xB);
            //Debug.Log ("Diff: "+diff);
            if (diff <= 1)
            {
                diff = Mathf.Abs(yA - yB);
                //Same X Value
                if (diff <= 1)
                {
                    diff = Mathf.Abs(zA - zB);
                    //Same Y Value
                    if (diff <= 1)
                    {
                        //Same Z Value
                        //They have the same X,Y,Z axis floats they are the Same Vector3
                        //Debug.Log("Same Vectors");
                        return true;
                    }
                    else
                    {
                        //Debug.Log("Different Z Values");
                        //Not the Same Z axis they are different
                        return false;
                    }
                }
                else
                {
                    //Debug.Log("Different Y Values");
                    //Not the Same Y Axis they are Different
                    return false;
                }
            }
            //Debug.Log("Different X Values");
            //Not the Same X Axis they are Different
            return false;
        }

        static bool BoundsCollide(Bounds a, Bounds b)
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

        //Used for Custom Polygon Insetting
        //Returns a LAV from Vector3 List.
        public static List<CiDyNode> CreateLav(List<CiDyVector3> origPoly, bool flattenY)
        {
            //We wish to Retain Orignal Data so we must clone the orig nodes and create a list that can be manipulated independent of original data.
            List<CiDyNode> newSlav = new List<CiDyNode>(0);
            //Turn Vector3 List into Nodes.
            for (int i = 0; i < origPoly.Count; i++)
            {
                CiDyNode newNode = null;
                if (!flattenY)
                {
                    //newNode = new CiDyNode("V"+i, new Vector3(origPoly[i].pos.x,origPoly[i].pos.y,origPoly[i].pos.z), i);
                    newNode = ScriptableObject.CreateInstance<CiDyNode>().Init("V" + i, new Vector3(origPoly[i].pos.x, origPoly[i].pos.y, origPoly[i].pos.z), i);
                }
                else
                {
                    //newNode = new CiDyNode("V"+i, new Vector3(origPoly[i].pos.x,0,origPoly[i].pos.z), i);
                    newNode = ScriptableObject.CreateInstance<CiDyNode>().Init("V" + i, new Vector3(origPoly[i].pos.x, 0, origPoly[i].pos.z), i);
                }
                //CiDyNode newNode = new CiDyNode("V"+i,origPoly[i].pos,i);
                if (origPoly[i].isCorner)
                {
                    newNode.isCorner = true;
                }
                if (origPoly[i].isCuldesac)
                {
                    newNode.isCuldesac = true;
                }
                newSlav.Add(newNode);
            }

            //First we need to create the bisectors for the slav nodes.
            for (int i = 0; i < newSlav.Count; i++)
            {
                //Grab our predecessor and successors for angle bisector calculations.
                CiDyNode n0;//Predecessor
                CiDyNode n1 = newSlav[i];//This node
                CiDyNode n2;//Successor
                if (i == newSlav.Count - 1)
                {
                    //We are looking at the last point.
                    //n2 = slav[0]
                    n0 = newSlav[i - 1];
                    n2 = newSlav[0];
                }
                else if (i == 0)
                {
                    //We are looking at the first point.
                    //n0 = slav[slav.Count-1]
                    n0 = newSlav[newSlav.Count - 1];
                    n2 = newSlav[i + 1];
                }
                else
                {
                    //We are looking in the middle of the list.
                    n0 = newSlav[i - 1];
                    n2 = newSlav[i + 1];
                }
                //Set Pred/Succ Nodes
                n1.predNode = n0;
                n1.succNode = n2;
                n1.edgeDir = (n1.succNode.position - n1.position).normalized;

                //Set Edge Pointers
                n1.eA = new CiDyEdge(n0, n1);
                n1.eB = new CiDyEdge(n2, n1);
                //Set Bisector and Reflex State
                Vector3 bisector = AngleBisector(n0.position, n1.position, n2.position);
                bisector.y = 0;
                //Vector3 bisector = AngleBisector3(n1.eA,n1.eB);
                //Determine if node is Reflex in polygon shape or not.
                float sign = AngleDir(n1.edgeDir, bisector, Vector3.up);
                if (sign == 1f)
                {
                    //Right Side
                    //This is a Reflex Node flip the Bisector Values.
                    //Store bisector direction in the Node n1
                    n1.bisector = -bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                    n1.reflex = true;
                    //Debug.Log(n1.gameObject.name+" are Reflex");
                }
                else if (sign == -1f)
                {
                    //Proper Direction
                    //Store bisector direction in the Node n1
                    n1.bisector = bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                }
                else
                {
                    //Place a point equal distance in the heading direction by 1.
                    //This is conicident and needs to be set to the perpendicular Left manually by using the Forward and up directions
                    n1.bisector = Vector3.Cross(n1.edgeDir, Vector3.up).normalized;
                }
            }
            //Return cloned Array
            return newSlav;
        }

        //Used for Custom Polygon Insetting
        //Returns a LAV from Vector3 List.
        public static List<CiDyNode> CreateLav(List<Vector3> origPoly)
        {
            //We wish to Retain Orignal Data so we must clone the orig nodes and create a list that can be manipulated independent of original data.
            List<CiDyNode> newSlav = new List<CiDyNode>(0);
            //Turn Vector3 List into Nodes.
            for (int i = 0; i < origPoly.Count; i++)
            {
                //CiDyNode newNode = new CiDyNode("V"+i,origPoly[i],i);
                CiDyNode newNode = ScriptableObject.CreateInstance<CiDyNode>().Init("V" + i, origPoly[i], i);
                newSlav.Add(newNode);
            }

            //First we need to create the bisectors for the slav nodes.
            for (int i = 0; i < newSlav.Count; i++)
            {
                //Grab our predecessor and successors for angle bisector calculations.
                CiDyNode n0;//Predecessor
                CiDyNode n1 = newSlav[i];//This node
                CiDyNode n2;//Successor
                if (i == newSlav.Count - 1)
                {
                    //We are looking at the last point.
                    //n2 = slav[0]
                    n0 = newSlav[i - 1];
                    n2 = newSlav[0];
                }
                else if (i == 0)
                {
                    //We are looking at the first point.
                    //n0 = slav[slav.Count-1]
                    n0 = newSlav[newSlav.Count - 1];
                    n2 = newSlav[i + 1];
                }
                else
                {
                    //We are looking in the middle of the list.
                    n0 = newSlav[i - 1];
                    n2 = newSlav[i + 1];
                }
                //Set Pred/Succ Nodes
                n1.predNode = n0;
                n1.succNode = n2;
                n1.edgeDir = (n1.succNode.position - n1.position).normalized;

                //Set Edge Pointers
                n1.eA = new CiDyEdge(n0, n1);
                n1.eB = new CiDyEdge(n2, n1);
                //Set Bisector and Reflex State
                Vector3 bisector = AngleBisector(n0.position, n1.position, n2.position);
                bisector.y = 0;
                //Vector3 bisector = AngleBisector3(n1.eA,n1.eB);
                //Determine if node is Reflex in polygon shape or not.
                float sign = AngleDir(n1.edgeDir, bisector, Vector3.up);
                if (sign == 1f)
                {
                    //Right Side
                    //This is a Reflex Node flip the Bisector Values.
                    //Store bisector direction in the Node n1
                    n1.bisector = -bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                    n1.reflex = true;
                    //Debug.Log(n1.gameObject.name+" are Reflex");
                }
                else if (sign == -1f)
                {
                    //Proper Direction
                    //Store bisector direction in the Node n1
                    n1.bisector = bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                }
                else
                {
                    //Place a point equal distance in the heading direction by 1.
                    //This is conicident and needs to be set to the perpendicular Left manually by using the Forward and up directions
                    n1.bisector = Vector3.Cross(n1.edgeDir, Vector3.up).normalized;
                }
            }
            //Return cloned Array
            return newSlav;
        }

        //Custom Striaght Skeleton Functions
        //Returns a Cloned Array of the Original Array with needed SLAV DATA
        static List<CiDyNode> CreateLav(List<CiDyNode> origLav, bool flattenY)
        {
            //We wish to Retain Orignal Data so we must clone the orig nodes and create a list that can be manipulated independent of original data.
            List<CiDyNode> newSlav = new List<CiDyNode>(0);
            //Clone OrigLAV
            for (int i = 0; i < origLav.Count; i++)
            {
                //Clone original Node to newSlav List.
                CiDyNode origNode = origLav[i];
                Vector3 newPos;
                if (!flattenY)
                {
                    newPos = new Vector3(origNode.position.x, origNode.position.y, origNode.position.z);
                }
                else
                {
                    newPos = new Vector3(origNode.position.x, 0, origNode.position.z);
                }
                //CiDyNode cloneNode = new CiDyNode(origNode.name,origNode.position,origNode.nodeNumber);
                CiDyNode cloneNode = ScriptableObject.CreateInstance<CiDyNode>().Init(origNode.name, newPos, origNode.nodeNumber);
                cloneNode.origNode = origNode;
                newSlav.Add(cloneNode);
            }

            //First we need to create the bisectors for the slav nodes.
            for (int i = 0; i < newSlav.Count; i++)
            {
                //Grab our predecessor and successors for angle bisector calculations.
                CiDyNode n0;//Predecessor
                CiDyNode n1 = newSlav[i];//This node
                CiDyNode n2;//Successor
                if (i == newSlav.Count - 1)
                {
                    //We are looking at the last point.
                    //n2 = slav[0]
                    n0 = newSlav[i - 1];
                    n2 = newSlav[0];
                }
                else if (i == 0)
                {
                    //We are looking at the first point.
                    //n0 = slav[slav.Count-1]
                    n0 = newSlav[newSlav.Count - 1];
                    n2 = newSlav[i + 1];
                }
                else
                {
                    //We are looking in the middle of the list.
                    n0 = newSlav[i - 1];
                    n2 = newSlav[i + 1];
                }
                //Set Pred/Succ Nodes
                n1.predNode = n0;
                n1.succNode = n2;
                n1.edgeDir = (n1.succNode.position - n1.position).normalized;
                n1.origNode = origLav[i];
                //Set Edge Pointers
                n1.eA = new CiDyEdge(n0, n1);
                n1.eB = new CiDyEdge(n2, n1);
                //Set Bisector and Reflex State
                Vector3 bisector = AngleBisector(n0.position, n1.position, n2.position);
                bisector.y = 0;
                //Vector3 bisector = AngleBisector3(n1.eA,n1.eB);
                //Determine if node is Reflex in polygon shape or not.
                float sign = AngleDir(n1.edgeDir, bisector, Vector3.up);
                if (sign == 1f)
                {
                    //Right Side
                    //This is a Reflext Node flip the Bisector Values.
                    //Store bisector direction in the Node n1
                    n1.bisector = -bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                    n1.reflex = true;
                    //Debug.Log(n1.gameObject.name+" are Reflex");
                }
                else if (sign == -1f)
                {
                    //Proper Direction
                    //Store bisector direction in the Node n1
                    n1.bisector = bisector.normalized;//Bisector in normalized Directional form.(Ray Dir)
                }
                else
                {
                    //Place a point equal distance in the heading direction by 1.
                    //This is conicident and needs to be set to the perpendicular Left manually by using the Forward and up directions
                    n1.bisector = Vector3.Cross(n1.edgeDir, Vector3.up).normalized;
                }
            }
            //Return cloned Array
            return newSlav;
        }

        //Create Straight Skeleton from Polygon
        // Use this for initialization
        public static StraightSkeletonNet.Skeleton CreateStraightSkeleton(List<CiDyVector3> polygon)
        {
            //Debug.Log("Create Straight Skeleton");
            //Initalize List for creating an inset polygon from a Straight Skeleton.
            //List<List<CiDyNode>> insets = new List<List<CiDyNode>>(0);
            List<Vector3> positions = new List<Vector3>(0);
            List<Vector2d> vectors = new List<Vector2d>(0);
            //To Get a Proper Set of Inset Polygons. We will need to first Calculate the Straight Skeleton of the Orignal Polygon
            if (polygon.Count > 0)
            {
                //Iterate through User Points
                for (int i = 0; i < polygon.Count; i++)
                {
                    positions.Add(polygon[i].pos);
                    //Create Stored Version
                    Vector2d vector = new Vector2d(polygon[i].pos.x, polygon[i].pos.z, polygon[i].isCorner, polygon[i].isCuldesac);
                    vectors.Add(vector);
                }
            }
            //Create Skeleton
            StraightSkeletonNet.Skeleton CiDySkeleton = StraightSkeletonNet.SkeletonBuilder.Build(vectors, null, 0);
            //Return Final Polygon
            return CiDySkeleton;
        }

        //Create Inset Polygon from Straight Skeleton
        public static List<List<CiDyNode>> CreateSkeletonInset(List<CiDyVector3> polygon, float inset)
        {
            //Debug.Log("Create StraightSkeletonInset");
            //Initalize List for creating an inset polygon from a Straight Skeleton.
            List<List<CiDyNode>> insets = new List<List<CiDyNode>>(0);
            List<Vector3> positions = new List<Vector3>(0);
            List<Vector2d> vectors = new List<Vector2d>(0);
            //To Get a Proper Set of Inset Polygons. We will need to first Calculate the Straight Skeleton of the Orignal Polygon
            if (polygon.Count > 0)
            {
                //Iterate through User Points
                for (int i = 0; i < polygon.Count; i++)
                {
                    positions.Add(polygon[i].pos);
                    //Create Stored Version
                    Vector2d vector = new Vector2d(polygon[i].pos.x, polygon[i].pos.z, polygon[i].isCorner, polygon[i].isCuldesac, polygon[i].position);
                    vectors.Add(vector);
                }
            }
            else
            {
                Debug.LogError("There is no polygon to Create a Skeleton Inset From!!!!");
            }
            //Create Skeleton
            StraightSkeletonNet.Skeleton CiDySkeleton = StraightSkeletonNet.SkeletonBuilder.Build(vectors, null, (double)inset);
            Dictionary<Vector2d, double> distance = CiDySkeleton.Distances;
            //Now that we have the Straight Skeleton. Lets extract the Inset Polygon.
            //Store Reference
            List<CheckedLine> lines = new List<CheckedLine>(0);
            //Calculate Poly from Straight Skeleton
            if (CiDySkeleton != null && CiDySkeleton.Edges.Count > 0)
            {
                Vector3 a = new Vector3(positions[0].x, positions[0].y + inset, positions[0].z);
                Vector3 b = new Vector3(positions[positions.Count - 1].x, positions[positions.Count - 1].y + inset, positions[positions.Count - 1].z);
                Vector3 c = new Vector3(positions[positions.Count / 2].x, positions[positions.Count / 2].y + inset, positions[positions.Count / 2].z);
                //Create A Plane and Test for intersections.
                Plane m_Plane;
                m_Plane = new Plane(a, b, c);
                List<InsetPoint> pointArray = new List<InsetPoint>(0);
                List<CiDyNode> newPoly = new List<CiDyNode>(0);
                //Iterate through Edges
                for (int i = 0; i < CiDySkeleton.Edges.Count; i++)
                {
                    //Iterate through Polygon of this Edge
                    for (int j = 0; j < CiDySkeleton.Edges[i].Polygon.Count; j++)
                    {
                        //Draw Polygon for Each Edge
                        Vector2d posA = CiDySkeleton.Edges[i].Polygon[j];
                        Vector2d posB;
                        if (j == CiDySkeleton.Edges[i].Polygon.Count - 1)
                        {
                            //Grab First
                            posB = CiDySkeleton.Edges[i].Polygon[0];
                        }
                        else
                        {
                            //Grab Next
                            posB = CiDySkeleton.Edges[i].Polygon[j + 1];
                        }
                        //Get Heights of Points
                        double heightA = 0;
                        distance.TryGetValue(posA, out heightA);
                        double heightB = 0;
                        distance.TryGetValue(posB, out heightB);
                        //This is an Outer Edge. So we will not Test along it.
                        if (heightA == 0 && heightB == 0)
                        {
                            continue;
                        }
                        //Reference Projected Point.
                        Vector3 pos1 = new Vector3((float)posA.X, (float)heightA, (float)posA.Y);//Height A
                        Vector3 pos2 = new Vector3((float)posB.X, (float)heightB, (float)posB.Y);//Height B
                                                                                                 //Create Line
                        CheckedLine newLine = new CheckedLine(pos1, pos2);
                        bool duplicate = false;
                        //Check if this Line has been Tested already?
                        if (lines.Count > 0)
                        {
                            for (int h = 0; h < lines.Count; h++)
                            {
                                if (newLine.vectorA == lines[h].vectorA || newLine.vectorA == lines[h].vectorB)
                                {
                                    if (newLine.vectorB == lines[h].vectorB || newLine.vectorB == lines[h].vectorA)
                                    {
                                        //This is a Duplicate do not test.
                                        duplicate = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (duplicate)
                        {
                            continue;
                        }
                        else
                        {
                            //Not a duplicate. So we will add it to the Tested Lines List.
                            lines.Add(newLine);
                        }
                        //Run a plane Raycast test to see where the line intersects along the Skeleton Edge.
                        Vector3 dir;
                        Ray ray;
                        if (heightA < heightB)
                        {
                            //Height B is High One
                            if (heightB < positions[0].y + inset)
                            {
                                continue;
                            }
                            //Direction is B-A
                            dir = pos2 - pos1;
                            ray = new Ray(pos1, dir);
                        }
                        else
                        {
                            //Height A is High One
                            if (heightA < positions[0].y + inset)
                            {
                                continue;
                            }
                            //direction is A-B
                            dir = pos1 - pos2;
                            ray = new Ray(pos2, dir);
                        }
                        //Create Raycast and Test Where plane Hits
                        float enter = 0.0f;

                        if (m_Plane.Raycast(ray, out enter))
                        {
                            Vector3 hitPoint = ray.GetPoint(enter);
                            if (CiDySkeleton.insetLavs.Count == 0)
                            {
                                //Store for Polygon Display when its only a single polygon outline still.
                                CiDyNode newNode = ScriptableObject.CreateInstance<CiDyNode>().Init(i + j.ToString(), new Vector3(hitPoint.x, 0, hitPoint.z), i + j);
                                if (heightA < heightB)
                                {
                                    //PosA is Original
                                    if (posA.isCorner || posB.isCorner)
                                    {
                                        newNode.isCorner = posA.isCorner;
                                        newNode.isCuldesac = posA.isCuldesac;
                                        newNode.polyPos = posA.position;
                                    }
                                    /*if (posB.isCorner)
                                    {
                                        newNode.isCorner = posB.isCorner;
                                        newNode.isCuldesac = posB.isCuldesac;
                                    }*/
                                }
                                else
                                {
                                    //PosB is Original
                                    /*newNode.isCorner = posB.isCorner;
                                    newNode.isCuldesac = posB.isCuldesac;*/
                                    /*if (posA.isCorner)
                                    {
                                        newNode.isCorner = posA.isCorner;
                                        newNode.isCuldesac = posA.isCuldesac;
                                    }*/
                                    if (posB.isCorner)
                                    {
                                        newNode.isCorner = posB.isCorner;
                                        newNode.isCuldesac = posB.isCuldesac;
                                        newNode.polyPos = posB.position;
                                    }
                                }
                                //Add To Poly List
                                newPoly.Add(newNode);
                            }
                            else
                            {
                                //
                                if (heightA < heightB)
                                {
                                    pointArray.Add(new InsetPoint(hitPoint, posA, posB));
                                }
                                else
                                {
                                    pointArray.Add(new InsetPoint(hitPoint, posB, posA));
                                }
                            }
                        }
                        //Debug.DrawLine(new Vector3((float)posA.X, (float)heightA, (float)posA.Y), new Vector3((float)posB.X, (float)heightB, (float)posB.Y), Color.yellow);
                    }
                }

                if (CiDySkeleton.insetLavs.Count == 0)
                {
                    //Debug.Log("There is only One Continous");
                    //There is Only One Continous Poly List.
                    insets = new List<List<CiDyNode>>(0);
                    insets.Add(newPoly);
                }
                else
                {
                    //Debug.Log("There are several Poly Lists we will need to generate");
                    //There are several Poly lists we will need to generate
                    insets = new List<List<CiDyNode>>(0);
                    //Iterate through SLAV Chains as they will tell us what Points should be connecting into a Line.
                    for (int i = 0; i < CiDySkeleton.insetLavs.Count; i++)
                    {
                        //Iterate through this Chain and Create its Polygon Inset Connections.
                        newPoly = new List<CiDyNode>(0);
                        for (int j = 0; j < CiDySkeleton.insetLavs[i].Count; j++)
                        {
                            //Store Reference to this Slavs current Point.
                            Vector2d chainPos1 = CiDySkeleton.insetLavs[i][j];
                            //Now that we have the Desired Origin Points. Lets Match to stored PointArray Origins and Check all potentials for a Match.
                            //Iterate through Point Array and Find Match Potential
                            for (int h = 0; h < pointArray.Count; h++)
                            {
                                Vector2d origin = pointArray[h].origin;
                                //Debug.Log("Origin: " + origin + " Chain: " + new Vector2((float)chainPos1.X, (float)chainPos1.Y));
                                if (origin == chainPos1)
                                {
                                    //Test End Point if inside Polygon
                                    //This is a Potential. Lets Test it further.
                                    if (CiDyUtils.PointInsidePolygon(CiDySkeleton.insetLavs[i], new Vector3((float)pointArray[h].endPoint.X, 0, (float)pointArray[h].endPoint.Y)))
                                    {
                                        //This is the Right Point. Add to Polygon
                                        CiDyNode newNode = ScriptableObject.CreateInstance<CiDyNode>().Init(h.ToString(), new Vector3(pointArray[h].point.x, 0, pointArray[h].point.z), h);
                                        if (pointArray[h].origin.isCorner || chainPos1.isCorner)
                                        {
                                            if (pointArray[h].origin.isCorner)
                                            {
                                                newNode.isCorner = pointArray[h].origin.isCorner;
                                                newNode.isCuldesac = pointArray[h].origin.isCuldesac;
                                                newNode.polyPos = pointArray[h].origin.position;
                                            }
                                            else
                                            {
                                                newNode.isCorner = chainPos1.isCorner;
                                                newNode.isCuldesac = chainPos1.isCuldesac;
                                                newNode.polyPos = chainPos1.position;
                                            }

                                            /*Vector3 originPoint = new Vector3((float)pointArray[h].origin.X, 0, (float)pointArray[h].origin.Y);
                                            GameObject tNode = CiDyUtils.MarkPoint(originPoint, h);
                                            tNode.name = "This is Corner Origin: " + h;
                                            //Set color Red for Corners
                                            tNode.GetComponent<MeshRenderer>().material.color = Color.red;*/
                                        }
                                        //Add to List
                                        newPoly.Add(newNode);
                                    }
                                }
                            }
                        }
                        //If we have a Poly created. Add it to the Insets
                        if (newPoly.Count > 0)
                        {
                            insets.Add(newPoly);
                        }
                    }
                }
            }
            //Return Final Polygon
            return insets;
        }

        //This Function Will Find the InsetPoly for the Input Poly
        public static List<List<CiDyNode>> InsetPolygon(List<CiDyVector3> origPoly, float inset)
        {
            eventCount = 0;//Reset Event Count as this count is per poly.
            List<List<CiDyNode>> finalLAVs = new List<List<CiDyNode>>(0);
            //Turn OrigPoly into LAV
            List<List<CiDyNode>> LAVQueue = new List<List<CiDyNode>>(0);
            //Translate into a LAV for Insetting.
            List<CiDyNode> LAV = CreateLav(origPoly, true);
            //Add Initial LAV to LAVQueue for testing.
            LAVQueue.Add(LAV);
            //Event Queue
            List<CiDyNode> eventQueue = new List<CiDyNode>(0);
            //Initilize Queue
            //Now that we have a LAV. Determine what its nxtEvents are.
            //eventQueue = FindNextEvents(LAVQueue[0]);
            while (LAVQueue.Count > 0)
            {
                //Debug.Log("Set LAV");
                LAV = LAVQueue[0];
                LAVQueue.RemoveAt(0);
                //Now that we have a LAV. Determine what its nxtEvents are.
                eventQueue = FindNextEvents(LAV);
                CiDyNode nxtEvent = null;
                if (eventQueue.Count > 0)
                {
                    //Grab first Event.
                    nxtEvent = eventQueue[0];
                    eventQueue.RemoveAt(0);
                    //Debug.Log("Grabbed Event: "+nxtEvent.name+" Is Reflex? "+nxtEvent.reflex);
                    //Make sure its at or above the desired inset plane.
                    if (nxtEvent.distToP > inset)
                    {
                        //Debug.Log("Above Inset plane.");
                        finalLAVs.Add(LAV);
                        continue;
                    }
                    if (nxtEvent.predNode.marked && nxtEvent.succNode.marked)
                    {
                        Debug.Log("Duplicate");
                        continue;
                    }
                    //Process Event based on Type(Edge/Split)
                    if (!nxtEvent.reflex)
                    {
                        if (nxtEvent.predNode.predNode.name == nxtEvent.succNode.succNode.name)
                        {
                            //Debug.Log("Peak Event");
                            LAV.Clear();
                        }
                        else
                        {
                            //Debug.Log("Edge Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                            //Check for Peak Event
                            //Debug.Log("Collapse Edge");
                            //Collapse Edge PredNode-SuccNode.
                            //Edge Event So we need to remove the nodes that created this event.(collapsed)
                            int insertPoint = LAV.FindIndex(x => x.name == nxtEvent.predNode.name);
                            //Nodes that need to Be Removed
                            CiDyNode oldNodeA = nxtEvent.predNode;
                            CiDyNode oldNodeB = nxtEvent.succNode;
                            //Before Updateing List. Lets update the Event Node with its new Nodes.
                            nxtEvent.predNode = oldNodeA.predNode;
                            nxtEvent.succNode = oldNodeB.succNode;
                            //Update Edges
                            nxtEvent.eA = oldNodeA.eA;
                            nxtEvent.eB = oldNodeB.eB;
                            //Update newly Connected Nodes.
                            nxtEvent.predNode.succNode = nxtEvent;
                            nxtEvent.succNode.predNode = nxtEvent;
                            //Recalculate new Bisector
                            nxtEvent.bisector = AngleBisector(nxtEvent.eA.pos1, nxtEvent.eA.pos2, nxtEvent.eB.pos1, nxtEvent.eB.pos2);
                            nxtEvent.bisector.y = 0;
                            //Remove Collapsed Edge and Update Poly
                            LAV.Insert(insertPoint, nxtEvent);
                            LAV.Remove(oldNodeA);
                            LAV.Remove(oldNodeB);
                            //Mark Nodes
                            oldNodeA.marked = true;
                            oldNodeB.marked = true;
                            //Did this LAV collapse?
                            if (LAV.Count > 2)
                            {
                                //LAV is Not Done. Add For Next Events.
                                LAVQueue.Add(LAV);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("Split Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                        List<List<CiDyNode>> splitPoly = SplitLav(LAV, nxtEvent.predNode.name, nxtEvent.succNode.name, nxtEvent);
                        if (splitPoly[0].Count > 2)
                        {
                            //Add New Polys to Queue to be tested further.
                            LAVQueue.Add(splitPoly[0]);
                        } /*else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[0]);
					}*/
                        if (splitPoly[1].Count > 2)
                        {
                            LAVQueue.Add(splitPoly[1]);
                        }/* else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[1]);
					}*/
                    }
                }
            }
            //Debug.Log("Finished");
            //Move FinalLavs to desired Inset
            //We want to move the Final LAVs into position.
            for (int i = 0; i < finalLAVs.Count; i++)
            {
                UpdateVertexPos(finalLAVs[i], inset);
            }
            return finalLAVs;
        }

        //This Function Will Find the Straight Skeleton for the Input Poly
        public static List<List<CiDyNode>> InsetPolygon(List<Vector3> origPoly, float inset)
        {
            eventCount = 0;//Reset Event Count as this count is per poly.
            List<List<CiDyNode>> finalLAVs = new List<List<CiDyNode>>(0);
            //Turn OrigPoly into LAV
            List<List<CiDyNode>> LAVQueue = new List<List<CiDyNode>>(0);
            //Translate into a LAV for Insetting.
            List<CiDyNode> LAV = CreateLav(origPoly);
            //Add Initial LAV to LAVQueue for testing.
            LAVQueue.Add(LAV);
            //Event Queue
            List<CiDyNode> eventQueue = new List<CiDyNode>(0);
            //Initilize Queue
            //Now that we have a LAV. Determine what its nxtEvents are.
            //eventQueue = FindNextEvents(LAVQueue[0]);
            while (LAVQueue.Count > 0)
            {
                //Debug.Log("Set LAV");
                LAV = LAVQueue[0];
                LAVQueue.RemoveAt(0);
                //Now that we have a LAV. Determine what its nxtEvents are.
                eventQueue = FindNextEvents(LAV);
                CiDyNode nxtEvent = null;
                if (eventQueue.Count > 0)
                {
                    //Grab first Event.
                    nxtEvent = eventQueue[0];
                    eventQueue.RemoveAt(0);
                    //Debug.Log("Grabbed Event: "+nxtEvent.name+" Is Reflex? "+nxtEvent.reflex);
                    //Make sure its at or above the desired inset plane.
                    if (nxtEvent.distToP > inset)
                    {
                        //Debug.Log("Above Inset plane.");
                        finalLAVs.Add(LAV);
                        continue;
                    }
                    if (nxtEvent.predNode.marked && nxtEvent.succNode.marked)
                    {
                        Debug.Log("Duplicate");
                        continue;
                    }
                    //Process Event based on Type(Edge/Split)
                    if (!nxtEvent.reflex)
                    {
                        if (nxtEvent.predNode.predNode.name == nxtEvent.succNode.succNode.name)
                        {
                            //Debug.Log("Peak Event");
                            LAV.Clear();
                        }
                        else
                        {
                            //Debug.Log("Edge Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                            //Check for Peak Event
                            //Debug.Log("Collapse Edge");
                            //Collapse Edge PredNode-SuccNode.
                            //Edge Event So we need to remove the nodes that created this event.(collapsed)
                            int insertPoint = LAV.FindIndex(x => x.name == nxtEvent.predNode.name);
                            //Nodes that need to Be Removed
                            CiDyNode oldNodeA = nxtEvent.predNode;
                            CiDyNode oldNodeB = nxtEvent.succNode;
                            //Before Updateing List. Lets update the Event Node with its new Nodes.
                            nxtEvent.predNode = oldNodeA.predNode;
                            nxtEvent.succNode = oldNodeB.succNode;
                            //Update Edges
                            nxtEvent.eA = oldNodeA.eA;
                            nxtEvent.eB = oldNodeB.eB;
                            //Update newly Connected Nodes.
                            nxtEvent.predNode.succNode = nxtEvent;
                            nxtEvent.succNode.predNode = nxtEvent;
                            //Recalculate new Bisector
                            nxtEvent.bisector = AngleBisector(nxtEvent.eA.pos1, nxtEvent.eA.pos2, nxtEvent.eB.pos1, nxtEvent.eB.pos2);
                            nxtEvent.bisector.y = 0;
                            //Remove Collapsed Edge and Update Poly
                            LAV.Insert(insertPoint, nxtEvent);
                            LAV.Remove(oldNodeA);
                            LAV.Remove(oldNodeB);
                            //Mark Nodes
                            oldNodeA.marked = true;
                            oldNodeB.marked = true;
                            //Did this LAV collapse?
                            if (LAV.Count > 2)
                            {
                                //LAV is Not Done. Add For Next Events.
                                LAVQueue.Add(LAV);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("Split Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                        List<List<CiDyNode>> splitPoly = SplitLav(LAV, nxtEvent.predNode.name, nxtEvent.succNode.name, nxtEvent);
                        if (splitPoly[0].Count > 2)
                        {
                            //Add New Polys to Queue to be tested further.
                            LAVQueue.Add(splitPoly[0]);
                        } /*else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[0]);
					}*/
                        if (splitPoly[1].Count > 2)
                        {
                            LAVQueue.Add(splitPoly[1]);
                        }/* else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[1]);
					}*/
                    }
                }
            }
            //Debug.Log("Finished");
            //Move FinalLavs to desired Inset
            //We want to move the Final LAVs into position.
            for (int i = 0; i < finalLAVs.Count; i++)
            {
                UpdateVertexPos(finalLAVs[i], inset);
            }
            return finalLAVs;
        }

        //This Function Will Find the InsetPoly for the Input Poly
        public static List<List<CiDyNode>> InsetPolygon(List<CiDyNode> origPoly, float inset)
        {
            eventCount = 0;//Reset Event Count as this count is per poly.
            List<List<CiDyNode>> finalLAVs = new List<List<CiDyNode>>(0);
            //Turn OrigPoly into LAV
            List<List<CiDyNode>> LAVQueue = new List<List<CiDyNode>>(0);
            //Translate into a LAV for Insetting.
            List<CiDyNode> LAV = CreateLav(origPoly, true);
            //Add Initial LAV to LAVQueue for testing.
            LAVQueue.Add(LAV);
            //Event Queue
            List<CiDyNode> eventQueue = new List<CiDyNode>(0);
            //Initilize Queue
            //Now that we have a LAV. Determine what its nxtEvents are.
            //eventQueue = FindNextEvents(LAVQueue[0]);
            while (LAVQueue.Count > 0)
            {
                //Debug.Log("Set LAV");
                LAV = LAVQueue[0];
                LAVQueue.RemoveAt(0);
                //Now that we have a LAV. Determine what its nxtEvents are.
                eventQueue = FindNextEvents(LAV);
                CiDyNode nxtEvent = null;
                if (eventQueue.Count > 0)
                {
                    //Grab first Event.
                    nxtEvent = eventQueue[0];
                    eventQueue.RemoveAt(0);
                    //Debug.Log("Grabbed Event: "+nxtEvent.name+" Is Reflex? "+nxtEvent.reflex);
                    //Make sure its at or above the desired inset plane.
                    if (nxtEvent.distToP > inset)
                    {
                        //Debug.Log("Above Inset plane.");
                        finalLAVs.Add(LAV);
                        continue;
                    }
                    if (nxtEvent.predNode.marked && nxtEvent.succNode.marked)
                    {
                        Debug.Log("Duplicate");
                        continue;
                    }
                    //Process Event based on Type(Edge/Split)
                    if (!nxtEvent.reflex)
                    {
                        if (nxtEvent.predNode.predNode.name == nxtEvent.succNode.succNode.name)
                        {
                            //Debug.Log("Peak Event");
                            LAV.Clear();
                        }
                        else
                        {
                            //Debug.Log("Edge Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                            //Check for Peak Event
                            //Debug.Log("Collapse Edge");
                            //Collapse Edge PredNode-SuccNode.
                            //Edge Event So we need to remove the nodes that created this event.(collapsed)
                            int insertPoint = LAV.FindIndex(x => x.name == nxtEvent.predNode.name);
                            //Nodes that need to Be Removed
                            CiDyNode oldNodeA = nxtEvent.predNode;
                            CiDyNode oldNodeB = nxtEvent.succNode;
                            //Before Updateing List. Lets update the Event Node with its new Nodes.
                            nxtEvent.predNode = oldNodeA.predNode;
                            nxtEvent.succNode = oldNodeB.succNode;
                            //Update Edges
                            nxtEvent.eA = oldNodeA.eA;
                            nxtEvent.eB = oldNodeB.eB;
                            //Update newly Connected Nodes.
                            nxtEvent.predNode.succNode = nxtEvent;
                            nxtEvent.succNode.predNode = nxtEvent;
                            //Recalculate new Bisector
                            nxtEvent.bisector = AngleBisector(nxtEvent.eA.pos1, nxtEvent.eA.pos2, nxtEvent.eB.pos1, nxtEvent.eB.pos2);
                            nxtEvent.bisector.y = 0;
                            //Remove Collapsed Edge and Update Poly
                            LAV.Insert(insertPoint, nxtEvent);
                            LAV.Remove(oldNodeA);
                            LAV.Remove(oldNodeB);
                            //Mark Nodes
                            oldNodeA.marked = true;
                            oldNodeB.marked = true;
                            //Did this LAV collapse?
                            if (LAV.Count > 2)
                            {
                                //LAV is Not Done. Add For Next Events.
                                LAVQueue.Add(LAV);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("Split Event "+nxtEvent.predNode.name+"-"+nxtEvent.succNode.name);
                        List<List<CiDyNode>> splitPoly = SplitLav(LAV, nxtEvent.predNode.name, nxtEvent.succNode.name, nxtEvent);
                        if (splitPoly[0].Count > 2)
                        {
                            //Add New Polys to Queue to be tested further.
                            LAVQueue.Add(splitPoly[0]);
                        } /*else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[0]);
					}*/
                        if (splitPoly[1].Count > 2)
                        {
                            LAVQueue.Add(splitPoly[1]);
                        }/* else {
						Debug.Log("Split Poly has Collapsed to Less than 3rd degree");
						//finalSlav.Add(splitPoly[1]);
					}*/
                    }
                }
            }
            //Debug.Log("Finished");
            //Move FinalLavs to desired Inset
            //We want to move the Final LAVs into position.
            for (int i = 0; i < finalLAVs.Count; i++)
            {
                UpdateVertexPos(finalLAVs[i], inset);
            }
            return finalLAVs;
        }

        //This function will take one SLAV and Split it into two at the Desired Split point. :)
        static List<List<CiDyNode>> SplitLav(List<CiDyNode> origLav, string splitNodeA, string splitNodeB, CiDyNode splitNode)
        {
            List<List<CiDyNode>> splitLav = new List<List<CiDyNode>>(0);
            CiDyNode dupNode = null;
            //CiDyNode dupNode2 = null;
            int splitPlace = 0;
            //To split the Edge first we want to iterate through the list and find the Splitting Edge Nodes in its current Sequence
            for (int i = 0; i < origLav.Count; i++)
            {
                CiDyNode nodeA = origLav[i];
                CiDyNode nodeB;
                if (i != origLav.Count - 1)
                {
                    nodeB = origLav[i + 1];
                }
                else
                {
                    nodeB = origLav[0];
                }
                if (nodeA.name == splitNodeA && nodeB.name == splitNodeB)
                {
                    //We found our Split Edge.
                    //Duplicate split node and seperate the List into Two at this point duplicating the splitNode at its set Position.(Edge Split Point)
                    //dupNode = new CiDyNode("N"+eventCount,splitNode.position,eventCount);
                    dupNode = ScriptableObject.CreateInstance<CiDyNode>().Init("N" + eventCount, splitNode.position, eventCount);
                    eventCount++;
                    //Debug.Log("Created Duplicate/s "+dupNode.name+" & "+dupNode2.name);
                    if (i == origLav.Count - 1)
                    {
                        splitPlace = 0;
                    }
                    else
                    {
                        splitPlace = (i + 1);
                    }
                    break;
                }
            }
            //Create new sub Lists
            List<CiDyNode> subList = new List<CiDyNode>(0);
            //Start the iteration process of the origLav from this point and add all points from this point.
            //Now that we have our split point and duplicateNode lets create a new List. starting at the Split point and ending one node before the OrigNode that caused the Split.
            string origNode = splitNode.origNode.name;
            bool secondList = false;
            //Debug.Log ("Starting Split operation");
            for (int i = splitPlace; i < origLav.Count; i++)
            {
                if (!secondList)
                {
                    //Add until we hit the Orig Node then stop.
                    if (origLav[i].name == origNode)
                    {
                        //We are done.
                        //connect new Pred and Succ Nodes.
                        dupNode.distToP = splitNode.distToP;
                        dupNode.predNode = subList[subList.Count - 1];
                        dupNode.succNode = subList[0];
                        dupNode.eA = dupNode.predNode.eB;
                        dupNode.eB = dupNode.succNode.eA;
                        dupNode.predNode.succNode = dupNode;
                        dupNode.succNode.predNode = dupNode;
                        dupNode.bisector = AngleBisector(dupNode.eA.pos1, dupNode.eA.pos2, dupNode.eB.pos1, dupNode.eB.pos2);
                        dupNode.bisector.y = 0;
                        dupNode.reflex = false;
                        dupNode.isCorner = splitNode.isCorner;
                        dupNode.position.y = splitNode.position.y;
                        dupNode.polyPos = splitNode.polyPos;
                        //Add duplicate Node instead
                        subList.Add(dupNode);
                        splitLav.Add(subList);
                        subList = new List<CiDyNode>(0);
                        secondList = true;//Now we will start to create the Second List. First node is OrigSplit Node.
                    }
                    else
                    {
                        subList.Add(origLav[i]);
                    }
                }
                else
                {
                    //Add Until we hit just before SplitPlace then stop
                    if (i == splitPlace)
                    {
                        //We are done adding to list.
                        //Update Split Nodes connections
                        splitNode.predNode = subList[subList.Count - 1];
                        splitNode.succNode = subList[0];//subList[subList.Count-2];
                        splitNode.eA = splitNode.predNode.eB;
                        splitNode.eB = splitNode.succNode.eA;
                        splitNode.predNode.succNode = splitNode;
                        splitNode.succNode.predNode = splitNode;
                        splitNode.bisector = AngleBisector(splitNode.eA.pos1, splitNode.eA.pos2, splitNode.eB.pos1, splitNode.eB.pos2);
                        splitNode.bisector.y = 0;
                        splitNode.reflex = false;
                        subList.Add(splitNode);
                        //Add to final List.
                        splitLav.Add(subList);
                        break;
                    }
                    else
                    {
                        subList.Add(origLav[i]);
                    }
                }
                if (i == origLav.Count - 1)
                {
                    //We are at the end of the list and have not completed our sub List start at the begining of list and continue.
                    i = -1;
                }
            }
            //Return Split List.
            return splitLav;
        }
        //Reset at the first call of Insettting.
        static int eventCount = 0;
        //This Function will Find the Next Events for the InputPoly and Return the Chains for those events.
        static List<CiDyNode> FindNextEvents(List<CiDyNode> poly)
        {
            float closestEvent = Mathf.Infinity;//We are trying to minimize Later Iterations on the FoundEvents List.
            List<CiDyNode> foundEvents = new List<CiDyNode>(0);
            //Intersection Vector
            Vector3 eventIntersection = Vector3.zero;
            //Iterate poly and test every node to its adjacent nodes and sort the returned events by distTo representative Edge.
            for (int i = 0; i < poly.Count; i++)
            {
                CiDyNode curNode = poly[i];
                List<CiDyNode> tempEvents = new List<CiDyNode>(0);
                //Create test lines for intersection.
                Vector3 p0 = curNode.position;
                Vector3 p1 = (p0 + curNode.bisector * 1000);
                //Test This bisector against Prv
                CiDyNode prevNode = curNode.predNode;
                //Create test lines for intersection.
                Vector3 p2 = prevNode.position;
                Vector3 p3 = (p2 + prevNode.bisector * 1000);
                //Do these bisectors have an Event?
                if (LineIntersection(p0, p1, p2, p3, ref eventIntersection))
                {
                    //We do so lets update its Distance to Line.
                    //CiDyNode newEvent = new CiDyNode("E"+eventCount,eventIntersection,eventCount);
                    CiDyNode newEvent = ScriptableObject.CreateInstance<CiDyNode>().Init("E" + eventCount, eventIntersection, eventCount);
                    eventCount++;
                    //Calculate dist to edge.Round to 100th
                    //float distToP = CiDyUtils.DistanceToLine(eventIntersection,p0,p2);
                    float distToP = DistanceToLine(eventIntersection, prevNode.eB.pos1, prevNode.eB.pos2);
                    newEvent.distToP = distToP;
                    newEvent.predNode = prevNode;
                    newEvent.succNode = curNode;
                    newEvent.eA = prevNode.eA;
                    newEvent.eB = curNode.eB;
                    newEvent.position.y = curNode.position.y;
                    if (prevNode.isCorner || curNode.isCorner)
                    {
                        newEvent.isCorner = true;
                    }
                    //Add To Temp List for later determining which is closest to CurNode.
                    tempEvents.Add(newEvent);
                    if (distToP < closestEvent)
                    {
                        closestEvent = distToP;
                    }
                }
                //Now Test against Next Bisector
                //test this bisector against next
                CiDyNode nxtNode = curNode.succNode;
                //Create test lines for intersection.
                p2 = nxtNode.position;
                p3 = (p2 + nxtNode.bisector * 1000);
                if (LineIntersection(p0, p1, p2, p3, ref eventIntersection))
                {
                    //We do so lets update its Distance to Line.
                    //CiDyNode newEvent = new CiDyNode("E"+eventCount,eventIntersection,eventCount);
                    CiDyNode newEvent = ScriptableObject.CreateInstance<CiDyNode>().Init("E" + eventCount, eventIntersection, eventCount);
                    eventCount++;
                    //Calculate dist to edge.Round to 100th
                    //float distToP = CiDyUtils.DistanceToLine(eventIntersection,p0,p2);
                    float distToP = DistanceToLine(eventIntersection, nxtNode.eA.pos1, nxtNode.eA.pos2);
                    newEvent.distToP = distToP;
                    newEvent.predNode = curNode;
                    newEvent.succNode = nxtNode;
                    newEvent.eA = curNode.eA;
                    newEvent.eB = nxtNode.eB;
                    newEvent.position.y = curNode.position.y;
                    if (nxtNode.isCorner || curNode.isCorner)
                    {
                        newEvent.isCorner = true;
                    }
                    //Add to Temp.
                    tempEvents.Add(newEvent);
                    if (distToP < closestEvent)
                    {
                        closestEvent = distToP;
                    }
                }
                //Additional Test for Reflex Nodes.
                if (curNode.reflex)
                {
                    //Debug.Log("Testing Reflex "+curNode.name);
                    //This is a Reflex Vertex and Will Require a Test for potential Split Events.
                    for (int j = 0; j < poly.Count; j++)
                    {
                        //Skip the reflex vertex and its adjacents as these do not need to be tested.
                        if (j == i || j == i - 1 || j == i + 1)
                        {
                            continue;
                        }
                        //Now Test Edge Line to bisector for all potential parrallel edges.
                        CiDyNode nodeA = poly[j];
                        CiDyNode nodeB;
                        if (j != poly.Count - 1)
                        {
                            nodeB = poly[j + 1];
                        }
                        else
                        {
                            nodeB = poly[0];
                        }
                        //Simulate an Infinite Line.
                        Vector3 endA = (nodeA.position + (-nodeA.edgeDir * 1000f));
                        Vector3 endB = (nodeB.position + (nodeA.edgeDir * 1000f));
                        //Test this line if its a potential match.
                        if (LineIntersection(p0, p1, endA, endB, ref eventIntersection))
                        {
                            Vector3 storedPoint = eventIntersection;
                            //This is a Potential Match. Is the EventIntersection Within the (endA/endB)Edge Bisector Lines?
                            //We need to Determine the Angle Bisector between the parrallel Line and the Lines coming from prednode/succNode to Vertex expanded to infinite.
                            Vector3 endC = p0 + (curNode.predNode.edgeDir * 1000f);
                            Vector3 newBisector;
                            //Find Intersection Point
                            if (LineIntersection(endA, endB, curNode.position, endC, ref eventIntersection))
                            {
                                //Determin Angle Bisector from V-eventIntersection-storedPoint
                                newBisector = AngleBisector(curNode.position, eventIntersection, storedPoint);
                            }
                            else
                            {
                                endC = p0 + (-curNode.edgeDir * 1000f);
                                //Test the other side for the bisector
                                if (LineIntersection(endA, endB, curNode.position, endC, ref eventIntersection))
                                {
                                    newBisector = AngleBisector(storedPoint, eventIntersection, curNode.position);
                                    newBisector.y = 0;
                                }
                                else
                                {
                                    //We cannot determine a Point so this must not be a split event.
                                    //Debug.Log("Couldn't Find a Valide Bisector Line for Split Testing");
                                    break;
                                }
                            }
                            //Store the Point we created a bisector for.
                            storedPoint = eventIntersection;
                            endC = storedPoint + (newBisector * 1000f);
                            if (LineIntersection(p0, p1, storedPoint, endC, ref eventIntersection))
                            {
                                //Now Find intersection between newBisector
                                //First Bisector
                                Vector3 pointDir = (eventIntersection - nodeA.position);
                                int angleDir = AngleDir(nodeA.bisector, pointDir, Vector3.up);
                                //Debug.Log("First Angle Test "+angleDir);
                                if (angleDir > 0)
                                {
                                    //Passed the First Check now we need to confirm the Other Side.
                                    pointDir = (eventIntersection - nodeB.position);
                                    angleDir = AngleDir(nodeB.bisector, pointDir, Vector3.up);
                                    //Debug.Log("Second Angle Test "+angleDir);
                                    if (angleDir < 0)
                                    {
                                        //We have found a Split Event. :)
                                        //lets update its Distance to Line.
                                        //CiDyNode newEvent = new CiDyNode("E"+eventCount,eventIntersection,eventCount);
                                        CiDyNode newEvent = ScriptableObject.CreateInstance<CiDyNode>().Init("E" + eventCount, eventIntersection, eventCount);
                                        eventCount++;
                                        //Calculate dist to edge.
                                        //float distToP = CiDyUtils.DistanceToLine(eventIntersection,nodeA.position,nodeB.position);
                                        float distToP = DistanceToLine(eventIntersection, nodeA.eB.pos1, nodeA.eB.pos2);
                                        newEvent.distToP = distToP;
                                        newEvent.predNode = nodeA;
                                        newEvent.succNode = nodeB;
                                        newEvent.eA = nodeA.eA;
                                        newEvent.eB = nodeB.eB;
                                        newEvent.reflex = true;
                                        newEvent.origNode = curNode;
                                        newEvent.position.y = curNode.position.y;
                                        if (nodeA.isCorner || nodeB.isCorner)
                                        {
                                            newEvent.isCorner = true;
                                        }
                                        //Add to Temp.
                                        tempEvents.Add(newEvent);
                                        if (distToP < closestEvent)
                                        {
                                            closestEvent = distToP;
                                        }
                                        //Debug.Log("Split Event "+distToP);
                                        //Found it. No need to Test Further
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (tempEvents.Count > 0)
                {
                    //Determine which event will happen first for this Bisector Line.
                    float closest = Mathf.Infinity;
                    CiDyNode bestMatch = null;
                    for (int j = 0; j < tempEvents.Count; j++)
                    {
                        float dist = Vector3.Distance(tempEvents[j].position, p0);
                        if (dist < closest)
                        {
                            closest = dist;
                            bestMatch = tempEvents[j];
                        }
                    }
                    if (bestMatch != null)
                    {
                        if (!DuplicateEvent(bestMatch, foundEvents))
                            foundEvents.Add(bestMatch);
                    }
                }
            }
            //foundEvents = foundEvents.OrderBy (x => x.distToP).ThenBy(x=>x.position.x).ThenBy(x=>x.position.z).ToList ();
            foundEvents = foundEvents.OrderBy(x => x.distToP).ToList();
            return foundEvents;
        }

        static bool DuplicateEvent(CiDyNode testNode, List<CiDyNode> eventList)
        {
            for (int i = 0; i < eventList.Count; i++)
            {
                CiDyNode curNode = eventList[i];
                if (SameVector3s(testNode.position, curNode.position))
                {
                    if (testNode.predNode.name == curNode.predNode.name)
                    {
                        if (testNode.succNode.name == curNode.succNode.name)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /*public static bool DuplicateEdge(CiDyLatticeEdge testEdge, CiDyLatticeEdge[] list)
        {

            //Check if duplicate edge
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].edgeName == testEdge.edgeName)
                {
                    //Duplicate
                    return true;
                }
            }
            return false;
        }*/

        //This function will Move all the vertices to there new Wavefront Postions based on the Current SweepZ point.
        public static List<CiDyNode> UpdateVertexPos(List<CiDyNode> oldSlav, float newSweep)
        {
            //Debug.Log ("Update VertexPos "+oldSlav.Count+" NewSweep: "+newSweep);
            //To move the vertices we will determine there distance using a combination of the hyptonuse theory and angles from there bisectors and Perp.
            for (int i = 0; i < oldSlav.Count; i++)
            {
                CiDyNode curNode = oldSlav[i];
                //Now determine the new position for this vertex.
                //Vector3 edgeDir = (curNode.position-curNode.succNode.position).normalized;
                //We need the Angle of from the bisector and the perpendicular of the a-b direction.
                float angle = Vector3.Angle(curNode.edgeDir, curNode.bisector);
                //Debug.Log("CurNode: "+curNode.name+" DistToP: "+curNode.distToP);
                float moveSweep = newSweep - curNode.distToP;
                //hypotenuse = oppositeSide / sin(theta);
                float distance = Mathf.Round(moveSweep / Mathf.Sin((angle * Mathf.PI / 180f)) * 100f) / 100f;
                curNode.position += (curNode.bisector * distance);
                //Debug.Log("Moved "+curNode.name+" by "+distance+" To Match a SweepZ of "+newSweep);
            }
            return oldSlav;
        }

        //Check Local Constraints for Snap Algorithm of Roads
        //Check if road is acceptable in the current Road Graph
        public static int CheckLocalConstraints(ref CiDyGraph graph, CiDyEdge newEdge, float testRoadLength, float minRoadLength, float connectPercent, float maxSlope, ref bool corrected, ref CiDyEdge splitEdge)
        {
            int changedInt = 2;
            //Debug.Log("Check Local Constraints");
            //We want to make sure we do not have duplicates.
            //We also want to check and correct the Following.
            //A = Overlap Intersections(Connect to intersection Point)
            //B = End Near Another End (connect to near end)
            //C = End Near Line (Extend edge to near Line Connection) (Not performed when doing cell roads)
            //Check if Line crosses over any other lines.
            //Iterate through roads list and check intersection against all others.
            Vector3 intersection = Vector3.zero;
            float lowestR = (1.1f + connectPercent);
            Vector3 closesetEvent = Vector3.zero;
            int snapNodeLocation = -1;

            if (graph.masterGraph.Count <= 0)
            {
                return -1;
            }

            for (int i = 0; i < graph.masterGraph.Count; i++)
            {

                //Skip any nodes at origin position of road.
                if (graph.masterGraph[i].position.Equals(newEdge.pos1))
                {
                    //Debug.Log("Same Position as Origin Point?: "+i+" , "+allNodes[i].position+" newRoad.pos1: "+newRoad.pos1);
                    continue;
                }

                float r = 0;
                float s = 0;
                float dist = CiDyUtils.DistanceToLine(graph.masterGraph[i].position, newEdge.pos1, newEdge.pos2, ref r, ref s);

                //Send Info to CiDyNode for later testing if needed.
                graph.masterGraph[i].r = r;
                graph.masterGraph[i].s = s;
                graph.masterGraph[i].distToP = dist;

                //Debug.Log("Node: " + allNodes[i].name + " R: " + r + " S: " + s + " Dist: " + dist);
                //Make sure distance to Line is within Snap Event, Then Make sure R value is closer than the current closest, Finally, Make sure R value is within Snap Range.
                if (r > 0 && r <= (1 + connectPercent) && r < lowestR && dist <= (testRoadLength * connectPercent))
                {
                    //Debug.Log("Node: " + allNodes[i].name + " R: " + r + " S: " + s + " Dist: " + dist);
                    //Look at the Closest possible Events only
                    lowestR = r;
                    //This Node is within snap distance.
                    closesetEvent = graph.masterGraph[i].position;
                    snapNodeLocation = i;
                    corrected = true;
                }
            }

            //Sort by R Value. to prioritize close nodes first.
            //allNodes = allNodes.OrderBy(x => x.r).ToList();
            float closestDist = Mathf.Infinity;
            if (closesetEvent != Vector3.zero)
            {
                //Update Dist
                closestDist = Vector3.Distance(newEdge.pos1, closesetEvent);
            }
            //Debug.Log("Test 1 Completed, Lowest R Available is Set, Starting Test 2");
            //Perform Test 2,  Proposed Segment Intersection Test
            for (int i = 0; i < graph.graphEdges.Count; i++)
            {
                //Skip any roads sharing origin point.
                if (graph.graphEdges[i].pos1.Equals(newEdge.pos1) || graph.graphEdges[i].pos2.Equals(newEdge.pos1))
                {
                    //Debug.Log("Skip any root connected roads");
                    continue;
                }

                //Only Look at potential Intersecting Segments, Nodes of road must be on different perpendicular planes as well as one node must be <= lowestR and roads with at least one node of the lowest r value.
                if (!CiDyUtils.SameSign(graph.graphEdges[i].v1.s, graph.graphEdges[i].v2.s))
                {
                    //Check the Actual Road Mesh. that this Graph Edge Represents.
                    //Get Road Mesh of Edge
                    CiDyRoad road = graph.ReturnRoadFromEdge(graph.graphEdges[i]);
                    if (road != null)
                    {
                        //Iterate through Road Center Point and Check for intersection
                        for (int j = 0; j < road.origPoints.Length - 1; j++)
                        {

                            if (CiDyUtils.LineIntersection(newEdge.pos1, newEdge.pos2, road.origPoints[j], road.origPoints[j + 1], ref intersection))
                            {
                                float dist = Vector3.Distance(intersection, newEdge.pos1);
                                if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    closesetEvent = intersection;
                                    corrected = true;
                                    snapNodeLocation = -1;
                                    splitEdge = graph.graphEdges[i];
                                    splitEdge.splitPoint = new Vector2(j, j + 1);
                                }
                            }
                        }
                    }
                }
            }

            //Check Overal Slope.
            //Check Slope
            Vector3 vA = newEdge.pos1;
            Vector3 vB = newEdge.pos2;
            Vector3 dir = (vB - vA).normalized;
            Vector3 flatVa = new Vector3(vA.x, 0, vA.z);
            Vector3 flatVb = new Vector3(vB.x, 0, vB.z);
            flatVa.y = 0;
            flatVb.y = 0;
            Vector3 fwd = (flatVb - flatVa).normalized;
            float angle = Vector3.Angle(dir, fwd);
            //Debug.Log("Checking Local Constraints Max Road Grade: " + angle);
            //If the Angle Slope is greater than Max Slope. This edge is not valid
            if (angle > maxSlope)
            {
                //Can we change the road so its within slope max?
                return -1;
            }
            //Only Perform Test 3 if no other snap events have been found in Test 1 & Test 2
            if (!corrected)
            {
                //Debug.Log("Test 1 & 2 Did not Find a Snap Event, Starting Test 3");
                //Perform Test 3, Proposed Node Distance to Segments (Make sure the Proposed Node is Not closer than snap distance to any other segment.)
                for (int i = 0; i < graph.graphEdges.Count; i++)
                {

                    //Skip any roads sharing origin point.
                    /*if (graph.graphEdges[i].pos1.Equals(newEdge.pos1) || graph.graphEdges[i].pos1.Equals(newEdge.pos2))
                    {
                        continue;
                    }*/

                    //Iterate through this GraphEdges Road Center Line.
                    CiDyRoad road = graph.ReturnRoadFromEdge(graph.graphEdges[i]);
                    if (road != null)
                    {
                        for (int j = 0; j < road.origPoints.Length - 1; j++)
                        {
                            float r = 0;
                            float s = 0;
                            float dist = CiDyUtils.DistanceToLine(newEdge.pos2, road.origPoints[j], road.origPoints[j + 1], ref r, ref s);

                            if (r <= (1 + connectPercent))
                            {
                                //Debug.Log("Testing Road: " + i + " Dist: " + dist + " R: " + r + " S: " + s);
                                if (dist < (testRoadLength * connectPercent))
                                {
                                    //Debug.Log("This Road is too close to another road.");
                                    //This is too Close to another road. This road is not acceptable.
                                    return -1;
                                }
                            }
                        }
                    }
                }
            }

            //Now that we have tested all Intersections.
            if (corrected)
            {
                if (snapNodeLocation != -1)
                {
                    //Debug.Log("Snap Node");
                    newEdge.CorrectEdge(graph.masterGraph[snapNodeLocation]);
                    changedInt = 0;//Snapped to an Intersection
                }
                else
                {
                    //Intersected.
                    //Debug.Log("Intersected Edge");
                    //We have an updated End point. Lets update the Position
                    //Find the Node that is at this point.
                    if (graph.ReturnTerrainPos(ref closesetEvent))
                    {
                        //Make sure this Node isn't too close to any other Node.
                        if (!graph.TooCloseToNodes(closesetEvent, minRoadLength))
                        {
                            newEdge.CorrectEdge(closesetEvent);
                            changedInt = 1;//Creating a New Node in the Middle of an Exisiting Edge.
                        }
                        else
                        {
                            //Debug.Log("Intersection too Close: ");
                            changedInt = -1;
                        }
                    }
                }
            }
            //Pass final Check. That corrected Distance is greater than minimum Segment Length
            if (Vector3.Distance(newEdge.pos1, newEdge.pos2) < minRoadLength)
            {
                changedInt = -1;
            }

            //Final Elevation along Terrain Check
            //Project Bezier and Contour to Terrain.
            Vector3[] rdPath = new Vector3[3];
            rdPath[0] = newEdge.pos1;
            rdPath[1] = ((newEdge.pos1 + newEdge.pos2) / 2);
            rdPath[2] = newEdge.pos2;
            rdPath = CiDyUtils.CreateBezier(rdPath, 3);
            //Contour to terrain
            //TODO Replace Logic of Countour with Multi-Terrain Support
            //graph.ContourPathToTerrain(ref rdPath, blendingTerrains);
            rdPath = CiDyUtils.CreateBezier(rdPath, 6);
            for (int i = 0; i < rdPath.Length - 1; i++)
            {
                Vector3 posA = rdPath[i];
                //CiDyUtils.MarkPoint(posA, i);
                Vector3 posB = rdPath[i + 1];

                dir = (posB - posA).normalized;
                flatVa = new Vector3(posA.x, 0, posA.z);
                flatVb = new Vector3(posB.x, 0, posB.z);
                fwd = (flatVb - flatVa).normalized;
                angle = Vector3.Angle(dir, fwd);
                //Calculate Direction to Forward
                if (angle > maxSlope)
                {
                    Debug.Log("Sub Slope Greater: " + angle + " > Max Slope: " + maxSlope);
                    //Return Fail
                    changedInt = -1;
                    break;
                }
            }

            return changedInt;
        }


        //ClipperLib Functions
        //Polygon Offset. Returns List<List> of PolygonOffset Solution from ClipperLib.Offset To Inset use -1 or < to Offset use 1 or >
        public static List<List<Vector3>> PolygonOffset(List<Vector3> vectorList, float inset)
        {
            double clipperOffset = inset;
            //Create Clipper Variables
            Path subj = new List<IntPoint>();
            Paths solution = new List<List<IntPoint>>();
            //Set SubJ/OrigPoly to Be Offset
            /*for(int i = 0;i<vectorList.Count;i++){
                Vector3 roundVector = new Vector3(Mathf.Round(vectorList[i].x*100)/100,0,Mathf.Round(vectorList[i].z*100)/100);
                subj.Add(new IntPoint(roundVector.x,roundVector.z));
            }*/
            for (int i = 0; i < vectorList.Count; i++)
            {
                subj.Add(new IntPoint(vectorList[i].x, vectorList[i].z));
            }
            //Run Clipper Offset
            ClipperOffset co = new ClipperOffset();
            //co.AddPath(subj, JoinType.jtRound, EndType.etClosedPolygon);
            co.AddPath(subj, JoinType.jtRound, EndType.etClosedPolygon);
            co.Execute(ref solution, clipperOffset);
            //Now return the Solution into a List<List<Vector3>>()
            List<List<Vector3>> insetPoly = new List<List<Vector3>>(0);
            //Iterate through Solution and and return vector3 with y flattend.(user will need to project the Y Value if needed.
            for (int i = 0; i < solution.Count; i++)
            {
                List<Vector3> curPoly = new List<Vector3>();
                for (int j = 0; j < solution[i].Count; j++)
                {
                    Vector3 pos = new Vector3(solution[i][j].X, 0, solution[i][j].Y);
                    curPoly.Add(pos);
                }
                insetPoly.Add(curPoly);
            }
            if (insetPoly.Count <= 0)
            {
                Debug.Log("Polygon Offset has Shrunk to 0");
            }
            return insetPoly;
        }

        //ClipperLib Functions
        //Polygon Offset. Returns List<List> of PolygonOffset Solution from ClipperLib.Offset To Inset use -1 or < to Offset use 1 or >
        public static List<List<Vector3>> PolygonOffset(List<CiDyNode> vectorList, float inset)
        {
            double clipperOffset = inset;
            //Create Clipper Variables
            Path subj = new List<IntPoint>();
            Paths solution = new List<List<IntPoint>>();
            //Set SubJ/OrigPoly to Be Offset
            /*for(int i = 0;i<vectorList.Count;i++){
                Vector3 roundVector = new Vector3(Mathf.Round(vectorList[i].x*100)/100,0,Mathf.Round(vectorList[i].z*100)/100);
                subj.Add(new IntPoint(roundVector.x,roundVector.z));
            }*/
            for (int i = 0; i < vectorList.Count; i++)
            {
                subj.Add(new IntPoint(vectorList[i].position.x, vectorList[i].position.z));
            }
            //Run Clipper Offset
            ClipperOffset co = new ClipperOffset();
            co.AddPath(subj, JoinType.jtRound, EndType.etClosedPolygon);
            co.Execute(ref solution, clipperOffset);
            //Now return the Solution into a List<List<Vector3>>()
            List<List<Vector3>> insetPoly = new List<List<Vector3>>(0);
            //Iterate through Solution and and return vector3 with y flattend.(user will need to project the Y Value if needed.
            for (int i = 0; i < solution.Count; i++)
            {
                List<Vector3> curPoly = new List<Vector3>();
                for (int j = 0; j < solution[i].Count; j++)
                {
                    Vector3 pos = new Vector3(solution[i][j].X, 0, solution[i][j].Y);
                    curPoly.Add(pos);
                }
                insetPoly.Add(curPoly);
            }
            return insetPoly;
        }

        public static List<List<Vector3>> PolygonOffset(List<CiDyVector3> vectorList, float inset)
        {
            double clipperOffset = inset;
            //Create Clipper Variables
            Path subj = new List<IntPoint>();
            Paths solution = new List<List<IntPoint>>();
            //Set SubJ/OrigPoly to Be Offset
            /*for(int i = 0;i<vectorList.Count;i++){
                Vector3 roundVector = new Vector3(Mathf.Round(vectorList[i].x*100)/100,0,Mathf.Round(vectorList[i].z*100)/100);
                subj.Add(new IntPoint(roundVector.x,roundVector.z));
            }*/
            for (int i = 0; i < vectorList.Count; i++)
            {
                subj.Add(new IntPoint(vectorList[i].pos.x, vectorList[i].pos.z));
            }
            //Run Clipper Offset
            ClipperOffset co = new ClipperOffset();
            co.AddPath(subj, JoinType.jtRound, EndType.etClosedPolygon);
            co.Execute(ref solution, clipperOffset);
            //Now return the Solution into a List<List<Vector3>>()
            List<List<Vector3>> insetPoly = new List<List<Vector3>>(0);
            //Iterate through Solution and and return vector3 with y flattend.(user will need to project the Y Value if needed.
            for (int i = 0; i < solution.Count; i++)
            {
                List<Vector3> curPoly = new List<Vector3>();
                for (int j = 0; j < solution[i].Count; j++)
                {
                    Vector3 pos = new Vector3(solution[i][j].X, 0, solution[i][j].Y);
                    curPoly.Add(pos);
                }
                insetPoly.Add(curPoly);
            }
            return insetPoly;
        }

        //Translate WorldPos to Terrain Position
        public static Vector3 GetNormalizedPositionRelativeToTerrain(Vector3 pos, Vector3 terrPos, Vector3 tSize)
        {
            Vector3 tempCoord = (pos - terrPos);
            Vector3 coord;
            coord.x = tempCoord.x / tSize.x;
            coord.y = tempCoord.y / tSize.y;
            coord.z = tempCoord.z / tSize.z;
            return coord;
        }

        public static Vector3 GetNormalizedTerrainPositionRelativeToWorld(Vector3 pos, Terrain terrain)
        {
            Vector3 tempCoord = (pos + terrain.transform.position);
            Vector3 coord;
            coord.x = tempCoord.x * terrain.terrainData.size.x;
            coord.y = tempCoord.y * terrain.terrainData.size.y;
            coord.z = tempCoord.z * terrain.terrainData.size.z;
            return coord;
        }

        //Used for Straight skeleton Inset
        public class CheckedLine
        {

            public Vector3 vectorA;//Point A
            public Vector3 vectorB;//Point B

            public CheckedLine(Vector3 a, Vector3 b)
            {
                vectorA = a;
                vectorB = b;
            }
        }

        //Create a Class that stores the Point position and the origin Base Point(lowest).
        public class InsetPoint
        {
            public Vector3 point;
            public Vector2d origin;
            public Vector2d endPoint;

            public InsetPoint(Vector3 newPoint, Vector2d originPoint, Vector2d end)
            {
                point = newPoint;
                origin = originPoint;
                endPoint = end;
            }
        }

        //this function will grab the even side of the counts.
        public static void GrabEvenVs(CiDyRoad newRoad, ref List<Vector3> interiorPnts)
        {
            Vector3[] meshVerts = newRoad.GetComponent<MeshFilter>().sharedMesh.vertices;
            //Iterate through vs list and grab when i is even only.
            for (int i = 0; i < meshVerts.Length; i++)
            {
                if (i % 2 == 0)
                { // Is even,
                    /*GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = "Even";
                    sphere.transform.localScale = new Vector3(5,5,5);
                    sphere.transform.position = newRoad.vs[i];*/
                    interiorPnts.Add(meshVerts[i]);
                }
            }
        }
        //This function will grab the odd side of the counts.
        public static void GrabOddVs(CiDyRoad newRoad, ref List<Vector3> interiorPnts)
        {
            Vector3[] meshVerts = new Vector3[0];
            //Iterate through vs list and grab when i is odd only.
            if (newRoad.mFilter != null)
            {
                meshVerts = newRoad.mFilter.sharedMesh.vertices;
            }

            for (int i = 0; i < meshVerts.Length; i++)
            {
                if (i % 2 == 1)
                { // IsOdd,
                    /*GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = "Even";
                    sphere.transform.localScale = new Vector3(5,5,5);
                    sphere.transform.position = newRoad.vs[i];*/
                    interiorPnts.Add(meshVerts[i]);
                }
            }
        }

        //WE ASSUME CLOCKWISE POINT ORIENTATION OF INPUTs
        //This Function will Modify a Ref Mesh by Adding Vertices and Triangles and Uvs that create a Quad Mesh at the 4 Pnts Given.
        public static Mesh AddQuad(Mesh refMesh, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, bool reverseTriangles = false)
        {
            //A Mesh Needs Vertices. These Four Points are those Vertices.//We have to account for the Current Mesh Vertices etc.
            //Current Verts
            int curVert = refMesh.vertexCount;
            //Preset Quad Verts.
            Vector3[] verts = new Vector3[4];
            int[] tris = new int[6];
            Vector2[] uvs = new Vector2[4];
            //Set the Vertices
            verts[0] = p0;
            verts[1] = p1;
            verts[2] = p2;
            verts[3] = p3;

            //Set the Triangles.
            if (reverseTriangles)
            {
                //Tri 1
                tris[0] = curVert + 2;
                tris[1] = curVert + 1;
                tris[2] = curVert + 0;
                //Tri 2
                tris[3] = curVert + 0;
                tris[4] = curVert + 3;
                tris[5] = curVert + 2;
            }
            else
            {
                //Tri 1
                tris[0] = curVert + 0;
                tris[1] = curVert + 1;
                tris[2] = curVert + 2;
                //Tri 2
                tris[3] = curVert + 2;
                tris[4] = curVert + 3;
                tris[5] = curVert + 0;
            }
            //Vertical UVs
            //Get p0-p3 Width Distance.
            //Get P0-p1 Height Distance.
            float height = Vector3.Distance(p0, p1);
            float width = Vector3.Distance(p0, p3);
            //UVS
            uvs[0] = new Vector2(0, 0);//Bottom Left
            uvs[1] = new Vector2(0, height);//Top Left(Striaght Line Up)
            uvs[2] = new Vector2(width, height);//Top Right(Stright Line Right)
            uvs[3] = new Vector2(width, 0);//Bottom Right(Straight Line Left)
                                           //Now that we have the Three needed Ingredients.
                                           //Lets Recombine this mesh with the New Data.
            List<Vector3> meshVerts = refMesh.vertices.ToList();
            List<Vector2> meshUvs = refMesh.uv.ToList();
            List<int> meshTris = refMesh.triangles.ToList();
            //Iterate over Verts
            for (int i = 0; i < verts.Length; i++)
            {
                meshVerts.Add(verts[i]);
            }
            //Iterate over Tris
            for (int i = 0; i < tris.Length; i++)
            {
                meshTris.Add(tris[i]);
            }
            //Uvs
            for (int i = 0; i < uvs.Length; i++)
            {
                meshUvs.Add(uvs[i]);
            }
            //Return Mesh with Updated Verts,Tris,Uvs
            Mesh newMesh = new Mesh();
            //Verts
            newMesh.vertices = meshVerts.ToArray();
            //Tris
            newMesh.triangles = meshTris.ToArray();
            //Uvs
            newMesh.uv = meshUvs.ToArray();
            //Return Mesh
            refMesh = newMesh;

            return refMesh;
        }

        //Returns a Box Mesh
        public static Mesh CreateBoxMesh(Transform ourTransform, float width, float height, float depth, bool convertFromInches, bool diamondPlateBack = false, Material diamondMat = null)
        {
            if (convertFromInches)
            {
                //Convert Values
                width = width / 39.3701f;
                height = height / 39.3701f;
                depth = depth / 39.3701f;
            }
            Vector3 curPos = Vector3.zero;
            //Calculate Four Points Top Left , top Right and Bottoms.
            //Current forward 
            Vector3 fwd = ourTransform.forward;
            Vector3 right = ourTransform.right;
            //Create Top Front Left Point then Right. Then Top Back Left and Top Back Right
            Vector3 tfl = (curPos + (width / 2 * -right)) + (Vector3.up * (height / 2) + (fwd * depth / 2));
            Vector3 tfr = (curPos + (width / 2 * right)) + (Vector3.up * (height / 2) + (fwd * depth / 2));
            //Top Back
            Vector3 tbl = (curPos + (width / 2 * -right)) + (Vector3.up * (height / 2) + (-fwd * depth / 2));
            Vector3 tbr = (curPos + (width / 2 * right)) + (Vector3.up * (height / 2) + (-fwd * depth / 2));
            //Bottom Points//
            Vector3 bfl = (curPos + (width / 2 * -right)) + (-Vector3.up * (height / 2) + (fwd * depth / 2));
            Vector3 bfr = (curPos + (width / 2 * right)) + (-Vector3.up * (height / 2) + (fwd * depth / 2));
            //Top Back
            Vector3 bbl = (curPos + (width / 2 * -right)) + (-Vector3.up * (height / 2) + (-fwd * depth / 2));
            Vector3 bbr = (curPos + (width / 2 * right)) + (-Vector3.up * (height / 2) + (-fwd * depth / 2));
            //Now Create the Mesh Quads for Each Face.
            //Create Mesh
            Mesh refMesh = new Mesh();
            //Top Quad
            refMesh = AddQuad(refMesh, tfl, tfr, tbr, tbl);
            //Bottom Quad
            refMesh = AddQuad(refMesh, bfl, bfr, bbr, bbl, true);
            //Front Quad
            refMesh = AddQuad(refMesh, tfr, tfl, bfl, bfr);
            //Back Quad
            refMesh = AddQuad(refMesh, tbl, tbr, bbr, bbl);
            //Right Side quad
            refMesh = AddQuad(refMesh, tbr, tfr, bfr, bbr);
            //Left Side
            refMesh = AddQuad(refMesh, tfl, tbl, bbl, bfl);
            //Recalc Mesh
            refMesh.RecalculateBounds();
            refMesh.RecalculateNormals();
            refMesh.RecalculateTangents();

            //Do we need to Diamond Plate the Back?
            if (diamondPlateBack)
            {
                GameObject diamondPlate = null;
                //Now add Mesh Renderer and Filter
                MeshRenderer mRenderer = null;
                MeshFilter mFilter = null;
                //See if it already Exist
                if (ourTransform.childCount > 0)
                {
                    //We might already have one.
                    diamondPlate = ourTransform.GetChild(0).gameObject;
                }
                if (diamondPlate == null)
                {
                    //Create Holder
                    diamondPlate = new GameObject("DiamondPlate");
                    diamondPlate.transform.position = ourTransform.position;
                    diamondPlate.transform.SetParent(ourTransform);
                    //Preturb it back a smidge.
                    diamondPlate.transform.position = diamondPlate.transform.position + ((-diamondPlate.transform.forward) * 0.0001618f);
                    //Now add Mesh Renderer and Filter
                    mRenderer = diamondPlate.AddComponent<MeshRenderer>();
                    mFilter = diamondPlate.AddComponent<MeshFilter>();
                }
                else
                {
                    //Grab Reference
                    mRenderer = diamondPlate.GetComponent<MeshRenderer>();
                    mFilter = diamondPlate.GetComponent<MeshFilter>();
                }
                //Create Mesh.
                Mesh diamondMesh = new Mesh();
                //Back Quad
                diamondMesh = AddQuad(diamondMesh, tbl, tbr, bbr, bbl);
                if (diamondMat)
                    mRenderer.material = diamondMat;
                //Set Mesh
                mFilter.sharedMesh = diamondMesh;
            }
            return refMesh;
        }
    }
}