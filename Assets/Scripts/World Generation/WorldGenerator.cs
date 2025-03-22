using System.Collections.Generic;
using UnityEngine;
using System.Collections;
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

    public Map baseWorldMap;
    [Range(0f, 1f)]
    public float baseWorldSeaLevel;
    [Range(0f, 1f)]
    public float riverAndLakeDensity;
    [Header("Compute Shader")]

    public ComputeShader compNoise;
    public int threadCountAndMapMult = 16;
    private int[] mapItterations;

    [Header("Layer Settings")]
    [Range(4, 1000)]
    public int mapsize;
    [Range(1, 10)]
    public int maxLayersSaved;
    
    public List<MapLayer> layers = new List<MapLayer>();

    //public GameObject debugMap;
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
        public float amplitude;
        [Range(0f, 10000f)]
        public float gradientDampening;
        public Map(Map copyFrom)
        {
            scale = copyFrom.scale;
            amplitude = copyFrom.amplitude;
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
            seaColour = copyFrom.seaColour;
            heightMult = copyFrom.heightMult;
        }
        public float tempreture;
        public float humidity;
        public float heightMult;
        public Gradient heightColour;
        public Gradient seaColour;
        
    }

        [System.Serializable]
    public class MapLayer
    {
        public MapLayer(MapLayer copyFrom, Map BaseMap)
        {
            
            minLayer = copyFrom.minLayer;
            maps = new List<Map>();
            biomes = new List<Biome>();
            compMap1 = copyFrom.compMap1;
            compMap2 = copyFrom.compMap2;
            Depth = BaseMap.amplitude;
            foreach (Map map in copyFrom.maps)
            {
                maps.Add(new Map(map));
                Depth += map.amplitude;
            }
            for(int e = 0; e< maps.Count;e++)
            {
                if (e > 0)
                {
                    maps[e].scale = Mathf.Clamp(maps[e].scale, 0, maps[e - 1].scale);
                }
                maps[e].amplitude = maps[e].amplitude/Depth;
            }
            biomeBuffer = new Vector3[copyFrom.biomes.Count];
            int i = 0;
            foreach (Biome biome in copyFrom.biomes)
            {
                biomeBuffer[i] = new Vector3(biome.tempreture,biome.humidity,i);
                biomes.Add(new Biome(biome));
                i++;
            }
        }
        public List<Biome> biomes;
        public List<Map> maps;
        public Map compMap1;
        public Map compMap2;

        [HideInInspector]
        public float Depth;
        [HideInInspector]
        public Vector3[] biomeBuffer;

        public int minLayer;
    }

    private void Start()
    {
        baseWorldMap.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));
        mapItterations = new int[layers[0].maps.Count];
        if (seed == 0)
        {
            seed = Random.Range(0, 10000);
        }
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
        public workingLayer(int currLayer, List<MapLayer> mls,Map BaseMap)
        {
            layer = currLayer;
            ML = mls[0];
            for (int i = mls.Count - 1; i >= 0; i--)
            {
                if (currLayer >= mls[i].minLayer)
                {
                    ML = new MapLayer(mls[i], BaseMap);
                    break;
                }
            }

        }
        public int layer;
        public MapLayer ML;
    }


    public List<workingLayer> WL = new List<workingLayer>();



    public void GetMap(Chunk chunk)
    {
        workingLayer worklayer;
        bool exists = false;
        for (int i = WL.Count - 1; i >= 0; i--)
        {
            if (WL[i].layer == chunk.layer)
            {

                worklayer = getExistingMapInRange(chunk, WL[i]);
                WL.RemoveAt(i);
                WL.Add(worklayer);
                exists = true;
                break;

            }
        }
        if (!exists)
        {

            if (WL.Count > maxLayersSaved)
            {
                WL.RemoveAt(0);
            }
            worklayer = generateOffsets(new workingLayer(chunk.layer, layers, baseWorldMap));
            WL.Add(outputMaps(chunk, worklayer, 0));
        }

    }

    public workingLayer generateOffsets(workingLayer worklayer)
    {
        Random.InitState(seed + worklayer.layer);
        worklayer.ML.compMap1.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));
        worklayer.ML.compMap2.offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));

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
        chunk.usedMaps = new Vector3[Worklayer.ML.maps.Count];
        compNoise.SetFloat("depth", chunk.depth);
        if (startMap == 0)
        {
            setShaderValuesK1(Worklayer.ML.maps[0], chunk);
            startMap = 1;
        }
        for (int i = startMap; i < Worklayer.ML.maps.Count; i++)
        {

            setShaderValuesK2(Worklayer.ML.maps[i - 1], Worklayer.ML.maps[i], chunk);
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
            setChunkArrays(setShaderValuesK3(Worklayer.ML.maps[Worklayer.ML.maps.Count - 1], chunk), caves(chunk, Worklayer), Worklayer, chunk);

        }
        return Worklayer;
    }

    public int[] biomes(Chunk chunk, workingLayer Worklayer)
    {
        float range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.compMap1.scale;
        Vector3 chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.compMap1.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.compMap1.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.compMap1.scale)
        {
            setShaderValuesK1(Worklayer.ML.compMap1, chunk);
        }

        range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.compMap2.scale;
        chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.compMap2.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.compMap2.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.compMap2.scale)
        {
            setShaderValuesK1(Worklayer.ML.compMap2, chunk);
        }


        ComputeBuffer currTempBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevTempBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevTempBuffer.SetData(Worklayer.ML.compMap1.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.compMap1.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.compMap1.position.x, Worklayer.ML.compMap1.position.z));
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
        prevHumidityBuffer.SetData(Worklayer.ML.compMap2.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.compMap2.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.compMap2.position.x, Worklayer.ML.compMap2.position.z));
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
        float range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.compMap1.scale;
        Vector3 chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.compMap1.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.compMap1.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.compMap1.scale)
        {
            setShaderValuesK1(Worklayer.ML.compMap1, chunk);
        }

        range = ((mapsize * threadCountAndMapMult) - 3) * Worklayer.ML.compMap2.scale;
        chunkoffset = (chunk.transform.position - new Vector3(chunk.chunkSize / 2, 0, chunk.chunkSize / 2)) - Worklayer.ML.compMap2.position;
        if ((chunkoffset).x + chunk.chunkSize > range || (chunkoffset).x < 3 * Worklayer.ML.compMap2.scale || (chunkoffset).z + chunk.chunkSize > range || (chunkoffset).z < 3 * Worklayer.ML.compMap2.scale)
        {
            setShaderValuesK1(Worklayer.ML.compMap2, chunk);
        }


        ComputeBuffer currTempBuffer = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(float));
        ComputeBuffer prevTempBuffer = new ComputeBuffer(mapsize * mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        prevTempBuffer.SetData(Worklayer.ML.compMap1.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.compMap1.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.compMap1.position.x, Worklayer.ML.compMap1.position.z));
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
        prevHumidityBuffer.SetData(Worklayer.ML.compMap2.heightData);

        compNoise.SetFloat("BaseMapScale", Worklayer.ML.compMap2.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(Worklayer.ML.compMap2.position.x, Worklayer.ML.compMap2.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x - (chunk.chunkSize / 2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize + 1);
        compNoise.SetInt("BaseMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currHumidityBuffer);
        compNoise.SetBuffer(2, "heightMap", prevHumidityBuffer);


        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);
        prevHumidityBuffer.Release();



        return setShaderValuesK5(currTempBuffer, currHumidityBuffer, chunk, Worklayer.ML.compMap2.amplitude);

    }
    public void setChunkArrays(float[] heightData, int[] biomeData, workingLayer workLayer, Chunk chunk)
    {
        float[] baseWorldHeight = setShaderValuesK3(baseWorldMap, chunk);
        float[,] heights = new float[(chunkSize+1), (chunkSize+1)];
        float[,] WaterHeights = new float[(chunkSize+1), (chunkSize+1)];
        bool[,] water = new bool[(chunkSize), (chunkSize)];
        Texture2D SurfaceTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        Texture2D WaterTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        chunk.depth = workLayer.ML.Depth+baseWorldMap.amplitude;
        float currBaseAmp = baseWorldMap.amplitude / chunk.depth;
        
        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                int biomeIndex = biomeData[y * (chunkSize + 1) + x];
                Biome bio = workLayer.ML.biomes[biomeIndex];

                float currBaseHeight = (baseWorldHeight[y * (chunkSize + 1) + x] / baseWorldMap.amplitude) * currBaseAmp;
                float h = currBaseHeight + (heightData[y * (chunkSize + 1) + x]*bio.heightMult);

                //float currSeaLevel = baseWorldSeaLevel;

                float currSeaLevel = baseWorldSeaLevel + (baseWorldSeaLevel*(currBaseHeight/currBaseAmp)*(riverAndLakeDensity));
                
                
               

                if (h < currSeaLevel||baseWorldSeaLevel>h)
                {
                    float waterDepth = heightData[y * (chunkSize + 1) + x] / currSeaLevel;
                    heights[x, y] =  (waterDepth * currSeaLevel) + currBaseHeight;
                    currSeaLevel = currSeaLevel + ((h - currSeaLevel) *0.2f * (1- waterDepth));
                    WaterHeights[x, y] = currSeaLevel<baseWorldSeaLevel?baseWorldSeaLevel-0.002f:currSeaLevel - 0.002f;
                    
                    if (x<chunkSize && y<chunkSize)
                       water[x, y] = true;
                    WaterTexture.SetPixel(y, x, bio.seaColour.Evaluate(waterDepth)*new Color(1f,1f,1f, 0.01f));
                    SurfaceTexture.SetPixel(y, x, (bio.seaColour.Evaluate(waterDepth) + bio.heightColour.Evaluate(h))/2);
                }
              
                else
                {
                    heights[x, y] = h;
               
                    if (x < chunkSize && y < chunkSize)
                        water[x, y] = false;
                    SurfaceTexture.SetPixel(y, x, bio.heightColour.Evaluate((h - currSeaLevel) / (1- currSeaLevel)));
                }
              

            }
        }
        SurfaceTexture.Apply();
        WaterTexture.Apply();
        chunk.drawMap(heights,SurfaceTexture,WaterHeights,water,WaterTexture);
    }
    public void setChunkArrays(float[] heightData, float[] caveValues, workingLayer workLayer, Chunk chunk)
    {
        float[,] heights = new float[(chunkSize + 1), (chunkSize + 1)];
        float[,] heights2 = new float[(chunkSize + 1), (chunkSize + 1)];
        bool[,] caves = new bool[(chunkSize), (chunkSize)];
        Texture2D SurfaceTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                Biome bio = workLayer.ML.biomes[0];
                heights[x, y] = heightData[y * (chunkSize + 1) + x];
                heights2[x,y] = heightData[y * (chunkSize + 1) + x]+(0.1f* caveValues[y * (chunkSize + 1) + x]);
                if (x < chunkSize && y < chunkSize)
                    caves[x, y] = caveValues[y * (chunkSize + 1) + x] > 0 ? true : false;
                SurfaceTexture.SetPixel(y, x, bio.heightColour.Evaluate(caveValues[y * (chunkSize + 1) + +x] + 0.5f));
            }
        }
        SurfaceTexture.Apply();

        chunk.drawMap(heights, SurfaceTexture,heights2,caves, SurfaceTexture);
    }



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
        compNoise.SetFloat("workMapAmplitude", workMap.amplitude);
        compNoise.SetFloat("workMapGradientDampening", workMap.gradientDampening);
        compNoise.SetInt("WorkMapSize", mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(0, "Result", heightBuffer);
        compNoise.Dispatch(0, mapsize + 1, mapsize + 1, 1);
        mapItterations[0]++;
        Debug.Log("Maps generated from Layer 0 = " + mapItterations[0]);
        heightBuffer.GetData(workMap.heightData);

        heightBuffer.Release();
    }
    public void setShaderValuesK2(Map lastMap, Map workMap, Chunk chunk)
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

        compNoise.SetFloat("BaseMapScale", lastMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(lastMap.position.x, lastMap.position.z));
        compNoise.SetFloat("workMapScale", workMap.scale);
        compNoise.SetVector("workMapPosition", new Vector2(workMap.position.x, workMap.position.z));
        compNoise.SetVector("workMapoffset", workMap.offset);
        compNoise.SetFloat("workMapAmplitude", workMap.amplitude);
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


        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);
        float[] heights = new float[(1 + chunk.chunkSize) * (1 + chunk.chunkSize)];
        currHeightBuffer.GetData(heights);

        finalHeightBuffer.Release();
        currHeightBuffer.Release();
        return heights;
    }
    public int[] setShaderValuesK4(ComputeBuffer m1, ComputeBuffer m2, Chunk chunk, workingLayer Worklayer)
    {
        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult) + 1;
        ComputeBuffer bb = new ComputeBuffer(Worklayer.ML.biomes.Count, sizeof(float) * 3);
        bb.SetData(Worklayer.ML.biomeBuffer);
        ComputeBuffer MapBiomes = new ComputeBuffer((1 + chunk.chunkSize) * (1 + chunk.chunkSize), sizeof(int));
        compNoise.SetBuffer(3, "TempMap", m1);
        compNoise.SetBuffer(3, "HumidityMap", m2);
        compNoise.SetBuffer(3, "BiomeMap", bb);
        compNoise.SetBuffer(3, "biomeResult", MapBiomes);
        compNoise.SetInt("biomeCount", Worklayer.ML.biomes.Count);
        compNoise.Dispatch(3, dispatchSize, dispatchSize, 1);
        int[] biomeData = new int[(1 + chunk.chunkSize) * (1 + chunk.chunkSize)];
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
