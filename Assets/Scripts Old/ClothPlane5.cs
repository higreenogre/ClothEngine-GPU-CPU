using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothPlane5 : MonoBehaviour
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
    float stiff = 1f;
    public float timer = 0.1f;


    //Data for the Compute Shader
    public ComputeShader shader;

    //Data for PBD Setup Shaders
    ComputeBuffer Pbuffer;
    ComputeBuffer Vbuffer;
    ComputeBuffer Wbuffer;
    ComputeBuffer Fbuffer;
    ComputeBuffer Tbuffer;

    int threadtimes;

    int kernel;
    #endregion

    #region Distance Constraint Parameters
    public struct constraint
    {
        public uint p1, p2, p3;
    }
    constraint[] constraints;

    //Set constraint positions

    Vector3[] lp1, lp2, lp3;

    float[] stiffness;
    Vector3 stiffl1, stiffl2, stiffl3;

    public ComputeShader distanceshader;

    //Constraint Buffers
    ComputeBuffer lengthp1, lengthp2, lengthp3;
    ComputeBuffer constraintbuffer;
    ComputeBuffer stiffnessbuffer;

    int distancethread;

    int distancekernel;

    #endregion

    #region Self Collision Parameters


    /*//Set constraint positions

    Vector3[] lp1, lp2, lp3;

    float[] stiffness;
    Vector3 stiffl1, stiffl2, stiffl3;

    public ComputeShader distanceshader;

    //Constraint Buffers
    ComputeBuffer lengthp1, lengthp2, lengthp3;
    ComputeBuffer constraintbuffer;
    ComputeBuffer stiffnessbuffer;

    int distancethread;

    int distancekernel;*/

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
        threadtimes = gridLength / 16 + 1;
        Debug.Log("threadtimes = " + threadtimes);

        pos = mesh.vertices;
        triangles = mesh.triangles;

        //create weights
        for (int x = 0; x < gridLength; x++)
        {

            m[x] = 1f;
            weights[x] = 1f;//1f / m[v];

        }

        weights[0] = 0f;
        //weights[1] = 0f;
        //weights[110] = 0f;

        //weights[50] = 0f;
        for (int x = 0; x < pos.Length; x++)
        {
            Debug.Log(x + " " + pos[x]);
        }
        fors[0] = forces; // assigning force to the array for buffer
                          //reset vertex tracker 


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

        kernel = shader.FindKernel("trydispatch");

        Fbuffer.SetData(fors);
        Wbuffer.SetData(weights);

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

        constraints = new constraint[tricount];

        //Creating Triangle list array of Vectors (eg: TriangleList[0] = (0,1,2); Which would be the the first three positions indices of the first triangle ) for iteration purposes, So we can go through each traingle of the Mesh Quads Easily (Quads being the the Square positions of the mesh)

        for (int x = 0; x < tricount; x++)///////////////
        {
            constraints[x].p1 = (uint)triangles[x * 3 + 0]; //Can be subjected to edit
            constraints[x].p2 = (uint)triangles[x * 3 + 1];
            constraints[x].p3 = (uint)triangles[x * 3 + 2];

            stiffl1 = pos[constraints[x].p2] - pos[constraints[x].p1];
            stiffl2 = pos[constraints[x].p3] - pos[constraints[x].p2];
            stiffl3 = pos[constraints[x].p1] - pos[constraints[x].p3];

            stiffness[x * 3 + 0] = stiffl1.magnitude;
            stiffness[x * 3 + 1] = stiffl2.magnitude;
            stiffness[x * 3 + 2] = stiffl3.magnitude;

            //Debug.Log(x + " triangle " + constraintp1[x] + " " + constraintp2[x] + " " + constraintp3[x]);

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
        constraintbuffer = new ComputeBuffer(tricount, 4 * 3);
        stiffnessbuffer = new ComputeBuffer(gridTriLength, 4);
        /////////////////////////////////////////////////////// 

        distancekernel = distanceshader.FindKernel("DistanceCompute");

        constraintbuffer.SetData(constraints);
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

        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("weights"), Wbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("pos"), Pbuffer);
        distanceshader.SetInt(Shader.PropertyToID("loop"), loopdistance);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("stiffness"), stiffnessbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("constraints"), constraintbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp1"), lengthp1);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp2"), lengthp2);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp3"), lengthp3);

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

    #region Self Collision Constraint
    void ForSelfCollisionConstraints()
    {

        constraints = new constraint[tricount];

        //Creating Triangle list array of Vectors (eg: TriangleList[0] = (0,1,2); Which would be the the first three positions indices of the first triangle ) for iteration purposes, So we can go through each traingle of the Mesh Quads Easily (Quads being the the Square positions of the mesh)

        for (int x = 0; x < tricount; x++)///////////////
        {
            constraints[x].p1 = (uint)triangles[x * 3 + 0]; //Can be subjected to edit
            constraints[x].p2 = (uint)triangles[x * 3 + 1];
            constraints[x].p3 = (uint)triangles[x * 3 + 2];

            stiffl1 = pos[constraints[x].p2] - pos[constraints[x].p1];
            stiffl2 = pos[constraints[x].p3] - pos[constraints[x].p2];
            stiffl3 = pos[constraints[x].p1] - pos[constraints[x].p3];

            stiffness[x * 3 + 0] = stiffl1.magnitude;
            stiffness[x * 3 + 1] = stiffl2.magnitude;
            stiffness[x * 3 + 2] = stiffl3.magnitude;

            //Debug.Log(x + " triangle " + constraintp1[x] + " " + constraintp2[x] + " " + constraintp3[x]);

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
        constraintbuffer = new ComputeBuffer(tricount, 4 * 3);
        stiffnessbuffer = new ComputeBuffer(gridTriLength, 4);

        distancekernel = distanceshader.FindKernel("DistanceCompute");

        constraintbuffer.SetData(constraints);
        stiffnessbuffer.SetData(stiffness);

        Wbuffer.SetData(weights);
    }

    void SelfCollisionConstraint()
    {
        Wbuffer.SetData(weights);
        Pbuffer.SetData(pos);
        lengthp1.SetData(lp1);
        lengthp2.SetData(lp2);
        lengthp3.SetData(lp3);

        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("weights"), Wbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("pos"), Pbuffer);
        distanceshader.SetInt(Shader.PropertyToID("loop"), loopdistance);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("stiffness"), stiffnessbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("constraints"), constraintbuffer);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp1"), lengthp1);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp2"), lengthp2);
        distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lp3"), lengthp3);

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
