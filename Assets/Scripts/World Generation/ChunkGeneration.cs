
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;


public class ChunkGeneration : MonoBehaviour
{
    public int chunkSize;
    public int layer;

    public Noise noise;
    public Terrain terrain;
    public float[] heightData;
    public float depth = 100;
    public Texture2D debugTexture;
    private void Start()
    {
        heightData = new float[(chunkSize + 1) * (chunkSize + 1)];
        noise.GetMap(this);
        terrain = this.GetComponent<Terrain>();
        TerrainData td = new TerrainData();
        td.heightmapResolution = chunkSize +1;
        td.size = new Vector3(chunkSize, depth, chunkSize);
        terrain.terrainData = td;
        this.GetComponent<Terrain>().terrainData = terrain.terrainData;
        if (terrain.materialTemplate == null)
        {
            terrain.materialTemplate = new Material(Shader.Find("Nature/Terrain/Standard"));
        }
        
        
        float[,] heights = new float[terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution];
       // Debug.Log(chunkSize);
       // Debug.Log(terrain.terrainData.size);
       // Debug.Log(terrain.terrainData.heightmapResolution);
        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                heights[x, y] = heightData[y * (chunkSize + 1) + x];

            }
        }
        terrain.terrainData.SetHeights(0, 0, heights);
        
    }
}