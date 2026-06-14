using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.AI.Editor
{
    /// <summary>
    /// Inspector do BotRacingLine: estatísticas da rota, validação automática com lista de
    /// problemas (clique em "Ver" para focar a câmera no local) e ferramentas de autoria.
    /// Com o objeto selecionado, cada ponto ganha um handle de posição na Scene View — dá
    /// para ajustar o traçado inteiro sem selecionar os filhos um a um.
    /// </summary>
    [CustomEditor(typeof(BotRacingLine))]
    public class BotRacingLineEditor : UnityEditor.Editor
    {
        private bool showIssues = true;

        public override void OnInspectorGUI()
        {
            var line = (BotRacingLine)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            DrawStats(line);

            EditorGUILayout.Space(4);
            DrawTools(line);

            EditorGUILayout.Space(4);
            DrawIssues(line);
        }

        private void DrawStats(BotRacingLine line)
        {
            BotPath preview = line.GetEditorPreviewPath();
            int pointCount = line.GetWorldPoints().Count;
            int branchCount = line.GetBranches().Count;
            int zoneCount = line.GetZones().Count;
            float length = preview != null && preview.IsValid ? preview.TotalLength : 0f;

            EditorGUILayout.LabelField("Rota", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Pontos: {pointCount}    Branches: {branchCount}    Zonas: {zoneCount}    Comprimento: {length:F0} m");
        }

        private void DrawTools(BotRacingLine line)
        {
            EditorGUILayout.LabelField("Ferramentas", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Colar pontos no chão"))
                    line.SnapPointsToGround();

                if (GUILayout.Button("Renumerar pontos"))
                    line.RenumberChildPoints();
            }

            if (GUILayout.Button("Recriar pontos a partir dos checkpoints (apaga os atuais)"))
            {
                if (EditorUtility.DisplayDialog(
                        "Recriar rota",
                        "Isso APAGA todos os pontos e branches atuais e cria um ponto por checkpoint. Continuar?",
                        "Recriar", "Cancelar"))
                {
                    line.CreatePointsFromRaceCheckpoints();
                }
            }

            if (GUILayout.Button("Revalidar rota"))
                line.InvalidateEditorCache();
        }

        private void DrawIssues(BotRacingLine line)
        {
            List<BotRacingLine.RouteIssue> issues = line.GetValidationIssues();

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Rota válida — nenhum problema encontrado.", MessageType.Info);
                return;
            }

            int errors = 0, warnings = 0;
            foreach (BotRacingLine.RouteIssue issue in issues)
            {
                if (issue.Severity == BotRacingLine.IssueSeverity.Error) errors++;
                else if (issue.Severity == BotRacingLine.IssueSeverity.Warning) warnings++;
            }

            showIssues = EditorGUILayout.Foldout(showIssues, $"Problemas ({errors} erros, {warnings} avisos)", true);
            if (!showIssues)
                return;

            foreach (BotRacingLine.RouteIssue issue in issues)
            {
                MessageType messageType = issue.Severity switch
                {
                    BotRacingLine.IssueSeverity.Error => MessageType.Error,
                    BotRacingLine.IssueSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, messageType);

                    if (issue.HasPosition && GUILayout.Button("Ver", GUILayout.Width(40), GUILayout.Height(38)))
                        FramePosition(issue.Position);
                }
            }
        }

        private static void FramePosition(Vector3 position)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                view.Frame(new Bounds(position, Vector3.one * 30f), false);
        }

        private void OnSceneGUI()
        {
            var line = (BotRacingLine)target;
            bool changed = false;

            foreach (Transform point in line.GetPointTransformList())
                changed |= DrawPointHandle(point, "Move Bot Racing Line Point");

            // Handles também para os pontos dos branches.
            foreach (BotRouteBranch branch in line.GetBranches())
            {
                foreach (Transform point in branch.transform)
                    changed |= DrawPointHandle(point, "Move Bot Route Branch Point");
            }

            if (changed)
                line.InvalidateEditorCache();
        }

        private static bool DrawPointHandle(Transform point, string undoLabel)
        {
            if (point == null)
                return false;

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(point.position, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(point, undoLabel);
            point.position = newPosition;
            return true;
        }
    }
}
