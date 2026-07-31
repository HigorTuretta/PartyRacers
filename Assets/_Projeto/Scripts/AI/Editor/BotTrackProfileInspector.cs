using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Ferramenta de diagnóstico: mede a pista como os bots a medem e mostra o resultado.
    ///
    /// Serve para responder, ANTES de rodar a corrida, à pergunta que sempre custava uma sessão
    /// inteira de tentativa e erro: "os bots conseguem passar aqui?". A janela aponta os pontos
    /// onde a resposta é não — vão largo demais, degrau alto demais, corredor mais estreito que
    /// o kart — com a posição no mundo para dar duplo clique e ir até lá.
    ///
    /// Abrir em: Tools ▸ PartyRacers ▸ Analisar pista para os bots.
    /// </summary>
    public class BotTrackProfileInspector : EditorWindow
    {
        private BotTrackProfile profile;
        private string report = string.Empty;
        private Vector2 scroll;
        private BotTrackBaker.Settings settings = new BotTrackBaker.Settings();
        private readonly List<Vector3> warningPositions = new List<Vector3>();

        private bool drawSpeed = true;
        private bool drawSurface = true;
        private bool drawCorridor = true;

        [MenuItem("Tools/PartyRacers/Analisar pista para os bots")]
        public static void Open()
        {
            GetWindow<BotTrackProfileInspector>("Pista dos bots").minSize = new Vector2(430f, 320f);
        }

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Mede a pista do jeito que os bots a medem: largura livre real, buracos, degraus " +
                "entre peças, rampas, raio das curvas e a velocidade possível em cada metro.\n\n" +
                "Nada disso precisa ser autorado à mão — vale para qualquer pista nova.",
                MessageType.Info);

            settings.spacing = EditorGUILayout.Slider("Espaçamento das amostras (m)", settings.spacing, 0.5f, 3f);
            settings.corridorHalfWidth = EditorGUILayout.Slider("Meia-largura sondada (m)", settings.corridorHalfWidth, 4f, 15f);
            settings.maxSpeedKmh = EditorGUILayout.FloatField("Velocidade máx. do kart (km/h)", settings.maxSpeedKmh);
            settings.maxClimbableStep = EditorGUILayout.Slider("Degrau que o kart sobe (m)", settings.maxClimbableStep, 0.3f, 2f);

            EditorGUILayout.Space();
            if (GUILayout.Button("Medir a pista agora", GUILayout.Height(30f)))
                Bake();

            if (profile == null)
                return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                drawSpeed = GUILayout.Toggle(drawSpeed, "Velocidade", EditorStyles.miniButtonLeft);
                drawSurface = GUILayout.Toggle(drawSurface, "Superfície", EditorStyles.miniButtonMid);
                drawCorridor = GUILayout.Toggle(drawCorridor, "Corredor livre", EditorStyles.miniButtonRight);
            }
            SceneView.RepaintAll();

            if (profile.Warnings.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Pontos que os bots NÃO passam ({profile.Warnings.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < profile.Warnings.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(profile.Warnings[i], MessageType.Warning);
                        if (i < warningPositions.Count && GUILayout.Button("Ver", GUILayout.Width(42f), GUILayout.Height(38f)))
                            FrameOn(warningPositions[i]);
                    }
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Bake()
        {
            BotRacingLine line = FindFirstObjectByType<BotRacingLine>();
            if (line == null || !line.HasEnoughPoints())
            {
                report = "Nenhuma BotRacingLine com pontos suficientes nesta cena.";
                profile = null;
                return;
            }

            var path = new BotPath();
            path.BuildFrom(line.GetWorldPoints(), line.Loop, 4f);

            BotTrackProfileCache.Clear();
            profile = BotTrackBaker.Bake(path, settings, line.GetZones(), out report);

            // Posições dos avisos, para o botão "Ver".
            warningPositions.Clear();
            if (profile != null)
            {
                for (int i = 0; i < profile.StationCount; i++)
                {
                    BotTrackStation st = profile.StationAt(i * profile.Spacing);
                    if (st.StepRise > settings.maxClimbableStep)
                        warningPositions.Add(st.Position);
                }
            }
            SceneView.RepaintAll();
        }

        private static void FrameOn(Vector3 position)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                view.LookAt(position, view.rotation, 22f);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (profile == null || !profile.IsValid)
                return;

            // Amostra esparsa: desenhar 1839 estações trava a Scene View.
            int stride = Mathf.Max(1, Mathf.RoundToInt(2f / profile.Spacing));

            for (int i = 0; i < profile.StationCount; i += stride)
            {
                BotTrackStation st = profile.StationAt(i * profile.Spacing);
                Vector3 p = st.Position + Vector3.up * 0.4f;
                Vector3 right = Vector3.Cross(Vector3.up, st.Tangent).normalized;

                if (drawCorridor && st.UsableWidth > 0.01f)
                {
                    Handles.color = new Color(0.2f, 0.9f, 1f, 0.5f);
                    Handles.DrawLine(p - right * st.HalfWidthLeft, p + right * st.HalfWidthRight);
                }

                if (drawSpeed)
                {
                    // Verde = rápido, vermelho = lento.
                    float t = Mathf.InverseLerp(55f, settings.maxSpeedKmh, st.SafeSpeedKmh);
                    Handles.color = Color.Lerp(Color.red, Color.green, t);
                    Handles.DrawLine(p, p + Vector3.up * (0.4f + t * 2.5f));
                }

                if (drawSurface && st.Surface != BotSurface.Normal)
                {
                    Handles.color = SurfaceColor(st.Surface);
                    Handles.SphereHandleCap(0, p + Vector3.up * 1.2f, Quaternion.identity, 1.1f, EventType.Repaint);
                }
            }

            // Curvas com drift.
            Handles.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            foreach (BotCorner c in profile.Corners)
            {
                if (!c.WantsDrift)
                    continue;
                Vector3 apex = profile.StationAt(c.ApexDistance).Position + Vector3.up * 2.5f;
                Handles.Label(apex, $"drift  R{c.MinRadius:F0}m  {c.ApexSpeedKmh:F0}km/h");
            }

            // Avisos.
            Handles.color = Color.red;
            for (int i = 0; i < warningPositions.Count; i++)
                Handles.DrawWireDisc(warningPositions[i] + Vector3.up * 0.5f, Vector3.up, 3f);
        }

        private static Color SurfaceColor(BotSurface s)
        {
            switch (s)
            {
                case BotSurface.Rampa: return new Color(1f, 0.85f, 0.1f, 0.85f);
                case BotSurface.Decolagem: return new Color(1f, 0.35f, 0.9f, 0.9f);
                case BotSurface.Pouso: return new Color(0.5f, 0.6f, 1f, 0.85f);
                case BotSurface.Vao: return new Color(1f, 0.15f, 0.1f, 0.9f);
                case BotSurface.Degrau: return new Color(1f, 0.55f, 0.15f, 0.9f);
                default: return Color.white;
            }
        }
    }
}
