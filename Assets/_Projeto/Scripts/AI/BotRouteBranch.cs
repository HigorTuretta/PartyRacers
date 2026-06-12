using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Atalho ou rota alternativa da pista para os bots.
    ///
    /// COMO CONFIGURAR NA CENA (passo a passo):
    /// 1. Na cena de corrida você já tem um objeto com BotRacingLine cujos FILHOS formam o
    ///    traçado PRINCIPAL (loop) da pista, em ordem de condução.
    /// 2. Para criar um atalho/rota alternativa, crie um GameObject FILHO do BotRacingLine
    ///    (ex.: "Branch_Atalho_Ponte") e adicione este componente BotRouteBranch a ele.
    ///    Objetos com BotRouteBranch NÃO contam como pontos do traçado principal.
    /// 3. Adicione filhos a esse branch, em ordem de condução, marcando o caminho do atalho:
    ///    - O PRIMEIRO ponto deve ficar perto do traçado principal, onde o atalho COMEÇA
    ///      (ponto de entrada — é projetado automaticamente na linha principal).
    ///    - O ÚLTIMO ponto deve ficar perto do traçado principal, onde o atalho TERMINA
    ///      (ponto de saída — também projetado automaticamente).
    ///    - Os pontos do meio desenham o caminho do atalho em si.
    /// 4. Ajuste 'takeChance' (chance de cada bot escolher o atalho a cada passagem) e
    ///    'minSkill01' (0 = qualquer bot pode usar; 1 = apenas os bots mais rápidos usam).
    /// 5. Os gizmos laranja mostram o atalho; as esferas maiores marcam entrada/saída.
    ///
    /// IMPORTANTE (checkpoints): garanta que os RaceCheckpoints cubram TODAS as rotas
    /// (alargue o trigger do checkpoint para cruzar tanto a rota principal quanto o atalho),
    /// senão a volta do bot que pegar o atalho não conta.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotRouteBranch : MonoBehaviour
    {
        [Header("Decisão")]
        [Tooltip("Chance (0-1) de um bot escolher esta rota a cada passagem pela entrada.")]
        [Range(0f, 1f)] [SerializeField] private float takeChance = 0.5f;

        [Tooltip("Habilidade mínima do bot (0 = todos; 1 = só os bots mais velozes) para considerar esta rota.")]
        [Range(0f, 1f)] [SerializeField] private float minSkill01 = 0f;

        [Header("Pontos")]
        [Tooltip("Usa os filhos deste objeto como pontos da rota, em ordem de Hierarchy.")]
        [SerializeField] private bool useChildrenAsPoints = true;

        [Tooltip("Pontos explícitos quando 'useChildrenAsPoints' está desligado.")]
        [SerializeField] private List<Transform> points = new List<Transform>();

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private float pointRadius = 1f;
        [SerializeField] private float lineYOffset = 0.35f;

        private readonly List<Vector3> worldBuffer = new List<Vector3>();

        public float TakeChance => takeChance;
        public float MinSkill01 => minSkill01;

        public List<Vector3> GetWorldPoints()
        {
            worldBuffer.Clear();

            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    if (child != null)
                        worldBuffer.Add(child.position);
                }
            }
            else
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i] != null)
                        worldBuffer.Add(points[i].position);
                }
            }

            return worldBuffer;
        }

        public bool HasEnoughPoints()
        {
            return GetWorldPoints().Count >= 2;
        }

        private void OnDrawGizmos()
        {
            List<Vector3> pts = GetWorldPoints();
            if (pts.Count == 0)
                return;

            Gizmos.color = gizmoColor;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 current = pts[i] + Vector3.up * lineYOffset;
                bool isEndpoint = i == 0 || i == pts.Count - 1;
                Gizmos.DrawWireSphere(current, isEndpoint ? pointRadius * 1.6f : pointRadius);

                if (i + 1 < pts.Count)
                {
                    Vector3 next = pts[i + 1] + Vector3.up * lineYOffset;
                    Gizmos.DrawLine(current, next);
                    DrawArrow(current, next);
                }
            }

#if UNITY_EDITOR
            DrawEntryExitLinks(pts);
#endif
        }

        private void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            direction.Normalize();
            Vector3 mid = Vector3.Lerp(from, to, 0.62f);
            Vector3 back = -direction * pointRadius * 1.5f;
            Gizmos.DrawLine(mid, mid + Quaternion.Euler(0f, 28f, 0f) * back);
            Gizmos.DrawLine(mid, mid + Quaternion.Euler(0f, -28f, 0f) * back);
        }

#if UNITY_EDITOR
        // Mostra onde a entrada (verde) e a saída (vermelho) do branch projetam na linha
        // principal — exatamente o que o BotPathFollower calcula em runtime.
        private void DrawEntryExitLinks(List<Vector3> pts)
        {
            if (pts.Count < 2)
                return;

            BotRacingLine line = GetComponentInParent<BotRacingLine>();
            if (line == null)
                return;

            BotPath main = line.GetEditorPreviewPath();
            if (main == null || !main.IsValid)
                return;

            Vector3 up = Vector3.up * lineYOffset;

            BotPath.NearestResult entry = main.FindNearestGlobal(pts[0]);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            Gizmos.DrawLine(pts[0] + up, entry.Point + up);
            Gizmos.DrawWireCube(entry.Point + up, Vector3.one * 1.2f);

            BotPath.NearestResult exit = main.FindNearestGlobal(pts[pts.Count - 1]);
            Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
            Gizmos.DrawLine(pts[pts.Count - 1] + up, exit.Point + up);
            Gizmos.DrawWireCube(exit.Point + up, Vector3.one * 1.2f);
        }
#endif
    }
}
