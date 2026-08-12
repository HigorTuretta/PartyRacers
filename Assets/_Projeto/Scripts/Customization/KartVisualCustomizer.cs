using System;
using System.Collections.Generic;
using UnityEngine;
using ithappy;

// Substitui o visual do kart pelos modelos do pack Modular_Cyber_Racing_Cars e
// expõe a customização (carro, cor, peças, rodas, motorista) tanto para a corrida
// (lê a seleção salva) quanto para a Garagem (troca em runtime).
//
// IMPORTANTE: não altera nenhum sistema de gameplay. Apenas monta o modelo sob
// Body/CarModelRoot e religa as rodas no KartArcadeVisuals via SetWheels.
public class KartVisualCustomizer : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Opcional. Se presente, as rodas instanciadas são religadas para a animação arcade.")]
    [SerializeField] private KartArcadeVisuals arcadeVisuals;
    [Tooltip("Nó (sob Body) onde o rig do carro é instanciado.")]
    [SerializeField] private Transform carModelRoot;

    [Header("Carros (rigs do pack)")]
    [SerializeField] private List<CarCustomizer> carRigs = new List<CarCustomizer>();

    [Header("Ajuste do modelo")]
    [SerializeField] private Vector3 rigLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 rigLocalEuler = Vector3.zero;
    [SerializeField] private float rigScale = 1f;

    [Header("Pintura")]
    [Tooltip("Material de pintura do pack (Color.mat) usado para identificar os slots a tingir.")]
    [SerializeField] private Material paintMaterial;
    [Tooltip("Trecho do nome do material de pintura (fallback de detecção).")]
    [SerializeField] private string paintMaterialNameContains = "Color";
    [SerializeField]
    private Color[] paintPalette =
    {
        new Color(0.85f, 0.12f, 0.14f), // vermelho
        new Color(0.10f, 0.45f, 0.90f), // azul
        new Color(0.96f, 0.78f, 0.10f), // amarelo
        new Color(0.10f, 0.70f, 0.35f), // verde
        new Color(0.95f, 0.95f, 0.97f), // branco
        new Color(0.08f, 0.09f, 0.11f), // preto
        new Color(1.00f, 0.45f, 0.05f), // laranja
        new Color(0.55f, 0.20f, 0.80f), // roxo
    };

    [Header("Comportamento")]
    [Tooltip("Se ligado, lê KartGarageSelection no Start (corrida). Desligue para preview controlado externamente.")]
    [SerializeField] private bool loadSelectionOnStart = true;

    [Tooltip("Desligue em carros de FOTO: eles trocam de peça a cada frame e cada troca gravaria " +
             "a escolha do jogador por cima da real.")]
    [SerializeField] private bool persistSelection = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private CarCustomizer _currentRig;
    private int _carIndex;
    private int _colorIndex;
    private MaterialPropertyBlock _mpb;

    public event Action CarRebuilt;

    public int CarCount => carRigs != null ? carRigs.Count : 0;
    public int CarIndex => _carIndex;
    public int ColorIndex => _colorIndex;
    public int ColorCount => paintPalette != null ? paintPalette.Length : 0;
    public Color[] PaintPalette => paintPalette;
    public CarCustomizer CurrentRig => _currentRig;

    public void SetLoadSelectionOnStart(bool enabled)
    {
        loadSelectionOnStart = enabled;
    }

    /// <summary>
    /// Liga/desliga a gravação da escolha em <see cref="KartGarageSelection"/>.
    ///
    /// O estúdio de previews monta uma CÓPIA deste componente e passa por todas as variantes de
    /// uma peça para fotografá-las. Cada <see cref="SetElement"/> gravava e salvava — ao abrir a
    /// aba RODAS, a roda equipada virava a última fotografada, e a moldura de "equipado" apontava
    /// para um card que o jogador nunca escolheu. O clone chama isto com <c>false</c>.
    /// </summary>
    public void SetPersistSelection(bool enabled)
    {
        persistSelection = enabled;
    }

    /// <summary>
    /// Nome do prefab da variante — a única fonte honesta de rótulo para os cards.
    ///
    /// O pack nomeia as peças (<c>Car_01_FrontBumper_Cool</c>, <c>Wheel_07</c>, <c>Empty</c>) e a
    /// ORDEM das variantes muda de carro para carro: no Car_01 o para-choque traseiro vem
    /// Cool/Simple e nos demais Simple/Cool. Rotular por índice ("TRASEIRA 01") mostraria nomes
    /// diferentes para a mesma peça conforme o modelo.
    /// </summary>
    public string GetElementVariantName(CarElementName element, int index)
    {
        if (_currentRig == null || _currentRig.Elements == null)
            return string.Empty;

        foreach (CarElementSettings settings in _currentRig.Elements)
        {
            if (settings == null || settings.ElementName != element || settings.Elements == null)
                continue;

            if (index < 0 || index >= settings.Elements.Count)
                return string.Empty;

            GameObject prefab = settings.Elements[index];
            return prefab != null ? prefab.name : string.Empty;
        }

        return string.Empty;
    }

    /// <summary>Nome real do prefab de carro usado por um card da garagem.</summary>
    public string GetCarVariantName(int index)
    {
        if (carRigs == null || index < 0 || index >= carRigs.Count || carRigs[index] == null)
            return string.Empty;

        return carRigs[index].name;
    }

    public void ApplySelection(KartVisualSelection selection)
    {
        ApplySelection(selection.CarIndex, selection.ColorIndex, selection.ElementData);
    }

    public void ApplySelection(int carIndex, int colorIndex, string elementData)
    {
        if (CarCount == 0)
            return;

        _carIndex = Mathf.Clamp(carIndex, 0, Mathf.Max(0, CarCount - 1));
        _colorIndex = colorIndex;
        BuildCar(applySavedElements: false, explicitElements: KartGarageSelection.DecodeElements(elementData));
    }

    /// <summary>
    /// Reaplica a customização salva na garagem (PlayerPrefs) sem gravar nada de volta.
    /// Necessário para carros de pré-visualização com <c>loadSelectionOnStart</c> desligado: sem
    /// isto eles nasciam com o carro padrão, e a primeira troca feita a partir desse estado salvava
    /// o padrão por cima da escolha real do jogador — o "reset" ao voltar da corrida.
    /// </summary>
    public void ApplySavedSelection()
    {
        KartGarageSelection.EnsureLoaded();
        ApplySelection(KartGarageSelection.Capture());
    }

    private void Start()
    {
        EnsureBuilt();
    }

    // Garante que o carro foi montado (idempotente). Seguro para chamar de outro
    // componente independentemente da ordem de execução de Start.
    public void EnsureBuilt()
    {
        if (_currentRig != null)
            return;

        if (loadSelectionOnStart)
        {
            KartGarageSelection.EnsureLoaded();
            _carIndex = Mathf.Clamp(KartGarageSelection.CarIndex, 0, Mathf.Max(0, CarCount - 1));
            _colorIndex = KartGarageSelection.ColorIndex;
            BuildCar(applySavedElements: true);
        }
        else
        {
            BuildCar(applySavedElements: false);
        }
    }

    // ---------------------------------------------------------------- API pública
    public void SetCar(int index)
    {
        if (CarCount == 0)
            return;

        _carIndex = ((index % CarCount) + CarCount) % CarCount;

        if (persistSelection)
            KartGarageSelection.CarIndex = _carIndex;

        BuildCar(applySavedElements: true);

        if (persistSelection)
            KartGarageSelection.Save();
    }

    public void NextCar() => SetCar(_carIndex + 1);
    public void PreviousCar() => SetCar(_carIndex - 1);

    public void SetColor(int index)
    {
        if (ColorCount == 0)
            return;

        _colorIndex = ((index % ColorCount) + ColorCount) % ColorCount;
        ApplyPaintColor();

        if (!persistSelection)
            return;

        KartGarageSelection.ColorIndex = _colorIndex;
        KartGarageSelection.Save();
    }

    /// <summary>
    /// Reaplica a pintura no rig inteiro. Necessario para sistemas que instanciam pecas direto no
    /// <see cref="CarCustomizer"/> (como os bots), sem passar por <see cref="SetElement"/>.
    /// </summary>
    public void RefreshPaint()
    {
        ApplyPaintColor();
    }

    public int GetElementVariantCount(CarElementName element)
        => _currentRig != null ? _currentRig.GetVariantCount(element) : 0;

    /// <summary>
    /// Índice equipado, já preso ao catálogo do carro ATUAL.
    ///
    /// A seleção é global e as listas não têm o mesmo tamanho: quem estava na roda 12 e troca para
    /// um modelo cujo para-choque tem 2 variantes voltaria com "equipado = 12" numa grade de dois
    /// cards, e nenhum card apareceria marcado.
    /// </summary>
    public int GetElementIndex(CarElementName element)
    {
        int saved = KartGarageSelection.GetElement(element);
        int count = GetElementVariantCount(element);
        return count > 0 ? Mathf.Clamp(saved, 0, count - 1) : saved;
    }

    public void SetElement(CarElementName element, int index)
    {
        if (_currentRig == null)
            return;

        int count = _currentRig.GetVariantCount(element);
        if (count == 0)
            return;

        index = ((index % count) + count) % count;
        _currentRig.SwitchCarElement(element, index);
        ApplyPaintColor();

        if (!persistSelection)
            return;

        KartGarageSelection.SetElement(element, index);
        KartGarageSelection.Save();
    }

    /// <summary>Caixa de todos os renderers do carro montado.</summary>
    public bool TryGetCarBounds(out Bounds bounds)
    {
        bounds = default;
        if (_currentRig == null)
            return false;

        return Unir(_currentRig.GetComponentsInChildren<Renderer>(), ref bounds);
    }

    /// <summary>
    /// Centro e raio do carro que NÃO mudam quando ele gira.
    ///
    /// A caixa de <see cref="Renderer.bounds"/> é alinhada aos eixos do mundo: um kart comprido
    /// medido de lado dá uma caixa menor do que o mesmo kart a 45°. Quem enquadra pela magnitude
    /// dela recalcula a distância a cada frame do giro, e o carro do lobby pulsa de tamanho a cada
    /// volta. Medido no espaço do modelo, o número é o mesmo em qualquer ângulo.
    ///
    /// A medida é feita uma vez por rig — trocar de peça invalida (os renderers mudam).
    /// </summary>
    public bool TryGetCarShape(out Vector3 center, out float radius)
    {
        center = default;
        radius = 0f;

        if (_currentRig == null)
            return false;

        Transform raiz = _currentRig.transform;
        Renderer[] renderers = _currentRig.GetComponentsInChildren<Renderer>();

        if (_shapeRig != _currentRig || _shapeCount != renderers.Length)
        {
            Matrix4x4 paraLocal = raiz.worldToLocalMatrix;
            bool achou = false;
            var caixa = new Bounds();

            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer)
                    continue;

                Bounds local = r.localBounds;
                Matrix4x4 m = paraLocal * r.localToWorldMatrix;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 canto = local.center + new Vector3(
                        ((i & 1) == 0 ? -1f : 1f) * local.extents.x,
                        ((i & 2) == 0 ? -1f : 1f) * local.extents.y,
                        ((i & 4) == 0 ? -1f : 1f) * local.extents.z);

                    Vector3 p = m.MultiplyPoint3x4(canto);

                    if (!achou)
                    {
                        caixa = new Bounds(p, Vector3.zero);
                        achou = true;
                    }
                    else
                    {
                        caixa.Encapsulate(p);
                    }
                }
            }

            if (!achou)
                return false;

            _shapeLocal = caixa;
            _shapeRig = _currentRig;
            _shapeCount = renderers.Length;
        }

        Vector3 escala = raiz.lossyScale;
        float uniforme = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z));

        center = raiz.TransformPoint(_shapeLocal.center);
        radius = _shapeLocal.extents.magnitude * uniforme;
        return radius > 0.001f;
    }

    private CarCustomizer _shapeRig;
    private int _shapeCount = -1;
    private Bounds _shapeLocal;

    /// <summary>
    /// Caixa da PEÇA montada agora naquele elemento, em espaço de mundo.
    ///
    /// Falso quando a variante equipada é o <c>Empty</c> do pack (aerofólio/adesivo/motor/farol de
    /// milha "desligados") — não há renderer nenhum para medir. Quem enquadra deve, nesse caso,
    /// mirar onde a peça ESTARIA, e não desistir: o card do "sem aerofólio" precisa mostrar
    /// exatamente o mesmo recorte do card "com", senão os dois não se comparam.
    /// </summary>
    public bool TryGetElementBounds(CarElementName element, out Bounds bounds)
    {
        bounds = default;
        if (_currentRig == null)
            return false;

        IReadOnlyList<GameObject> spawned = _currentRig.GetSpawnedElements(element);
        if (spawned == null || spawned.Count == 0)
            return false;

        // Só a PRIMEIRA cópia. A roda é instanciada quatro vezes, uma por eixo de montagem, e as
        // quatro juntas dão uma caixa do tamanho do carro — a garagem enquadrava o carro inteiro e
        // as quinze rodas do catálogo saíam em quinze cards idênticos. Cópias do mesmo elemento são
        // iguais por construção, então uma basta e é ela que se vê de perto.
        foreach (GameObject go in spawned)
        {
            if (go == null)
                continue;

            if (Unir(go.GetComponentsInChildren<Renderer>(), ref bounds))
                return bounds.extents.magnitude > 0.0005f;
        }

        return false;
    }

    private static bool Unir(Renderer[] renderers, ref Bounds bounds, bool jaComecou = false)
    {
        bool achou = jaComecou;

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer)
                continue;

            if (!achou)
            {
                bounds = r.bounds;
                achou = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return achou;
    }

    // ---------------------------------------------------------------- Construção
    private void BuildCar(bool applySavedElements, Dictionary<CarElementName, int> explicitElements = null)
    {
        if (carModelRoot == null || CarCount == 0)
            return;

        // Some com TODO carro que já esteja montado, não só com o que este componente lembra de ter
        // criado. `_currentRig` é um campo de runtime: ele nasce nulo a cada play, então um rig
        // SALVO na cena (de quando a garagem foi aberta no editor) não era apagado por ninguém.
        // Dois carros ficavam empilhados no mesmo lugar; trocar de peça mexia só no de baixo e a
        // metade que o jogador via continuava igual — a garagem parecia não funcionar.
        _currentRig = null;

        for (int i = carModelRoot.childCount - 1; i >= 0; i--)
        {
            Transform filho = carModelRoot.GetChild(i);
            if (filho.GetComponent<CarCustomizer>() == null)
                continue;

            // fora do playmode Destroy é adiado e nunca roda: o rig antigo ficaria na cena
            // sobreposto ao novo, e a Garagem mostraria o carro errado no editor.
            if (Application.isPlaying)
                Destroy(filho.gameObject);
            else
                DestroyImmediate(filho.gameObject);
        }

        CarCustomizer prefab = carRigs[Mathf.Clamp(_carIndex, 0, CarCount - 1)];
        if (prefab == null)
            return;

        _currentRig = Instantiate(prefab, carModelRoot);
        Transform t = _currentRig.transform;
        t.localPosition = rigLocalPosition;
        t.localRotation = Quaternion.Euler(rigLocalEuler);
        t.localScale = Vector3.one * rigScale;

        _currentRig.Initialize();

        if (explicitElements != null)
        {
            foreach (var element in _currentRig.Elements)
            {
                if (explicitElements.TryGetValue(element.ElementName, out int index) && index > 0)
                    _currentRig.SwitchCarElement(element.ElementName, index);
            }
        }
        else if (applySavedElements)
        {
            foreach (var element in _currentRig.Elements)
            {
                int saved = KartGarageSelection.GetElement(element.ElementName);
                if (saved > 0)
                    _currentRig.SwitchCarElement(element.ElementName, saved);
            }
        }

        RebindWheels();
        ApplyPaintColor();

        CarRebuilt?.Invoke();
    }

    private void RebindWheels()
    {
        if (arcadeVisuals == null || _currentRig == null)
            return;

        Transform[] front = _currentRig.VisualWheelsFront;
        Transform[] rear = _currentRig.VisualWheelsRear;

        if (front == null || rear == null || front.Length < 2 || rear.Length < 2)
            return;

        arcadeVisuals.SetWheels(front[0], front[1], rear[0], rear[1]);
    }

    private void ApplyPaintColor()
    {
        if (_currentRig == null || ColorCount == 0)
            return;

        Color color = paintPalette[Mathf.Clamp(_colorIndex, 0, ColorCount - 1)];
        _mpb ??= new MaterialPropertyBlock();

        Renderer[] renderers = _currentRig.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int slot = 0; slot < mats.Length; slot++)
            {
                if (!IsPaintMaterial(mats[slot]))
                    continue;

                renderer.GetPropertyBlock(_mpb, slot);

                // O atlas do pack nao e somente uma paleta: o valor de cada pixel carrega os
                // recortes, luz e sombra autorados para as pecas. Substitui-lo por whiteTexture
                // deixa o carro inteiro chapado. Multiplicar o atlas colorido tambem e incorreto
                // (branco sobre a faixa rosa continua rosa). A copia neutra preserva o VALOR do
                // atlas e remove apenas o matiz; a cor escolhida volta a ser aplicada pelo shader.
                Texture sourceAtlas = GetPaintAtlas(mats[slot]);
                Texture neutralAtlas = NeutralPaintAtlasCache.Get(sourceAtlas);
                if (neutralAtlas != null)
                {
                    _mpb.SetTexture(BaseMapId, neutralAtlas);
                    _mpb.SetTexture(MainTexId, neutralAtlas);
                }
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_mpb, slot);
            }
        }
    }

    private static Texture GetPaintAtlas(Material material)
    {
        if (material == null)
            return null;

        Texture atlas = material.HasProperty(BaseMapId) ? material.GetTexture(BaseMapId) : null;
        if (atlas == null && material.HasProperty(MainTexId))
            atlas = material.GetTexture(MainTexId);

        return atlas;
    }

    private bool IsPaintMaterial(Material material)
    {
        if (material == null)
            return false;

        if (paintMaterial != null)
            return material == paintMaterial || material.name == paintMaterial.name
                || material.name == paintMaterial.name + " (Instance)";

        return !string.IsNullOrEmpty(paintMaterialNameContains)
            && material.name.IndexOf(paintMaterialNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Cria uma unica derivacao neutra por atlas, em runtime, sem alterar a textura nem o material
    /// do pacote. O maior canal RGB representa o "value" do atlas HSV e conserva exatamente sua
    /// modelagem de volume; somente o matiz original e removido.
    /// </summary>
    private static class NeutralPaintAtlasCache
    {
        private sealed class Entry
        {
            public Texture Source;
            public Texture2D Neutral;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (Entry entry in Entries.Values)
                DestroyGenerated(entry.Neutral);

            Entries.Clear();
        }

        public static Texture Get(Texture source)
        {
            if (source == null)
                return null;

            int id = source.GetInstanceID();
            if (Entries.TryGetValue(id, out Entry cached) && cached.Source == source && cached.Neutral != null)
                return cached.Neutral;

            Texture2D neutral = Build(source);
            if (neutral == null)
                return source;

            Entries[id] = new Entry { Source = source, Neutral = neutral };
            return neutral;
        }

        private static Texture2D Build(Texture source)
        {
            // 512 px mantem os recortes largos do atlas e evita uma copia RGBA de 4 MiB por
            // sessao. O proprio material usa filtragem bilinear + mipmaps, entao a leitura visual
            // permanece igual na escala em que o carro aparece.
            const int MaxRuntimeAtlasSize = 512;
            float scale = Mathf.Min(1f, MaxRuntimeAtlasSize / (float)Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            bool mipChain = source.mipmapCount > 1;
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Texture2D readable = null;

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);

                Color32[] pixels = readable.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    byte value = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                    pixels[i] = new Color32(value, value, value, pixel.a);
                }

                var neutral = new Texture2D(width, height, TextureFormat.RGBA32, mipChain, false)
                {
                    name = source.name + "_PaintNeutral_Runtime",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    anisoLevel = source.anisoLevel,
                    mipMapBias = source.mipMapBias,
                    wrapModeU = source.wrapModeU,
                    wrapModeV = source.wrapModeV,
                    wrapModeW = source.wrapModeW
                };

                neutral.SetPixels32(pixels);
                neutral.Apply(mipChain, true);
                return neutral;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Nao foi possivel neutralizar o atlas de pintura '{source.name}': {exception.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                DestroyGenerated(readable);
            }
        }

        private static void DestroyGenerated(UnityEngine.Object generated)
        {
            if (generated == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(generated);
            else
                UnityEngine.Object.DestroyImmediate(generated);
        }
    }
}
