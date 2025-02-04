
using UnityEngine;
using UnityEngine.Tilemaps;


public class ChunkGeneration : MonoBehaviour
{
    public GameObject LayerTransition;
    public Vector3 LoadPoint;
    public int chunkSize;
    public int layer;
    public float layerDifference;
    // Noise Scale in chunks
    public int LargeScale;
    public float LargeScaleDepth;
    public int standardScale;

    public int offsetX, offsetY;
    public int seed;
    public Tilemap floor;
    public Tile ground;

    public Gradient floorColour;

    // corner Heights
    private Vector3 BLSH;
    private Vector3 TLSH;
    private Vector3 BRSH;
    private Vector3 TRSH;
    private Vector3 BLLH;
    private Vector3 TLLH;
    private Vector3 BRLH;
    private Vector3 TRLH;

    private int sX;
    private int sY;
    private int lX;
    private int lY;
    private void Start()
    {
        
        Random.InitState(seed + layer);
        offsetX = Random.Range(0, 1000000);
        offsetY = Random.Range(0, 1000000);
        GetNoise();
        generate();
    }
    public void GetNoise()
    {
        sX = Mathf.FloorToInt(transform.position.x/ (chunkSize * standardScale))* (chunkSize * standardScale);
        sY = Mathf.FloorToInt(transform.position.y / (chunkSize * standardScale))* (chunkSize * standardScale);
        lX =Mathf.FloorToInt(transform.position.x / (chunkSize * LargeScale))* (chunkSize * LargeScale);
        lY =Mathf.FloorToInt(transform.position.y/(chunkSize * LargeScale))* (chunkSize * LargeScale);
        
        BLSH = generateNoiseHeights(sX* standardScale, sY* standardScale);
        TLSH = generateNoiseHeights(sX* standardScale, (sY + (chunkSize * standardScale)) * standardScale);
        BRSH = generateNoiseHeights((sX + (chunkSize * standardScale))* standardScale, sY* standardScale);
        TRSH = generateNoiseHeights((sX + (chunkSize * standardScale)) * standardScale, (sY + (chunkSize * standardScale)) * standardScale);
        BLLH = generateNoiseHeights(lX * LargeScale, lY * LargeScale);
        if (lX * LargeScale==0&& lY * LargeScale == 0)
        {
            if(layer == 0)
            {
                BLLH.y = 0;
            }
            else if (layer == 1) 
            {
                BLLH.x = 0;
            }

        }
        Debug.Log("bottom left big height = "+BLLH.y);
        
        TLLH = generateNoiseHeights(lX * LargeScale,( lY+ (chunkSize * LargeScale)) * LargeScale);
        if (lX * LargeScale == 0 && (lY + (chunkSize * LargeScale)) * LargeScale == 0)
        {
            if (layer == 0)
            {
                TLLH.y = 0;
            }
            else if (layer == 1)
            {
                TLLH.x = 0;
            }

        }
        Debug.Log("TLLH = " + TLLH.y);
        BRLH = generateNoiseHeights((lX + (chunkSize * LargeScale)) * LargeScale, lY * LargeScale);
        if ((lX + (chunkSize * LargeScale)) * LargeScale == 0 && lY * LargeScale == 0)
        {
            if (layer == 0)
            {
                BRLH.y = 0;
            }
            else if (layer == 1)
            {
                BRLH.x = 0;
            }

        }
        Debug.Log("BRLH = " + BRLH.y);
        TRLH = generateNoiseHeights((lX + (chunkSize * LargeScale)) * LargeScale, (lY + (chunkSize * LargeScale)) * LargeScale);
        if ((lX + (chunkSize * LargeScale)) * LargeScale == 0 && (lY + (chunkSize * LargeScale)) * LargeScale == 0)
        {
            if (layer == 0)
            {
                TRLH.y = 0;
            }
            else if (layer == 1)
            {
                TRLH.x = 0;
            }

        }
        Debug.Log("TRLH = " + TRLH.y);
    }
    private Vector3 generateNoiseHeights(int x, int y)
    {
        float scale = 0.1f;
        float height1 = 3f;
        if (layer >= 1)
        {
            Random.InitState(seed + layer-1);
            offsetX = Random.Range(0, 1000000);
            offsetY = Random.Range(0, 1000000);
            height1 = Mathf.PerlinNoise(scale * (x + offsetX), scale * (y + offsetY));
        }
        Random.InitState(seed + layer);
        offsetX = Random.Range(0, 1000000);
        offsetY = Random.Range(0, 1000000);
        float height2 = Mathf.PerlinNoise(scale * (x + offsetX), scale * (y + offsetY));
        Random.InitState(seed + layer + 1);
        offsetX = Random.Range(0, 1000000);
        offsetY = Random.Range(0, 1000000);
        float height3 = Mathf.PerlinNoise(scale * (x + offsetX), scale * (y + offsetY));
        
        return new Vector3(height1,height2,height3);
    }
    private void generate()
    {
        for(int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                PlaceTile(x, y);
            }
        }
    }
    private Vector2 getHeight(float x, float y)
    {
        float stanX = (x / (chunkSize*standardScale)) - Mathf.FloorToInt(x/ (chunkSize * standardScale));
        float stanY = (y / (chunkSize * standardScale)) - Mathf.FloorToInt(y / (chunkSize * standardScale));
        float largeX = (x / (chunkSize * LargeScale)) - Mathf.FloorToInt(x / (chunkSize * LargeScale));
        float largeY = (y / (chunkSize * LargeScale)) - Mathf.FloorToInt(y / (chunkSize * LargeScale));
        float standardScaleHeight = ((1 - (stanX)) * (1 - (stanY)) * BLSH.y + (stanX) * (1 - (stanY)) * BRSH.y + ((1 - (stanX)) * (stanY) * TLSH.y) + ((stanX) * (stanY) * TRSH.y));
        float largeScaleHeight = ((1 - (largeX)) * (1 - (largeY)) * BLLH.y + (largeX) * (1 - (largeY)) * BRLH.y + ((1 - (largeX)) * (largeY) * TLLH.y) + ((largeX) * (largeY) * TRLH.y));
        float lowerLevelHeight = ((1 - (stanX)) * (1 - (stanY)) * BLSH.z + (stanX) * (1 - (stanY)) * BRSH.z + ((1 - (stanX)) * (stanY) * TLSH.z) + ((stanX) * (stanY) * TRSH.z))-layerDifference;

        
        return new Vector2(standardScaleHeight - ((LargeScaleDepth * largeScaleHeight)),lowerLevelHeight - standardScaleHeight);//+(LargeScaleDepth/2)


    }
    private void PlaceTile(int x, int y)
    {
        
        Vector3Int tilePosition = new Vector3Int(x, y, 0);
        Vector2 GlobalTilePos = floor.CellToWorld(tilePosition);
        Vector2 height = getHeight(GlobalTilePos.x, GlobalTilePos.y);
        //Debug.Log("Set Tile "+height);
        floor.SetTile(tilePosition, ground);
        floor.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None);
        if (height.y > 0 && height.x>0)
        {
            floor.SetColor(tilePosition, new Color(0,0,0));
        }
        else
        {
            floor.SetColor(tilePosition, floorColour.Evaluate((height.x / 2) + 0.5f));
        }

    }
}
