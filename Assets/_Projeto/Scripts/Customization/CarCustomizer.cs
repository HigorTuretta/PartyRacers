using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    // Rig de customização de um carro do pack Modular_Cyber_Racing_Cars.
    // Contém o corpo base + os pais de montagem (peças, rodas, piloto) e as listas
    // de variantes autoradas por carro. No PartyRacers ele é instanciado sob o
    // KartVisual/Body e controlado pelo KartVisualCustomizer (a animação de rodas/corpo
    // continua sendo feita pelo KartArcadeVisuals do projeto).
    public class CarCustomizer : MonoBehaviour
    {
        [SerializeField] private Transform _elementParent;
        [SerializeField] private List<CarElementSettings> _elements;
        [SerializeField] private Transform[] _wheelParents;
        [SerializeField] private Transform[] _visualWheelsFront;
        [SerializeField] private Transform[] _visualWheelsRear;
        [SerializeField] private Transform _racerParent;

        private readonly Dictionary<CarElementName, List<GameObject>> _carElementInfos =
            new Dictionary<CarElementName, List<GameObject>>();

        public List<CarElementSettings> Elements => _elements;
        public Transform[] WheelParents => _wheelParents;
        public Transform[] VisualWheelsFront => _visualWheelsFront;
        public Transform[] VisualWheelsRear => _visualWheelsRear;
        public Transform RacerParent => _racerParent;
        public Transform ElementParent => _elementParent;

        // Instancia a variante 0 de cada elemento (estado padrão do carro).
        public void Initialize()
        {
            if (_elements == null)
                return;

            foreach (var element in _elements)
                SwitchCarElement(element.ElementName, 0);
        }

        public void Dispose()
        {
        }

        public int GetVariantCount(CarElementName elementName)
        {
            if (_elements == null)
                return 0;

            foreach (var element in _elements)
            {
                if (element.ElementName == elementName)
                    return element.Elements != null ? element.Elements.Count : 0;
            }

            return 0;
        }

        public void SwitchCarElement(CarElementName elementName, int index)
        {
            if (!_carElementInfos.TryGetValue(elementName, out var spawned))
            {
                spawned = new List<GameObject>();
                _carElementInfos[elementName] = spawned;
            }

            foreach (var go in spawned)
            {
                if (go != null)
                    Destroy(go);
            }
            spawned.Clear();

            CarElementSettings settings = null;
            foreach (var element in _elements)
            {
                if (element.ElementName == elementName)
                {
                    settings = element;
                    break;
                }
            }

            if (settings == null || settings.Elements == null || settings.Elements.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, settings.Elements.Count - 1);
            GameObject prefab = settings.Elements[index];
            if (prefab == null)
                return;

            if (elementName == CarElementName.Wheel)
            {
                if (_wheelParents == null)
                    return;

                foreach (var parent in _wheelParents)
                {
                    if (parent == null)
                        continue;

                    GameObject wheel = Instantiate(prefab, parent);
                    wheel.transform.localPosition = Vector3.zero;
                    wheel.transform.localRotation = Quaternion.identity;
                    spawned.Add(wheel);
                }
            }
            else if (elementName == CarElementName.Racer)
            {
                Transform parent = _racerParent != null ? _racerParent : _elementParent;
                spawned.Add(Instantiate(prefab, parent));
            }
            else
            {
                spawned.Add(Instantiate(prefab, _elementParent));
            }
        }
    }
}
