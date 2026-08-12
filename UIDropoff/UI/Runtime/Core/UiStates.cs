using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Troca de estado = ligar UM irmao e desligar os outros.
    /// Nunca trocar cor, sprite, tamanho ou posicao por codigo:
    /// os estados ja existem montados no UXML.
    /// </summary>
    public static class UiStates
    {
        public static void ShowOnly(VisualElement parent, string visibleName, params string[] allNames)
        {
            foreach (var n in allNames)
            {
                var el = parent.Q<VisualElement>(n);
                if (el == null) continue;
                el.style.display = n == visibleName ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public static void Show(VisualElement el, bool on)
        {
            if (el != null) el.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Troca de variante por CLASSE, nunca por valor.</summary>
        public static void SetVariant(VisualElement el, string activeClass, params string[] allClasses)
        {
            if (el == null) return;
            foreach (var c in allClasses) el.RemoveFromClassList(c);
            el.AddToClassList(activeClass);
        }

        /// <summary>Preenche uma lista clonando um template ja autorado.</summary>
        public static List<TemplateContainer> Fill(VisualElement host, VisualTreeAsset template, int count)
        {
            host.Clear();
            var list = new List<TemplateContainer>(count);
            for (int i = 0; i < count; i++)
            {
                var inst = template.Instantiate();
                host.Add(inst);
                list.Add(inst);
            }
            return list;
        }
    }
}
