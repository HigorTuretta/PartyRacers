using UnityEngine;
using UnityEngine.UI;
using Hovl;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Mostra o efeito fullscreen de vento do turbo (Screen wind.prefab) APENAS no cliente local
// dono do kart, enquanto o boost estiver ativo.
//
// Por que isto resolve o "efeito de turbo aparecendo para todos":
//  - É local POR CONSTRUÇÃO. Só age quando KartLocalRig.IsLocalPlayer == true, que é:
//      * true em modo offline/single-player (gameplay local intacto);
//      * true APENAS para o Owner no online (KartNetworkSync.ApplyNetworkRole seta isso).
//  - Nunca instancia nada para karts remotos, então nenhum cliente vê o vento de outro player.
//  - Substitui o antigo VFX fullscreen compartilhado de cena (SpeedBoost.vfx_Hyperdrive_01),
//    que acendia para todos por estar num objeto único da cena.
//
// Reutiliza UMA instância do prefab por kart, ancorada na câmera local (o próprio
// Hovl.HS_ScreenEffect do prefab se prende à Camera.main no Start). Anima a saída deixando as
// partículas existentes morrerem antes de desativar.
[RequireComponent(typeof(KartController))]
public class KartTurboScreenEffect : MonoBehaviour
{
    private const string ScreenWindPrefabPath = "Assets/_Projeto/Materials/Fullscreen effects/Prefabs/Screen wind.prefab";

    [Header("Referências")]
    [SerializeField] private KartController kart;
    [Tooltip("Define se este kart é o dono local. Vazio = busca no mesmo GameObject. " +
             "Quando nulo, assume local (offline).")]
    [SerializeField] private KartLocalRig localRig;

    [Header("Efeito")]
    [Tooltip("Prefab de tela do turbo. Esperado: " +
             "Assets/_Projeto/Materials/Fullscreen effects/Prefabs/Screen wind.prefab")]
    [SerializeField] private GameObject screenWindPrefab;
    [Tooltip("Câmera onde o efeito é ancorado. Vazio = Camera.main (a câmera do player local).")]
    [SerializeField] private Camera targetCamera;

    [Header("Tuning")]
    [Tooltip("Segundos que o efeito continua emitindo após o boost acabar, para uma saída suave.")]
    [SerializeField] private float fadeOutDelay = 0.35f;
    [SerializeField] private float particleSettleDelay = 0.25f;
    [SerializeField] private bool useEdgeVignette = false;
    [SerializeField, Range(0f, 0.5f)] private float edgeDarknessAlpha = 0.22f;
    [SerializeField] private float vignetteFadeSpeed = 8f;
    [SerializeField] private int vignetteSortingOrder = 90;
    [SerializeField, Min(1f)] private float screenEffectOverscan = 1.18f;
    [SerializeField, Min(0.01f)] private float cameraPlanePadding = 0.08f;

    private GameObject instance;
    private ParticleSystem[] particles;
    private CanvasGroup vignetteGroup;
    private bool effectActive;
    private float stopAtTime;
    private float deactivateAtTime;
    private float vignetteTargetAlpha;

    private void Awake()
    {
        if (kart == null) kart = GetComponent<KartController>();
        if (localRig == null) localRig = GetComponent<KartLocalRig>();
        useEdgeVignette = false;
        ResolveMissingPrefab();
    }

    public void SetScreenWindPrefab(GameObject prefab)
    {
        if (prefab == null || screenWindPrefab == prefab)
            return;

        screenWindPrefab = prefab;
    }

    // Sem rede / offline: localRig nulo => trata como local. Online: respeita o Owner.
    private bool IsLocalOwner => localRig == null || localRig.IsLocalPlayer;

    private void Update()
    {
        if (!IsLocalOwner)
        {
            if (effectActive)
                StopEffect(immediate: true);
            UpdateEdgeVignette();
            return;
        }

        bool boosting = kart != null && kart.IsBoosting;

        if (boosting)
        {
            if (!effectActive)
                StartEffect();

            // Renova o prazo de saída enquanto o turbo seguir ativo.
            stopAtTime = Time.time + fadeOutDelay;
        }
        else if (effectActive && Time.time >= stopAtTime)
        {
            StopEffect(immediate: false);
        }

        if (!effectActive && instance != null && instance.activeSelf && Time.time >= deactivateAtTime)
            instance.SetActive(false);

        UpdateEdgeVignette();
    }

    private void StartEffect()
    {
        if (!EnsureInstance())
            return;

        effectActive = true;
        instance.SetActive(true);
        ConfigureScreenEffect(instance);
        deactivateAtTime = 0f;
        vignetteTargetAlpha = edgeDarknessAlpha;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            particles[i].Clear();
            particles[i].Play();
        }
    }

    private void StopEffect(bool immediate)
    {
        effectActive = false;
        vignetteTargetAlpha = 0f;

        if (instance == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            particles[i].Stop(
                true,
                immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        if (immediate)
        {
            instance.SetActive(false);
            deactivateAtTime = 0f;
        }
        else
        {
            deactivateAtTime = Time.time + particleSettleDelay;
        }
    }

    private bool EnsureInstance()
    {
        if (instance != null)
            return true;

        if (screenWindPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: KartTurboScreenEffect sem 'screenWindPrefab' atribuído " +
                "(esperado Screen wind.prefab). Turbo seguirá funcionando, mas sem efeito de tela.",
                this);
            return false;
        }

        instance = Instantiate(screenWindPrefab);
        instance.name = screenWindPrefab.name + " (Local Turbo)";
        instance.SetActive(false);
        particles = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);

        // Configure before the first visible Play so the prefab covers the camera from frame one.
        ConfigureScreenEffect(instance);

        return true;
    }

    private void ResolveMissingPrefab()
    {
        if (screenWindPrefab != null)
            return;

        KartTurboScreenEffect[] siblings = GetComponents<KartTurboScreenEffect>();
        for (int i = 0; i < siblings.Length; i++)
        {
            KartTurboScreenEffect sibling = siblings[i];
            if (sibling == null || sibling == this)
                continue;

            if (sibling.screenWindPrefab != null)
            {
                enabled = false;
                return;
            }
        }

#if UNITY_EDITOR
        screenWindPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenWindPrefabPath);
#endif
    }

    private void UpdateEdgeVignette()
    {
        if (!useEdgeVignette)
            return;

        if (vignetteGroup == null && vignetteTargetAlpha > 0f)
            EnsureEdgeVignette();

        if (vignetteGroup == null)
            return;

        vignetteGroup.alpha = Mathf.MoveTowards(
            vignetteGroup.alpha,
            vignetteTargetAlpha,
            vignetteFadeSpeed * Time.unscaledDeltaTime);

        bool shouldShow = vignetteGroup.alpha > 0.001f || vignetteTargetAlpha > 0f;
        if (vignetteGroup.gameObject.activeSelf != shouldShow)
            vignetteGroup.gameObject.SetActive(shouldShow);
    }

    private void EnsureEdgeVignette()
    {
        GameObject canvasGo = new GameObject("Turbo Edge Vignette",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = vignetteSortingOrder;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        vignetteGroup = canvasGo.GetComponent<CanvasGroup>();
        vignetteGroup.alpha = 0f;
        vignetteGroup.blocksRaycasts = false;
        vignetteGroup.interactable = false;

        AddEdge(canvasGo.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -140f), Vector2.zero);
        AddEdge(canvasGo.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 140f));
        AddEdge(canvasGo.transform, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(160f, 0f));
        AddEdge(canvasGo.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-160f, 0f), Vector2.zero);

        canvasGo.SetActive(false);
    }

    private static void AddEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void ConfigureScreenEffect(GameObject root)
    {
        if (root == null)
            return;

        Camera cam = ResolveTargetCamera();
        HS_ScreenEffect[] effects = root.GetComponentsInChildren<HS_ScreenEffect>(includeInactive: true);
        for (int i = 0; i < effects.Length; i++)
        {
            HS_ScreenEffect effect = effects[i];
            if (effect == null)
                continue;

            effect.sourceCamera = cam;
            effect.parentToCameraOnStart = true;
            effect.snapOnStart = true;
            effect.screenOverscan = Mathf.Max(1f, screenEffectOverscan);
            effect.nearPlanePadding = Mathf.Max(0.01f, cameraPlanePadding);

            if (cam != null)
                effect.fallbackDistance = Mathf.Max(effect.fallbackDistance, cam.nearClipPlane + effect.nearPlanePadding);
        }
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        targetCamera = Camera.main;
        return targetCamera;
    }

    private void OnDestroy()
    {
        if (instance != null)
            Destroy(instance);
        if (vignetteGroup != null)
            Destroy(vignetteGroup.gameObject);
    }
}
