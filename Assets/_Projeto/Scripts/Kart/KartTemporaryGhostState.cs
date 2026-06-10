using System.Collections.Generic;
using UnityEngine;

// Estado temporário de "ghost" aplicado a um kart atingido pelo míssil.
// Durante a janela (padrão 3s) o kart:
//  - NÃO colide fisicamente com outros karts (Physics.IgnoreCollision por par de colisores);
//  - CONTINUA colidindo com pista/chão/paredes (só ignoramos pares kart-kart, nada de layers globais);
//  - pisca (renderers ligam/desligam) para comunicar a proteção;
//  - permanece controlável (não mexe em input nem em SetControlEnabled).
// Tudo é restaurado ao fim do tempo, ao desabilitar o objeto ou ao reaplicar.
// Funciona igual para player e bots (não depende de quem dirige).
[DisallowMultipleComponent]
public class KartTemporaryGhostState : MonoBehaviour
{
    [Header("Duração")]
    [Tooltip("Tempo padrão (s) do estado ghost quando o chamador não especifica.")]
    [SerializeField] private float defaultDuration = 3f;

    [Header("Visual (piscada)")]
    [Tooltip("Intervalo (s) entre cada troca liga/desliga dos renderers.")]
    [SerializeField] private float blinkInterval = 0.11f;
    [Tooltip("Se falso, mantém o kart sempre visível (sem piscar) durante o ghost.")]
    [SerializeField] private bool blink = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private bool active;
    private float endTime;
    private float nextBlinkTime;
    private bool renderersHidden;

    private Collider[] ownColliders;
    private Renderer[] ownRenderers;

    // Pares (colisor do outro kart) que tivemos que ignorar — guardados para restaurar.
    private readonly List<Collider> ignoredOtherColliders = new List<Collider>();

    public bool IsActive => active;

    /// <summary>Aplica/renova o estado ghost a um kart. Cria o componente se necessário.</summary>
    public static KartTemporaryGhostState Apply(GameObject target, float duration)
    {
        if (target == null)
            return null;

        KartTemporaryGhostState ghost = target.GetComponent<KartTemporaryGhostState>();
        if (ghost == null)
            ghost = target.AddComponent<KartTemporaryGhostState>();

        ghost.Begin(duration);
        return ghost;
    }

    private void Awake()
    {
        CacheOwnComponents();
    }

    private void CacheOwnComponents()
    {
        List<Collider> colliders = new List<Collider>();
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null && !col.isTrigger)
                colliders.Add(col);
        }
        ownColliders = colliders.ToArray();

        ownRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void Begin(float duration)
    {
        if (duration <= 0f)
            duration = defaultDuration;

        endTime = Mathf.Max(endTime, Time.time + duration);

        if (active)
            return;

        active = true;
        nextBlinkTime = Time.time + blinkInterval;
        IgnoreOtherKarts();

        if (debugMode)
            Debug.Log($"[Ghost] {name} entrou em estado ghost por {duration:0.0}s.");
    }

    private void Update()
    {
        if (!active)
            return;

        if (Time.time >= endTime)
        {
            End();
            return;
        }

        if (blink && Time.time >= nextBlinkTime)
        {
            nextBlinkTime = Time.time + Mathf.Max(0.02f, blinkInterval);
            SetRenderersHidden(!renderersHidden);
        }
    }

    private void IgnoreOtherKarts()
    {
        if (ownColliders == null || ownColliders.Length == 0)
            return;

        KartController[] allKarts = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
        foreach (KartController other in allKarts)
        {
            if (other == null || other.gameObject == gameObject)
                continue;

            foreach (Collider otherCol in other.GetComponentsInChildren<Collider>(true))
            {
                if (otherCol == null || otherCol.isTrigger)
                    continue;

                SetIgnore(otherCol, true);
                ignoredOtherColliders.Add(otherCol);
            }
        }
    }

    private void SetIgnore(Collider otherCol, bool ignore)
    {
        if (otherCol == null)
            return;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
                Physics.IgnoreCollision(ownColliders[i], otherCol, ignore);
        }
    }

    private void RestoreCollisions()
    {
        for (int i = 0; i < ignoredOtherColliders.Count; i++)
            SetIgnore(ignoredOtherColliders[i], false);

        ignoredOtherColliders.Clear();
    }

    private void SetRenderersHidden(bool hidden)
    {
        renderersHidden = hidden;

        if (ownRenderers == null)
            return;

        for (int i = 0; i < ownRenderers.Length; i++)
        {
            if (ownRenderers[i] != null)
                ownRenderers[i].enabled = !hidden;
        }
    }

    private void End()
    {
        if (!active)
            return;

        active = false;
        RestoreCollisions();
        SetRenderersHidden(false);

        if (debugMode)
            Debug.Log($"[Ghost] {name} saiu do estado ghost.");
    }

    private void OnDisable()
    {
        // Restaura tudo se o carro for desativado/resetado/terminar a corrida durante o ghost.
        End();
    }
}
