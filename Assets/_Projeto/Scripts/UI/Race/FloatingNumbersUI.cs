using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PartyRacers.UI.HUD;
using PartyRacers.UI.Motion;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Números flutuantes de DANO e CURA (AlertLayer → FloatNumber). É o feedback imediato do que
    /// acabou de acontecer com a vida: −15 em vermelho ao levar um item, +40 em verde ao passar na
    /// caixa de cura.
    ///
    /// Dano e cura NÃO são o mesmo objeto com a cor trocada: são dois filhos de estado já
    /// estilizados na cena, e o binder liga um ou o outro. É o que impede que cura, escudo e HP
    /// pareçam a mesma coisa no meio da corrida.
    ///
    /// Os slots já existem na cena — nada é instanciado. Com todos ocupados, o mais antigo é
    /// reaproveitado, para que um golpe novo nunca fique sem resposta na tela.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingNumbersUI : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            [Tooltip("Raiz do slot, com CanvasGroup e UIFloatRise.")]
            public GameObject raiz;
            public UIFloatRise movimento;

            [Header("Estados (filhos já estilizados)")]
            public GameObject estadoDano;
            public GameObject estadoCura;

            [Header("Textos de dentro de cada estado")]
            public TextMeshProUGUI textoDano;
            public TextMeshProUGUI textoCura;
        }

        [Header("Slots já montados na cena")]
        [SerializeField] private List<Slot> slots = new List<Slot>();

        [Header("Dados")]
        [Tooltip("Filtra os eventos: só o que acontece com o kart do jogador vira número na tela.")]
        [SerializeField] private RaceHUDDataProvider dados;

        private readonly List<float> nascimento = new List<float>();

        private void Awake()
        {
            nascimento.Clear();
            for (int i = 0; i < slots.Count; i++)
                nascimento.Add(float.NegativeInfinity);

            // Prefab não serializa referência de cena — ver VitalClusterUI.Awake.
            if (dados == null)
                dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        private void OnEnable() => RaceHudEvents.Raised += AoEvento;
        private void OnDisable() => RaceHudEvents.Raised -= AoEvento;

        private void AoEvento(RaceHudEvents.EventData e)
        {
            if (e.Kind != RaceHudEventKind.Damaged && e.Kind != RaceHudEventKind.Healed)
                return;

            if (!EhDoJogadorLocal(e.Actor))
                return;

            int quantidade = Mathf.RoundToInt(e.Amount);
            if (quantidade <= 0)
                return;

            Mostrar(quantidade, e.Kind == RaceHudEventKind.Healed);
        }

        /// <summary>Mostra um número. <paramref name="cura"/> escolhe o filho de estado.</summary>
        public void Mostrar(int quantidade, bool cura)
        {
            int indice = EscolherSlot();
            if (indice < 0)
                return;

            Slot slot = slots[indice];
            nascimento[indice] = Time.time;

            Ligar(slot.estadoDano, !cura);
            Ligar(slot.estadoCura, cura);

            // O sinal faz parte do número, não da cor: quem joga sem distinguir bem verde de
            // vermelho ainda lê "+40" e "−15". O traço é o menos (U+2212), que alinha com os
            // dígitos em fonte tabular; o hífen fica curto e baixo.
            if (cura && slot.textoCura != null)
                slot.textoCura.text = "+" + quantidade;
            else if (!cura && slot.textoDano != null)
                slot.textoDano.text = "−" + quantidade;

            if (slot.raiz != null && !slot.raiz.activeSelf)
                slot.raiz.SetActive(true);

            if (slot.movimento != null)
                slot.movimento.Disparar();
        }

        /// <summary>Primeiro slot livre; sem nenhum, recicla o mais antigo.</summary>
        private int EscolherSlot()
        {
            if (slots.Count == 0)
                return -1;

            int maisAntigo = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot == null || slot.raiz == null)
                    continue;

                bool livre = slot.movimento == null ? !slot.raiz.activeSelf : !slot.movimento.EmUso;
                if (livre)
                    return i;

                if (nascimento[i] < nascimento[maisAntigo])
                    maisAntigo = i;
            }

            return maisAntigo;
        }

        private bool EhDoJogadorLocal(GameObject ator)
        {
            if (dados == null || !dados.HasLocalKart)
                return true;

            if (ator == null)
                return false;

            GameObject local = dados.LocalKart.gameObject;
            return ator == local || ator.transform.IsChildOf(local.transform);
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
