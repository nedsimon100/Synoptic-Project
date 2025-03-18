using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{

    public Terrain T1;
    public TerrainCollider TC1;

    public Terrain T2;
    public TerrainCollider TC2;

    public int chunkSize;
    public float depth;

    public int layer;

    public Vector3[] usedMaps;
    public void drawMap(float[,] heightData, Texture2D FloorTexture, float[,] heightData2,  bool[,] holeArray, Texture2D Texture2)
    {
        TerrainData td = new TerrainData();
        td.heightmapResolution = chunkSize + 1;
        td.size = new Vector3(chunkSize, depth, chunkSize);
        T1.terrainData = td;
        TerrainData td2 = new TerrainData();
        td2.heightmapResolution = chunkSize + 1;
        td2.size = new Vector3(chunkSize, depth, chunkSize);
        T2.terrainData = td2;
        
        T2.terrainData.SetHeights(0, 0, heightData2);
        T2.terrainData.SetHoles(0, 0, holeArray);

        T1.terrainData.SetHeights(0, 0, heightData);

        ApplyTextureToTerrain(FloorTexture,T1);
        ApplyTextureToTerrain(Texture2, T2);

        TC1.terrainData = T1.terrainData;

        if(layer == 0)
        {
            TC2.enabled = false;
        }
        else
        {
            TC2.terrainData = T2.terrainData;
        }
    }


    private void ApplyTextureToTerrain(Texture2D texture, Terrain terrain)
    {
        if (terrain.materialTemplate == null)
        {
            Material terrainMaterial = new Material(Shader.Find("Standard"));
            terrain.materialTemplate = terrainMaterial;
        }
        TerrainLayer terrainLayer = new TerrainLayer();
        terrainLayer.diffuseTexture = texture;
        terrainLayer.tileSize = new Vector2(chunkSize, chunkSize);

        terrain.terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };
    }
}
