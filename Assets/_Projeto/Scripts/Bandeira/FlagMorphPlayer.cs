using UnityEngine;

public class FlagBlendShapePlayer : MonoBehaviour
{
    public float framesPerSecond = 24f;
    public float blendShapeWeight = 1f; // NÃO use 100

    private SkinnedMeshRenderer smr;
    private int blendShapeCount;

    void Start()
    {
        smr = GetComponent<SkinnedMeshRenderer>();
        blendShapeCount = smr.sharedMesh.blendShapeCount;
    }

    void Update()
    {
        int frame = Mathf.FloorToInt(Time.time * framesPerSecond) % blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            smr.SetBlendShapeWeight(i, 0f);
        }

        smr.SetBlendShapeWeight(frame, blendShapeWeight);
    }
}