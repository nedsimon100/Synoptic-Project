using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ChunkManager : MonoBehaviour
{
    public GameObject ChunkPrefab;
    public List<GameObject> LoadedChunks = new List<GameObject>();
    public int chunkSize = 10;
    public int LoadDist = 5;
    public Vector3 LastLoadPoint = Vector3.zero;
    public GameObject Player;
    public int Layer;
    public Noise noiseFunction;
    
    private void Start()
    {
        noiseFunction = this.GetComponent<Noise>();
        LastLoadPoint = Player.transform.position;
        LastLoadPoint.y = 0;
        DeleteChunks();
        loadChunks();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            Layer++;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            Layer--;
        }
        Vector2 PlayerPos = new Vector2(Player.transform.position.x,Player.transform.position.z);
        if((PlayerPos - new Vector2(LastLoadPoint.x,LastLoadPoint.z)).sqrMagnitude > chunkSize*chunkSize|| Layer!=LastLoadPoint.y)
        {
            LastLoadPoint = new Vector3(PlayerPos.x,Layer,PlayerPos.y);
            DeleteChunks();
            loadChunks();
        }
    }

    private void loadChunks()
    {
        Vector3 centerChunkPos = new Vector3(Mathf.RoundToInt(LastLoadPoint.x / chunkSize) * chunkSize,
                                     0, Mathf.RoundToInt(LastLoadPoint.z / chunkSize) * chunkSize);

        for (int x = Mathf.RoundToInt(centerChunkPos.x-(chunkSize*LoadDist)); x < centerChunkPos.x + (chunkSize * LoadDist); x += chunkSize)
        {
            for (int y = Mathf.RoundToInt(centerChunkPos.z - (chunkSize * LoadDist)); y < centerChunkPos.z + (chunkSize * LoadDist); y += chunkSize)
            {
                bool alreadyPlaced = false;
                foreach(GameObject chunk in LoadedChunks)
                {
                    if(chunk.transform.position == new Vector3(x, 0, y))
                    {
                        alreadyPlaced = true; break;
                    }
                }
                if (!alreadyPlaced)
                {
                    GameObject chunk = Instantiate(ChunkPrefab,new Vector3(x, 0, y),Quaternion.identity);
                    chunk.transform.parent = this.transform;
                    chunk.GetComponent<ChunkGeneration>().chunkSize = chunkSize;
                    chunk.GetComponent<ChunkGeneration>().layer = Layer;
                    LoadedChunks.Add(chunk);
                }
            }
        }

    }


    private void DeleteChunks()
    {
        float maxDistanceSqr = chunkSize * LoadDist * chunkSize * LoadDist;

        for (int i = LoadedChunks.Count - 1; i >= 0; i--)
        {
            GameObject chunk = LoadedChunks[i];
            Vector3 chunkPos = chunk.transform.position;

            if ((chunkPos - (Vector3)LastLoadPoint).sqrMagnitude > maxDistanceSqr||Layer!=chunk.GetComponent<ChunkGeneration>().layer)
            {
                Destroy(chunk);
                LoadedChunks.RemoveAt(i);
            }
        }
    }


}
