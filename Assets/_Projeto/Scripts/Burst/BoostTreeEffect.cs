using UnityEngine;

public class BoostGlow : MonoBehaviour
{
    private Renderer[] renderers;

    public float velocidade = 3f;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            rend.material.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        float t = Mathf.PingPong(Time.time * velocidade, 1f);
        t = Mathf.Pow(t, 2f);

        Color azulTurbo = new Color(0f, 0.4f, 1f);
        Color glow = Color.Lerp(azulTurbo, Color.white, t);

        foreach (Renderer rend in renderers)
        {
            rend.material.SetColor("_EmissionColor", glow * 10f);
        }
    }
}