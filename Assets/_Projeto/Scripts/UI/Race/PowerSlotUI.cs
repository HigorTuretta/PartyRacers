using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.HUD;
using PartyRacers.UI.Settings;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder do slot de poder (tela 01). Alterna os 4 filhos de estado que já existem no prefab
    /// PowerSlot e troca o ícone entre variantes vindas de <see cref="PowerDefinition"/>.
    ///
    /// Hoje o gameplay só distingue "tem poder" de "não tem" (KartPowerInventory não tem cooldown),
    /// então só Empty e Filled são usados. Recharging e Locked continuam montados no prefab para
    /// quando a recarga existir — o binder já sabe ligá-los por <see cref="MostrarRecarga"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerSlotUI : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;
        [SerializeField] private PowerDefinition[] catalogo;

        [Header("Estados do slot (filhos já montados)")]
        [SerializeField] private GameObject estadoVazio;
        [SerializeField] private GameObject estadoCheio;
        [SerializeField] private GameObject estadoRecarga;
        [SerializeField] private GameObject estadoBloqueado;

        [Header("Peças de dentro dos estados")]
        [SerializeField] private Image iconeCheio;
        [SerializeField] private Image iconeRecarga;
        [SerializeField] private Image mascaraRecarga;
        [SerializeField] private Image iconeBloqueado;
        [SerializeField] private TextMeshProUGUI nomeDoPoder;

        [Header("Some junto com o poder")]
        [Tooltip("Cartão do nome do poder. Sem poder o texto fica vazio e sobra uma caixa preta.")]
        [SerializeField] private GameObject cartaoDoNome;
        [Tooltip("Dica de tecla. Só faz sentido quando há poder para usar.")]
        [SerializeField] private GameObject dicaDeTecla;

        private KartPowerType ultimoTipo = (KartPowerType)(-1);
        private bool ultimoTinha;
        private static PowerDefinition[] supplementalCatalog;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();

            if (dados.CurrentPower == ultimoTipo && dados.HasPower == ultimoTinha)
                return;

            ultimoTipo = dados.CurrentPower;
            ultimoTinha = dados.HasPower;

            PowerDefinition def = Resolver(dados.CurrentPower);

            if (iconeCheio != null && def != null) iconeCheio.sprite = def.iconeColorido;
            if (iconeRecarga != null && def != null) iconeRecarga.sprite = def.iconeMono;
            if (iconeBloqueado != null && def != null) iconeBloqueado.sprite = def.iconeCinza;

            if (nomeDoPoder != null)
                nomeDoPoder.text = def != null ? def.nomeExibido : string.Empty;

            // sem poder, nome e tecla saem de cena: um rótulo vazio deixava uma caixa preta
            // solta no canto e a dica de tecla mandava apertar algo que não faz nada
            bool mostraRotulo = dados.HasPower && def != null;
            Ligar(cartaoDoNome, mostraRotulo);
            Ligar(dicaDeTecla, mostraRotulo);

            Ligar(estadoVazio, !dados.HasPower);
            Ligar(estadoCheio, dados.HasPower);
            Ligar(estadoRecarga, false);
            Ligar(estadoBloqueado, false);
        }

        /// <summary>Liga o estado de recarga com o preenchimento pedido (0..1). Para quando houver cooldown.</summary>
        public void MostrarRecarga(float progresso01)
        {
            Ligar(estadoVazio, false);
            Ligar(estadoCheio, false);
            Ligar(estadoBloqueado, false);
            Ligar(estadoRecarga, true);

            if (mascaraRecarga != null)
                mascaraRecarga.fillAmount = 1f - Mathf.Clamp01(progresso01);
        }

        private PowerDefinition Resolver(KartPowerType tipo)
        {
            if (catalogo != null)
            {
                foreach (PowerDefinition def in catalogo)
                {
                    if (def != null && def.tipo == tipo)
                        return def;
                }
            }

            // Mantém o catálogo serializado das cenas como fonte principal. Poderes acrescentados
            // depois da montagem da HUD podem fornecer uma definição suplementar em Resources,
            // sem exigir editar e salvar todas as cenas/prefabs que já usam este binder.
            if (supplementalCatalog == null)
                supplementalCatalog = Resources.LoadAll<PowerDefinition>("PowerDefinitions");

            for (int i = 0; i < supplementalCatalog.Length; i++)
            {
                PowerDefinition def = supplementalCatalog[i];
                if (def != null && def.tipo == tipo)
                    return def;
            }

            return null;
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
