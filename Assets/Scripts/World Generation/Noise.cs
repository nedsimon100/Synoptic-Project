using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;



public class Noise : MonoBehaviour
{
    public bool debug;
    public int seed;
    public ComputeShader compNoise;
    public int threadCountAndMapMult = 16;
    private int[] mapItterations;
    //public GameObject debugMap;
    [System.Serializable]

    public class Map
    {
        //[HideInInspector]
        public Vector3 position;
        //[HideInInspector]
        public Vector3 offset;
        //[HideInInspector]

        public float[] heightData;

        [Range(1, 1000)]
        public float scale;
        [Range(4, 1000)]
        public int mapsize;
        [Range(0f, 1f)]
        public float amplitude;
        [Range(0f, 100f)]
        public float gradientDampening;
#if debug == true
        public Texture2D debugTexture;
#endif
        public Map(Map copyFrom)
        {
            scale = copyFrom.scale;
            mapsize = copyFrom.mapsize;
            amplitude = copyFrom.amplitude;
            gradientDampening = copyFrom.gradientDampening;
            heightData = copyFrom.heightData;
        }
        
    }

    

    [System.Serializable]
    public class MapLayer
    {
        public MapLayer(MapLayer copyFrom) 
        { 
            layerOffset = copyFrom.layerOffset;
            minLayer = copyFrom.minLayer;
            maps = new List<Map>();
            foreach (Map map in copyFrom.maps)
            {
                maps.Add(new Map(map));
            }
        }
        public List<Map> maps;
        public float layerOffset;
        public int minLayer;
    }
    public List<MapLayer> layers = new List<MapLayer>();
    private void Start()
    {
        mapItterations = new int[layers[0].maps.Count];
        if(seed == 0)
        {
            seed = Random.Range(0, 10000);
        }
    }

    [System.Serializable]
    public class workingLayer
    {
        public workingLayer(int currLayer,List<MapLayer> mls)
        {
            layer = currLayer;
            ML = mls[0];
            for(int i = mls.Count-1; i >= 0 ; i--)
            {
                if (currLayer >= mls[i].minLayer)
                {
                    ML = new MapLayer(mls[i]);
                    break;
                }
            }
           
        }
        public int layer;
        public MapLayer ML;
        
    }


    public List<workingLayer> WL = new List<workingLayer>();

    [Range(1,10)]
    public int maxLayersSaved;

    public void GetMap(ChunkGeneration chunk)
    {
        workingLayer worklayer;
        bool exists = false;
        for (int i = WL.Count-1;i>=0;i--)
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
            worklayer = generateOffsets(new workingLayer(chunk.layer,layers));
            WL.Add(outputMaps(chunk,worklayer,0));
        }

    }

    public workingLayer generateOffsets(workingLayer worklayer)
    {
        Random.InitState(seed + worklayer.layer);
        for (int i = worklayer.ML.maps.Count-1; i >= 0; i--)
        {
            worklayer.ML.maps[i].offset = new Vector3(Random.Range(0, 99999), Random.Range(0, 99999));

        }
        return worklayer;
    }
    public workingLayer getExistingMapInRange(ChunkGeneration chunk, workingLayer Worklayer)
    {
        for (int i = Worklayer.ML.maps.Count-1; i >= 0; i--)
        {
            float range = ((Worklayer.ML.maps[i].mapsize * threadCountAndMapMult )- 3) * Worklayer.ML.maps[i].scale ;
            Vector3 chunkoffset =   (chunk.transform.position - new Vector3(chunk.chunkSize/2,0, chunk.chunkSize / 2))- Worklayer.ML.maps[i].position;
            Debug.LogWarning("at "+i+" ||Range = " + range + " || Chunk Offset = " + chunkoffset.ToString());
            if ((chunkoffset).x + chunk.chunkSize < range && (chunkoffset).x > 3 && (chunkoffset).z + chunk.chunkSize < range && (chunkoffset).z > 3) 
            {
                Debug.LogError("Chunk: "+chunk.transform.position.ToString()+" -- In Range of map " + i);
                return outputMaps(chunk, Worklayer, i+1);
            }
        }
        return outputMaps(chunk, Worklayer, 0);
    }

    public workingLayer outputMaps(ChunkGeneration chunk, workingLayer Worklayer,int startMap)
    {

        if (startMap == 0)
        {
            ComputeBuffer heightBuffer = new ComputeBuffer(Worklayer.ML.maps[0].mapsize * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
            

           
            Worklayer.ML.maps[0].heightData = new float[Worklayer.ML.maps[0].mapsize * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult * threadCountAndMapMult];


            Worklayer.ML.maps[0].position = new Vector3(Mathf.FloorToInt(chunk.transform.position.x / Worklayer.ML.maps[0].scale) * Worklayer.ML.maps[0].scale,
                0, Mathf.FloorToInt(chunk.transform.position.z / Worklayer.ML.maps[0].scale) * Worklayer.ML.maps[0].scale)
                - new Vector3((Worklayer.ML.maps[0].scale * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult) / 2,
                Worklayer.layer * Worklayer.ML.layerOffset,
                (Worklayer.ML.maps[0].scale * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult) / 2);
            compNoise.SetFloat("workMapScale", Worklayer.ML.maps[0].scale);
            compNoise.SetVector("workMapPosition", new Vector2(Worklayer.ML.maps[0].position.x, Worklayer.ML.maps[0].position.z));

            compNoise.SetVector("workMapoffset", Worklayer.ML.maps[0].offset);
            compNoise.SetFloat("workMapAmplitude", Worklayer.ML.maps[0].amplitude);
            compNoise.SetFloat("workMapGradientDampening", Worklayer.ML.maps[0].gradientDampening);
            compNoise.SetInt("WorkMapSize", Worklayer.ML.maps[0].mapsize * threadCountAndMapMult);
            compNoise.SetBuffer(0, "Result", heightBuffer);
            compNoise.Dispatch(0, Worklayer.ML.maps[0].mapsize+1, Worklayer.ML.maps[0].mapsize+1,1);
            mapItterations[0]++;
            Debug.Log("Maps generated from Layer 0 = " + mapItterations[0]);
            heightBuffer.GetData(Worklayer.ML.maps[0].heightData);
#if debug == true
            Worklayer.ML.maps[0].debugTexture = new Texture2D(Worklayer.ML.maps[0].mapsize * threadCountAndMapMult, Worklayer.ML.maps[0].mapsize * threadCountAndMapMult);
            Worklayer.ML.maps[0].debugTexture.filterMode = FilterMode.Point;
            for (int x = 0; x < Worklayer.ML.maps[0].mapsize * threadCountAndMapMult; x++)
            {
                for (int y = 0; y < Worklayer.ML.maps[0].mapsize * threadCountAndMapMult; y++)
                {
                    Worklayer.ML.maps[0].debugTexture.SetPixel(x, y, new Color(1 - Worklayer.ML.maps[0].heightData[y * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult + x], 1 - Worklayer.ML.maps[0].heightData[y * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult + x], 1 - Worklayer.ML.maps[0].heightData[y * Worklayer.ML.maps[0].mapsize * threadCountAndMapMult + x]));
            
                }
            }
#endif
           

            startMap = 1;
            heightBuffer.Release();
        }
        for (int i = startMap; i < Worklayer.ML.maps.Count; i++)
        {
            mapItterations[i]++;
            Debug.Log("Maps generated from Layer "+i+" = " + mapItterations[i]);
            ComputeBuffer heightBuffer = new ComputeBuffer(Worklayer.ML.maps[i].mapsize * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));


            
            Worklayer.ML.maps[i].heightData = new float[Worklayer.ML.maps[i].mapsize * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult * threadCountAndMapMult];

            
            Map lastMap = Worklayer.ML.maps[i - 1];

            ComputeBuffer lastHeightBuffer = new ComputeBuffer(lastMap.mapsize * lastMap.mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
            lastHeightBuffer.SetData(lastMap.heightData);

            Worklayer.ML.maps[i].position = new Vector3(Mathf.FloorToInt(chunk.transform.position.x / Worklayer.ML.maps[i].scale ) * Worklayer.ML.maps[i].scale,
                0, Mathf.FloorToInt(chunk.transform.position.z /  Worklayer.ML.maps[i].scale) * Worklayer.ML.maps[i].scale)
                - new Vector3((Worklayer.ML.maps[i].scale * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult) / 2,
                Worklayer.layer * Worklayer.ML.layerOffset,
                (Worklayer.ML.maps[i].scale * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult) / 2);

            compNoise.SetFloat("BaseMapScale", lastMap.scale);
            compNoise.SetVector("BaseMapPosition", new Vector2(lastMap.position.x, lastMap.position.z));
            compNoise.SetFloat("workMapScale", Worklayer.ML.maps[i].scale);
            compNoise.SetVector("workMapPosition", new Vector2(Worklayer.ML.maps[i].position.x, Worklayer.ML.maps[i].position.z));
            compNoise.SetVector("workMapoffset", Worklayer.ML.maps[i].offset);
            compNoise.SetFloat("workMapAmplitude", Worklayer.ML.maps[i].amplitude);
            compNoise.SetFloat("workMapGradientDampening", Worklayer.ML.maps[i].gradientDampening);
            compNoise.SetInt("WorkMapSize", Worklayer.ML.maps[i].mapsize * threadCountAndMapMult);
            compNoise.SetInt("BaseMapSize", Worklayer.ML.maps[i].mapsize * threadCountAndMapMult);
            compNoise.SetBuffer(1, "Result", heightBuffer);
            compNoise.SetBuffer(1, "heightMap", lastHeightBuffer);
            
            compNoise.Dispatch(1, Worklayer.ML.maps[i].mapsize+1, Worklayer.ML.maps[i].mapsize+1, 1);

            heightBuffer.GetData(Worklayer.ML.maps[i].heightData);

#if debug == true
            Worklayer.ML.maps[i].debugTexture = new Texture2D(Worklayer.ML.maps[i].mapsize * threadCountAndMapMult, Worklayer.ML.maps[i].mapsize * threadCountAndMapMult);
            Worklayer.ML.maps[i].debugTexture.filterMode = FilterMode.Point;
            for (int x = 0; x < Worklayer.ML.maps[i].mapsize * threadCountAndMapMult; x++)
            {
                for (int y = 0; y < Worklayer.ML.maps[i].mapsize * threadCountAndMapMult; y++)
                {
                    Worklayer.ML.maps[i].debugTexture.SetPixel(x, y, new Color(1 - Worklayer.ML.maps[i].heightData[y * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult + x], 1 - Worklayer.ML.maps[i].heightData[y * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult + x], 1 - Worklayer.ML.maps[i].heightData[y * Worklayer.ML.maps[i].mapsize * threadCountAndMapMult + x]));
                    // step 1: apply guassian blur
                    // step 2 : check the chunks edge
                    // step 3: apply edge restitching
                }
            }
#endif
            


            lastHeightBuffer.Release();
            heightBuffer.Release();

        }
        
        Map finalMap = Worklayer.ML.maps[Worklayer.ML.maps.Count - 1];

        ComputeBuffer currHeightBuffer = new ComputeBuffer((1+chunk.chunkSize)*(1+chunk.chunkSize), sizeof(float));
        ComputeBuffer finalHeightBuffer = new ComputeBuffer(finalMap.mapsize * finalMap.mapsize * threadCountAndMapMult * threadCountAndMapMult, sizeof(float));
        finalHeightBuffer.SetData(finalMap.heightData);


        compNoise.SetFloat("BaseMapScale", finalMap.scale);
        compNoise.SetVector("BaseMapPosition", new Vector2(finalMap.position.x, finalMap.position.z));
        compNoise.SetFloat("workMapScale", 1);
        compNoise.SetVector("workMapPosition", new Vector2(chunk.transform.position.x-(chunk.chunkSize/2), chunk.transform.position.z - (chunk.chunkSize / 2)));
        compNoise.SetInt("WorkMapSize", chunk.chunkSize+1);
        compNoise.SetInt("BaseMapSize", finalMap.mapsize * threadCountAndMapMult);
        compNoise.SetBuffer(2, "Result", currHeightBuffer);
        compNoise.SetBuffer(2, "heightMap", finalHeightBuffer);


        int dispatchSize = Mathf.CeilToInt((chunk.chunkSize + 1) / threadCountAndMapMult)+1;
        compNoise.Dispatch(2, dispatchSize, dispatchSize, 1);

        currHeightBuffer.GetData(chunk.heightData);
        
        finalHeightBuffer.Release();
        currHeightBuffer.Release();
        return Worklayer;
    }



}
