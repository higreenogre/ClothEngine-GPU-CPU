using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour
{
    Mesh mesh;
    Vector3[] pos;
    Vector3[] vel;
    int[] triangles;

    //grid settings
    public float cellsize = 1;
    public Vector3 gridOffset;
    public int gridSize;
    public Vector3 forces;

    // Start is called before the first frame update
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Start()
    {
        MakeProceduralGrid();
        //UpdateMesh();
    }

    void MakeProceduralGrid()
    {
        //Set array sizes
        pos = new Vector3[(gridSize + 1) * (gridSize + 1)];
        triangles = new int[gridSize * gridSize * 6];

        //Set Tracker
        int v = 0;
        int t = 0;

        //Set vertex Offset
        float vertexOffset = cellsize * 0.5f;

        //create vertex grid
        for (int x = 0; x <= gridSize; x++)
        {
            for (int y = 0; y <= gridSize; y++)
            {
                pos[v] = new Vector3((x * cellsize) - vertexOffset, 0, (y * cellsize) - vertexOffset);
                v++;
            }
        }
        
        //reset vertex tracker 
        v = 0;
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
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
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    void UpdateMesh()
    {

        mesh.Clear();
        mesh.vertices = pos;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
