using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothPlane8 : MonoBehaviour
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
    public ComputeShader updateshader;

    //Data for PBD Setup Shaders
    ComputeBuffer Pbuffer;
    ComputeBuffer Plastbuffer;
    ComputeBuffer Vbuffer;
    ComputeBuffer Wbuffer;
    ComputeBuffer Fbuffer;
    ComputeBuffer Tbuffer;

    int threadtimes;

    int kernel;
    int kernelupdate;

    #endregion

    #region Graph Colouring Parameters

    int colorcount = 0;
    int[] Colors;
    int[] ColoredTriangles;
    int[] ColoredTriangleNumbering;
    int[] SecondColoredTriangleNumbering;
    int[] Coloredsort;

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
    float maxstiff;
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

    Vector3 p1, p2, p3, p4,
    plast1, plast2, plast3, plast4,
    delp1, delp2, delp3, delp4,
    n, n1, n2, n3, nlast, nlast1, nlast2, nlast3;
    float h, h1, h2, h3, hlast, hlast1, hlast2, hlast3;
    Vector3[] poslast;

    Color[] collided;

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

        ForSelfCollisionConstraints();

        //UpdateMesh();


    }

    // Update is called once per frame
    void Update()
    {
        //Dispatching For 

        velocitydispatch();
        
        //Self Constraint

        SelfCollisionConstraint();

        //Distance Constraint

        DistanceConstraint();


        //velocity update

        velocityupdate();

        //DistanceConstraint();

        /*for (int x = 0; x < pos.Length; x++)
        {
            Debug.Log(x + " " + pos[x]); ///////////////////////////////////////////////////////////////////////////////////////
        }*/

        mesh.Clear();
        //mesh.colors = collided;
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

        Debug.Log("no of points :" + gridLength);

        //Set array sizes
        pos = new Vector3[gridLength];
        vel = new Vector3[gridLength];
        fors = new Vector3[1];
        triangles = new int[gridTriLength];
        m = new float[gridLength];
        weights = new float[gridLength];
        stiffness = new float[gridTriLength];
        threadtimes = gridLength / 32 + 1;
        Debug.Log("threadtimes = " + threadtimes);

        pos = mesh.vertices;
        triangles = mesh.triangles;

        //create weights 
        for (int x = 0; x < gridLength; x++)
        {

            m[x] = 1f;
            weights[x] = 0.5f;//1f / m[v];

        }

        //weights[60] = 0f;
        weights[0] = 0f;
        //weights[6] = 2f;
        //weights[110] = 0f;
        //weights[50] = 0f;

        fors[0] = forces; // assigning force to the array for buffer
                          //reset vertex tracker 

        /*for (int x = 0; x < gridLength; x++)
        {
            Debug.Log(x + " " + pos[x]);       ////////////////////////////**********PRINT
        }*/



        tricount = (triangles.Length) / 3;/////////////////////
        Debug.Log("No of triangles :" + tricount);


        // Initial settings
        mesh.Clear();
        vel = pos;

        //Linking HLSL

        Pbuffer = new ComputeBuffer(gridLength, 3 * 4);
        Plastbuffer = new ComputeBuffer(gridLength, 3 * 4);
        Vbuffer = new ComputeBuffer(gridLength, 3 * 4);
        Fbuffer = new ComputeBuffer(1, 3 * 4);
        Wbuffer = new ComputeBuffer(gridLength, 4);

        kernel = shader.FindKernel("trydispatch");
        kernelupdate = updateshader.FindKernel("tryupdate");

        Fbuffer.SetData(fors);
        Wbuffer.SetData(weights);

        /*mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();*/
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

        colorcount += 3;
        Debug.Log("taken color count = " + colorcount);  ////////////////////////////**********PRINT

        int colorend = 0;

        for (int x = 0; x < tricount; x++)
        {
            for (int color = colorend; color < colorcount * tricount; color++)
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

                    colorend = color + 1;

                    break;
                }

            }

            //colorbegin = secondcolorindex ;
            //Debug.Log("*****************************" + colorbegin);

        }

        int colorbegin = 0;
        for (int colorpos = 1; colorpos <= colorcount; colorpos++)
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

        for (int i = 0; i < tricount; i++)
        {
            if (ColoredTriangleNumbering[i] == 0)
            {
                zero += 1;
            }
        }
        Debug.Log("number of zero colored triangles = " + zero);

        /*for (int j = 0; j < gridTriLength; j++)
        {
            Debug.Log("Triangle values " + j + " : " + triangles[j]);
        }
        for (int j = 0; j < gridTriLength; j++)
        {
            Debug.Log("Colored Triangle values " + j + " : " + ColoredTriangles[j]);
        }

        for (int j = 0; j < tricount; j++)
        {
            Debug.Log("Colored Triangle numbering " + j + " : " + ColoredTriangleNumbering[j]);
        }*/


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        if (zero == 0)
        {
            triangles = ColoredTriangles;
        }
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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

        //Vector3 force = ()

        //pos[0].x += -0.1f;
        //pos[0].z += 0.11f;
    }

    void velocityupdate()
    {
        Pbuffer.SetData(pos);
        Plastbuffer.SetData(poslast);
        Vbuffer.SetData(vel);

        updateshader.SetBuffer(kernelupdate, Shader.PropertyToID("pos"), Pbuffer);
        updateshader.SetBuffer(kernelupdate, Shader.PropertyToID("poslast"), Plastbuffer);
        updateshader.SetBuffer(kernelupdate, Shader.PropertyToID("vel"), Vbuffer);
        updateshader.SetBuffer(kernelupdate, Shader.PropertyToID("weights"), Wbuffer);
        updateshader.SetFloat(Shader.PropertyToID("timer"), timer);
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
            if (stiffness[x * 3 + 0] > maxstiff)
            {
                maxstiff = stiffness[x * 3 + 0];
            }
            if (stiffness[x * 3 + 1] > maxstiff)
            {
                maxstiff = stiffness[x * 3 + 1];
            }
            if (stiffness[x * 3 + 2] > maxstiff)
            {
                maxstiff = stiffness[x * 3 + 2];
            }
        }
        Debug.Log("maximum stiffness : " + maxstiff);

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

        /*for (int x = 0; x < tricount; x++)
        {
            Debug.Log(x + " " + lp1[x] + " " + lp2[x] + " " + lp3[x]);/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Debug.Log("triangle " + x + " delptap1 " + dp1[0] + ", deltap2 " + dp2[0]);
        }*/
        Pbuffer.GetData(pos);

    }
    #endregion

    #region Self Collision Constraint
    void ForSelfCollisionConstraints()
    {
        /*h = new float[3 * 4];
        hlast = new float[3 * 4];*/
        poslast = new Vector3[gridLength];
        collided = new Color[gridLength];
        System.Array.Copy(pos, 0, poslast, 0, gridLength);
    }

    void SelfCollisionConstraint()
    {
        float k; Vector3 v, w, i;
        Vector3 pm1, pm2, pm3, pr1, pr2, pr3, pi1, pi2, pi3;
        float barycentric1, barycentric2, barycentric3;

        for (int iter = 0; iter < 1; iter++)
        {
            int detectioncount = 0;
            for (int y = 0; y < gridLength; y++)
            {
                for (int x = 0; x < tricount; x++)
                {
                    if ((y != triangles[3 * x + 0]) && (y != triangles[3 * x + 1]) && (y != triangles[3 * x + 2]))
                    {
                        //Debug.Log("x = " + x + ", y = " + y);
                        p1 = pos[y];
                        p2 = pos[triangles[3 * x + 0]];
                        p3 = pos[triangles[3 * x + 1]];
                        p4 = pos[triangles[3 * x + 2]];

                        pr1 = p3 - p2; pr2 = p4 - p3; pr3 = p2 - p4;

                        plast1 = poslast[y];////////////////////////////////////////////////////////////////
                        plast2 = poslast[triangles[3 * x + 0]];/////////////////////////////////////////////
                        plast3 = poslast[triangles[3 * x + 1]];/////////////////////////////////////////////
                        plast4 = poslast[triangles[3 * x + 2]];/////////////////////////////////////////////
                        nlast = Vector3.Normalize(Vector3.Cross(plast3 - plast2, plast4 - plast2));/////////
                        
                        

                        hlast = Vector3.Dot(plast1 - plast2, nlast);////////////////////////////////////////

                        float denom = 0;

                        k = 0f;
                        v = p1 - plast1;
                        w = p2 - plast1;
                        n = Vector3.Cross(pr1, -pr3);
                        float length = n.magnitude;
                        n = Vector3.Normalize(n);
                        k = Vector3.Dot(v, n) / Vector3.Dot(w, n);
                        i = plast1 + Vector3.ProjectOnPlane(v, n);
                        h = Vector3.Dot(p1 - p2, n);

                        pi1 = i - p2; pi2 = i - p3; pi3 = i - p4;

                        Vector3 t = (maxstiff/2f) * Vector3.Normalize(plast1 - i);
                        //Vector3 collisionvector = i - p1;
                        float collisiondistance = h < 0f ? -h : h; ;

                        pm1 = pr1 + (Vector3.Project(-pr1, Vector3.Normalize(pr2)));
                        pm2 = pr2 + (Vector3.Project(-pr2, Vector3.Normalize(pr3)));
                        pm3 = pr3 + (Vector3.Project(-pr3, Vector3.Normalize(pr1)));
                        //pm2 = pr2 - (Vector3.Dot(Vector3.Normalize(-pr3), pr2)) * Vector3.Normalize(-pr3);
                        //pm1 = pr1 - (Vector3.Dot(Vector3.Normalize(-pr2), pr1)) * Vector3.Normalize(-pr2);

                        barycentric1 = 1f - Vector3.Dot(pi1, pm1) / Vector3.Dot(pr1, pm1);
                        barycentric2 = 1f - Vector3.Dot(pi2, pm2) / Vector3.Dot(pr2, pm2);
                        barycentric3 = 1f - Vector3.Dot(pi3, pm3) / Vector3.Dot(pr3, pm3);

                        //Debug.Log(barycentric1 + "  " + barycentric2);

                        //if ((hlast / h < 0f) && (howfar1.magnitude <= maxstiff) && (howfar2.magnitude <= maxstiff) && (howfar3.magnitude <= maxstiff))//(h1 < 0f) && (h2 < 0f) && (h3 < 0f))//&& (hlast1 > 0f) && (hlast2 > 0f) && (hlast3 > 0f))//&& (howfar1.magnitude <= maxstiff) && (howfar2.magnitude <= maxstiff) && (howfar3.magnitude <= maxstiff))//&& (h1 < 0f) && (h2 < 0f) && (h3 < 0f))//&& (hlast1 > 0f) && (hlast2 > 0f) && (hlast3 > 0f)) //
                        //if ( f < 0 )
                        if (barycentric2 >= 0f && barycentric2 <= 1f && barycentric1 >= 0f && barycentric1 <= 1f && barycentric3 >= 0f && barycentric3 <= 1f )//&& collisiondistance < 0.4f )// & (hlast / h < 0f))
                        {
                            detectioncount++;

                            if(collisiondistance < 0.6f && k < 1f )
                            {
                                float thickness = h;
                                float scale = -0.8f;
                                delp1 = n;
                                delp3 = (Vector3.Cross(p4 - p2, p1 - p2) + Vector3.Cross(n, p4 - p2) * h) / length;
                                delp4 = -1f * (Vector3.Cross(p3 - p2, p1 - p2) + Vector3.Cross(n, p3 - p2) * h) / length;
                                delp2 = -delp1 - delp3 - delp4;

                                denom = weights[y] * delp1.sqrMagnitude + weights[triangles[3 * x + 0]] * delp2.sqrMagnitude + weights[triangles[3 * x + 1]] * delp3.sqrMagnitude + weights[triangles[3 * x + 2]] * delp4.sqrMagnitude;

                                //Debug.Log("denom = " + 1 / denom);



                                pos[y] -= scale * weights[y] * thickness * delp1 / denom;
                                pos[triangles[3 * x + 0]] -= scale * weights[triangles[3 * x + 0]] * thickness * delp2 / denom;
                                pos[triangles[3 * x + 1]] -= scale * weights[triangles[3 * x + 1]] * thickness * delp3 / denom;
                                pos[triangles[3 * x + 2]] -= scale * weights[triangles[3 * x + 2]] * thickness * delp4 / denom;
                            }

                            else if(k > 1f)
                            {
                                float thickness = h;
                                float scale = 2f;
                                delp1 = n;
                                delp3 = (Vector3.Cross(p4 - p2, p1 - p2) + Vector3.Cross(n, p4 - p2) * h) / length;
                                delp4 = -1f * (Vector3.Cross(p3 - p2, p1 - p2) + Vector3.Cross(n, p3 - p2) * h) / length;
                                delp2 = -delp1 - delp3 - delp4;

                                denom = weights[y] * delp1.sqrMagnitude + weights[triangles[3 * x + 0]] * delp2.sqrMagnitude + weights[triangles[3 * x + 1]] * delp3.sqrMagnitude + weights[triangles[3 * x + 2]] * delp4.sqrMagnitude;

                                //Debug.Log("denom = " + 1 / denom);



                                pos[y] -= scale * weights[y] * thickness * delp1 / denom;
                                pos[triangles[3 * x + 0]] -= scale * weights[triangles[3 * x + 0]] * thickness * delp2 / denom;
                                pos[triangles[3 * x + 1]] -= scale * weights[triangles[3 * x + 1]] * thickness * delp3 / denom;
                                pos[triangles[3 * x + 2]] -= scale * weights[triangles[3 * x + 2]] * thickness * delp4 / denom;
                            }

                            /*pos[y] += -h * n;
                            pos[triangles[3 * x + 0]] -= 2 * barycentric1 * -h * n;
                            pos[triangles[3 * x + 1]] -= 2f * barycentric2 * -h * n;
                            pos[triangles[3 * x + 2]] -= 2f * barycentric3 * -h * n;*/

                            /*pos[y] += t + i - p1;
                            pos[triangles[3 * x + 0]] -= barycentric1 * t ;
                            pos[triangles[3 * x + 1]] -= barycentric2 * t;
                            pos[triangles[3 * x + 2]] -= barycentric3 * t;

                            /*delp3 = (Vector3.Cross(p4 - p2, p1 - p2) + Vector3.Cross(n, p4 - p2) * Vector3.Dot(n, p1 - p2)) / Vector3.Cross(p3 - p2, p4 - p2).magnitude;
                            
                            delp4 = (Vector3.Cross(p3 - p2, p1 - p2) + Vector3.Cross(n, p3 - p2) * Vector3.Dot(n, p1 - p2)) / Vector3.Cross(p3 - p2, p4 - p2).magnitude;*/
                            //float thickness = h < 0f ? -h : h;
                            

                            /*svel[y] -= 2 * weights[triangles[y]] * delp1 / denom;
                            vel[triangles[3 * x + 0]] -= 2 * weights[triangles[3 * x + 0]] * delp2 / denom;
                            vel[triangles[3 * x + 1]] -= 2 * weights[triangles[3 * x + 1]] * delp3 / denom;
                            vel[triangles[3 * x + 2]] -= 2 * weights[triangles[3 * x + 2]] * delp4 / denom; */

                        }
                    }
                }

            }
            /*if (detectioncount > 0)
            {
                Debug.Log("h = " + h + " hlast = " + hlast + " detection = " + detectioncount);
            }*/

            System.Array.Copy(pos, 0, poslast, 0, gridLength);
        }


        
    }
    #endregion
}
