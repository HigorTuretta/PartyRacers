using UnityEngine;

// HUD do kart — LOADER do HUD componentizado (prefab HUDRoot), não mais construtor procedural.
//
// Robustez entre cenas (corrige "HUD só aparecia no DEMO"):
//  - Resolve o prefab por: campo serializado 'hudRootPrefab' OU fallback Resources.Load("HUDRoot").
//    Assim funciona em QUALQUER cena de corrida (DEMO, MiniGolfeRun, MainTrack...), inclusive em
//    HUDs avulsos legados de cena cujo campo está vazio.
//  - Liga-se ao kart LOCAL correto (KartLocalRig.IsLocalPlayer), reavaliando até o kart spawnar
//    (karts são instanciados em runtime pelo RaceManager/rede).
//  - Garante UM HUD ativo por vez e nunca duplica para karts remotos. Esconde no lobby.
//
// A contagem regressiva é do CountdownUI (eventos do RaceManager) — independente deste componente.
[RequireComponent(typeof(RectTransform))]
public class KartHUDOverlay : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Kart alvo. Se vazio, usa o kart deste objeto (se houver) ou busca o kart LOCAL na cena.")]
    [SerializeField] private KartController kart;
    [Tooltip("Prefab do HUD (HUDRoot). Se vazio, carrega de Resources/HUDRoot.")]
    [SerializeField] private GameObject hudRootPrefab;

    [Header("Contexto de Cena")]
    [Tooltip("Cena de lobby/garagem onde o HUD de corrida NÃO deve aparecer.")]
    [SerializeField] private string lobbySceneName = "Garage";

    [Tooltip("Caminho em Resources usado quando 'hudRootPrefab' está vazio.")]
    [SerializeField] private string hudResourcePath = "HUDRoot";

    private static KartHUDOverlay activeHud;

    private HUDRootUI hudInstance;
    private Canvas legacyCanvas;
    private bool attachedToKart; // true se este HUD está no próprio GameObject do kart

    private bool InLobby =>
        !string.IsNullOrEmpty(lobbySceneName)
        && gameObject.scene.IsValid()
        && gameObject.scene.name == lobbySceneName;

    private void Awake()
    {
        KartController parentKart = GetComponentInParent<KartController>();
        if (parentKart != null)
        {
            kart = parentKart;
            attachedToKart = true;
        }

        // Desliga o Canvas legado (HUD procedural antiga) para não competir com o HUD novo.
        legacyCanvas = GetComponentInParent<Canvas>();
        if (legacyCanvas != null)
            legacyCanvas.enabled = false;
    }

    private void OnEnable() => Refresh();
    private void Start() => Refresh();
    private void Update() => Refresh();

    private void OnDisable()
    {
        if (activeHud == this)
            activeHud = null;
        HideInstance();
    }

    private void OnDestroy()
    {
        if (hudInstance != null)
            Destroy(hudInstance.gameObject);
    }

    private void Refresh()
    {
        // HUD avulso de cena: garante que aponta para o kart LOCAL (resolvido quando spawnar).
        if (!attachedToKart && (kart == null || !IsLocal(kart)))
            kart = FindLocalKart();

        // Em lobby ou para kart remoto: sem HUD.
        if (InLobby || (kart != null && !IsLocal(kart)))
        {
            HideInstance();
            if (activeHud == this)
                activeHud = null;
            return;
        }

        // Reivindica o HUD único (cobre dono anterior destruído).
        if (activeHud != null && activeHud != this)
            return;

        activeHud = this;
        EnsureInstance();

        if (hudInstance != null)
        {
            hudInstance.Bind(kart); // re-bind contínuo (kart pode ter spawnado agora)
            if (!hudInstance.gameObject.activeSelf)
                hudInstance.gameObject.SetActive(true);
        }
    }

    private void EnsureInstance()
    {
        if (hudInstance != null)
            return;

        GameObject prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError(
                $"{name}: HUDRoot não encontrado (campo 'hudRootPrefab' vazio e Resources/{hudResourcePath} ausente). " +
                "Rode 'PartyRacers/HUD/Gerar Prefabs do HUD'.", this);
            activeHud = null; // libera para outro HUD tentar
            return;
        }

        GameObject go = Instantiate(prefab);
        go.name = prefab.name;

        hudInstance = go.GetComponent<HUDRootUI>() ?? go.GetComponentInChildren<HUDRootUI>();
        if (hudInstance == null)
        {
            Debug.LogError($"{name}: HUDRoot '{prefab.name}' sem componente HUDRootUI.", this);
            Destroy(go);
            activeHud = null;
            return;
        }
    }

    private GameObject ResolvePrefab()
    {
        if (hudRootPrefab != null)
            return hudRootPrefab;

        if (!string.IsNullOrEmpty(hudResourcePath))
            hudRootPrefab = Resources.Load<GameObject>(hudResourcePath);

        return hudRootPrefab;
    }

    private void HideInstance()
    {
        if (hudInstance != null && hudInstance.gameObject.activeSelf)
            hudInstance.gameObject.SetActive(false);
    }

    // ----------------------------------------------------------------- Helpers

    private static bool IsLocal(KartController k)
    {
        if (k == null)
            return false;

        KartLocalRig rig = k.GetComponent<KartLocalRig>();
        return rig == null || rig.IsLocalPlayer;
    }

    // Encontra o kart do jogador LOCAL: prioriza o que tem KartLocalRig.IsLocalPlayer == true
    // (online: o Owner). Offline (karts sem rig), usa o primeiro encontrado.
    private static KartController FindLocalKart()
    {
        KartController[] all = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
        KartController firstRigless = null;

        foreach (KartController k in all)
        {
            if (k == null)
                continue;

            KartLocalRig rig = k.GetComponent<KartLocalRig>();
            if (rig == null)
            {
                if (firstRigless == null)
                    firstRigless = k;
                continue;
            }

            if (rig.IsLocalPlayer)
                return k;
        }

        return firstRigless;
    }
}
