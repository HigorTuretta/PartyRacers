using System;
using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Aplica variação visual determinística a um kart de bot: MODELO do carro (uma das variantes
    /// do pack, ex.: 15 rigs), cor de pintura e peças (para-choques, rodas, spoiler, piloto...).
    ///
    /// Nunca toca no estado estático da garagem do player (KartGarageSelection): a seleção do bot
    /// é aplicada via KartVisualCustomizer.ApplySelection (que não salva nada) e as peças são
    /// trocadas direto no CarCustomizer do rig instanciado.
    ///
    /// A distribuição dos modelos usa um "baralho" embaralhado pela seed da corrida: com 15 bots e
    /// 15 modelos, cada bot recebe um modelo diferente. Configurável via RaceBotManager (Inspector).
    /// </summary>
    [DisallowMultipleComponent]
    public class BotKartCustomizer : MonoBehaviour
    {
        [Header("Modelo")]
        [Tooltip("Sorteia o modelo do carro entre as variantes do KartVisualCustomizer.")]
        [SerializeField] private bool randomizeCarModel = true;
        [Tooltip("Evita que bots usem o modelo escolhido pelo player na garagem.")]
        [SerializeField] private bool avoidPlayerCarModel = true;
        [Tooltip("Restringe os modelos usados pelos bots (vazio = todos os disponíveis).")]
        [SerializeField] private List<int> allowedCarIndices = new List<int>();

        [Header("Peças")]
        [Tooltip("Sorteia as variantes de peças (para-choques, rodas, spoiler, piloto...).")]
        [SerializeField] private bool randomizeElements = true;

        [Header("Pintura (fallback sem KartVisualCustomizer)")]
        [SerializeField] private string paintMaterialNameContains = "Color";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Color[] FallbackPalette =
        {
            new Color(0.85f, 0.12f, 0.14f), new Color(0.10f, 0.45f, 0.90f),
            new Color(0.96f, 0.78f, 0.10f), new Color(0.10f, 0.70f, 0.35f),
            new Color(0.95f, 0.95f, 0.97f), new Color(0.08f, 0.09f, 0.11f),
            new Color(1.00f, 0.45f, 0.05f), new Color(0.55f, 0.20f, 0.80f)
        };

        private KartVisualCustomizer customizer;
        private MaterialPropertyBlock mpb;
        private Color paintColor;
        private bool hasColor;

        /// <summary>Configuração vinda do RaceBotManager (Inspector centralizado).</summary>
        public void Configure(bool randomizeModel, bool avoidPlayerModel, bool randomizeParts, List<int> allowedModels)
        {
            randomizeCarModel = randomizeModel;
            avoidPlayerCarModel = avoidPlayerModel;
            randomizeElements = randomizeParts;
            allowedCarIndices = allowedModels != null ? new List<int>(allowedModels) : new List<int>();
        }

        /// <summary>
        /// Aplica o visual do bot. 'seed' deriva da corrida (determinístico) e 'deckIndex' é a
        /// posição do bot no baralho de modelos (garante variedade: bots consecutivos recebem
        /// modelos diferentes). 'shuffleSeed' embaralha o baralho por corrida.
        /// </summary>
        public void Apply(int seed, int deckIndex = -1, int shuffleSeed = 0)
        {
            customizer = GetComponentInChildren<KartVisualCustomizer>(true);

            if (customizer == null)
            {
                ApplyFallbackTint(seed);
                return;
            }

            var rng = new System.Random(seed);

            // Bots nunca leem/escrevem a seleção da garagem do player.
            customizer.SetLoadSelectionOnStart(false);

            int carIndex = ResolveCarIndex(rng, deckIndex, shuffleSeed);
            int colorIndex = customizer.ColorCount > 0 ? rng.Next(customizer.ColorCount) : 0;

            customizer.CarRebuilt -= RepaintExtras; // evita assinatura dupla
            customizer.CarRebuilt += RepaintExtras;

            paintColor = ResolvePaletteColor(colorIndex);
            hasColor = true;

            customizer.ApplySelection(carIndex, colorIndex, null);

            if (randomizeElements)
                RandomizeElements(rng);
        }

        private int ResolveCarIndex(System.Random rng, int deckIndex, int shuffleSeed)
        {
            int carCount = customizer.CarCount;
            if (carCount <= 0)
                return 0;

            List<int> pool = BuildModelPool(carCount);
            if (pool.Count == 0)
                return rng.Next(carCount);

            if (!randomizeCarModel)
                return pool[0];

            if (deckIndex >= 0)
            {
                // Baralho embaralhado pela seed da corrida: distribui os modelos sem repetição
                // até esgotar o pool (15 bots / 15 modelos = todos diferentes).
                Shuffle(pool, new System.Random(shuffleSeed * 486187739 + 17));
                return pool[deckIndex % pool.Count];
            }

            return pool[rng.Next(pool.Count)];
        }

        private List<int> BuildModelPool(int carCount)
        {
            var pool = new List<int>();

            if (allowedCarIndices != null && allowedCarIndices.Count > 0)
            {
                foreach (int index in allowedCarIndices)
                {
                    if (index >= 0 && index < carCount && !pool.Contains(index))
                        pool.Add(index);
                }
            }
            else
            {
                for (int i = 0; i < carCount; i++)
                    pool.Add(i);
            }

            if (avoidPlayerCarModel && pool.Count > 1)
            {
                KartGarageSelection.EnsureLoaded();
                pool.Remove(Mathf.Clamp(KartGarageSelection.CarIndex, 0, carCount - 1));
            }

            return pool;
        }

        private static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Troca as peças direto no rig instanciado (CarCustomizer.SwitchCarElement), sem passar
        // pelo KartVisualCustomizer.SetElement — que salvaria em PlayerPrefs do player.
        private void RandomizeElements(System.Random rng)
        {
            ithappy.CarCustomizer rig = customizer.CurrentRig;
            if (rig == null || rig.Elements == null)
                return;

            foreach (var element in rig.Elements)
            {
                if (element == null)
                    continue;

                int variants = rig.GetVariantCount(element.ElementName);
                if (variants > 1)
                    rig.SwitchCarElement(element.ElementName, rng.Next(variants));
            }

            RepaintExtras();
        }

        private Color ResolvePaletteColor(int colorIndex)
        {
            Color[] palette = customizer != null && customizer.PaintPalette != null && customizer.PaintPalette.Length > 0
                ? customizer.PaintPalette
                : FallbackPalette;

            return palette[((colorIndex % palette.Length) + palette.Length) % palette.Length];
        }

        private void ApplyFallbackTint(int seed)
        {
            paintColor = FallbackPalette[((seed % FallbackPalette.Length) + FallbackPalette.Length) % FallbackPalette.Length];
            hasColor = true;
            RepaintExtras();
        }

        private void OnDestroy()
        {
            if (customizer != null)
                customizer.CarRebuilt -= RepaintExtras;
        }

        // Tinge os slots de pintura de TODOS os renderers (cobre peças trocadas depois do
        // ApplyPaintColor interno do KartVisualCustomizer).
        private void RepaintExtras()
        {
            if (!hasColor)
                return;

            mpb ??= new MaterialPropertyBlock();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            foreach (Renderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int slot = 0; slot < mats.Length; slot++)
                {
                    if (!IsPaintMaterial(mats[slot]))
                        continue;

                    r.GetPropertyBlock(mpb, slot);
                    mpb.SetColor(BaseColorId, paintColor);
                    mpb.SetColor(ColorId, paintColor);
                    r.SetPropertyBlock(mpb, slot);
                }
            }
        }

        private bool IsPaintMaterial(Material material)
        {
            if (material == null || string.IsNullOrEmpty(paintMaterialNameContains))
                return false;
            return material.name.IndexOf(paintMaterialNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
