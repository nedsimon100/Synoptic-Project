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

    public int seed;
    private void Start()
    {
        seed = Random.Range(0, 10000);
        LastLoadPoint = Player.transform.position;
        LastLoadPoint.z = Layer;
        DeleteChunks();
        loadChunks();
    }
    private void Update()
    {
        Vector2 PlayerPos = Player.transform.position;
        if((PlayerPos - (Vector2)LastLoadPoint).sqrMagnitude > chunkSize*chunkSize|| Layer!=LastLoadPoint.z)
        {
            LastLoadPoint = PlayerPos;
            LastLoadPoint.z = Layer;
            DeleteChunks();
            loadChunks();
        }
    }

    private void loadChunks()
    {
        Vector3 centerChunkPos = new Vector3(Mathf.RoundToInt(LastLoadPoint.x / chunkSize) * chunkSize,
                                     Mathf.RoundToInt(LastLoadPoint.y / chunkSize) * chunkSize, 0);

        for (int x = Mathf.RoundToInt(centerChunkPos.x-(chunkSize*LoadDist)); x < centerChunkPos.x + (chunkSize * LoadDist); x += chunkSize)
        {
            for (int y = Mathf.RoundToInt(centerChunkPos.y - (chunkSize * LoadDist)); y < centerChunkPos.y + (chunkSize * LoadDist); y += chunkSize)
            {
                bool alreadyPlaced = false;
                foreach(GameObject chunk in LoadedChunks)
                {
                    if(chunk.transform.position == new Vector3(x, y, 0))
                    {
                        alreadyPlaced = true; break;
                    }
                }
                if (!alreadyPlaced)
                {
                    GameObject chunk = Instantiate(ChunkPrefab,new Vector3(x, y, 0),Quaternion.identity);
                    chunk.transform.parent = this.transform;
                    chunk.GetComponent<ChunkGeneration>().chunkSize = chunkSize;
                    chunk.GetComponent<ChunkGeneration>().layer = Layer;
                    chunk.GetComponent<ChunkGeneration>().seed = seed;
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
            Vector2 chunkPos = chunk.transform.position;

            if ((chunkPos - (Vector2)LastLoadPoint).sqrMagnitude > maxDistanceSqr||Layer!=LastLoadPoint.z)
            {
                Destroy(chunk);
                LoadedChunks.RemoveAt(i);
            }
        }
    }


}
