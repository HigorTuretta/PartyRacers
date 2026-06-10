using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PartyRacers.AI
{
    /// <summary>
    /// Optional hand-authored racing line for bots.
    /// Create this object in the race scene, add child transforms in driving order, and move them
    /// along the center/racing line of the road. BotPathFollower uses this line before checkpoints.
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

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField] private float pointRadius = 1.2f;
        [SerializeField] private float lineYOffset = 0.35f;
        [SerializeField] private bool drawDirectionArrows = true;
        [SerializeField] private bool drawLabelsWhenSelected = true;

        public bool Loop => loop;

        private readonly List<Vector3> worldBuffer = new List<Vector3>();

        public List<Vector3> GetWorldPoints()
        {
            worldBuffer.Clear();

            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    // Filhos com BotRouteBranch são rotas alternativas, não pontos da linha principal.
                    if (child != null && child.GetComponent<BotRouteBranch>() == null)
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

        private int CountValidPoints()
        {
            int count = 0;
            if (useChildrenAsPoints)
            {
                foreach (Transform child in transform)
                {
                    if (child != null && child.GetComponent<BotRouteBranch>() == null)
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
        [ContextMenu("AI/Create Points From Race Checkpoints")]
        private void CreatePointsFromRaceCheckpoints()
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
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("AI/Snap Points To Ground")]
        private void SnapPointsToGround()
        {
            foreach (Transform point in GetPointTransforms())
            {
                if (point == null)
                    continue;

                Vector3 origin = point.position + Vector3.up * 30f;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                Undo.RecordObject(point, "Snap Bot Racing Line Point");
                point.position = hit.point + Vector3.up * 0.05f;
                EditorUtility.SetDirty(point);
            }
        }

        [ContextMenu("AI/Renumber Child Points")]
        private void RenumberChildPoints()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Undo.RecordObject(child.gameObject, "Renumber Bot Racing Line Point");
                child.name = $"P{i:00}";
                EditorUtility.SetDirty(child.gameObject);
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

            Gizmos.color = selected ? selectedColor : gizmoColor;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 current = Offset(pts[i]);
                Gizmos.DrawWireSphere(current, pointRadius);

                int next = i + 1;
                if (next >= pts.Count)
                {
                    if (loop)
                        next = 0;
                    else
                        break;
                }

                Vector3 nextPoint = Offset(pts[next]);
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
