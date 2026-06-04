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

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

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
        KartGarageSelection.CarIndex = _carIndex;
        BuildCar(applySavedElements: true);
        KartGarageSelection.Save();
    }

    public void NextCar() => SetCar(_carIndex + 1);
    public void PreviousCar() => SetCar(_carIndex - 1);

    public void SetColor(int index)
    {
        if (ColorCount == 0)
            return;

        _colorIndex = ((index % ColorCount) + ColorCount) % ColorCount;
        KartGarageSelection.ColorIndex = _colorIndex;
        ApplyPaintColor();
        KartGarageSelection.Save();
    }

    public int GetElementVariantCount(CarElementName element)
        => _currentRig != null ? _currentRig.GetVariantCount(element) : 0;

    public int GetElementIndex(CarElementName element)
        => KartGarageSelection.GetElement(element);

    public void SetElement(CarElementName element, int index)
    {
        if (_currentRig == null)
            return;

        int count = _currentRig.GetVariantCount(element);
        if (count == 0)
            return;

        index = ((index % count) + count) % count;
        KartGarageSelection.SetElement(element, index);
        _currentRig.SwitchCarElement(element, index);
        ApplyPaintColor();
        KartGarageSelection.Save();
    }

    // ---------------------------------------------------------------- Construção
    private void BuildCar(bool applySavedElements, Dictionary<CarElementName, int> explicitElements = null)
    {
        if (carModelRoot == null || CarCount == 0)
            return;

        if (_currentRig != null)
        {
            Destroy(_currentRig.gameObject);
            _currentRig = null;
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
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_mpb, slot);
            }
        }
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
}
