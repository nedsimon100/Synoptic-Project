using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public GameObject chunkPrefab;


    public int chunkSize;
    public int layer;

    public Terrain terrain;
    public float[] heightData;
    public float depth = 100;
    private Texture2D FloorTexture;
    public Gradient floorColour;

    public float seaLevel;
    public void GenerateMap(float[] heightData)
    {
        terrain = this.GetComponent<Terrain>();
        TerrainData td = new TerrainData();
        td.heightmapResolution = chunkSize + 1;
        td.size = new Vector3(chunkSize, depth, chunkSize);
        terrain.terrainData = td;
        this.GetComponent<Terrain>().terrainData = terrain.terrainData;
        if (terrain.materialTemplate == null)
        {
            Material terrainMaterial = new Material(Shader.Find("Standard"));
            terrain.materialTemplate = terrainMaterial;
        }


        float[,] heights = new float[terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution];
        // Debug.Log(chunkSize);
        // Debug.Log(terrain.terrainData.size);
        // Debug.Log(terrain.terrainData.heightmapResolution);
        FloorTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                if (heightData[y * (chunkSize + 1) + x] < seaLevel)
                {
                    heights[x, y] = (seaLevel - 0.05f) + (heightData[y * (chunkSize + 1) + x] / (seaLevel / 0.05f));
                }
                else
                {
                    heights[x, y] = heightData[y * (chunkSize + 1) + x];
                }
                FloorTexture.SetPixel(y, x, floorColour.Evaluate(heightData[y * (chunkSize + 1) + x]));
            }
        }

        FloorTexture.Apply();
        terrain.terrainData.SetHeights(0, 0, heights);
        ApplyTextureToTerrain(FloorTexture);
    }

    private void ApplyTextureToTerrain(Texture2D texture)
    {
        TerrainLayer terrainLayer = new TerrainLayer();
        terrainLayer.diffuseTexture = texture;
        terrainLayer.tileSize = new Vector2(chunkSize, chunkSize);

        terrain.terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };
    }
}
