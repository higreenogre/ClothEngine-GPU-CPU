using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothPlane : MonoBehaviour
{
    #region initialisation
    //Mesh
    Mesh mesh;

    //grid settings
    public float cellsize = 1;
    //public Vector3 gridOffset;
    int gridLength;
    int gridTriLength;
    public float damp = 0.9999f;
    public Vector3 forces;
    int tricount;
    int loopdistance;

    //Position parameters
    Vector3[] fors;
    public Vector3[] pos;
    Vector3[] p;
    Vector3[] vel;
    int[] triangles;
    float[] m;
    float[] weights;
    float[] stiffness;
    Vector3 stiffl1, stiffl2, stiffl3;
    float stiff = 1f;
    public float timer = 0.1f;

    // Constraint Parameters

    public uint[] constraintp1, constraintp2, constraintp3;

    //Set constraint positions

    Vector3[] lp1, lp2, lp3;
    Vector3[] dp1, dp2;

    ComputeBuffer lengthp1, lengthp2, lengthp3;
    ComputeBuffer constraintp1buffer, constraintp2buffer, constraintp3buffer;
    ComputeBuffer stiffnessbuffer;
    ComputeBuffer deltap1;
    ComputeBuffer deltap2;

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
    int distancethread;

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

        velocitydispatch();

        //Distance Constraint

        DistanceConstraint();

        for (int x = 0; x < pos.Length; x++)
        {
            Debug.Log(x + " " + pos[x]);
        }

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

        mesh = GetComponent<MeshFilter>().mesh;
        gridLength = mesh.vertices.Length;
        gridTriLength = mesh.triangles.Length;

        Debug.Log(gridLength);

        //Set array sizes
        pos = new Vector3[gridLength];
        vel = new Vector3[gridLength];
        fors = new Vector3[1];
        triangles = new int[gridTriLength];
        m = new float[gridLength];
        weights = new float[gridLength];
        stiffness = new float[gridTriLength];

        pos = mesh.vertices;
        triangles = mesh.triangles;

        //Set Tracker

        //Set vertex Offset
        //float vertexOffset = cellsize * 0.5f;

        //create vertex grid
        for (int x = 0; x < gridLength; x++)
        {
           
                m[x] = 1f;
                weights[x] = 1f;//1f / m[v];
          
        }
        
        weights[0] = 0f;

        //Cretae stiffness array 
       
        //weights[50] = 0f;
        for (int x = 0; x < pos.Length; x++)
        {
            Debug.Log(x + " " + pos[x]);
        }
        fors[0] = forces; // assigning force to the array for buffer
        //reset vertex tracker 
        
        /*for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                triangles[t + 0] = triangles[t + 6] = v;
                triangles[t + 1] = triangles[t + 4] = triangles[t + 8] = triangles[t + 9] = v + 1;
                triangles[t + 2] = triangles[t + 3] = triangles[t + 7] = triangles[t + 10] = v + (gridSize + 1);
                triangles[t + 5] = triangles[t + 11] = v + (gridSize + 1) + 1;
                v++;
                t += 12;
            }
            v++;
        }*/
        tricount = (triangles.Length) / 3;/////////////////////
        Debug.Log(tricount);



        // Initial settings
        mesh.Clear();
        vel = pos;


        //Linking HLSL

        Pbuffer = new ComputeBuffer(gridLength, 3 * 4);
        Vbuffer = new ComputeBuffer(gridLength, 3 * 4);
        Fbuffer = new ComputeBuffer(1, 3 * 4);
        Wbuffer = new ComputeBuffer(gridLength, 4);
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

    #region Velocity
    void velocitydispatch()
    {
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
    }
    #endregion

    #region Distance Constraint
    void ForDistanceConstraints()
    {
        dp1 = new Vector3[1];
        dp2 = new Vector3[1];
        //Set constraint positions
        //dconstraint[] Trilist = new dconstraint[gridSize * gridSize * 2];
        constraintp1 = new uint[tricount];
        constraintp2 = new uint[tricount];
        constraintp3 = new uint[tricount];

        //Creating Triangle list array of Vectors (eg: TriangleList[0] = (0,1,2); Which would be the the first three positions indices of the first triangle ) for iteration purposes, So we can go through each traingle of the Mesh Quads Easily (Quads being the the Square positions of the mesh)
        
        for (int x = 0; x < tricount; x++)///////////////
        {
            constraintp1[x] = (uint)triangles[x * 3 + 0]; //Can be subjected to edit
            constraintp2[x] = (uint)triangles[x * 3 + 1];
            constraintp3[x] = (uint)triangles[x * 3 + 2];
            
            stiffl1 = pos[constraintp2[x]] - pos[constraintp1[x]];
            stiffl2 = pos[constraintp3[x]] - pos[constraintp2[x]];
            stiffl3 = pos[constraintp1[x]] - pos[constraintp3[x]];

            stiffness[x * 3 + 0] = stiffl1.magnitude;
            stiffness[x * 3 + 1] = stiffl2.magnitude;
            stiffness[x * 3 + 2] = stiffl3.magnitude;

            Debug.Log(x + " triangle " + constraintp1[x] + " " + constraintp2[x] + " " + constraintp3[x]);

        }
        distancethread = tricount;
        loopdistance = tricount * 3;

        //Trilistbuffer = new ComputeBuffer(tricount, 4 * 3);
        lp1 = new Vector3[tricount];
        lp2 = new Vector3[tricount];
        lp3 = new Vector3[tricount];

        lengthp1 = new ComputeBuffer(tricount, 4 * 3);
        lengthp2 = new ComputeBuffer(tricount, 4 * 3);
        lengthp3 = new ComputeBuffer(tricount, 4 * 3);
        constraintp1buffer = new ComputeBuffer(tricount, 4);
        constraintp2buffer = new ComputeBuffer(tricount, 4);
        constraintp3buffer = new ComputeBuffer(tricount, 4);
        stiffnessbuffer = new ComputeBuffer(gridTriLength, 4);
        ///////////////////////////////////////////////////////
        deltap1 = new ComputeBuffer(1, 4 * 3);
        deltap2 = new ComputeBuffer(1, 4 * 3);



        //Trilistbuffer.SetData(Trilist);
        distancekernel = distanceshader.FindKernel("DistanceCompute");
        //distancekernel = 0;
        //Linking Constraint Buffer
        constraintp1buffer.SetData(constraintp1);
        constraintp2buffer.SetData(constraintp2);
        constraintp3buffer.SetData(constraintp3);
        stiffnessbuffer.SetData(stiffness);

        Wbuffer.SetData(weights);
    }

    void DistanceConstraint()
    {
        Wbuffer.SetData(weights);
        Pbuffer.SetData(pos);
        lengthp1.SetData(lp1);
        lengthp2.SetData(lp2);
        lengthp3.SetData(lp3);
        //deltap1.SetData(dp1);
        //deltap2.SetData(dp2);


        //Trilistbuffer.SetData(Trilist);

        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("weights"), Wbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("pos"), Pbuffer);
        //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilist"), Trilistbuffer);
        //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Iterateconstraint"), DeltaPbuffer);
        distanceshader.SetInt(Shader.PropertyToID("loop"), loopdistance);
        distanceshader.SetFloat(Shader.PropertyToID("stiffness"), stiff);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("constraintp1"), constraintp1buffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("constraintp2"), constraintp2buffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("constraintp3"), constraintp3buffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp1"), lengthp1);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp2"), lengthp2);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp3"), lengthp3);
        //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("deltap1"), deltap1);
        //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("deltap2"), deltap2);


        distanceshader.Dispatch(distancekernel, 1, 1, 1);

        ///////////////////////////////////////////////////////////////
        lengthp1.GetData(lp1);
        lengthp2.GetData(lp2);
        lengthp3.GetData(lp3);

        //deltap1.GetData(dp1);
        //deltap2.GetData(dp2);

        for (int x = 0; x < tricount; x++)
        {
            Debug.Log(x + " " + lp1[x] + " " + lp2[x] + " " + lp3[x]);
            //Debug.Log("triangle " + x + " delptap1 " + dp1[0] + ", deltap2 " + dp2[0]);
        }
        Pbuffer.GetData(pos);

    }
    #endregion
}
