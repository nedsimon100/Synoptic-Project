using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using static WorldGenerator;
using Unity.VisualScripting;
using System.Linq;
public class WorldGenerator : MonoBehaviour
{
    [Header("Chunk Settings")]

    public int maxChunksPerFrame = 2;
    public int chunkSize = 10;
    public int LoadDist = 5;
    public int deletionDist = 10;
    public GameObject Player;
    public int seed;
    public GameObject ChunkPrefab;
    [HideInInspector]
    public List<GameObject> LoadedChunks = new List<GameObject>();
    [HideInInspector]
    public Vector3 LastLoadPoint = Vector3.zero;

    [HideInInspector]
    public int Layer;
    [HideInInspector]
    public float layerOffset;
    public Map FoliageDensityMap;
    public Map baseWorldMap;
    [Range(0f, 1f)]
    public float baseWorldSeaLevel;
    [Range(0f, 1f)]
    public float riverAndLakeDensity;
    [Header("Compute Shader")]

    public ComputeShader compNoise;
    [HideInInspector]
    public int threadCountAndMapMult = 16;
    private int[] mapItterations;

    [Header("Layer Settings")]
    [Range(4, 1000)]
    public int mapsize;
    [Range(0f, 10f)]
    public float sharpness;
    public MapLayer LandSettings;

    //public GameObject debugMap;
    [System.Serializable]
    public class worldObjects
    {
        public GameObject obj;
        public float minHeight;
        public float maxHeight;
        public float minWidth;
        public float maxWidth;
        public int spawnChance;
    }
    [System.Serializable]
    public class Map
    {
        [HideInInspector]
        public Vector3 position;
        [HideInInspector]
        public Vector3 offset;
        [HideInInspector]

        public float[] heightData;

        [Range(1, 10000)]
        public float scale;
        
        [Range(0f, 1000f)]
        public float maxHeight;
        [Range(0f, 20f)]
        public float gradientDampening;
        public Map(Map copyFrom)
        {
            scale = copyFrom.scale;
            maxHeight = copyFrom.maxHeight;
            gradientDampening = copyFrom.gradientDampening;
            heightData = copyFrom.heightData;
        }

    }

    [System.Serializable]
    public class Biome
    {
        public Biome(Biome copyFrom)
        {
            tempreture = copyFrom.tempreture;
            humidity = copyFrom.humidity;
            heightColour = copyFrom.heightColour;
            seaFloorColour = copyFrom.seaFloorColour;
            seaColour = copyFrom.seaColour;
            heightMult = copyFrom.heightMult;
            
            foreach (HeightLayer GO in copyFrom.heightLandSettings)
            {
                heightLandSettings.Add(new HeightLayer(GO));
            }
        }
        public float tempreture;
        public float humidity;
        public float heightMult;
        public Gradient heightColour;
        public Gradient seaColour;
        public Gradient seaFloorColour;
        public List<HeightLayer> heightLandSettings = new List<HeightLayer>();
    }
    [System.Serializable]
    public class HeightLayer
    {
        public HeightLayer(HeightLayer copyFrom)
        {
            
            NormalMap = copyFrom.NormalMap;
            TextureScale = copyFrom.TextureScale;
            DiffuseTexture = copyFrom.DiffuseTexture;
            foliageSpawnDensity = copyFrom.foliageSpawnDensity;
            weight = copyFrom.weight;
            MinHeight = copyFrom.MinHeight;
            BlendRange = copyFrom.BlendRange;
            foreach (worldObjects GO in copyFrom.Folliage)
            {
                for (int i = 0; i < GO.spawnChance; i++)
                {
                    Folliage.Add(GO);
                }
            }
        }
        [Range(-1f, 1f)]
        public float MinHeight;
        [Range(0, 2f)]
        public float BlendRange;
        [Range(0f, 1f)]
        public float foliageSpawnDensity;
        [Range(0f, 1f)]
        public float weight;
        public Texture2D NormalMap;
        public Texture2D DiffuseTexture;
        public float TextureScale;
        public List<worldObjects> Folliage = new List<worldObjects>();
    }

        [System.Serializable]
    public class MapLayer
    {
        public MapLayer(MapLayer copyFrom, Map BaseMap)
        {
            
            maps = new List<Map>();
            biomes = new List<Biome>();
            TempretureMap = copyFrom.TempretureMap;
            HumidityMap = copyFrom.HumidityMap;
            Depth = 0;
            foreach (Map map in copyFrom.maps)
            {
                maps.Add(new Map(map));
                Depth += map.maxHeight;
            }
            for(int e = 0; e< maps.Count;e++)
            {
                if (e > 0)
                {
                    maps[e].scale = Mathf.Clamp(maps[e].scale, 0, maps[e - 1].scale);
                }
                maps[e].maxHeight = maps[e].maxHeight/Depth;
            }
            biomeBuffer = new Vector4[copyFrom.biomes.Count];
            int i = 0;
            foreach (Biome biome in copyFrom.biomes)
            {
                biomeBuffer[i] = new Vector4(biome.tempreture,biome.humidity,i,biome.heightMult);
                biomes.Add(new Biome(biome));
                i++;
            }
        }
        public List<Biome> biomes;
        public List<Map> maps;
        public Map TempretureMap;
        public Map HumidityMap;

        [HideInInspector]
        public float Depth;
        [HideInInspector]
        public Vector4[] biomeBuffer;

    }
    bool firstGen = true;
    private void Start()
    {
        
        mapItterations = new int[LandSettings.maps.Count];
        if (seed == 0)
        {
            seed = Random.Range(0, 10000);
        }
        Random.InitState(seed);
        baseWorldMap.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));
        FoliageDensityMap.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));
        LastLoadPoint = Player.transform.position;
        LastLoadPoint.y = 0;
        chunkSize = Mathf.ClosestPowerOfTwo(chunkSize);
        DeleteChunks();
        StartCoroutine(loadChunks());
    }
    private void Update()
    {
        Vector2 PlayerPos = new Vector2(Player.transform.position.x, Player.transform.position.z);
        if ((PlayerPos - new Vector2(LastLoadPoint.x, LastLoadPoint.z)).sqrMagnitude > chunkSize * chunkSize || Layer != LastLoadPoint.y)
        {
            LastLoadPoint = new Vector3(PlayerPos.x, Layer, PlayerPos.y);
            DeleteChunks();
            StartCoroutine(loadChunks());
        }
    }



    [System.Serializable]
    public class workingLayer
    {
        public workingLayer(int currLayer, MapLayer mls,Map BaseMap)
        {
            layer = currLayer;
            ML = new MapLayer(mls,BaseMap);
        }
        public int layer;
        public MapLayer ML;
    }

    [HideInInspector]
    public workingLayer WL;



    public void GetMap(Chunk chunk)
    {
        workingLayer worklayer;
        if (firstGen)
        {
            worklayer = generateOffsets(new workingLayer(chunk.layer, LandSettings, baseWorldMap));
            WL = worklayer;
            firstGen = false;
        }
        else
        {
            worklayer = getExistingMapInRange(chunk, WL);
            WL = worklayer;
        }

    }

    public workingLayer generateOffsets(workingLayer worklayer)
    {
        Random.InitState(seed + worklayer.layer);
        worklayer.ML.TempretureMap.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));
        worklayer.ML.HumidityMap.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));

        for (int i = worklayer.ML.maps.Count - 1; i >= 0; i--)
        {
            worklayer.ML.maps[i].offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));

        }
        return worklayer;
    }
    public workingLayer getExistingMapInRange(Chunk chunk, workingLayer Worklayer)
    {
        for (int i = 0; i < Worklayer.ML.maps.Count; i++)
        {
            if (!checkMapRange(chunk, Worklayer.ML.maps[i]))
            {
                Debug.LogError("Chunk: " + chunk.transform.position.ToString() + " -- out of Range of map " + i);
                return outputMaps(chunk, Worklayer, i);
            }
        }
        return outputMaps(chunk, Worklayer, Worklayer.ML.maps.Count);
    }
    public bool checkMapRange(Chunk chunk, Map map)
    {
        float range = ((mapsize * threadCountAndMapMult) - 3) * map.scale;
        Vector3 chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) -map.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * map.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * map.scale)
        {
            return false;
        }
        return true;
    }

    public workingLayer outputMaps(Chunk chunk, workingLayer Worklayer, int startMap)
    {
        if (!checkMapRange(chunk, baseWorldMap))
        {
            setShaderValuesK1(baseWorldMap, chunk);
        }
        if (!checkMapRange(chunk, FoliageDensityMap))
        {
            setShaderValuesK1(FoliageDensityMap, chunk);
        }
        chunk.usedMaps = new Vector3[Worklayer.ML.maps.Count];
        compNoise.SetFloat("depth", chunk.depth);
        if (startMap == 0)
        {
            setShaderValuesK1(Worklayer.ML.maps[0], chunk);
            startMap = 1;
        }
        for (int i = startMap; i < Worklayer.ML.maps.Count; i++)
        {
            setShaderValuesK2(Worklayer.ML.maps[i - 1], Worklayer.ML.maps[i], chunk, Worklayer.ML.Depth);
            mapItterations[i]++;
            Debug.Log("Maps generated from Layer " + i + " = " + mapItterations[i]);
            
        }
        for (int i = 0; i < Worklayer.ML.maps.Count; i++)
        {
            chunk.usedMaps[i] = new Vector3(mapItterations[i], Worklayer.ML.maps[i].position.x, Worklayer.ML.maps[i].position.z);
        }
        if (Layer == 0)
        {
            setChunkArrays(setShaderValuesK3(Worklayer.ML.maps[Worklayer.ML.maps.Count - 1], chunk), biomes(chunk, Worklayer), Worklayer, chunk);

        }
        else
        {
        //    setChunkArrays(setShaderValuesK3(Worklayer.ML.maps[Worklayer.ML.maps.Count - 1], chunk), caves(chunk, Worklayer), Worklayer, chunk);

        }
        return Worklayer;
    }

    public Vector2[] biomes(Chunk chunk, workingLayer Worklayer)
    {
        float range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.TempretureMap.scale;
        Vector3 chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.TempretureMap.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.TempretureMap.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.TempretureMap.scale)
        {
            setShaderValuesK1(Worklayer.ML.TempretureMap, chunk);
        }

        range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.HumidityMap.scale;
        chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.HumidityMap.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.HumidityMap.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.HumidityMap.scale)
        {
            setShaderValuesK1(Worklayer.ML.HumidityMap, chunk);
        }


        ComputeBuffer currTempBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevTempBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevTempBuffer.SetData(Worklayer.ML.TempretureMap.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.TempretureMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.TempretureMap.position.x, Worklayer.ML.TempretureMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currTempBuffer);
        compNoise.SetBuffer(2, "heightMap", prevTempBuffer);

        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);

        prevTempBuffer.Release();


        ComputeBuffer currHumidityBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevHumidityBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevHumidityBuffer.SetData(Worklayer.ML.HumidityMap.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.HumidityMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.HumidityMap.position.x, Worklayer.ML.HumidityMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currHumidityBuffer);
        compNoise.SetBuffer(2, "heightMap", prevHumidityBuffer);

        
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);
        prevHumidityBuffer.Release();



        return setShaderValuesK4(currTempBuffer, currHumidityBuffer, chunk, Worklayer);

    }
    public float[] caves(Chunk chunk, workingLayer Worklayer)
    {
        float range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.TempretureMap.scale;
        Vector3 chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.TempretureMap.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.TempretureMap.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.TempretureMap.scale)
        {
            setShaderValuesK1(Worklayer.ML.TempretureMap, chunk);
        }

        range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.HumidityMap.scale;
        chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.HumidityMap.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.HumidityMap.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.HumidityMap.scale)
        {
            setShaderValuesK1(Worklayer.ML.HumidityMap, chunk);
        }


        ComputeBuffer currTempBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevTempBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevTempBuffer.SetData(Worklayer.ML.TempretureMap.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.TempretureMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.TempretureMap.position.x, Worklayer.ML.TempretureMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currTempBuffer);
        compNoise.SetBuffer(2, "heightMap", prevTempBuffer);

        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);

        prevTempBuffer.Release();


        ComputeBuffer currHumidityBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevHumidityBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevHumidityBuffer.SetData(Worklayer.ML.HumidityMap.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.HumidityMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.HumidityMap.position.x, Worklayer.ML.HumidityMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currHumidityBuffer);
        compNoise.SetBuffer(2, "heightMap", prevHumidityBuffer);


        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);
        prevHumidityBuffer.Release();



        return setShaderValuesK5(currTempBuffer, currHumidityBuffer, chunk, Worklayer.ML.HumidityMap.maxHeight);

    }

    public void setChunkArrays(float[] heightData, Vector2[] biomeData, workingLayer workLayer, Chunk chunk)
    {
        float[] baseWorldHeight = setShaderValuesK3(baseWorldMap, chunk);
        float[] FoliageDensity = setShaderValuesK3(FoliageDensityMap, chunk);
        float[,] heights = new float[(chunkSize+1), (chunkSize+1)];
        float[,] WaterHeights = new float[(chunkSize+1), (chunkSize+1)];
        bool[,] water = new bool[(chunkSize), (chunkSize)];
        Texture2D SurfaceTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        Texture2D WaterTexture = new Texture2D(chunkSize + 1, chunkSize + 1);

        List<TerrainLayer> biomeLayer = new List<TerrainLayer>();
        List<int> biomesUsed = new List<int>();
        List<float[,]> SplatMap = new List<float[,]>();

        int biomeCount = workLayer.ML.biomes.Count;

        float[,] baseSplat = new float[chunkSize + 1, chunkSize + 1];
        TerrainLayer firstLayer = new TerrainLayer();
        firstLayer.smoothness = 0;
        firstLayer.metallic = 0;
        firstLayer.smoothnessSource = 0;
        firstLayer.tileSize = new Vector2(chunkSize + 1, chunkSize + 1);
        

        chunk.depth = workLayer.ML.Depth;
        float currBaseAmp = baseWorldMap.maxHeight / chunk.depth;

        Random.InitState(Mathf.RoundToInt(seed * chunk.transform.position.x+chunk.transform.position.y* chunk.transform.position.y + seed));

        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                int biomeIndex = Mathf.RoundToInt(biomeData[y * (chunkSize + 1) + x].x);
                float biomeHeightMult = biomeData[y * (chunkSize + 1) + x].y;
                Biome bio = workLayer.ML.biomes[biomeIndex];

                

                float WaterHeightMap = (((1-Mathf.Abs(((baseWorldHeight[y * (chunkSize + 1) + x] / baseWorldMap.maxHeight)*2)-0.5f)) * riverAndLakeDensity )- (riverAndLakeDensity*0.5f) +baseWorldSeaLevel);
                float landHeight = (heightData[y * (chunkSize + 1) + x] * biomeHeightMult);
                
       
                float currWaterLevel = baseWorldSeaLevel;
                if (WaterHeightMap  > baseWorldSeaLevel)
                {
                    currWaterLevel = Mathf.Clamp((WaterHeightMap), baseWorldSeaLevel, 1);
                }

                float colourHeight = landHeight-currWaterLevel;

                HeightLayer hl = null;
                HeightLayer hl2 = null;
                float BlendMult = 0f;
                if (landHeight < currWaterLevel)
                {
                    float WaterDepth = landHeight / (currWaterLevel);
                    colourHeight = WaterDepth - 1;
                    for (int i = bio.heightLandSettings.Count - 1; i >= 0; i--)
                    {
                        if (colourHeight > bio.heightLandSettings[i].MinHeight)
                        {
                            if (i < bio.heightLandSettings.Count - 1 && colourHeight > (bio.heightLandSettings[i + 1].MinHeight - bio.heightLandSettings[i + 1].BlendRange))
                            {
                                hl2 = bio.heightLandSettings[i + 1];
                                float blendRange = Mathf.Min(bio.heightLandSettings[i + 1].MinHeight- bio.heightLandSettings[i].MinHeight, bio.heightLandSettings[i + 1].BlendRange);
                                BlendMult = (colourHeight - (bio.heightLandSettings[i + 1].MinHeight - blendRange)) / (bio.heightLandSettings[i + 1].MinHeight - (bio.heightLandSettings[i + 1].MinHeight - blendRange));
                            }
                            hl = bio.heightLandSettings[i]; break;
                        }
                    }

                    heights[x, y] = landHeight-(currWaterLevel - landHeight);
                    float waterHeight = landHeight < baseWorldSeaLevel ? baseWorldSeaLevel : Mathf.Clamp(currWaterLevel - ((1-WaterDepth)*0.1f),0,landHeight) ;
                    WaterHeights[x, y] = waterHeight - 0.001f;
                    
                    if (x < chunkSize && y < chunkSize)
                        water[x, y] = true;
                    WaterTexture.SetPixel(y, x, bio.seaColour.Evaluate(WaterDepth) * new Color(1f, 1f, 1f, 0.01f));
                    SurfaceTexture.SetPixel(y, x, (bio.seaFloorColour.Evaluate(WaterDepth)));
                }
                else
                {
                    colourHeight = (landHeight - currWaterLevel) / ((currBaseAmp + ((1 - currBaseAmp) * biomeHeightMult)) - currWaterLevel);
                    heights[x, y] = landHeight;
                    for (int i = bio.heightLandSettings.Count-1;i>=0;i--)
                    {
                        if (colourHeight > bio.heightLandSettings[i].MinHeight)
                        {
                            if (i < bio.heightLandSettings.Count - 1 && colourHeight > (bio.heightLandSettings[i + 1].MinHeight - bio.heightLandSettings[i + 1].BlendRange))
                            {
                                hl2 = bio.heightLandSettings[i + 1];
                                float blendRange = Mathf.Min(bio.heightLandSettings[i + 1].MinHeight - bio.heightLandSettings[i].MinHeight, bio.heightLandSettings[i + 1].BlendRange);
                                BlendMult = (colourHeight - (bio.heightLandSettings[i + 1].MinHeight - blendRange)) / (bio.heightLandSettings[i + 1].MinHeight - (bio.heightLandSettings[i + 1].MinHeight - blendRange));
                            }
                            hl = bio.heightLandSettings[i]; break;
                        }
                    }

                    
                    
                    if (x < chunkSize && y < chunkSize)
                        water[x, y] = false;
                    SurfaceTexture.SetPixel(y, x, bio.heightColour.Evaluate(colourHeight));
                }
                if (FoliageDensityMap.maxHeight != 0 && hl.Folliage.Count > 0)
                {
                    if (Random.Range(0f, FoliageDensityMap.maxHeight) < FoliageDensity[y * (chunkSize + 1) + x] * hl.foliageSpawnDensity * 0.001f)
                    {
                        worldObjects obj = hl.Folliage[Random.Range(0, hl.Folliage.Count)];
                        GameObject spawnObj = obj.obj;
                        float widthMult = Random.Range(obj.minWidth, obj.maxWidth);
                        float heightMult = Random.Range(obj.minHeight, obj.maxHeight);
                        Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        Vector3 pos = new Vector3(chunk.transform.position.x + y, (landHeight < 1 ? chunk.depth * landHeight + ((spawnObj.transform.localScale.y * heightMult) / 2.5f) : chunk.depth + ((spawnObj.transform.localScale.y * heightMult) / 2.5f)), chunk.transform.position.z + x);
                        GameObject newObj = Instantiate(spawnObj, pos, rotation, chunk.transform);
                        newObj.transform.localScale = new Vector3(spawnObj.transform.localScale.x * widthMult, spawnObj.transform.localScale.y * heightMult, spawnObj.transform.localScale.z * widthMult);
                    }
                }
                int layerID = Mathf.FloorToInt(hl.MinHeight*100)*100 + biomeIndex;

                int layerIndex = -1;
                if (!biomesUsed.Contains(layerID))
                {
                    biomesUsed.Add(layerID);
                    float[,] sm = new float[chunkSize + 1, chunkSize + 1];
                    TerrainLayer tl = new TerrainLayer();
                    tl.normalMapTexture = hl.NormalMap;
                    tl.diffuseTexture = hl.DiffuseTexture;
                    tl.smoothness = 0;
                    tl.metallic = 0;
                    tl.tileSize = new Vector2(hl.TextureScale, hl.TextureScale);
                    tl.smoothnessSource = 0;
                    biomeLayer.Add(tl);
                    SplatMap.Add(sm);
                    layerIndex = biomeLayer.Count - 1;
                }
                else
                {
                    layerIndex = biomesUsed.IndexOf(layerID);
                }
                if (BlendMult != 0)
                {
                    int layerID2 = Mathf.FloorToInt(hl2.MinHeight * 100) * 100 + biomeIndex;

                    int layerIndex2 = -1;
                    if (!biomesUsed.Contains(layerID2))
                    {
                        biomesUsed.Add(layerID2);
                        float[,] sm = new float[chunkSize + 1, chunkSize + 1];
                        TerrainLayer tl = new TerrainLayer();
                        tl.normalMapTexture = hl2.NormalMap;
                        tl.diffuseTexture = hl2.DiffuseTexture;
                        tl.smoothness = 0;
                        tl.metallic = 0;
                        tl.tileSize = new Vector2(hl2.TextureScale, hl2.TextureScale);
                        tl.smoothnessSource = 0;
                        biomeLayer.Add(tl);
                        SplatMap.Add(sm);
                        layerIndex2 = biomeLayer.Count - 1;
                    }
                    else
                    {
                        layerIndex2 = biomesUsed.IndexOf(layerID2);
                    }
                    SplatMap[layerIndex][x, y] = hl.weight*(1-BlendMult);
                    SplatMap[layerIndex2][x, y] = hl2.weight * BlendMult;
                    baseSplat[x, y] = 1 - ((hl.weight * (1 - BlendMult))+ (hl2.weight * BlendMult));
                }
                else
                {
                    SplatMap[layerIndex][x, y] = hl.weight;
                    baseSplat[x, y] = 1 - hl.weight;
                }
               

            }
        }

        SurfaceTexture.Apply();
        firstLayer.diffuseTexture = SurfaceTexture;
        WaterTexture.Apply();
        SplatMap.Add(baseSplat);
        biomeLayer.Add(firstLayer);
        chunk.drawMap(heights,SurfaceTexture,WaterHeights,water,WaterTexture, biomeLayer.ToArray(), ConvertListTo3DArray(SplatMap));
    }

    float[,,] ConvertListTo3DArray(List<float[,]> list)
    {
        int width = list[0].GetLength(0);
        int height = list[0].GetLength(1);
        int depth = list.Count;

        float[,,] array = new float[width, height, depth];

        for (int z = 0; z < depth; z++)
        {
            float[,] layer = list[z];

            if (layer.GetLength(0) != width || layer.GetLength(1) != height)
            {
                Debug.LogError("All LandSettings must be the same size.");
                continue;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    array[x, y, z] = layer[x, y];
                }
            }
        }

        return array;
    }

//cave generation not yet finished
// public void setChunkArrays(float[] heightData, float[] caveValues, workingLayer workLayer, Chunk chunk)
// {
//     float[,] heights = new float[(chunkSize + 1), (chunkSize + 1)];
//     float[,] heights2 = new float[(chunkSize + 1), (chunkSize + 1)];
//     bool[,] caves = new bool[(chunkSize), (chunkSize)];
//     Texture2D SurfaceTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
//     for (int x = 0; x < (chunkSize + 1); x++)
//     {
//         for (int y = 0; y < (chunkSize + 1); y++)
//         {
//             Biome bio = workLayer.ML.biomes[0];
//             heights[x, y] = heightData[y * (chunkSize + 1) + x];
//             heights2[x,y] = heightData[y * (chunkSize + 1) + x]+(0.1f* caveValues[y * (chunkSize + 1) + x]);
//             if (x < chunkSize && y < chunkSize)
//                 caves[x, y] = caveValues[y * (chunkSize + 1) + x] > 0 ? true : false;
//             SurfaceTexture.SetPixel(y, x, bio.heightColour.Evaluate(caveValues[y * (chunkSize + 1) + +x] + 0.5f));
//         }
//     }
//     SurfaceTexture.Apply();
//
//     chunk.drawMap(heights, SurfaceTexture,heights2,caves, SurfaceTexture);
// }



//------------------------------------------------------------------------------------------------------------------------------------------------------
// Place Chunks

IEnumerator loadChunks()
    {
        Vector3 centerChunkPos = new Vector3(Mathf.RoundToInt(LastLoadPoint.x / chunkSize) * chunkSize,
                                     0, Mathf.RoundToInt(LastLoadPoint.z / chunkSize) * chunkSize);
        int i = 0;
        for (int x = Mathf.RoundToInt(centerChunkPos.x - (chunkSize * LoadDist)); x < centerChunkPos.x + (chunkSize * LoadDist); x += chunkSize)
        {
            for (int y = Mathf.RoundToInt(centerChunkPos.z - (chunkSize * LoadDist)); y < centerChunkPos.z + (chunkSize * LoadDist); y += chunkSize)
            {
                bool alreadyPlaced = false;
                foreach (GameObject chunk in LoadedChunks)
                {
                    if (chunk.transform.position == new Vector3(x, 0, y))
                    {
                        alreadyPlaced = true; break;
                    }
                }
                if (!alreadyPlaced)
                {
                    
                    GameObject chunk = Instantiate(ChunkPrefab, new Vector3(x, -Layer, y), Quaternion.identity);
                    i++;
                    chunk.transform.parent = this.transform;
                    chunk.GetComponent<Chunk>().chunkSize = chunkSize;
                    chunk.GetComponent<Chunk>().layer = Layer;
                    LoadedChunks.Add(chunk);
                    GetMap(chunk.GetComponent<Chunk>());
                    if (i >= maxChunksPerFrame)
                    {
                        i = 0;
                        yield return null;
                    }
                }
            }
        }
        yield return null;

    }

    private void DeleteChunks()
    {
        float maxDistanceSqr = chunkSize * deletionDist * chunkSize * deletionDist;

        for (int i = LoadedChunks.Count - 1; i >= 0; i--)
        {
            GameObject chunk = LoadedChunks[i];
            Vector3 chunkPos = chunk.transform.position;

            if ((chunkPos - (Vector3)LastLoadPoint).sqrMagnitude > maxDistanceSqr || Layer != chunk.GetComponent<Chunk>().layer)
            {
                Destroy(chunk);
                LoadedChunks.RemoveAt(i);
            }
        }
    }
    //-------------------------------------------------------------------------------------------------------------
    // shader scripts
    public void setShaderValuesK1(Map workMap, Chunk chunk)
    {
        ComputeBuffer heightBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));



        workMap.heightData = new float[mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult];


        Vector3 snappedPosition = new Vector3(
        Mathf.RoundToInt(chunk.transform.position.x / workMap.scale) * workMap.scale,
        0,
        Mathf.RoundToInt(chunk.transform.position.z / workMap.scale) * workMap.scale
        );

        float halfMapSize = (workMap.scale * mapsize * threadCountAndMapMult) / 2;

        Vector3 offset = new Vector3(
            halfMapSize,
            0,
            halfMapSize
        );

        workMap.position = snappedPosition - offset;
        compNoise.SetFloat("workMapScale", workMap.scale);
        compNoise.SetVector("workMapPosition", new Vector2(workMap.position.x, workMap.position.z));
        compNoise.SetVector("workMapoffset", workMap.offset);
        compNoise.SetFloat("workMapAmplitude", workMap.maxHeight);
        compNoise.SetFloat("workMapGradientDampening", workMap.gradientDampening);
        compNoise.SetInt("WorkMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(0, "Result", heightBuffer);
        compNoise.Dispatch(0, mapsize + 1, mapsize + 1, 1);

        heightBuffer.GetData(workMap.heightData);

        heightBuffer.Release();
    }
    public void setShaderValuesK2(Map lastMap, Map workMap, Chunk chunk, float depth)
    {
        ComputeBuffer heightBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));



        workMap.heightData = new float[mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult];

        ComputeBuffer lastHeightBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        lastHeightBuffer.SetData(lastMap.heightData);

        Vector3 snappedPosition = new Vector3(
            Mathf.RoundToInt(chunk.transform.position.x / workMap.scale) * workMap.scale,
            0,
            Mathf.RoundToInt(chunk.transform.position.z / workMap.scale) * workMap.scale
        );

        float halfMapSize = (workMap.scale * mapsize * threadCountAndMapMult) / 2;

        Vector3 offset = new Vector3(
            halfMapSize,
            0,
            halfMapSize
        );

        workMap.position = snappedPosition - offset;
        compNoise.SetFloat("depth", depth);
        compNoise.SetFloat("BaseMapScale", lastMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(lastMap.position.x, lastMap.position.z));
        compNoise.SetFloat("workMapScale", workMap.scale);
        compNoise.SetVector("workMapPosition", new Vector2(workMap.position.x, workMap.position.z));
        compNoise.SetVector("workMapoffset", workMap.offset);
        compNoise.SetFloat("workMapAmplitude", workMap.maxHeight);
        compNoise.SetFloat("workMapGradientDampening", workMap.gradientDampening);
        compNoise.SetInt("WorkMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(1, "Result", heightBuffer);
        compNoise.SetBuffer(1, "heightMap", lastHeightBuffer);

        compNoise.Dispatch(1, mapsize + 1, mapsize + 1, 1);

        heightBuffer.GetData(workMap.heightData);

        lastHeightBuffer.Release();
        heightBuffer.Release();
    }

    public float[] setShaderValuesK3(Map finalMap, Chunk chunk)
    {

        ComputeBuffer currHeightBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer finalHeightBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        finalHeightBuffer.SetData(finalMap.heightData);


        compNoise.SetFloat("BaseMapScale", finalMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(finalMap.position.x, finalMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currHeightBuffer);
        compNoise.SetBuffer(2, "heightMap", finalHeightBuffer);
        compNoise.SetFloat("workMapGradientDampening", sharpness);

        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);
        float[] heights = new float[(1 + chunk.chunkSize) * (1 + chunk.chunkSize)];
        currHeightBuffer.GetData(heights);

        finalHeightBuffer.Release();
        currHeightBuffer.Release();
        return heights;
    }
    public Vector2[] setShaderValuesK4(ComputeBuffer m1, ComputeBuffer m2, Chunk chunk, workingLayer Worklayer)
    {
        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        ComputeBuffer bb = new ComputeBuffer(Worklayer.ML.biomes.Count, sizeof(float) * 4);
        bb.SetData(Worklayer.ML.biomeBuffer);
        ComputeBuffer MapBiomes = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), 2*sizeof(float));
        compNoise.SetBuffer(3, "TempMap", m1);
        compNoise.SetBuffer(3, "HumidityMap", m2);
        compNoise.SetBuffer(3, "BiomeMap", bb);
        compNoise.SetBuffer(3, "biomeResult", MapBiomes);
        compNoise.SetInt("biomeCount", Worklayer.ML.biomes.Count);
        compNoise.Dispatch(3, dispatchSize, dispatchSize, 1);
        Vector2[] biomeData = new Vector2[(1 + chunk.chunkSize) * (1 + chunk.chunkSize)];

        MapBiomes.GetData(biomeData);
        MapBiomes.Release();
        m1.Release();
        m2.Release();
        bb.Release();
        return (biomeData);
    }
    public float[] setShaderValuesK5(ComputeBuffer m1, ComputeBuffer m2, Chunk chunk, float compMapMult)
    {
        ComputeBuffer compValue = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        compNoise.SetBuffer(4, "TempMap", m1);
        compNoise.SetBuffer(4, "HumidityMap", m2);
        compNoise.SetBuffer(4, "compResult", compValue);
        
        compNoise.SetFloat("compMapMult", compMapMult);

        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        compNoise.Dispatch(3, dispatchSize, dispatchSize, 1);
        float[] compData = new float[(1 + chunk.chunkSize) * (1 + chunk.chunkSize)];
        compValue.GetData(compData);
        
        m1.Release();
        m2.Release();
        compValue.Release();
        
        return(compData);
    }
}
