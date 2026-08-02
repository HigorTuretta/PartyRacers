#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Mede o custo real de um frame da cena em playmode e grava o resultado num arquivo, para dar
    /// número às otimizações em vez de opinião.
    ///
    /// Fluxo: entra em playmode, espera a corrida engrenar (spawn dos 15 bots, aquecimento de shader),
    /// acumula por alguns segundos e então escreve a média e sai do playmode. O resultado sai em
    /// <c>Temp/partyracers_perf.txt</c>, legível de fora do Editor.
    ///
    /// <para>
    /// O detalhe que faz a coisa funcionar: entrar em playmode dispara um DOMAIN RELOAD, que apaga
    /// todos os delegates estáticos. Uma versão anterior registrava <c>EditorApplication.update</c>
    /// dentro de <see cref="Medir"/> e o handler era descartado antes do primeiro frame — a medição
    /// nunca acontecia e o arquivo ficava preso em "medindo...". Por isso o estado de "estou medindo"
    /// vive em <see cref="SessionState"/> (que sobrevive ao reload) e o handler é REARMADO por
    /// <c>[InitializeOnLoadMethod]</c>, que roda depois de cada reload. Os acumuladores estáticos
    /// serem zerados pelo reload é justamente o que queremos: a contagem começa dentro do playmode.
    /// </para>
    /// </summary>
    public static class ScenePerfProbe
    {
        const string Arquivo = "Temp/partyracers_perf.txt";
        const string ChaveAtiva = "PartyRacers.PerfProbe.Ativa";

        /// <summary>
        /// Espera em segundos de playmode antes de começar a medir. Generosa de propósito: a largada
        /// leva ~20 s para os bots se espalharem, e medir antes disso mede a cena parada.
        /// </summary>
        const float SegundosDeAquecimento = 25f;

        const float SegundosMedindo = 5f;

        [MenuItem("Tools/PartyRacers/Cenário/Medir performance (playmode)")]
        public static void Medir()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Perf] já está em playmode — saia antes de medir.");
                return;
            }

            File.WriteAllText(Arquivo, "medindo...\n");
            SessionState.SetBool(ChaveAtiva, true);

            // Nada de registrar o handler aqui: o domain reload que vem a seguir apagaria. Quem
            // rearma é Rearmar(), abaixo.
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Rearmar()
        {
            if (!SessionState.GetBool(ChaveAtiva, false))
                return;

            EditorApplication.update += Bombear;
            EditorApplication.playModeStateChanged += AoMudarPlaymode;
        }

        /// <summary>Se o playmode acabar sem a medição fechar, não deixa a flag armada para sempre.</summary>
        static void AoMudarPlaymode(PlayModeStateChange estado)
        {
            if (estado != PlayModeStateChange.ExitingPlayMode)
                return;

            if (SessionState.GetBool(ChaveAtiva, false))
            {
                SessionState.SetBool(ChaveAtiva, false);
                if (!fechou)
                    Debug.LogWarning("[Perf] playmode encerrado antes de a medição terminar.");
            }

            Desarmar();
        }

        static float relogio;
        static int frames;
        static double somaMs, piorMs, somaRenderMs;
        static long somaTris, somaDraws, somaSetPass, somaCasters;
        static bool fechou;

        static void Bombear()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                return;

            relogio += Time.unscaledDeltaTime;

            if (relogio < SegundosDeAquecimento)
                return;

            double ms = Time.unscaledDeltaTime * 1000.0;
            somaMs += ms;
            if (ms > piorMs) piorMs = ms;

            somaRenderMs += UnityStats.renderTime;
            somaTris += UnityStats.triangles;
            somaDraws += UnityStats.drawCalls;
            somaSetPass += UnityStats.setPassCalls;
            somaCasters += UnityStats.shadowCasters;
            frames++;

            if (relogio < SegundosDeAquecimento + SegundosMedindo)
                return;

            Fechar();
        }

        static void Fechar()
        {
            if (frames == 0)
            {
                Debug.LogError("[Perf] nenhum frame medido.");
                SessionState.SetBool(ChaveAtiva, false);
                Desarmar();
                EditorApplication.ExitPlaymode();
                return;
            }

            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("frames medidos: " + frames + " em " + SegundosMedindo.ToString("F0", c) + "s");
            sb.AppendLine("frame medio (ms): " + (somaMs / frames).ToString("F2", c));
            sb.AppendLine("fps medio: " + (1000.0 / (somaMs / frames)).ToString("F1", c));
            sb.AppendLine("pior frame (ms): " + piorMs.ToString("F2", c));
            sb.AppendLine("render time (ms): " + (somaRenderMs / frames).ToString("F2", c));
            sb.AppendLine("triangulos (media): " + (somaTris / frames));
            sb.AppendLine("draw calls (media): " + (somaDraws / frames));
            sb.AppendLine("setpass calls (media): " + (somaSetPass / frames));
            sb.AppendLine("shadow casters (media): " + (somaCasters / frames));
            sb.AppendLine("memoria alocada (MB): " +
                          (Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0)).ToString("F1", c));

            File.WriteAllText(Arquivo, sb.ToString());
            Debug.Log("[Perf]\n" + sb);

            fechou = true;
            SessionState.SetBool(ChaveAtiva, false);
            Desarmar();
            EditorApplication.ExitPlaymode();
        }

        static void Desarmar()
        {
            EditorApplication.update -= Bombear;
            EditorApplication.playModeStateChanged -= AoMudarPlaymode;
        }
    }
}
#endif
