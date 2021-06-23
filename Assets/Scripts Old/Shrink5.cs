using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Shrink5 : MonoBehaviour
{
    #region initialisation
    //Mesh
    Mesh mesh;

    //grid settings
    public float cellsize = 1;
    public Vector3 gridOffset;
    public int gridSize = 10;
    public float damp = 0.9999f;
    public Vector3 forces;
    public float stiffness = 1f;
    int tricount;

    //Position parameters
    Vector3[] fors;
    public Vector3[] pos;
    Vector3[] p;
    Vector3[] vel;
    int[] triangles;
    float[] m;
    float[] weights;
    public float timer = 0.1f;

    // Constraint Parameters
    public struct dconstraint
    {
        public int p1, p2, p3;
    };
    //Set constraint positions
    public dconstraint[] Trilist;
    public struct deltaP
    {
        public Vector3 p1, p2, p3;
    };


    //Data for the Compute Shader
    public ComputeShader shader;
    public ComputeShader distanceshader;
    //Data for PBD Setup Shaders
    ComputeBuffer Pbuffer;
    ComputeBuffer Vbuffer;
    ComputeBuffer Wbuffer;
    ComputeBuffer Fbuffer;
    ComputeBuffer Tbuffer;
    public int threadtimes = 1;
    int kernel;
    int distancekernel;

    //Data for Constraint Buffers
    public ComputeBuffer Trilistbuffer;
    ComputeBuffer DeltaPbuffer;

    #endregion

    // Start is called before the first frame update
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    // Start is called before the first frame update
    void Start()
    {
        MakeProceduralGrid();
        ForDistanceConstraints();

        //UpdateMesh();


    }

    // Update is called once per frame
    void Update()
    {


        //Dispatching For 

        Pbuffer.SetData(pos);
        Vbuffer.SetData(vel);
        fors[0] = forces;
        Fbuffer.SetData(fors);

        shader.SetBuffer(kernel, Shader.PropertyToID("pos"), Pbuffer);
        shader.SetBuffer(kernel, Shader.PropertyToID("vel"), Vbuffer);
        shader.SetBuffer(kernel, Shader.PropertyToID("weights"), Wbuffer);
        shader.SetBuffer(kernel, Shader.PropertyToID("forces"), Fbuffer);
        shader.SetFloat(Shader.PropertyToID("timer"), timer);
        shader.SetFloat(Shader.PropertyToID("damp"), damp);

        shader.Dispatch(kernel, threadtimes, 1, 1); //Execution

        Pbuffer.GetData(pos);
        Vbuffer.GetData(vel);

        //Distance Constraint

        DistanceConstraint();

        mesh.Clear();
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();



    }

    /*void OnDestroy()
    {
        Trilistbuffer.Dispose();
    }*/

    #region Making Grid
    void MakeProceduralGrid()
    {
        //Set array sizes
        pos = new Vector3[(gridSize + 1) * (gridSize + 1)];
        vel = new Vector3[(gridSize + 1) * (gridSize + 1)];
        fors = new Vector3[1];
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

                /*Debug.Log(fors[v]);
                Debug.Log(v);*/
                m[v] = 5f;
                weights[v] = 1f / m[v];
                v++;
            }
        }
        fors[0] = forces; // assigning force to the array for buffer
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
        tricount = gridSize*gridSize*2;/////////////////////
         weights[0] = 0f;



        // Initial settings
        mesh.Clear();
        vel = pos;


        //Linking HLSL

        Pbuffer = new ComputeBuffer((gridSize + 1) * (gridSize + 1), 3 * 4);
        Vbuffer = new ComputeBuffer((gridSize + 1) * (gridSize + 1), 3 * 4);
        Fbuffer = new ComputeBuffer(1, 3 * 4);
        Wbuffer = new ComputeBuffer((gridSize + 1) * (gridSize + 1), 4);
        //Tbuffer = new ComputeBuffer(1, 4);



        kernel = shader.FindKernel("trydispatch");

        /*Pbuffer.SetData(pos);
        Vbuffer.SetData(vel);*/
        Fbuffer.SetData(fors);
        Wbuffer.SetData(weights);

        //Debug.Log(vel[1].ToString();
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

    }
#endregion

    #region Distance Constraint
    void ForDistanceConstraints()
    {

        //Set constraint positions
        Trilist = new dconstraint[gridSize * gridSize * 2];

        //Creating Triangle list array of Vectors (eg: TriangleList[0] = (0,1,2); Which would be the the first three positions indices of the first triangle ) for iteration purposes, So we can go through each traingle of the Mesh Quads Easily (Quads being the the Square positions of the mesh)
        int t = 0;
        for (int x = 0; x < tricount; x++)///////////////
        {
            Trilist[x].p1 = triangles[t + 0]; //Can be subjected to edit
            Trilist[x].p2 = triangles[t + 1];
            Trilist[x].p3 = triangles[t + 2];
            //Debug.Log(Trilist[x].p1);
            t += 3;
        }

        //Linking Constraint Buffer
        Trilistbuffer = new ComputeBuffer(tricount , 4 * 3);
        DeltaPbuffer = new ComputeBuffer(tricount, 4 *3* 3);
        Trilistbuffer.SetData(Trilist);
        distancekernel = distanceshader.FindKernel("DistanceCompute");
        //distancekernel = 0;
    }

    void DistanceConstraint()
    {
        Pbuffer.SetData(pos);

        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("weights"), Wbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("pos"), Pbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilist"), Trilistbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Iterateconstraint"), DeltaPbuffer);
        distanceshader.SetInt(Shader.PropertyToID("TriCount"), tricount);
        distanceshader.SetFloat(Shader.PropertyToID("stiffness"), stiffness);

        distanceshader.Dispatch(distancekernel, threadtimes, 1, 1); //Execution

        Pbuffer.GetData(pos);

    }
    #endregion
}
