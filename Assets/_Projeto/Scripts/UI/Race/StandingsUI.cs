using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder da classificação (tela 01). Preenche as linhas Row_Standing que já existem na cena
    /// e mantém a linha do jogador local sempre visível no card de baixo.
    /// Não instancia nada: as 5 linhas + a linha local são montadas à mão no prefab da tela.
    /// </summary>
    [DisallowMultipleComponent]
    public class StandingsUI : MonoBehaviour
    {
        /// <summary>Uma linha já montada na cena. Os badges são irmãos, um por medalha.</summary>
        [System.Serializable]
        public class Linha
        {
            public GameObject raiz;
            public TextMeshProUGUI nome;
            public TextMeshProUGUI tempo;
            [Tooltip("Badges de medalha (ouro, prata, bronze, comum, local) — só um fica ativo.")]
            public GameObject badgeOuro;
            public GameObject badgePrata;
            public GameObject badgeBronze;
            public GameObject badgeComum;
            public GameObject badgeLocal;
            [Tooltip("TMP do número dentro de cada badge, na mesma ordem dos badges acima.")]
            public TextMeshProUGUI valorOuro;
            public TextMeshProUGUI valorPrata;
            public TextMeshProUGUI valorBronze;
            public TextMeshProUGUI valorComum;
            public TextMeshProUGUI valorLocal;
            [Tooltip("Contorno que marca a linha como sendo do jogador local.")]
            public GameObject destaqueLocal;
        }

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Linhas já montadas na cena")]
        [SerializeField] private List<Linha> linhas = new List<Linha>();
        [SerializeField] private Linha linhaLocal;
        [Tooltip("Bloco 'SUA POSIÇÃO' inteiro (rótulo + linha). Some quando você já está na lista.")]
        [SerializeField] private GameObject blocoLocal;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();
            IReadOnlyList<RaceHUDDataProvider.Standing> lista = dados.Standings;

            // onde o jogador local está na lista (-1 = não está)
            int indiceLocal = -1;
            for (int i = 0; i < lista.Count; i++)
            {
                if (!lista[i].IsLocal)
                    continue;
                indiceLocal = i;
                break;
            }

            for (int i = 0; i < linhas.Count; i++)
            {
                Linha linha = linhas[i];
                if (linha == null || linha.raiz == null)
                    continue;

                bool temDado = i < lista.Count;
                if (linha.raiz.activeSelf != temDado)
                    linha.raiz.SetActive(temDado);
                if (!temDado)
                    continue;

                Preencher(linha, lista[i], i + 1, ehLocal: i == indiceLocal);
            }

            // O card "SUA POSIÇÃO" existe para quando o jogador está fora das linhas visíveis.
            // Se ele já aparece na lista, mostrar os dois duplicava a mesma pessoa na tela.
            bool jaVisivel = indiceLocal >= 0 && indiceLocal < linhas.Count;
            bool mostrarBloco = indiceLocal >= 0 && !jaVisivel;

            if (mostrarBloco && linhaLocal != null && linhaLocal.raiz != null)
                Preencher(linhaLocal, lista[indiceLocal], lista[indiceLocal].Position,
                          ehLocal: true, forcarBadgeLocal: true);

            // prefere desligar o bloco inteiro: só a linha deixaria o rótulo "SUA POSIÇÃO" órfão
            GameObject alvoBloco = blocoLocal != null ? blocoLocal
                                 : linhaLocal != null ? linhaLocal.raiz
                                 : null;
            if (alvoBloco != null && alvoBloco.activeSelf != mostrarBloco)
                alvoBloco.SetActive(mostrarBloco);
        }

        private static void Preencher(Linha linha, RaceHUDDataProvider.Standing dado, int posicao,
                                      bool ehLocal = false, bool forcarBadgeLocal = false)
        {
            if (linha.nome != null)
                linha.nome.text = dado.DisplayName;

            // contorno de "é você" também na lista, não só no card de baixo
            if (linha.destaqueLocal != null && linha.destaqueLocal.activeSelf != ehLocal)
                linha.destaqueLocal.SetActive(ehLocal);

            if (linha.tempo != null)
                linha.tempo.text = HUDFormat.LapTimeShort(dado.BestLapTime);

            // um badge por medalha: nenhum código pinta cor, só liga o irmão certo
            GameObject alvo = forcarBadgeLocal ? linha.badgeLocal
                            : posicao == 1 ? linha.badgeOuro
                            : posicao == 2 ? linha.badgePrata
                            : posicao == 3 ? linha.badgeBronze
                            : linha.badgeComum;

            Ligar(linha.badgeOuro, alvo, linha.valorOuro, posicao);
            Ligar(linha.badgePrata, alvo, linha.valorPrata, posicao);
            Ligar(linha.badgeBronze, alvo, linha.valorBronze, posicao);
            Ligar(linha.badgeComum, alvo, linha.valorComum, posicao);
            Ligar(linha.badgeLocal, alvo, linha.valorLocal, posicao);
        }

        private static void Ligar(GameObject badge, GameObject alvo, TextMeshProUGUI valor, int posicao)
        {
            if (badge == null)
                return;

            bool ativo = badge == alvo;
            if (badge.activeSelf != ativo)
                badge.SetActive(ativo);

            if (ativo && valor != null)
                valor.text = posicao.ToString();
        }
    }
}
