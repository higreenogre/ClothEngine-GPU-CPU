using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Shrink7 : MonoBehaviour
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

    public uint[] constraintp1, constraintp2, constraintp3;

    //Set constraint positions

    Vector3[] lp1, lp2, lp3;
    Vector3[] dp1, dp2;
    Vector3 deltap;
    Vector3 deltap1;
    Vector3 deltap2;

    ComputeBuffer lengthp1, lengthp2, lengthp3;
    ComputeBuffer constraintp1buffer, constraintp2buffer, constraintp3buffer;
    

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

    #region Graph Colouring Parameters

    int colorcount = 0;
    int[] Colors;
    int[] ColoredTriangles;
    int[] ColoredTriangleNumbering;
    int[] SecondColoredTriangleNumbering;
    int[] Coloredsort;
    int gridLength, gridTriLength;
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
        GraphColoring();
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

        //Distance Constraint ////////////////////////////////////////////////////////////////////////////

        DistanceConstraintCPU();

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
                m[v] = 1f;
                weights[v] = 1f / m[v];
                v++;
            }
        }
        /*for (int x = 0; x < pos.Length; x++)
        {
            Debug.Log(x + " " + pos[x]);
        }*/
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
        //Debug.Log(tricount);
        weights[0] = 0f;/////////////////////////////////////////////////////////////////////////////////////////////////////////



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
        gridLength = mesh.vertices.Length;
        gridTriLength = mesh.triangles.Length;

    }
    #endregion

    #region Graph Coloring

    void GraphColoring()
    {
        ////////////////////////////////////////////////////////////////
        for (int x = 0; x < gridLength; x++)
        {
            int count = 0;
            for (int y = 0; y < tricount; y++)
            {
                for (int z = 0; z < 3; z++)
                {
                    if (x == triangles[3 * y + z])
                    {
                        count += 1;
                    }
                }
            }
            if (count > colorcount)
            {
                colorcount = count;
            }
        }
        Debug.Log("color count = " + colorcount);  ////////////////////////////**********PRINT
        ////////////////////////////////////////////////////////////////

        Colors = new int[colorcount];
        ColoredTriangles = new int[gridTriLength];
        ColoredTriangleNumbering = new int[tricount];
        SecondColoredTriangleNumbering = new int[tricount];
        Coloredsort = new int[gridTriLength];

        int colorend = 0;

        for (int x = 0; x < tricount; x++)
        {
            for (int color = colorend; color < colorcount*tricount; color++ )
            {
                int count = 0;
                for (int check = 0; check < x + 1; check++)
                {
                    if (ColoredTriangleNumbering[check] == color % colorcount + 1)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            for (int u = 0; u < 3; u++)
                            {
                                if (triangles[3 * x + y] == triangles[3 * check + u])
                                {
                                    count += 1;
                                }
                            }
                        }
                    }
                }

                if (count == 0)
                {

                    ColoredTriangleNumbering[x] = color % colorcount + 1;
                    //SecondColoredTriangleNumbering[secondcolorindex] = color;

                    colorend = color+1;

                    break;
                }
               
            }

            //colorbegin = secondcolorindex ;
            //Debug.Log("*****************************" + colorbegin);

        }

        int colorbegin = 0;
        for (int colorpos = 1; colorpos <= colorcount ; colorpos++)
        {
            for (int x = 0; x < tricount; x++)
            {
                if (ColoredTriangleNumbering[x] == colorpos)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        ColoredTriangles[3 * colorbegin + y] = triangles[3 * x + y];
                    }
                    colorbegin += 1;
                }
            }
        }

        int zero = 0;

        for (int i =0; i < tricount; i++)
        {
            if (ColoredTriangleNumbering[i] == 0)
            {
                zero += 1;
            }
        }
        Debug.Log("number of zero triangles = " + zero);
        /*for (int j = 0; j < gridTriLength; j++)
        {
            Debug.Log("Triangle values " + j + " : " + triangles[j]);
        }
        for (int j = 0; j < gridTriLength; j++)
        {
            Debug.Log("Colored Triangle values " + j + " : " + ColoredTriangles[j]);
        }*/

        for (int j = 0; j < tricount; j++)
        {
            Debug.Log("Colored Triangle numbering " + j + " : " + ColoredTriangleNumbering[j]);
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        triangles = ColoredTriangles;
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
        int t = 0;
        for (int x = 0; x < tricount; x++)///////////////
        {
            constraintp1[x] = (uint)triangles[x * 3 + 0]; //Can be subjected to edit
            constraintp2[x] = (uint)triangles[x * 3 + 1];
            constraintp3[x] = (uint)triangles[x * 3 + 2];
            Debug.Log(x + " triangle " + constraintp1[x] + " " + constraintp2[x] + " " + constraintp3[x]);

            t += 3;
        }


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
        ///////////////////////////////////////////////////////
        //deltap1 = new ComputeBuffer(1, 4 * 3);
        //deltap2 = new ComputeBuffer(1, 4 * 3);



        //Trilistbuffer.SetData(Trilist);
        distancekernel = distanceshader.FindKernel("DistanceCompute");
        //distancekernel = 0;
        //Linking Constraint Buffer
        constraintp1buffer.SetData(constraintp1);
        constraintp2buffer.SetData(constraintp2);
        constraintp3buffer.SetData(constraintp3);

        Wbuffer.SetData(weights);
    }

    void DistanceConstraintCPU()
    {
        int j;
        for (int iter=0; iter<2; iter++)
        {
            for (int i=0; i< tricount*3; i++)
            {
                Pbuffer.SetData(pos);
                lengthp1.SetData(lp1);
                lengthp2.SetData(lp2);
                lengthp3.SetData(lp3);
                //deltap1.SetData(dp1);
                //deltap2.SetData(dp2);


                //Trilistbuffer.SetData(Trilist);

                //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("weight"), Wbuffer);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("pos"), Pbuffer);
                //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilist"), Trilistbuffer);
                //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Iterateconstraint"), DeltaPbuffer);
                //distanceshader.SetInt(Shader.PropertyToID("TriCount"), tricount);
                //distanceshader.SetFloat(Shader.PropertyToID("stiffness"), stiffness);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilistp1"), constraintp1buffer);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilistp2"), constraintp2buffer);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("Trilistp3"), constraintp3buffer);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lengthp1"), lengthp1);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lengthp2"), lengthp2);
                distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("lengthp3"), lengthp3);
                //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("deltap1"), deltap1);
                //distanceshader.SetBuffer(distancekernel, Shader.PropertyToID("deltap2"), deltap2);


                distanceshader.Dispatch(distancekernel, threadtimes, 1, 1); //Execution

                ///////////////////////////////////////////////////////////////
                lengthp1.GetData(lp1);
                lengthp2.GetData(lp2);
                lengthp3.GetData(lp3);

                if (i%3 == 0)
                {
                    j=(i)/ 3;
                    deltap = lp1[j];
                    deltap1 = (weights[constraintp1[j]] / (weights[constraintp1[j]] + weights[constraintp2[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    deltap2 = (weights[constraintp2[j]] / (weights[constraintp1[j]] + weights[constraintp2[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    pos[constraintp1[j]] += deltap1;
                    pos[constraintp2[j]] -= deltap2;
                }
                else if (i % 3 == 1)
                {
                    j = (i-1) / 3;
                    deltap = lp2[j];
                    deltap1 = (weights[constraintp3[j]] / (weights[constraintp3[j]] + weights[constraintp2[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    deltap2 = (weights[constraintp2[j]] / (weights[constraintp3[j]] + weights[constraintp2[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    pos[constraintp2[j]] += deltap1;
                    pos[constraintp3[j]] -= deltap2;
                }
                else
                {
                    j = (i - 2) / 3;
                    deltap = lp3[j];
                    deltap1 = (weights[constraintp3[j]] / (weights[constraintp3[j]] + weights[constraintp1[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    deltap2 = (weights[constraintp1[j]] / (weights[constraintp3[j]] + weights[constraintp1[j]])) * (deltap.magnitude - stiffness) * deltap.normalized;
                    pos[constraintp3[j]] += deltap1;
                    pos[constraintp1[j]] -= deltap2;
                }

            }
            
        }
        

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
