
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldGen : MonoBehaviour
{
    float Depth = 0;
    private bool FirstGen=true;

    [Header("Chunk Settings")]

    public int maxChunksPerFrame = 2;
    public int chunkSize = 100;
    public int LoadDist = 5;
    public int deletionDist = 10;
    public GameObject Player;
    public int seed;
    public GameObject ChunkPrefab;
    [HideInInspector]
    public List<GameObject> LoadedChunks = new List<GameObject>();
    [HideInInspector]
    public Vector3 LastLoadPoint = Vector3.zero;

    public int mapSize = 100;

    public float biomeBlendDistance;

    [System.Serializable]
    public class worldObjects
    {
        public GameObject obj;
        public float minHeight = 1f;
        public float maxHeight = 1f;
        public float minWidth = 1f;
        public float maxWidth = 1f;
        public int spawnChance = 1;
    }
    [System.Serializable]
    public class Biome
    {
        [Header("Ideal Biome Location")]
        public float Humidity;
        public float Temperature;
        public float Height;
        [Header("Colours")]
        public Gradient heightColour;
        public Gradient seaColour;
        public Gradient seaFloorColour;
        [Header("Height Modifiers")]
        public float HeightMult;
        public float sharpnessExponent;

        [Header("Texture")]

        public Texture2D DiffuseTexture;
        public Texture2D NormalMap;
        [Range(0f, 1f)]
        public float weight = 1f;
        [Range(0f, 3f)]
        public float NormalMapScale = 1f;
        [Range(0f, 100f)]
        public float TextureScale = 1f;
        [Range(0f, 2f)]
        public float smoothness;
        [Range(0f, 2f)]
        public float metallic;
        [Header("Possible Spawn Objects")]
        [Range(0f, 1f)]
        public float foliageDensity;
        public List<worldObjects> Folliage = new List<worldObjects>();
        [HideInInspector]
        public TerrainLayer tl;
        public void initialise()
        {
            tl = new TerrainLayer();
            tl.normalMapTexture = NormalMap;
            tl.diffuseTexture = DiffuseTexture;
            tl.normalScale = NormalMapScale;
            tl.smoothness = smoothness;
            tl.metallic = metallic;
            tl.tileSize = new Vector2(TextureScale, TextureScale);
            tl.smoothnessSource = 0;
        }
    }



    // structs

    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct MapSt
    {
        [HideInInspector]
        public Vector2 Position;
        [HideInInspector]
        public Vector2 offset;
        [Range(1,10000)]
        public float scale;
        [Range(1, 5000)]
        public float maxHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BiomeSt
    {
        public Vector3 TempHumidityHeight;
        public float HeightMult;
        public float sharpnessExponent;
        public float foliageDensity;
    }
    BiomeSt ConvertBiome(Biome biome)
    {
        BiomeSt result = new BiomeSt();
        result.TempHumidityHeight = new Vector3(biome.Temperature, biome.Humidity, biome.Height);
        result.HeightMult = biome.HeightMult;
        result.sharpnessExponent = biome.sharpnessExponent;
        result.foliageDensity = biome.foliageDensity;
        return result;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MapPointSt
    {
        public int spawnPoint;
        public float height;
        public float gradient;
        public float WaterHeight;
        public int biomesCount;
        public Vector2 biome1;
        public Vector2 biome2;
        public Vector2 biome3;
        public Vector2 biome4;
        public Vector2 biome5;
    }
    [Header("Water Settings")]
    public float minWaterLevel = 0;
    public MapSt WaterMap;
    [Header("Height Maps")]
    public List<MapSt> heightMaps = new List<MapSt>();

    [Header("Biome Maps")]
    public MapSt TemperatureMap;
    public MapSt HumidityMap;
    public MapSt FoliageMap;

    [Header("Biomes")]
    public List<Biome> biomes = new List<Biome>();

    [Header("Compute Shader")]
    public ComputeShader GenShader;

    // hidden
    private ComputeBuffer mapsBuffer;
    private List<BiomeSt> computeBiomes = new List<BiomeSt>();
    private List<MapSt> computeMaps = new List<MapSt>();
    private ComputeBuffer FoliageBuffer;
    private ComputeBuffer HumidityBuffer;
    private ComputeBuffer TempBuffer;
    private ComputeBuffer WaterBuffer;
    private ComputeBuffer EvenHeightBuffer;
    private ComputeBuffer OddHeightBuffer;

    private ComputeBuffer biomesBuffer;

    private float[][] fractalHeights;
    int randomNoiseKernel;
    int fractalMapKernel;
    int chunkGenKernel;
    void Start()
    {
        chunkSize = Mathf.ClosestPowerOfTwo(chunkSize);
        if (seed == 0)
        {
            seed = Mathf.RoundToInt(Random.Range(0, 10000000000));
        }
        initialiseBuffers();

        DeleteChunks();
        StartCoroutine(loadChunks());
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 PlayerPos = new Vector2(Player.transform.position.x, Player.transform.position.z);
        if ((PlayerPos - new Vector2(LastLoadPoint.x, LastLoadPoint.z)).sqrMagnitude > chunkSize * chunkSize)
        {
            LastLoadPoint = new Vector3(PlayerPos.x, 0, PlayerPos.y);
            DeleteChunks();
            StartCoroutine(loadChunks());
        }
    }
    

    void initialiseBuffers()
    {
        FirstGen = true;
        Random.InitState(seed);
        randomNoiseKernel = GenShader.FindKernel("RandomNoiseGen");
        fractalMapKernel = GenShader.FindKernel("FractalMapGen");
        chunkGenKernel = GenShader.FindKernel("ChunkGen");
        computeMaps.Clear();
        computeMaps.Add(WaterMap);
        computeMaps.Add(TemperatureMap);
        computeMaps.Add(HumidityMap);
        computeMaps.Add(FoliageMap);
        computeMaps.AddRange(heightMaps);


        for (int i = 0; i < computeMaps.Count; i++)
        {
            MapSt map = computeMaps[i];
            map.offset = new Vector2(Random.Range(-999999999999f, 999999999999f), Random.Range(-999999999999f, 999999999999f));
            if(i>=4) Depth += map.maxHeight;
            computeMaps[i] = map;
        }
        foreach (Biome biome in biomes)
        {
            biome.initialise();
            computeBiomes.Add(ConvertBiome(biome));
        }


        BufferRelease();

        mapsBuffer = new ComputeBuffer(computeMaps.Count, Marshal.SizeOf(typeof(MapSt)), ComputeBufferType.Structured);
        WaterBuffer = new ComputeBuffer(mapSize*mapSize, sizeof(float), ComputeBufferType.Structured);
        FoliageBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float), ComputeBufferType.Structured);
        TempBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float), ComputeBufferType.Structured);
        HumidityBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float), ComputeBufferType.Structured);
        EvenHeightBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float), ComputeBufferType.Structured);
        OddHeightBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float), ComputeBufferType.Structured);
        biomesBuffer = new ComputeBuffer(biomes.Count, Marshal.SizeOf(typeof(BiomeSt)), ComputeBufferType.Structured);

        float[] initialHeights = new float[mapSize * mapSize];
        for (int i = 0; i < initialHeights.Length; i++)
        {
            initialHeights[i] = 0f;
        }

        fractalHeights = new float[heightMaps.Count][];
        for (int i = 0; i < heightMaps.Count; i++)
        {
            fractalHeights[i] = new float[mapSize * mapSize];
        }

        HumidityBuffer.SetData(initialHeights);
        TempBuffer.SetData(initialHeights);
        EvenHeightBuffer.SetData(initialHeights);
        OddHeightBuffer.SetData(initialHeights);
        mapsBuffer.SetData(computeMaps);                                                                                                     
        biomesBuffer.SetData(computeBiomes);

        // For RandomNoiseGen kernel:
        GenShader.SetBuffer(randomNoiseKernel, "Maps", mapsBuffer);
        GenShader.SetBuffer(randomNoiseKernel, "WaterBuffer", WaterBuffer);
        GenShader.SetBuffer(randomNoiseKernel, "TempBuffer", TempBuffer);
        GenShader.SetBuffer(randomNoiseKernel, "HumidityBuffer", HumidityBuffer);
        GenShader.SetBuffer(randomNoiseKernel, "FoliageBuffer", FoliageBuffer);
        GenShader.SetBuffer(randomNoiseKernel, "EvenHeightBuffer", EvenHeightBuffer);

        // For FractalMapGen kernel:
        GenShader.SetBuffer(fractalMapKernel, "Maps", mapsBuffer);
        GenShader.SetBuffer(fractalMapKernel, "EvenHeightBuffer", EvenHeightBuffer);
        GenShader.SetBuffer(fractalMapKernel, "OddHeightBuffer", OddHeightBuffer);

        // For ChunkGen kernel:
        GenShader.SetBuffer(chunkGenKernel, "Maps", mapsBuffer);
        GenShader.SetBuffer(chunkGenKernel, "EvenHeightBuffer", EvenHeightBuffer);
        GenShader.SetBuffer(chunkGenKernel, "OddHeightBuffer", OddHeightBuffer);
        GenShader.SetBuffer(chunkGenKernel, "FoliageBuffer", FoliageBuffer);
        GenShader.SetBuffer(chunkGenKernel, "WaterBuffer", WaterBuffer);
        GenShader.SetBuffer(chunkGenKernel, "HumidityBuffer", HumidityBuffer);
        GenShader.SetBuffer(chunkGenKernel, "TempBuffer", TempBuffer);
        GenShader.SetBuffer(chunkGenKernel, "biomes", biomesBuffer);

        GenShader.SetFloat("minWaterLevel", minWaterLevel);
        GenShader.SetInt("MapSize", mapSize);

        GenShader.SetInt("ChunkSize", chunkSize);

        GenShader.SetInt("biomeCount", biomes.Count);
        GenShader.SetFloat("BiomeBlendDist", biomeBlendDistance);
        GenShader.SetFloat("Depth", Depth);
    }

    void BufferRelease()
    {
        if (mapsBuffer != null) mapsBuffer.Release();
        if (FoliageBuffer != null) FoliageBuffer.Release();
        if (WaterBuffer != null) WaterBuffer.Release();
        if (HumidityBuffer != null) HumidityBuffer.Release();
        if (TempBuffer != null) TempBuffer.Release();
        if (biomesBuffer != null) biomesBuffer.Release();
        if (OddHeightBuffer != null) OddHeightBuffer.Release();
        if (EvenHeightBuffer != null) EvenHeightBuffer.Release();
    }
    private void OnDisable()
    {
        BufferRelease();
    }





    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///
    // spawn Chunks

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

                    GameObject chunk = Instantiate(ChunkPrefab, new Vector3(x, 0, y), Quaternion.identity);
                    i++;
                    chunk.transform.parent = this.transform;
                    chunk.GetComponent<Chunk>().chunkSize = chunkSize;
                    chunk.GetComponent<Chunk>().layer = 0;
                    LoadedChunks.Add(chunk);
                    chunk.GetComponent<Chunk>().depth = Depth;
                    GenerateChunk(getExistingMapInRange(chunk.GetComponent<Chunk>()), chunk.GetComponent<Chunk>());
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

            if ((chunkPos - (Vector3)LastLoadPoint).sqrMagnitude > maxDistanceSqr)
            {
                Destroy(chunk);
                LoadedChunks.RemoveAt(i);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///
    public MapPointSt[] getExistingMapInRange(Chunk chunk)
    {
        if (FirstGen)
        {
            FirstGen = false;
            return getMaps(chunk, 0);
        }
        for (int i = 0; i < computeMaps.Count; i++)
        {
            if (!checkMapRange(chunk, computeMaps[i]))
            {
                Debug.LogError("Chunk: " + chunk.transform.position.ToString() + " -- out of Range of map " + i);
                if (i <= 4)
                {
                    int dispatchSize = Mathf.CeilToInt((mapSize + 1) / 16f);
                    GenShader.SetInt("MapIndex", i);
                    GenShader.Dispatch(randomNoiseKernel, dispatchSize, dispatchSize, 1);
                }
                else
                {
                    if (i % 2 == 1)
                    {
                        EvenHeightBuffer.SetData(fractalHeights[i-1]);
                    }
                    else
                    {
                        OddHeightBuffer.SetData(fractalHeights[i - 1]);
                    }
                    return getMaps(chunk, i);
                }

                
            }
        }
        return getMaps(chunk, computeMaps.Count);
    }
    public bool checkMapRange(Chunk chunk, MapSt map)
    {
        float mapWidth = mapSize * map.scale;
        Vector2 chunkMin = new Vector2(chunk.transform.position.x, chunk.transform.position.z);
        Vector2 chunkMax = chunkMin + new Vector2(chunk.chunkSize, chunk.chunkSize);
        Vector2 mapMin = map.Position;
        Vector2 mapMax = map.Position + new Vector2(mapWidth, mapWidth);

        // Check if chunk is fully within map bounds with some margin
        if (chunkMin.x < mapMin.x + 3 * map.scale || chunkMax.x > mapMax.x - 3 * map.scale ||
            chunkMin.y < mapMin.y + 3 * map.scale || chunkMax.y > mapMax.y - 3 * map.scale)
        {
            return false;
        }
        return true;
    }

    public MapPointSt[] getMaps(Chunk chunk,int mapIndex)
    {
        int dispatchSize = Mathf.CeilToInt((mapSize+1) / 16f);
        Vector2 chunkCenterPos = new Vector2(chunk.transform.position.x + chunk.chunkSize * 0.5f, chunk.transform.position.z + chunk.chunkSize * 0.5f);

        for (int i = mapIndex; i < computeMaps.Count; i++)
        {
            MapSt map = computeMaps[i];
            float scale = map.scale;

            map.Position = chunkCenterPos - new Vector2(mapSize * scale * 0.5f, mapSize * scale * 0.5f);


            computeMaps[i] = map;
        }
        mapsBuffer.SetData(computeMaps);
        AsyncGPUReadback.WaitAllRequests();
        for (int i = mapIndex; i < computeMaps.Count; i++)
        {
            GenShader.SetInt("MapIndex", i);
            if (i < 4)
            {
                GenShader.Dispatch(randomNoiseKernel, dispatchSize, dispatchSize, 1);
            }
            else if (i == 4)
            {
                if ((i - 5) >= 0 && (i - 5) < fractalHeights.Length)
                {
                    if (i % 2 == 0)
                    {
                        EvenHeightBuffer.SetData(fractalHeights[i - 5]);
                    }
                    else
                    {
                        OddHeightBuffer.SetData(fractalHeights[i - 5]);
                    }
                }
                GenShader.Dispatch(randomNoiseKernel, dispatchSize, dispatchSize, 1);
                EvenHeightBuffer.GetData(fractalHeights[0]);
            }
            else
            {
                GenShader.Dispatch(fractalMapKernel, dispatchSize, dispatchSize, 1);
                if (i % 2 == 1)
                {
                    OddHeightBuffer.GetData(fractalHeights[i - 4]);
                }
                else
                {
                    EvenHeightBuffer.GetData(fractalHeights[i-4]);
                }
            }
        }
        ComputeBuffer chunkBuffer = new ComputeBuffer((chunkSize+1) * (chunkSize + 1), Marshal.SizeOf(typeof(MapPointSt)));

        MapPointSt[] chunkData = new MapPointSt[(chunkSize+1) * (chunkSize+1)];
        
        GenShader.SetBuffer(chunkGenKernel, "Chunk", chunkBuffer);

        GenShader.SetVector("ChunkPos", new Vector2(chunk.transform.position.x, chunk.transform.position.z));
        dispatchSize = Mathf.CeilToInt((chunkSize+1) / 16f);
        GenShader.Dispatch(chunkGenKernel, dispatchSize, dispatchSize, 1);

        chunkBuffer.GetData(chunkData);
        chunkBuffer.Release();

        return chunkData;
    }

    void GenerateChunk(MapPointSt[] chunkData,Chunk chunk)
    {
        float[,] heights = new float[chunkSize + 1, chunkSize+1];
        float[,] waterHeights = new float[chunkSize+1, chunkSize+1];
        bool[,] water = new bool[(chunkSize), (chunkSize)];

        Texture2D SurfaceTexture = new Texture2D(chunkSize + 1, chunkSize + 1);
        Texture2D WaterTexture = new Texture2D(chunkSize + 1, chunkSize + 1);

        List<TerrainLayer> biomeLayer = new List<TerrainLayer>();
        List<int> biomesUsed = new List<int>();
        List<float[,]> SplatMap = new List<float[,]>();

        int biomeCount = biomes.Count;

        float[,] baseSplat = new float[chunkSize + 1, chunkSize + 1];
        TerrainLayer firstLayer = new TerrainLayer();
        firstLayer.smoothness = 0;
        firstLayer.metallic = 0;
        firstLayer.smoothnessSource = 0;
        firstLayer.tileSize = new Vector2(chunkSize + 1, chunkSize + 1);
        Random.InitState(Mathf.RoundToInt(seed * chunk.transform.position.x + chunk.transform.position.y+seed));
        for (int x = 0; x < (chunkSize + 1); x++)
        {
            for (int y = 0; y < (chunkSize + 1); y++)
            {
                MapPointSt point = chunkData[y * (chunkSize + 1) + x];

                float h = point.height / Depth;
                float wh = point.WaterHeight / Depth;

                
                Color pointColor = new Color();
                Color waterColour = new Color();
                float totalSplat=0;
                float totalWeight = 0;
                List<Biome> foliageBiome = new List<Biome>();
                for (int i = 0; i < point.biomesCount; i++)
                {
                    Vector2 bioLay = getBiomeLayer(point, i);
                    int bioIndex = Mathf.RoundToInt(bioLay.x);
                    Biome bio = biomes[bioIndex];
                    if (biomesUsed.Contains(bioIndex))
                    {
                        int layerIndex = biomesUsed.IndexOf(bioIndex);
                        SplatMap[layerIndex][x, y] = bioLay.y * bio.weight;
                        
                    }
                    else
                    {
                        biomeLayer.Add(bio.tl);
                        biomesUsed.Add(bioIndex);
                        float[,] sm = new float[chunkSize + 1, chunkSize + 1];
                        sm[x, y] = bioLay.y*bio.weight;
                        SplatMap.Add(sm);
                    }
                    if (point.spawnPoint==1)
                    {
                        for (int c = 0; c < bioLay.y * 10; c++)
                        {
                            foliageBiome.Add(bio);
                        }
                    }
                    totalWeight += bioLay.y * bio.weight;
                    totalSplat += bioLay.y *  bio.weight;
                    pointColor += h > wh ? bioLay.y  *bio.weight* bio.heightColour.Evaluate(h) : bioLay.y * bio.weight * bio.seaFloorColour.Evaluate(h);
                    waterColour += bioLay.y * bio.weight * bio.seaColour.Evaluate(wh-h);
                }
                if (point.spawnPoint==1&&h>wh)
                {
                    Biome fb = foliageBiome[Random.Range(0, foliageBiome.Count)];
                    worldObjects obj = fb.Folliage[Random.Range(0, fb.Folliage.Count)];
                    GameObject spawnObj = obj.obj;
                    float widthMult = Random.Range(obj.minWidth, obj.maxWidth);
                    float heightMult = Random.Range(obj.minHeight, obj.maxHeight);
                    Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    Vector3 pos = new Vector3(chunk.transform.position.x + y, (h < 1 ? chunk.depth * h + ((spawnObj.transform.localScale.y * heightMult) / 2.5f) : chunk.depth + ((spawnObj.transform.localScale.y * heightMult) / 2.5f)), chunk.transform.position.z + x);
                    GameObject newObj = Instantiate(spawnObj, pos, rotation, chunk.transform);
                    newObj.transform.localScale = new Vector3(spawnObj.transform.localScale.x * widthMult, spawnObj.transform.localScale.y * heightMult, spawnObj.transform.localScale.z * widthMult);

                }
                if (wh > h)
                {
                    float diff = h - (wh - h);
                    wh = h;
                    h = diff;
                    if (x < chunkSize && y < chunkSize)
                        water[x, y] = true;
                }
                else
                {
                    if (x < chunkSize && y < chunkSize)
                        water[x, y] = false;
                }
                baseSplat[x, y] = 1-totalSplat;
                SurfaceTexture.SetPixel(y, x, pointColor/totalWeight);
                WaterTexture.SetPixel(y, x, waterColour/totalWeight);
                heights[x, y] = h;
                waterHeights[x, y] = wh;
            }
        }
        SurfaceTexture.Apply();
        WaterTexture.Apply();
        firstLayer.diffuseTexture = SurfaceTexture;
        SplatMap.Add(baseSplat);
        biomeLayer.Add(firstLayer);
        chunk.drawMap(heights, SurfaceTexture, waterHeights, water, WaterTexture, biomeLayer.ToArray(), ConvertListTo3DArray(SplatMap));
    }

    Vector2 getBiomeLayer(MapPointSt mp, int index)
    {
        switch (index)
        {
            case 0:
                return mp.biome1;
            case 1:
                return mp.biome2;
            case 2:
                return mp.biome3;
            case 3:
                return mp.biome4;
            case 4:
                return mp.biome5;
        }
        return Vector2.zero;
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
                Debug.LogError("All layers must be the same size.");
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
}
