using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;

public class Traingulation {

    public static bool TriangulatePolygon(List<Vector3> points, List<List<Vector3>> holes, out List<int> outIndices, out List<Vector3> outVertices)
    {
        outIndices = new List<int>(0);
        outVertices = new List<Vector3>(0);
        Polygon poly = new Polygon();

        for (int i = 0; i < points.Count; i++) {
            Vertex point = new Vertex(points[i].x, points[i].z);
            poly.Add(point);
            if (i == points.Count - 1)
            {
                poly.Add(new Segment(new Vertex(point.X, point.Y), new Vertex(points[0].x, points[0].z)));
            }
            else {
                poly.Add(new Segment(new Vertex(point.X, point.Y), new Vertex(points[i+1].x, points[i+1].z)));
            }
        }

        if (holes != null)
        {
            for (int i = 0; i < holes.Count; i++)
            {
                List<Vertex> vertices = new List<Vertex>(0);

                for (int j = 0; j < holes[i].Count; j++)
                {
                    vertices.Add(new Vertex(holes[i][j].x, holes[i][j].z));
                }
                poly.Add(new Contour(vertices), true);
            }
        }

        var mesh = poly.Triangulate();

        foreach (ITriangle t in mesh.Triangles) {
            for (int j = 2; j >= 0; j--) {
                bool found = false;
                Vector3 point = new Vector3((float)t.GetVertex(j).X,0,(float)t.GetVertex(j).Y);
                for (int k = 0; k < outVertices.Count; k++) {
                    if (outVertices[k].x == t.GetVertex(j).X && outVertices[k].z == t.GetVertex(j).Y) {
                        outIndices.Add(k);
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    outVertices.Add(point);
                    outIndices.Add(outVertices.Count - 1);
                }
            }
        }

        for (int n = 0; n < outVertices.Count; n++) {
            Vector3 point = outVertices[n];
            //Compare point to orignal points and holes to determine its height value
            for (int i = 0; i < points.Count; i++)
            {
                if (point.x == points[i].x && point.z == points[i].z)
                {
                   point.y = points[i].y;
                   outVertices[n] = point;
                }
            }
            if (holes != null)
            {
                //Check against holes list
                for (int i = 0; i < holes.Count; i++)
                {
                    for (int k = 0; k < holes[i].Count; k++)
                    {
                        if (point.x == holes[i][k].x && point.z == holes[i][k].z)
                        {
                            point.y = holes[i][k].y;
                            outVertices[n] = point;
                        }
                    }
                }
            }
        }
        return true;
    }
}
