using System.Collections.Generic;
using UnityEngine;
using ithappy;

// Seleção de customização persistida entre a cena de Garagem e a corrida.
// Guarda o carro escolhido, a cor da pintura e o índice de cada elemento
// (peças, rodas, motorista). Persiste em PlayerPrefs.
public static class KartGarageSelection
{
    private const string CarKey = "garage.car";
    private const string ColorKey = "garage.color";
    private const string ElementKeyPrefix = "garage.el.";

    public static int CarIndex;
    public static int ColorIndex;
    public static readonly Dictionary<CarElementName, int> ElementIndices = new Dictionary<CarElementName, int>();

    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Load();
    }

    public static int GetElement(CarElementName element)
    {
        EnsureLoaded();
        return ElementIndices.TryGetValue(element, out int value) ? value : 0;
    }

    public static void SetElement(CarElementName element, int index)
    {
        EnsureLoaded();
        ElementIndices[element] = index;
    }

    public static KartVisualSelection Capture()
    {
        EnsureLoaded();
        return new KartVisualSelection(CarIndex, ColorIndex, EncodeElements());
    }

    public static string EncodeElements()
    {
        EnsureLoaded();
        return EncodeElements(ElementIndices);
    }

    public static string EncodeElements(Dictionary<CarElementName, int> elements)
    {
        if (elements == null || elements.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        foreach (CarElementName element in System.Enum.GetValues(typeof(CarElementName)))
        {
            if (element == CarElementName.None)
                continue;

            if (elements.TryGetValue(element, out int index) && index > 0)
                parts.Add(((int)element) + ":" + index);
        }

        return string.Join("|", parts);
    }

    public static Dictionary<CarElementName, int> DecodeElements(string encoded)
    {
        Dictionary<CarElementName, int> result = new Dictionary<CarElementName, int>();

        foreach (CarElementName element in System.Enum.GetValues(typeof(CarElementName)))
        {
            if (element != CarElementName.None)
                result[element] = 0;
        }

        if (string.IsNullOrWhiteSpace(encoded))
            return result;

        string[] parts = encoded.Split('|');
        foreach (string part in parts)
        {
            string[] pair = part.Split(':');
            if (pair.Length != 2)
                continue;

            if (!int.TryParse(pair[0], out int elementValue) || !int.TryParse(pair[1], out int index))
                continue;

            CarElementName element = (CarElementName)elementValue;
            if (element == CarElementName.None || !System.Enum.IsDefined(typeof(CarElementName), element))
                continue;

            result[element] = Mathf.Max(0, index);
        }

        return result;
    }

    public static void Load()
    {
        CarIndex = PlayerPrefs.GetInt(CarKey, 0);
        ColorIndex = PlayerPrefs.GetInt(ColorKey, 0);

        ElementIndices.Clear();
        foreach (CarElementName element in System.Enum.GetValues(typeof(CarElementName)))
        {
            if (element == CarElementName.None)
                continue;

            ElementIndices[element] = PlayerPrefs.GetInt(ElementKeyPrefix + (int)element, 0);
        }

        _loaded = true;
    }

    public static void Save()
    {
        EnsureLoaded();

        PlayerPrefs.SetInt(CarKey, CarIndex);
        PlayerPrefs.SetInt(ColorKey, ColorIndex);

        foreach (var pair in ElementIndices)
            PlayerPrefs.SetInt(ElementKeyPrefix + (int)pair.Key, pair.Value);

        PlayerPrefs.Save();
    }
}

[System.Serializable]
public struct KartVisualSelection
{
    public int CarIndex;
    public int ColorIndex;
    public string ElementData;

    public KartVisualSelection(int carIndex, int colorIndex, string elementData)
    {
        CarIndex = carIndex;
        ColorIndex = colorIndex;
        ElementData = elementData ?? string.Empty;
    }
}
