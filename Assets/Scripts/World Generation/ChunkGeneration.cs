
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;


public class ChunkGeneration : MonoBehaviour
{
    public int chunkSize;
    public int layer;
    public int workLayer;
    public Terrain terrain;

    public float depth = 100;
    public Vector3[] usedMaps;
    
    public void drawMap(float[,] heightData, Texture2D FloorTexture)
    {
        
      

        terrain = this.GetComponent<Terrain>();
        TerrainData td = new TerrainData();
        td.heightmapResolution = chunkSize +1;
        td.size = new Vector3(chunkSize, depth, chunkSize);
        terrain.terrainData = td;
        this.GetComponent<Terrain>().terrainData = terrain.terrainData;
        if (terrain.materialTemplate == null)
        {
            Material terrainMaterial = new Material(Shader.Find("Standard"));
            terrain.materialTemplate = terrainMaterial;
        }


        terrain.terrainData.SetHeights(0, 0, heightData);
        ApplyTextureToTerrain(FloorTexture);

        this.GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
    }


    private void ApplyTextureToTerrain(Texture2D texture)
    {
        TerrainLayer terrainLayer = new TerrainLayer();
        terrainLayer.diffuseTexture = texture;
        terrainLayer.tileSize = new Vector2(chunkSize, chunkSize);

        terrain.terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };
    }
}