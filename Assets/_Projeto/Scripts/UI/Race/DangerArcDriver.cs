using System.Collections.Generic;
using UnityEngine;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Decide QUANDO o arco vermelho acende. O <see cref="DangerArcUI"/> sabe desenhar o aviso mas
    /// não sabe que há um foguete chegando — este componente faz a ponte.
    ///
    /// Uma ameaça só conta quando está APROXIMANDO: um foguete que já passou reto pelo jogador não
    /// pode manter a borda vermelha, senão o aviso vira ruído de fundo e o jogador aprende a
    /// ignorá-lo justamente antes da vez em que ele importava.
    ///
    /// Duas intensidades, como manda a spec: fraco = ameaça se aproximando (pulso 0,8 s), forte =
    /// impacto iminente (pulso 0,25 s). E só isso: sem texto, sem seta, sem alerta no centro.
    /// </summary>
    [DisallowMultipleComponent]
    public class DangerArcDriver : MonoBehaviour
    {
        [Header("Peças")]
        [SerializeField] private DangerArcUI arco;
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Distâncias")]
        [Tooltip("Dentro deste raio uma ameaça que se aproxima acende o arco fraco.")]
        [SerializeField] private float distanciaAproximando = 45f;
        [Tooltip("Dentro deste raio o aviso passa a iminente.")]
        [SerializeField] private float distanciaIminente = 16f;

        [Header("Filtro")]
        [Tooltip("Velocidade mínima de aproximação (m/s) para a ameaça contar. Abaixo disso ela " +
                 "está passeando ao lado, não vindo para cima.")]
        [SerializeField] private float velocidadeMinimaDeAproximacao = 2f;
        [Tooltip("Intervalo entre varreduras. O arco não precisa de precisão de frame.")]
        [SerializeField] private float intervaloDeVarredura = 0.1f;

        private readonly Dictionary<Transform, float> distanciaAnterior = new Dictionary<Transform, float>();
        private float proximaVarredura;

        private void Reset()
        {
            arco = GetComponent<DangerArcUI>();
            dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        // Prefab não serializa referência de cena — ver VitalClusterUI.Awake.
        private void Awake()
        {
            if (arco == null)
                arco = GetComponent<DangerArcUI>();

            if (dados == null)
                dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        private void Update()
        {
            if (arco == null || dados == null)
                return;

            if (Time.time < proximaVarredura)
                return;

            float dt = Mathf.Max(0.0001f, Time.time - (proximaVarredura - intervaloDeVarredura));
            proximaVarredura = Time.time + intervaloDeVarredura;

            dados.Refresh();

            if (!dados.HasLocalKart)
            {
                arco.Limpar();
                return;
            }

            Avaliar(dados.LocalKart.transform, dt);
        }

        private void Avaliar(Transform jogador, float dt)
        {
            RaceThreats.Limpar();
            IReadOnlyList<Transform> ameacas = RaceThreats.Ativos;

            DangerArcUI.Nivel pior = DangerArcUI.Nivel.Nenhum;
            bool deTras = false;
            float menorDistancia = float.MaxValue;

            for (int i = 0; i < ameacas.Count; i++)
            {
                Transform ameaca = ameacas[i];
                if (ameaca == null)
                    continue;

                // Projétil disparado pelo próprio jogador sai do carro dele: nos primeiros metros
                // ele está longe e "afastando", então o teste de aproximação já o descarta sozinho.
                float distancia = Vector3.Distance(ameaca.position, jogador.position);

                bool tinhaAnterior = distanciaAnterior.TryGetValue(ameaca, out float anterior);
                distanciaAnterior[ameaca] = distancia;

                if (!tinhaAnterior)
                    continue;

                float aproximacao = (anterior - distancia) / dt;
                if (aproximacao < velocidadeMinimaDeAproximacao)
                    continue;

                if (distancia > distanciaAproximando)
                    continue;

                DangerArcUI.Nivel nivel = distancia <= distanciaIminente
                    ? DangerArcUI.Nivel.Forte
                    : DangerArcUI.Nivel.Fraco;

                if (nivel > pior || (nivel == pior && distancia < menorDistancia))
                {
                    pior = nivel;
                    menorDistancia = distancia;

                    Vector3 paraAmeaca = ameaca.position - jogador.position;
                    deTras = Vector3.Dot(paraAmeaca, jogador.forward) < 0f;
                }
            }

            LimparAmeacasVelhas(ameacas);
            arco.Definir(pior, deTras);
        }

        private static readonly List<Transform> remover = new List<Transform>();

        private void LimparAmeacasVelhas(IReadOnlyList<Transform> vivas)
        {
            if (distanciaAnterior.Count <= vivas.Count)
                return;

            remover.Clear();
            foreach (Transform conhecida in distanciaAnterior.Keys)
            {
                if (conhecida == null || !Contem(vivas, conhecida))
                    remover.Add(conhecida);
            }

            for (int i = 0; i < remover.Count; i++)
                distanciaAnterior.Remove(remover[i]);
        }

        private static bool Contem(IReadOnlyList<Transform> lista, Transform alvo)
        {
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i] == alvo)
                    return true;
            }

            return false;
        }
    }
}
