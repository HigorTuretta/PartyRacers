using System;
using System.Collections.Generic;
using ithappy;
using PartyRacers.UI.Frontend;
using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>Garagem rolavel ligada ao KartVisualCustomizer e ao estudio 3D existentes.</summary>
    public sealed class GarageController : MonoBehaviour
    {
        enum CategoryKind { Model, Color, Element }

        readonly struct Category
        {
            public Category(string id, string label, CategoryKind kind, CarElementName element = CarElementName.None)
            {
                Id = id;
                Label = label;
                Kind = kind;
                Element = element;
            }

            public string Id { get; }
            public string Label { get; }
            public CategoryKind Kind { get; }
            public CarElementName Element { get; }
        }

        static readonly Category[] Categories =
        {
            new Category("Modelo", "MODELO", CategoryKind.Model),
            new Category("Cor", "COR", CategoryKind.Color),
            new Category("Rodas", "RODAS", CategoryKind.Element, CarElementName.Wheel),
            new Category("Frente", "FRENTE", CategoryKind.Element, CarElementName.FrontBumper),
            new Category("Traseira", "TRASEIRA", CategoryKind.Element, CarElementName.RearBumper),
            new Category("Escape", "ESCAPE", CategoryKind.Element, CarElementName.Pipe),
            new Category("Farois", "FARÓIS", CategoryKind.Element, CarElementName.Headlight),
            new Category("Neblina", "NEBLINA", CategoryKind.Element, CarElementName.FogLight),
            new Category("Motor", "MOTOR", CategoryKind.Element, CarElementName.Engine),
            new Category("Teto", "TETO", CategoryKind.Element, CarElementName.Spoiler),
            new Category("Adesivos", "ADESIVOS", CategoryKind.Element, CarElementName.Decals),
            new Category("Piloto", "PILOTO", CategoryKind.Element, CarElementName.Racer),
        };

        [SerializeField] VisualTreeAsset itemTemplate;
        [SerializeField] PartyRacers.UI.Garage.PreviewStudio previewStudio;
        [SerializeField] StagePresenter stage;
        [SerializeField] KartVisualCustomizer customizer;
        [SerializeField] FrontendFlow flow;

        VisualElement root;
        VisualElement grid;
        VisualElement cameraChip;
        VisualElement undoAction;
        VisualElement equipAction;
        Button undoButton;
        Button equipButton;
        Label cameraLabel;
        Label gridCount;
        Category category;
        int equippedIndex;
        int selectedIndex;
        bool hasPending;

        public void Bind(VisualElement value)
        {
            Unbind();
            root = value;
            if (customizer == null)
                customizer = FindAnyObjectByType<KartVisualCustomizer>(FindObjectsInactive.Include);
            if (previewStudio == null)
                previewStudio = FindAnyObjectByType<PartyRacers.UI.Garage.PreviewStudio>(FindObjectsInactive.Include);
            if (flow == null)
                flow = FindAnyObjectByType<FrontendFlow>(FindObjectsInactive.Include);

            customizer?.EnsureBuilt();
            ScrollView scroll = root.Q<ScrollView>("Item_Grid");
            grid = scroll.contentContainer;
            cameraChip = root.Q<VisualElement>("CameraChip");
            cameraLabel = root.Q<Label>("CameraChip_Label");
            gridCount = root.Q<Label>("Grid_Count");

            for (int i = 0; i < Categories.Length; i++)
            {
                Category item = Categories[i];
                var chip = root.Q<VisualElement>("Tab_" + item.Id);
                chip.Q<Button>("State_On_Btn").text = item.Label;
                chip.Q<Button>("State_Off_Btn").text = item.Label;
                chip.Q<Button>("State_On_Btn").clicked += () => SelectCategory(item);
                chip.Q<Button>("State_Off_Btn").clicked += () => SelectCategory(item);
            }

            undoAction = root.Q<VisualElement>("Btn_Undo");
            equipAction = root.Q<VisualElement>("Btn_Equip");
            undoButton = root.Q<Button>("Btn_Undo_Face");
            equipButton = root.Q<Button>("Btn_Equip_Face");
            undoButton.clicked += Undo;
            equipButton.clicked += Equip;

            UiStates.Show(cameraChip, false);
            RefreshCategoryAvailability();
            SelectCategory(Categories[0]);
        }

        public void Unbind()
        {
            Undo();
            root = null;
            grid = null;
            undoAction = null;
            equipAction = null;
            undoButton = null;
            equipButton = null;
        }

        void SelectCategory(Category next)
        {
            if (root == null || customizer == null)
                return;

            Undo();
            category = next;
            equippedIndex = GetEquippedIndex(next);
            selectedIndex = equippedIndex;
            hasPending = false;
            RefreshCategoryAvailability();

            for (int i = 0; i < Categories.Length; i++)
            {
                Category item = Categories[i];
                var chip = root.Q<VisualElement>("Tab_" + item.Id);
                UiStates.ShowOnly(chip, item.Id == next.Id ? "State_On" : "State_Off", "State_On", "State_Off");
            }

            stage?.SetGarageCategory(next.Label);
            cameraLabel.text = "CAMERA \u00B7 " + CameraLabel(next);
            UiStates.Show(cameraChip, true);
            cameraChip.schedule.Execute(() => UiStates.Show(cameraChip, false)).StartingIn(1650);
            RebuildGrid();
        }

        void RebuildGrid()
        {
            if (grid == null || itemTemplate == null || customizer == null)
                return;

            int count = ItemCount(category);
            gridCount.text = count == 1 ? "1 ITEM" : count + " ITENS";
            List<TemplateContainer> cards = UiStates.Fill(grid, itemTemplate, count);

            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                TemplateContainer card = cards[i];
                string label = ItemName(category, index);
                SetCardLabels(card, label);
                UiStates.Show(card.Q<Label>("Badge_New"), false);
                card.RegisterCallback<ClickEvent>(_ => SelectItem(index));
                RequestPreview(card, category, index);
            }

            RefreshCardStates();
        }

        void SelectItem(int index)
        {
            if (customizer == null || index < 0 || index >= ItemCount(category))
                return;

            selectedIndex = index;
            hasPending = selectedIndex != equippedIndex;
            ApplyIndex(category, index, persist: false);
            if (category.Kind == CategoryKind.Model)
                RefreshCategoryAvailability();
            RefreshCardStates();
        }

        void Equip()
        {
            if (customizer == null || !hasPending)
                return;

            ApplyIndex(category, selectedIndex, persist: true);
            equippedIndex = selectedIndex;
            hasPending = false;
            if (category.Kind == CategoryKind.Model)
                previewStudio?.EsquecerPecas();
            flow?.SalvarEstilo();
            RefreshCategoryAvailability();
            RefreshCardStates();
        }

        void Undo()
        {
            if (!hasPending || customizer == null)
                return;

            ApplyIndex(category, equippedIndex, persist: false);
            selectedIndex = equippedIndex;
            hasPending = false;
            if (category.Kind == CategoryKind.Model)
                RefreshCategoryAvailability();
            RefreshCardStates();
        }

        void ApplyIndex(Category item, int index, bool persist)
        {
            customizer.SetPersistSelection(persist);
            switch (item.Kind)
            {
                case CategoryKind.Model: customizer.SetCar(index); break;
                case CategoryKind.Color: customizer.SetColor(index); break;
                default: customizer.SetElement(item.Element, index); break;
            }
            customizer.SetPersistSelection(true);
        }

        void RefreshCardStates()
        {
            if (grid == null) return;
            int index = 0;
            foreach (VisualElement child in grid.Children())
            {
                string state = index == equippedIndex
                    ? "State_Equipped"
                    : hasPending && index == selectedIndex ? "State_Selected" : "State_Free";
                SetItemState(child, state);
                index++;
            }

            RefreshActions();
        }

        void RequestPreview(VisualElement card, Category item, int index)
        {
            if (previewStudio == null || customizer == null)
                return;

            Action<Texture2D> deliver = texture =>
            {
                if (texture == null || card == null || card.parent == null)
                    return;

                SetPreview(card.Q<VisualElement>("Eq_Preview"), texture);
                SetPreview(card.Q<VisualElement>("Sel_Preview"), texture);
                SetPreview(card.Q<VisualElement>("Free_Preview"), texture);
            };

            if (item.Kind == CategoryKind.Color)
                previewStudio.PedirCor(index, deliver);
            else
                previewStudio.Pedir(customizer.CarIndex, item.Label, item.Element,
                    item.Kind == CategoryKind.Model, index, deliver);
        }

        static void SetPreview(VisualElement target, Texture2D texture)
        {
            if (target != null)
                target.style.backgroundImage = new StyleBackground(texture);
        }

        int ItemCount(Category item) => item.Kind switch
        {
            CategoryKind.Model => customizer.CarCount,
            CategoryKind.Color => customizer.ColorCount,
            _ => customizer.GetElementVariantCount(item.Element),
        };

        int GetEquippedIndex(Category item) => item.Kind switch
        {
            CategoryKind.Model => customizer.CarIndex,
            CategoryKind.Color => customizer.ColorIndex,
            _ => customizer.GetElementIndex(item.Element),
        };

        string ItemName(Category item, int index)
        {
            if (item.Kind == CategoryKind.Color)
            {
                Color[] palette = customizer.PaintPalette;
                return palette != null && index >= 0 && index < palette.Length
                    ? ColorName(palette[index])
                    : "COR " + (index + 1).ToString("00");
            }
            if (item.Kind == CategoryKind.Model)
                return CleanName(customizer.GetCarVariantName(index), "MODELO " + (index + 1).ToString("00"));
            return CleanName(customizer.GetElementVariantName(item.Element, index), item.Label + " " + (index + 1).ToString("00"));
        }

        static string CleanName(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            string value = raw.Replace('_', ' ').Replace("Car ", string.Empty).Trim();
            string suffix = value.Substring(value.LastIndexOf(' ') + 1);
            if (suffix.Equals("Empty", StringComparison.OrdinalIgnoreCase)) return "NENHUM";
            if (suffix.Equals("Simple", StringComparison.OrdinalIgnoreCase)) return "PADRAO";
            if (suffix.Equals("Cool", StringComparison.OrdinalIgnoreCase)) return "ESPORTIVO";
            return value.ToUpperInvariant();
        }

        static string ColorName(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (value < 0.22f) return "PRETO";
            if (saturation < 0.16f) return value > 0.72f ? "BRANCO" : "GRAFITE";

            float degree = hue * 360f;
            if (degree < 15f || degree >= 340f) return "VERMELHO";
            if (degree < 40f) return "LARANJA";
            if (degree < 68f) return "AMARELO";
            if (degree < 160f) return "VERDE";
            if (degree < 200f) return "CIANO";
            if (degree < 255f) return "AZUL";
            if (degree < 295f) return "ROXO";
            return "MAGENTA";
        }

        void RefreshCategoryAvailability()
        {
            if (root == null || customizer == null)
                return;

            foreach (Category item in Categories)
            {
                VisualElement chip = root.Q<VisualElement>("Tab_" + item.Id);
                UiStates.Show(chip, IsCategoryAvailable(item));
            }
        }

        bool IsCategoryAvailable(Category item)
            => item.Kind switch
            {
                CategoryKind.Model => customizer.CarCount > 0,
                CategoryKind.Color => customizer.ColorCount > 0,
                _ => customizer.GetElementVariantCount(item.Element) > 1,
            };

        void RefreshActions()
        {
            SetActionEnabled(undoAction, undoButton, hasPending);
            SetActionEnabled(equipAction, equipButton, hasPending);
        }

        static void SetActionEnabled(VisualElement action, Button button, bool enabled)
        {
            button?.SetEnabled(enabled);
            action?.EnableInClassList("garage__action--disabled", !enabled);
        }

        static void SetCardLabels(VisualElement card, string label)
        {
            card.Q<Label>("Eq_Name").text = label;
            card.Q<Label>("Sel_Name").text = label;
            card.Q<Label>("Free_Name").text = label;
            card.Q<Label>("Lk_Name").text = label;
            card.Q<Label>("Free_Rarity").text = "LIVRE";
        }

        static string CameraLabel(Category item) => item.Id switch
        {
            "Modelo" => "ORBITAL 3/4",
            "Cor" => "ORBITAL 3/4",
            "Rodas" => "DETALHE LATERAL",
            "Frente" => "DETALHE FRONTAL",
            "Traseira" => "DETALHE TRASEIRO",
            "Teto" => "DETALHE SUPERIOR",
            "Escape" => "DETALHE TRASEIRO",
            "Farois" => "DETALHE FRONTAL",
            "Neblina" => "DETALHE FRONTAL",
            "Motor" => "DETALHE SUPERIOR",
            "Piloto" => "CABINE",
            _ => "PERFIL LATERAL",
        };

        static void SetItemState(VisualElement card, string state)
        {
            UiStates.ShowOnly(card, state, "State_Equipped", "State_Selected", "State_Free", "State_Locked");
        }
    }
}
