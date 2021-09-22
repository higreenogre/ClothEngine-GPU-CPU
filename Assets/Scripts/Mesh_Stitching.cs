using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mesh_Stitching : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Mesh_Stitch();
    }

    // Update is called once per frame
    void Mesh_Stitch()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        int gridLength = mesh.vertices.Length;
        int gridTriLength = mesh.triangles.Length;

        Debug.Log("no of points :" + gridLength);

        //Set array sizes
        Vector3[] pos = new Vector3[gridLength];
        int[] triangles = new int[gridTriLength];

        pos = mesh.vertices;
        triangles = mesh.triangles;




        for (int x = 0; x < gridLength; x++)
        {
            for (int y = 0; y < x; y++)
            {
                if (pos[x] == pos[y])
                {
                    for (int z = 0; z < gridTriLength; z++)
                    {
                        triangles[z] = triangles[z] == y ? x : triangles[z];
                    }
                }
            }
        }

        mesh.vertices = pos;
        mesh.triangles = triangles;
    }
}
