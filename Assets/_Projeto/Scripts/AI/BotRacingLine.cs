using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PartyRacers.AI
{
    /// <summary>
    /// Linha de corrida autorada à mão para os bots.
    ///
    /// COMO MONTAR EM UMA PISTA NOVA:
    /// 1. Crie um GameObject vazio "BotRacingLine" e adicione este componente.
    /// 2. Crie FILHOS vazios em ordem de condução marcando o traçado (P00, P01, ...).
    ///    Use o menu de contexto "AI/Create Points From Race Checkpoints" para começar
    ///    a partir dos checkpoints e depois refine adicionando pontos entre eles.
    /// 3. Mantenha os pontos no MEIO da pista (ou na linha de corrida desejada), próximos
    ///    do chão. Use "AI/Snap Points To Ground" para colar tudo no asfalto.
    /// 4. Espaçamento recomendado: 15–40 m em retas, 8–15 m em curvas. A curva amarela
    ///    desenhada na Scene View é EXATAMENTE o caminho que os bots vão seguir.
    /// 5. O Inspector valida a rota automaticamente e lista problemas (ponto sem chão,
    ///    segmento longo demais, checkpoint fora da rota etc.). Corrija o que aparecer.
    ///
    /// ATALHOS / ROTAS ALTERNATIVAS: crie um GameObject FILHO deste objeto com o componente
    /// BotRouteBranch (os filhos do branch desenham a rota alternativa). Filhos com BotRouteBranch
    /// NÃO contam como pontos do traçado principal. Veja as instruções completas em BotRouteBranch.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotRacingLine : MonoBehaviour
    {
        [Tooltip("Uses this object's children as racing-line points in Hierarchy order.")]
        [SerializeField] private bool useChildrenAsPoints = true;

        [Tooltip("Explicit points used when 'useChildrenAsPoints' is disabled.")]
        [SerializeField] private List<Transform> points = new List<Transform>();

        [Tooltip("Connects the last point back to the first point.")]
        [SerializeField] private bool loop = true;

        [Header("Validação")]
        [Tooltip("Altura máxima (m) tolerada de um ponto acima do chão antes de virar aviso.")]
        [SerializeField] private float maxPointHeightAboveGround = 3f;
        [Tooltip("Comprimento máximo (m) recomendado de um segmento entre pontos.")]
        [SerializeField] private float maxSegmentLength = 80f;
        [Tooltip("Distância máxima (m) entre um RaceCheckpoint e a rota para considerá-lo coberto.")]
        [SerializeField] private float checkpointCoverageDistance = 25f;

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField] private Color issueColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private float pointRadius = 1.2f;
        [SerializeField] private float lineYOffset = 0.35f;
        [SerializeField] private bool drawDirectionArrows = true;
        [SerializeField] private bool drawLabelsWhenSelected = true;
        [Tooltip("Desenha a curva suavizada que os bots realmente seguem (recomendado).")]
        [SerializeField] private bool drawSmoothedCurve = true;

        public bool Loop => loop;
        public float MaxPointHeightAboveGround => maxPointHeightAboveGround;
        public float MaxSegmentLength => maxSegmentLength;
        public float CheckpointCoverageDistance => checkpointCoverageDistance;

        private readonly List<Vector3> worldBuffer = new List<Vector3>();

        // ------------------------------------------------------------------ rota compartilhada (runtime)
        // Cache estático usado por sistemas fora da IA (ex.: KartRespawn detecta queda abaixo da
        // pista comparando a altura do kart com a altura da rota). Construído uma vez por cena.
        //
        // IMPORTANTE: o cache inclui a linha PRINCIPAL **e todos os branches/atalhos**. Assim um
        // kart que está num atalho válido (ex.: o anel leste da MiniGolfeRun, longe da main) é
        // reconhecido como "em cima de uma rota válida" e NÃO sofre respawn automático indevido.
        private static BotRacingLine runtimeSource;
        private static BotPath runtimeMainPath;
        private static System.Collections.Generic.List<BotPath> runtimeAllPaths;

        private static void EnsureRuntimePaths()
        {
            if (runtimeSource == null)
            {
                runtimeSource = FindAnyObjectByType<BotRacingLine>(FindObjectsInactive.Exclude);
                runtimeMainPath = null;
                runtimeAllPaths = null;
            }

            if (runtimeSource == null || !runtimeSource.HasEnoughPoints())
                return;

            if (runtimeMainPath == null)
            {
                runtimeMainPath = new BotPath();
                runtimeMainPath.BuildFrom(runtimeSource.GetWorldPoints(), runtimeSource.loop, 6f);

                runtimeAllPaths = new System.Collections.Generic.List<BotPath>();
                if (runtimeMainPath.IsValid)
                    runtimeAllPaths.Add(runtimeMainPath);

                foreach (BotRouteBranch branch in runtimeSource.GetBranches())
                {
                    var bp = new BotPath();
                    bp.BuildFrom(branch.GetWorldPoints(), false, 6f);
                    if (bp.IsValid)
                        runtimeAllPaths.Add(bp);
                }
            }
        }

        /// <summary>
        /// Ponto mais próximo de QUALQUER rota válida da cena (principal + branches), planar.
        /// Retorna false se a cena não tem BotRacingLine válida. Use para checagens de fora-da-pista:
        /// um kart num atalho válido fica perto de uma rota e não é tratado como OOB.
        /// </summary>
        public static bool TryGetNearestRoutePoint(Vector3 position, out Vector3 nearestOnRoute)
        {
            nearestOnRoute = default;
            EnsureRuntimePaths();
            if (runtimeAllPaths == null || runtimeAllPaths.Count == 0)
                return false;

            float bestSqr = float.MaxValue;
            for (int i = 0; i < runtimeAllPaths.Count; i++)
            {
                BotPath.NearestResult n = runtimeAllPaths[i].FindNearestGlobal(position);
                if (n.SqrPlanarDistance < bestSqr)
                {
                    bestSqr = n.SqrPlanarDistance;
                    nearestOnRoute = n.Point;
                }
            }

            return bestSqr < float.MaxValue;
        }

        /// <summary>
        /// Info sobre a rota PRINCIPAL: ponto mais próximo + distância percorrida + comprimento +
        /// loop. Usado para ordenar "o quão à frente" duas posições estão no sentido da corrida
        /// (pose segura à frente do checkpoint). Mede só na principal de propósito.
        /// </summary>
        public static bool TryGetNearestRouteInfo(
            Vector3 position,
            out Vector3 nearestOnRoute,
            out float distanceOnRoute,
            out float routeTotalLength,
            out bool routeLooped)
        {
            nearestOnRoute = default;
            distanceOnRoute = 0f;
            routeTotalLength = 0f;
            routeLooped = false;

            EnsureRuntimePaths();
            if (runtimeMainPath == null || !runtimeMainPath.IsValid)
                return false;

            BotPath.NearestResult nearest = runtimeMainPath.FindNearestGlobal(position);
            nearestOnRoute = nearest.Point;
            distanceOnRoute = nearest.DistanceOnPath;
            routeTotalLength = runtimeMainPath.TotalLength;
            routeLooped = runtimeMainPath.Looped;
            return true;
        }

        // Filhos com BotRouteBranch (rotas alternativas) ou BotTrackZone (marcadores de trecho)
        // NÃO contam como pontos do traçado principal.
        private static bool IsRoutePointChild(Transform child)
        {
            return child != null
                && child.GetComponent<BotRouteBranch>() == null
                && child.GetComponent<BotTrackZone>() == null;
        }

        public List<Vector3> GetWorldPoints()
        {
            worldBuffer.Clear();

            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    if (IsRoutePointChild(child))
                        worldBuffer.Add(child.position);
                }
            }
            else
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Transform point = points[i];
                    if (point != null)
                        worldBuffer.Add(point.position);
                }
            }

            return worldBuffer;
        }

        public bool HasEnoughPoints()
        {
            return CountValidPoints() >= 3;
        }

        /// <summary>Rotas alternativas/atalhos autorados como filhos deste objeto.</summary>
        public List<BotRouteBranch> GetBranches()
        {
            var branches = new List<BotRouteBranch>();
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                BotRouteBranch branch = child.GetComponent<BotRouteBranch>();
                if (branch != null && branch.HasEnoughPoints())
                    branches.Add(branch);
            }

            return branches;
        }

        /// <summary>Zonas especiais (rampa, salto, curva forte...) autoradas como filhos deste objeto.</summary>
        public List<BotTrackZone> GetZones()
        {
            var zones = new List<BotTrackZone>();
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                BotTrackZone zone = child.GetComponent<BotTrackZone>();
                if (zone != null)
                    zones.Add(zone);
            }

            return zones;
        }

        /// <summary>Transforms dos pontos do traçado principal, na ordem de condução.</summary>
        public List<Transform> GetPointTransformList()
        {
            var list = new List<Transform>();
            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    if (IsRoutePointChild(child))
                        list.Add(child);
                }
            }
            else
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i] != null)
                        list.Add(points[i]);
                }
            }

            return list;
        }

        private int CountValidPoints()
        {
            int count = 0;
            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    if (IsRoutePointChild(child))
                        count++;
                }
            }
            else
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i] != null)
                        count++;
                }
            }

            return count;
        }

#if UNITY_EDITOR
        // ------------------------------------------------------------------ validação (Editor)
        public enum IssueSeverity { Info, Warning, Error }

        public struct RouteIssue
        {
            public IssueSeverity Severity;
            public string Message;
            public Vector3 Position;
            public bool HasPosition;

            public RouteIssue(IssueSeverity severity, string message)
            {
                Severity = severity;
                Message = message;
                Position = Vector3.zero;
                HasPosition = false;
            }

            public RouteIssue(IssueSeverity severity, string message, Vector3 position)
            {
                Severity = severity;
                Message = message;
                Position = position;
                HasPosition = true;
            }
        }

        // Cache do preview/validação para não recalcular a cada gizmo frame.
        private BotPath cachedPreviewPath;
        private List<RouteIssue> cachedIssues;
        private HashSet<int> cachedIssuePointIndices;
        private int cachedHash;

        /// <summary>Curva suavizada (a mesma que o BotPathFollower constrói). Pode ser null.</summary>
        public BotPath GetEditorPreviewPath()
        {
            RefreshEditorCache();
            return cachedPreviewPath;
        }

        /// <summary>Valida a rota e devolve a lista de problemas encontrados (Editor).</summary>
        public List<RouteIssue> GetValidationIssues()
        {
            RefreshEditorCache();
            return cachedIssues;
        }

        /// <summary>Força revalidação no próximo acesso (ex.: após mover pontos por script).</summary>
        public void InvalidateEditorCache()
        {
            cachedHash = 0;
            cachedPreviewPath = null;
            cachedIssues = null;
            cachedIssuePointIndices = null;
        }

        private void RefreshEditorCache()
        {
            int hash = ComputeRouteHash();
            if (hash == cachedHash && cachedIssues != null && cachedPreviewPath != null)
                return;

            cachedHash = hash;
            cachedPreviewPath = new BotPath();
            cachedPreviewPath.BuildFrom(GetWorldPoints(), loop, 4f);

            cachedIssues = new List<RouteIssue>();
            cachedIssuePointIndices = new HashSet<int>();
            RunValidation(cachedIssues, cachedIssuePointIndices);
        }

        private int ComputeRouteHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (loop ? 1 : 0);
                foreach (Vector3 p in GetWorldPoints())
                {
                    hash = hash * 31 + Mathf.RoundToInt(p.x * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(p.y * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(p.z * 10f);
                }

                foreach (BotRouteBranch branch in GetBranches())
                {
                    foreach (Vector3 p in branch.GetWorldPoints())
                    {
                        hash = hash * 31 + Mathf.RoundToInt(p.x * 10f);
                        hash = hash * 31 + Mathf.RoundToInt(p.z * 10f);
                    }
                }

                return hash;
            }
        }

        private void RunValidation(List<RouteIssue> issues, HashSet<int> issuePoints)
        {
            List<Vector3> pts = GetWorldPoints();

            if (pts.Count < 3)
            {
                issues.Add(new RouteIssue(IssueSeverity.Error,
                    $"A rota precisa de pelo menos 3 pontos (tem {pts.Count}). Com menos que isso os bots usam o fallback de checkpoints."));
                return;
            }

            ValidatePointsAgainstGround(pts, issues, issuePoints);
            ValidateSegments(pts, issues, issuePoints);
            ValidateCheckpointCoverage(issues);
            ValidateBranches(issues);

            if (!loop)
                issues.Add(new RouteIssue(IssueSeverity.Info,
                    "Loop desligado: a rota não fecha no primeiro ponto. Para pistas de circuito, ligue 'Loop'."));
        }

        private void ValidatePointsAgainstGround(List<Vector3> pts, List<RouteIssue> issues, HashSet<int> issuePoints)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = pts[i];
                // Origem logo acima do ponto: evita falsos positivos com obstáculos suspensos
                // sobre a pista (pás de moinho, pontes, faixas).
                Vector3 origin = p + Vector3.up * 2f;

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Sem chão a partir de +2 m: ou o ponto está fora da pista, ou está ENTERRADO
                    // (a pista fica acima da origem do raio). Tenta de novo de mais alto.
                    if (Physics.Raycast(p + Vector3.up * 25f, Vector3.down, out RaycastHit highHit, 300f, ~0, QueryTriggerInteraction.Ignore)
                        && highHit.point.y > p.y + 0.5f)
                    {
                        issues.Add(new RouteIssue(IssueSeverity.Error,
                            $"Ponto {i:00}: está {highHit.point.y - p.y:F1} m ABAIXO da pista ({highHit.collider.name}) — suba o ponto ou use 'Snap Points To Ground'.", p));
                    }
                    else
                    {
                        issues.Add(new RouteIssue(IssueSeverity.Error,
                            $"Ponto {i:00}: sem chão abaixo (raycast não acertou nada). O ponto está fora da pista?", p));
                    }

                    issuePoints.Add(i);
                    continue;
                }

                float dy = p.y - hit.point.y;
                if (dy > maxPointHeightAboveGround)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Ponto {i:00}: está {dy:F1} m acima do chão (máx. recomendado {maxPointHeightAboveGround:F0} m). Use 'Snap Points To Ground'.", p));
                    issuePoints.Add(i);
                }
                else if (dy < -0.5f)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Ponto {i:00}: está {-dy:F1} m ABAIXO da superfície detectada ({hit.collider.name}).", p));
                    issuePoints.Add(i);
                }
            }
        }

        private void ValidateSegments(List<Vector3> pts, List<RouteIssue> issues, HashSet<int> issuePoints)
        {
            int count = pts.Count;
            int segments = loop ? count : count - 1;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % count];
                float length = BotPath.PlanarDistance(a, b);

                if (length < 1.5f)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Pontos {i:00} e {(i + 1) % count:00} estão muito próximos ({length:F1} m). Remova um deles.", a));
                    issuePoints.Add(i);
                }
                else if (length > maxSegmentLength)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Segmento {i:00}→{(i + 1) % count:00} tem {length:F0} m (máx. recomendado {maxSegmentLength:F0} m). " +
                        "Adicione pontos intermediários para a curva não cortar fora da pista.",
                        Vector3.Lerp(a, b, 0.5f)));
                }

                // Reversão brusca: a rota dobra sobre si mesma neste ponto.
                Vector3 inDir = b - a;
                Vector3 c = pts[(i + 2) % count];
                Vector3 outDir = c - b;
                inDir.y = 0f;
                outDir.y = 0f;
                bool lastOpenSegment = !loop && i >= segments - 1;
                if (!lastOpenSegment && inDir.sqrMagnitude > 0.01f && outDir.sqrMagnitude > 0.01f)
                {
                    float angle = Vector3.Angle(inDir, outDir);
                    if (angle > 120f)
                    {
                        issues.Add(new RouteIssue(IssueSeverity.Warning,
                            $"Ponto {(i + 1) % count:00}: curva de {angle:F0}° — a rota dobra sobre si mesma. Ordem dos pontos está certa?",
                            b));
                        issuePoints.Add((i + 1) % count);
                    }
                }
            }
        }

        private void ValidateCheckpointCoverage(List<RouteIssue> issues)
        {
            if (cachedPreviewPath == null || !cachedPreviewPath.IsValid)
                return;

            RaceCheckpoint[] checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
            foreach (RaceCheckpoint cp in checkpoints)
            {
                if (cp == null)
                    continue;

                BotPath.NearestResult nearest = cachedPreviewPath.FindNearestGlobal(cp.transform.position);
                float distance = Mathf.Sqrt(nearest.SqrPlanarDistance);
                if (distance > checkpointCoverageDistance)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Error,
                        $"Checkpoint {cp.CheckpointIndex} ('{cp.name}') está a {distance:F0} m da rota — os bots não vão passar por ele. " +
                        "A rota está incompleta ou o checkpoint ficou fora do traçado.",
                        cp.transform.position));
                }
            }
        }

        private void ValidateBranches(List<RouteIssue> issues)
        {
            if (cachedPreviewPath == null || !cachedPreviewPath.IsValid)
                return;

            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                BotRouteBranch branch = child.GetComponent<BotRouteBranch>();
                if (branch == null)
                    continue;

                List<Vector3> bpts = branch.GetWorldPoints();
                if (bpts.Count < 2)
                {
                    issues.Add(new RouteIssue(IssueSeverity.Error,
                        $"Branch '{branch.name}': precisa de pelo menos 2 pontos (tem {bpts.Count}).", child.position));
                    continue;
                }

                BotPath.NearestResult entry = cachedPreviewPath.FindNearestGlobal(bpts[0]);
                BotPath.NearestResult exit = cachedPreviewPath.FindNearestGlobal(bpts[bpts.Count - 1]);

                float entryDistance = Mathf.Sqrt(entry.SqrPlanarDistance);
                float exitDistance = Mathf.Sqrt(exit.SqrPlanarDistance);

                if (entryDistance > 15f)
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Branch '{branch.name}': o PRIMEIRO ponto está a {entryDistance:F0} m da linha principal — aproxime-o do ponto de entrada.",
                        bpts[0]));

                if (exitDistance > 15f)
                    issues.Add(new RouteIssue(IssueSeverity.Warning,
                        $"Branch '{branch.name}': o ÚLTIMO ponto está a {exitDistance:F0} m da linha principal — aproxime-o do ponto de saída.",
                        bpts[bpts.Count - 1]));

                // Saída deve ficar À FRENTE da entrada no sentido da corrida.
                if (loop && cachedPreviewPath.TotalLength > 1f)
                {
                    float ahead = Mathf.Repeat(exit.DistanceOnPath - entry.DistanceOnPath, cachedPreviewPath.TotalLength);
                    if (ahead > cachedPreviewPath.TotalLength * 0.6f)
                        issues.Add(new RouteIssue(IssueSeverity.Warning,
                            $"Branch '{branch.name}': a saída projeta ATRÁS da entrada no sentido da corrida — os pontos podem estar em ordem invertida.",
                            bpts[0]));
                }
            }
        }

        // ------------------------------------------------------------------ ferramentas (Editor)
        [ContextMenu("AI/Create Points From Race Checkpoints")]
        public void CreatePointsFromRaceCheckpoints()
        {
            Undo.RegisterFullObjectHierarchyUndo(gameObject, "Create Bot Racing Line Points");
            ClearChildPoints();

            RaceCheckpoint[] checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
            var sorted = new List<RaceCheckpoint>(checkpoints);
            sorted.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));

            for (int i = 0; i < sorted.Count; i++)
            {
                RaceCheckpoint checkpoint = sorted[i];
                if (checkpoint == null)
                    continue;

                CreateChildPoint(i, checkpoint.name, checkpoint.transform.position, checkpoint.transform.rotation);
            }

            useChildrenAsPoints = true;
            loop = true;
            InvalidateEditorCache();
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("AI/Snap Points To Ground")]
        public void SnapPointsToGround()
        {
            foreach (Transform point in GetPointTransforms())
            {
                if (point == null)
                    continue;

                // Origem logo acima do ponto: não puxa o ponto para cima de obstáculos
                // suspensos (pás de moinho, pontes) que estejam sobre a pista.
                Vector3 origin = point.position + Vector3.up * 2f;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Ponto enterrado: a pista está acima da origem — tenta de mais alto.
                    if (!Physics.Raycast(point.position + Vector3.up * 25f, Vector3.down, out hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                }

                Undo.RecordObject(point, "Snap Bot Racing Line Point");
                point.position = hit.point + Vector3.up * 0.05f;
                EditorUtility.SetDirty(point);
            }

            InvalidateEditorCache();
        }

        [ContextMenu("AI/Renumber Child Points")]
        public void RenumberChildPoints()
        {
            int mainIndex = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!IsRoutePointChild(child))
                    continue;

                Undo.RecordObject(child.gameObject, "Renumber Bot Racing Line Point");
                child.name = $"P{mainIndex:00}";
                EditorUtility.SetDirty(child.gameObject);
                mainIndex++;
            }
        }

        private IEnumerable<Transform> GetPointTransforms()
        {
            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                    yield return child;

                yield break;
            }

            for (int i = 0; i < points.Count; i++)
                yield return points[i];
        }

        private void ClearChildPoints()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
        }

        private void CreateChildPoint(int index, string sourceName, Vector3 position, Quaternion rotation)
        {
            string suffix = string.IsNullOrWhiteSpace(sourceName) ? "Point" : sourceName;
            var point = new GameObject($"P{index:00}_{suffix}");
            Undo.RegisterCreatedObjectUndo(point, "Create Bot Racing Line Point");
            point.transform.SetParent(transform, false);
            point.transform.SetPositionAndRotation(position, rotation);
        }
#endif

        private void OnDrawGizmos()
        {
            DrawGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);
        }

        private void DrawGizmos(bool selected)
        {
            List<Vector3> pts = GetWorldPoints();
            if (pts.Count == 0)
                return;

            Color baseColor = selected ? selectedColor : gizmoColor;

#if UNITY_EDITOR
            // Curva suavizada = caminho REAL dos bots.
            if (drawSmoothedCurve && pts.Count >= 3)
            {
                BotPath preview = GetEditorPreviewPath();
                if (preview != null && preview.IsValid)
                {
                    Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, selected ? 1f : 0.8f);
                    int sampleCount = preview.Points.Count;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int next = i + 1;
                        if (next >= sampleCount)
                        {
                            if (!preview.Looped)
                                break;
                            next = 0;
                        }

                        Gizmos.DrawLine(Offset(preview.Points[i]), Offset(preview.Points[next]));
                    }
                }
            }
#endif

            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 current = Offset(pts[i]);

#if UNITY_EDITOR
                bool hasIssue = cachedIssuePointIndices != null && cachedIssuePointIndices.Contains(i);
                Gizmos.color = hasIssue ? issueColor : baseColor;
#else
                Gizmos.color = baseColor;
#endif
                Gizmos.DrawWireSphere(current, pointRadius);

                int next = i + 1;
                if (next >= pts.Count)
                {
                    if (loop)
                        next = 0;
                    else
                        continue;
                }

                Vector3 nextPoint = Offset(pts[next]);

                if (!drawSmoothedCurve)
                    Gizmos.DrawLine(current, nextPoint);

                if (drawDirectionArrows)
                    DrawArrow(current, nextPoint);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Offset(pts[0]), pointRadius * 1.4f);

#if UNITY_EDITOR
            if (selected && drawLabelsWhenSelected)
            {
                for (int i = 0; i < pts.Count; i++)
                    Handles.Label(Offset(pts[i]) + Vector3.up * (pointRadius * 1.2f), i.ToString("00"));
            }

            // Marca os pontos com problema mesmo sem seleção (esfera vermelha maior).
            if (cachedIssuePointIndices != null)
            {
                Gizmos.color = issueColor;
                foreach (int index in cachedIssuePointIndices)
                {
                    if (index >= 0 && index < pts.Count)
                        Gizmos.DrawWireSphere(Offset(pts[index]), pointRadius * 1.8f);
                }
            }
#endif
        }

        private Vector3 Offset(Vector3 point)
        {
            return point + Vector3.up * lineYOffset;
        }

        private void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            direction.Normalize();
            Vector3 mid = Vector3.Lerp(from, to, 0.62f);
            Vector3 back = -direction * pointRadius * 1.7f;
            Vector3 left = Quaternion.Euler(0f, 28f, 0f) * back;
            Vector3 right = Quaternion.Euler(0f, -28f, 0f) * back;

            Gizmos.DrawLine(mid, mid + left);
            Gizmos.DrawLine(mid, mid + right);
        }
    }
}
