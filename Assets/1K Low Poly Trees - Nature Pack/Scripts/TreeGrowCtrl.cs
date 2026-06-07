using System.Collections;
using UnityEngine;

public class TreeGrowCtrl : MonoBehaviour
{
    [HideInInspector]
    public int ID;
    [HideInInspector]
    public float t;
    [HideInInspector]
    public int Genre;
    [HideInInspector]
    public int TreeType;
    
    IEnumerator Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        for (int i = ID; i < 9; i++)
        {
            string str = "G" + Genre + "_T" + TreeType + "_ID_" + i;
            meshFilter.mesh = Resources.Load<MeshFilter>(str).sharedMesh;
            yield return new WaitForSeconds(t);
        }
    }
}
