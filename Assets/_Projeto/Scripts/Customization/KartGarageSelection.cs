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
