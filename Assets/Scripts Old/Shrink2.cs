using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Shrink2 : MonoBehaviour
{
    public ComputeShader shader;
    ComputeBuffer buffer;

    //Mesh
    Mesh mesh;

    //grid settings
    public float cellsize = 1;
    public Vector3 gridOffset;
    public int gridSize;
    public float damp = 0.9f;
    public Vector3 forces;

    //Position parameters
    Vector3[] pos;
    Vector3[] vel;
    int[] triangles;
    float[] m;
    float[] weights;
    float timer;

    //Data for the Compute Shader
    public struct Datastruct
    {
        public float w;
        public Vector3 p, v;
    }

    //Datastruct[] data = new Datastruct[(0 + 1) * (0 + 1)];

    // Start is called before the first frame update
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    // Start is called before the first frame update
    void Start()
    {
        MakeProceduralGrid();
        //UpdateMesh();
/*
        for (int x = 0; x < (gridSize + 1) * (gridSize + 1); x++)
        {
            data[x] = new Datastruct
            {
                p = pos[x],
                v = vel[x],
                w = weights[x]
            };
        }
*/
       // buffer = new ComputeBuffer(data.Length, 28);
    }

    void MakeProceduralGrid()
    {
        //Set array sizes
        pos = new Vector3[(gridSize + 1) * (gridSize + 1)];
        triangles = new int[gridSize * gridSize * 6];
        m = new float[(gridSize + 1) * (gridSize + 1)];
        weights = new float[(gridSize + 1) * (gridSize + 1)];
        //Set Tracker
        int v = 0;
        int t = 0;

        //Set vertex Offset
        float vertexOffset = cellsize * 0.5f;

        //create vertex grid
        for (int x = 0; x <= gridSize; x++)
        {
            for (int z = 0; z <= gridSize; z++)
            {
                pos[v] = new Vector3((x * cellsize) - vertexOffset, 0, (z * cellsize) - vertexOffset);
                Debug.Log(pos[v].ToString());
                m[v] = 5f;
                weights[v] = 1f / m[v];
                v++;
            }
        }

        //reset vertex tracker 
        v = 0;
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                triangles[t + 0] = v;
                triangles[t + 1] = triangles[t + 4] = v + 1;
                triangles[t + 2] = triangles[t + 3] = v + (gridSize + 1);
                triangles[t + 5] = v + (gridSize + 1) + 1;
                v++;
                t += 6;
            }
            v++;
        }
        // Initial settings
        mesh.Clear();
        vel = pos;
        //Debug.Log(vel[1].ToString();
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
    // Update is called once per frame
    void Update()
    {
        timer = 0.1f;
        for (int x = 0; x < (gridSize + 1) * (gridSize + 1); x++)
        {
            vel[x] = damp * (vel[x] + weights[x] * timer * forces);
            pos[x] += timer * vel[x];
            Debug.Log(vel[x].ToString());
        }
        mesh.Clear();
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
