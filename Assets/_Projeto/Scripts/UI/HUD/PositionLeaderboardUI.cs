using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Tabela de posições à esquerda. Mostra os top N (padrão 5) e, quando o jogador local
    /// está fora do top N, exibe um card separado abaixo com a posição dele.
    /// </summary>
    public class PositionLeaderboardUI : MonoBehaviour
    {
        [Header("Top N")]
        [SerializeField] private Transform cardsContainer;
        [Tooltip("Card desativado usado como molde — é clonado para preencher as linhas.")]
        [SerializeField] private PositionCardUI cardTemplate;
        [SerializeField] private int maxRows = 5;

        [Header("Card do jogador local fora do top N")]
        [SerializeField] private GameObject localOutsideGroup;
        [SerializeField] private PositionCardUI localOutsideCard;

        private readonly List<PositionCardUI> pool = new List<PositionCardUI>();

        public void SetEntries(IReadOnlyList<RaceHUDDataProvider.Standing> standings, int localPosition)
        {
            if (cardTemplate != null)
                cardTemplate.SetVisible(false);

            int rows = standings != null ? Mathf.Min(standings.Count, maxRows) : 0;
            EnsurePool(rows);

            for (int i = 0; i < pool.Count; i++)
            {
                if (i < rows)
                {
                    RaceHUDDataProvider.Standing s = standings[i];
                    pool[i].SetVisible(true);
                    pool[i].SetData(s.Position, s.DisplayName, s.BestLapTime, s.IsLocal);
                }
                else
                {
                    pool[i].SetVisible(false);
                }
            }

            UpdateLocalOutsideCard(standings, localPosition);
        }

        private void UpdateLocalOutsideCard(IReadOnlyList<RaceHUDDataProvider.Standing> standings, int localPosition)
        {
            bool localOutside = standings != null && localPosition > maxRows;

            if (localOutsideGroup != null)
                localOutsideGroup.SetActive(localOutside);

            if (!localOutside || localOutsideCard == null || standings == null)
                return;

            for (int i = 0; i < standings.Count; i++)
            {
                if (!standings[i].IsLocal)
                    continue;

                RaceHUDDataProvider.Standing s = standings[i];
                localOutsideCard.SetVisible(true);
                localOutsideCard.SetData(s.Position, s.DisplayName, s.BestLapTime, true);
                return;
            }
        }

        private void EnsurePool(int count)
        {
            if (cardTemplate == null || cardsContainer == null)
                return;

            while (pool.Count < count)
            {
                PositionCardUI card = Instantiate(cardTemplate, cardsContainer);
                card.name = $"PositionCard_{pool.Count + 1}";
                pool.Add(card);
            }
        }
    }
}
