using UnityEngine;

public class TreeCreator : MonoBehaviour
{
    public int xSize = 100;
    public int zSize = 100;
    [Range(0.1f,1.0f)]
    public float Density = 1;
    bool[,] placeObj;

    [Range(0, 9)]
    public int Genre = 0;
    public bool RandomGenre = false;
    [Range(0.0f,1.0f)]
    public float GrowSpeedMultipler = 1.0f;

    // Start is called before the first frame update
    void Start()
    {
        int index = 0;
        placeObj = new bool[xSize, zSize];
        
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                float random = Random.Range(0.0f, 1.0f);
                if (random <= Density/10)
                {
                    placeObj[x, z] = random == 0 ? true : false;
                    GameObject tree = new GameObject();
                    tree.name = "ID:"+index + "_X["+ x + "]Z[" + z +"]";
                    tree.transform.position = new Vector3(x, 0, z);
                    tree.transform.Rotate(Vector3.up, Random.Range(0, 360));
                    tree.AddComponent<TreeSetup>();
                    TreeSetup treeSetup = tree.GetComponent<TreeSetup>();
                    treeSetup.Genre = RandomGenre == true ? Random.Range(0,9) : Genre;
                    treeSetup.TreeType = Random.Range(0, 9);
                    treeSetup.StartIndex = Random.Range(0, 5);
                    treeSetup.GrowSpeed =  Random.Range(1.0f, 3.0f) - GrowSpeedMultipler;
                    index++;
                }
            }
        }
    }
}
