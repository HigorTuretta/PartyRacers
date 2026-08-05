using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.UI.Garage
{
    /// <summary>
    /// A câmera da garagem é parte da INTERFACE, não do cenário: trocar de categoria aproxima a
    /// câmera exatamente da peça que está sendo editada. Editar rodas sem ver as rodas é o defeito
    /// que a grade de cards sozinha não resolve.
    ///
    /// As poses vêm de `Screen_Garage.json → camera3D.poses`. O blend é 0,45 s easeInOutCubic —
    /// rápido o bastante para não atrasar a navegação e lento o bastante para o olho acompanhar
    /// para onde foi.
    ///
    /// A oscilação de repouso (0,05 unidade em X a 0,12 Hz) existe para a cena não parecer uma
    /// foto; ela some assim que o jogador interage, porque respiração de câmera durante a
    /// comparação de dois cosméticos atrapalha em vez de ajudar.
    /// </summary>
    [DisallowMultipleComponent]
    public class GarageCameraRig : MonoBehaviour
    {
        /// <summary>Uma pose nomeada. O alvo é procurado por nome dentro do carro montado.</summary>
        /// <summary>
        /// Uma pose nomeada, descrita em relação à CAIXA do carro — não em coordenadas do mundo.
        ///
        /// Coordenadas fixas quebram a cada troca de modelo: os carros do pack têm tamanhos
        /// diferentes, e a mesma posição que enquadra a roda de um mostra o asfalto ao lado do
        /// outro. Com direção + zoom + alvo em frações da caixa, a pose vale para qualquer carro.
        ///
        /// São os mesmos ângulos do `PreviewBaker`: o card e a câmera mostram a peça do mesmo
        /// jeito, então clicar num card não desorienta.
        /// </summary>
        [System.Serializable]
        public class Pose
        {
            [Tooltip("Chave da categoria: modelo, cor, rodas, frente, traseira, teto, adesivos.")]
            public string categoria;
            [Tooltip("De onde a câmera olha, em espaço do carro. Normalizado.")]
            public Vector3 direcao = new Vector3(1f, 0.45f, 1f);
            [Tooltip("Menor que 1 aproxima da peça.")]
            public float zoom = 1f;
            [Tooltip("Para onde olhar, em frações do tamanho da caixa a partir do centro.")]
            public Vector3 alvo;
            public float fov = 34f;
            [Tooltip("Deixa o carro parado enquanto esta categoria está aberta.")]
            public bool congelarGiro = true;
        }

        [Header("Peças")]
        [SerializeField] private Camera camera3D;
        [Tooltip("Raiz do palco. As posições das poses são locais a ele, para que mover o palco " +
                 "(x=1341 em vez de 960) leve a câmera junto.")]
        [SerializeField] private Transform palco;
        [Tooltip("Raiz do carro montado, onde os alvos são procurados por nome.")]
        [SerializeField] private Transform carro;

        [Header("Poses (Screen_Garage.json → camera3D.poses)")]
        [SerializeField]
        private List<Pose> poses = new List<Pose>
        {
            new Pose { categoria = "modelo",   direcao = new Vector3( 1f,   0.45f,  1f),    zoom = 1f,    fov = 34f, congelarGiro = false },
            new Pose { categoria = "cor",      direcao = new Vector3( 1f,   0.45f,  1f),    zoom = 1f,    fov = 34f, congelarGiro = false },
            new Pose { categoria = "rodas",    direcao = new Vector3( 1f,   0.12f,  0.35f), zoom = 0.72f, fov = 30f, alvo = new Vector3(0.5f, -0.5f, 0.45f) },
            new Pose { categoria = "frente",   direcao = new Vector3( 0.35f, 0.28f, 1f),    zoom = 0.78f, fov = 32f, alvo = new Vector3(0f, -0.2f, 0.65f) },
            new Pose { categoria = "traseira", direcao = new Vector3( 0.35f, 0.3f, -1f),    zoom = 0.78f, fov = 32f, alvo = new Vector3(0f, -0.1f, -0.65f) },
            new Pose { categoria = "teto",     direcao = new Vector3( 0.6f,  0.85f, 0.6f),  zoom = 0.86f,  fov = 32f, alvo = new Vector3(0f, 0.4f, 0f) },
            new Pose { categoria = "adesivos", direcao = new Vector3( 1f,   0.12f,  0.05f), zoom = 0.92f,  fov = 30f },
        };

        [Tooltip("Empurra o carro para a direita da tela, para o espaço livre ao lado do painel. " +
                 "0 = centralizado (a garagem usa ~0,6; §4 do handoff pede o kart em x=1341).")]
        [Range(0f, 2f)] [SerializeField] private float viesLateral = 0.9f;

        [Tooltip("Negativo desce o carro na tela, para assentá-lo na plataforma do cenário.")]
        [Range(-1.5f, 1f)] [SerializeField] private float viesVertical = -0.12f;

        [Tooltip("Quanto a câmera recua além do necessário para a peça caber. Menor = mais perto.")]
        [Range(1f, 3f)] [SerializeField] private float afastamento = 2.15f;

        [Header("Blend")]
        [SerializeField, Min(0.05f)] private float duracao = 0.45f;

        [Header("Respiração de repouso")]
        [SerializeField] private float amplitudeIdle = 0.05f;
        [SerializeField] private float frequenciaIdle = 0.12f;
        [Tooltip("Segundos sem interação para a respiração voltar.")]
        [SerializeField] private float atrasoDoIdle = 1.2f;

        private Vector3 origemPos, destinoPos;
        private Quaternion origemRot, destinoRot;
        private float origemFov, destinoFov;
        private float t = 1f;
        private float ultimaInteracao;
        private float relogioIdle;

        private void Awake()
        {
            if (camera3D == null)
                camera3D = GetComponentInChildren<Camera>();

            if (palco == null)
                palco = transform;
        }

        /// <summary>Vai para a pose da categoria. Categoria desconhecida cai na de visão completa.</summary>
        public void IrPara(string categoria)
        {
            Pose pose = Resolver(categoria);
            if (pose == null || camera3D == null)
                return;

            if (!MedirCarro(out Bounds caixa))
                return;

            origemPos = camera3D.transform.position;
            origemRot = camera3D.transform.rotation;
            origemFov = camera3D.fieldOfView;

            // A peça em edição precisa estar SEMPRE do mesmo lado. Com o carro girando sozinho,
            // ir para a pose das rodas mostrava ora a dianteira, ora a traseira — a câmera fazia o
            // movimento certo e a peça não estava lá. Zerar a rotação é o que torna a pose
            // previsível, e é o gesto que o jogador espera: "me mostre ISTO".
            ZerarRotacaoDoCarro();

            Vector3 centro = caixa.center + new Vector3(pose.alvo.x * caixa.extents.x,
                                                        pose.alvo.y * caixa.extents.y,
                                                        pose.alvo.z * caixa.extents.z);

            float raio = Mathf.Max(0.05f, caixa.extents.magnitude * Mathf.Max(0.05f, pose.zoom));
            // 1,3 deixava o carro estourando a tela: a caixa do kart é comprida e o fator só
            // afasta o suficiente para a MAIOR dimensão caber justa, sem respiro nenhum.
            float distancia = raio / Mathf.Tan(pose.fov * 0.5f * Mathf.Deg2Rad) * afastamento;

            destinoPos = centro + pose.direcao.normalized * distancia;
            destinoFov = pose.fov;

            // O carro não fica no meio da tela: ele mora no espaço livre à DIREITA do painel de
            // customização (§4). Deslocar o ALVO para a esquerda empurra o carro para a direita —
            // mexer na posição da câmera mudaria o ângulo, e a pose deixaria de mostrar a peça.
            // O carro mora no espaço livre à DIREITA do painel (§4: kart em x=1341) e assentado na
            // plataforma do cenário, não no meio da tela. Deslocar a MIRA é o que move o carro sem
            // mexer no ângulo: mudar a posição da câmera giraria a pose e a peça sairia de quadro.
            Vector3 direita = Vector3.Cross(Vector3.up, (centro - destinoPos).normalized).normalized;
            Vector3 mira = centro
                         - direita * (raio * viesLateral)
                         - Vector3.up * (raio * viesVertical);
            destinoRot = Quaternion.LookRotation((mira - destinoPos).normalized, Vector3.up);

            t = 0f;
            ultimaInteracao = Time.time;

            // Peça em edição fica parada; carro inteiro (modelo, cor) continua girando, porque é
            // girando que se avalia a silhueta.
            if (palcoQueGira == null && palco != null)
                palcoQueGira = palco.GetComponentInParent<PartyRacers.UI.Motion.CarStage>();

            palcoQueGira?.DefinirGiroAutomatico(!pose.congelarGiro);
        }

        private PartyRacers.UI.Motion.CarStage palcoQueGira;

        /// <summary>Caixa dos renderers do carro. Sem carro montado não há o que enquadrar.</summary>
        private bool MedirCarro(out Bounds caixa)
        {
            caixa = default;

            Transform raiz = carro != null ? carro : palco;
            if (raiz == null)
                return false;

            Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return false;

            caixa = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                caixa.Encapsulate(renderers[i].bounds);

            return caixa.extents.magnitude > 0.01f;
        }

        /// <summary>
        /// Devolve o carro ao ângulo de repouso, com uma volta curta em vez de um corte.
        ///
        /// O giro automático do <see cref="PartyRacers.UI.Motion.CarStage"/> continua depois — o
        /// que se zera é a POSE de partida da edição, não o comportamento do palco.
        /// </summary>
        private void ZerarRotacaoDoCarro()
        {
            Transform alvo = palco != null ? palco : carro;
            if (alvo == null)
                return;

            if (retorno != null)
                StopCoroutine(retorno);

            retorno = StartCoroutine(Retornando(alvo));
        }

        private System.Collections.IEnumerator Retornando(Transform alvo)
        {
            Quaternion inicio = alvo.localRotation;
            Quaternion fim = Quaternion.identity;

            for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.05f, duracao))
            {
                alvo.localRotation = Quaternion.Slerp(inicio, fim, EaseInOutCubic(Mathf.Clamp01(k)));
                yield return null;
            }

            alvo.localRotation = fim;
            retorno = null;
        }

        private Coroutine retorno;

        /// <summary>Avisa que o jogador mexeu — a respiração para e reinicia o atraso.</summary>
        public void NotificarInteracao() => ultimaInteracao = Time.time;

        /// <summary>
        /// Liga e desliga o controle da câmera.
        ///
        /// O rig mora na câmera do frontend, que é a MESMA do lobby e da loja. Enquanto ele
        /// continuava respirando fora da garagem, o enquadramento do lobby era sobrescrito frame a
        /// frame e o carro aparecia fora de posição depois de qualquer troca de tela.
        /// </summary>
        public void Ativar(bool ligado)
        {
            ativo = ligado;

            if (!ligado)
            {
                t = 1f;
                relogioIdle = 0f;
                palcoQueGira?.DefinirGiroAutomatico(true);
            }
        }

        private bool ativo = true;

        private void LateUpdate()
        {
            if (camera3D == null || !ativo)
                return;

            if (t < 1f)
            {
                t = Mathf.Clamp01(t + Time.deltaTime / duracao);
                float k = EaseInOutCubic(t);

                camera3D.transform.position = Vector3.Lerp(origemPos, destinoPos, k);
                camera3D.transform.rotation = Quaternion.Slerp(origemRot, destinoRot, k);
                camera3D.fieldOfView = Mathf.Lerp(origemFov, destinoFov, k);

                // Zera o relógio da respiração para ela não começar no meio do ciclo, o que faria
                // a câmera dar um salto lateral no instante em que o blend termina.
                relogioIdle = 0f;
                return;
            }

            AplicarRespiracao();
        }

        private void AplicarRespiracao()
        {
            if (amplitudeIdle <= 0f || Time.time - ultimaInteracao < atrasoDoIdle)
                return;

            relogioIdle += Time.deltaTime;

            float deslocamento = Mathf.Sin(relogioIdle * frequenciaIdle * Mathf.PI * 2f) * amplitudeIdle;
            camera3D.transform.position = destinoPos + camera3D.transform.right * deslocamento;
        }

        private Pose Resolver(string categoria)
        {
            if (poses == null || poses.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                for (int i = 0; i < poses.Count; i++)
                {
                    if (string.Equals(poses[i].categoria, categoria, System.StringComparison.OrdinalIgnoreCase))
                        return poses[i];
                }
            }

            return poses[0];
        }

        private Vector3 ResolverAlvo(string nome)
        {
            if (carro == null)
                return palco.position;

            if (!string.IsNullOrWhiteSpace(nome))
            {
                Transform encontrado = BuscarRecursivo(carro, nome);
                if (encontrado != null)
                    return encontrado.position;
            }

            // Sem o socket nomeado no modelo, olha para o centro dos renderers em vez do pivô: em
            // vários carros do pack o pivô fica no chão, e a câmera apontaria para o asfalto.
            Renderer[] renderers = carro.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return carro.position;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            return b.center;
        }

        private static Transform BuscarRecursivo(Transform raiz, string nome)
        {
            if (string.Equals(raiz.name, nome, System.StringComparison.OrdinalIgnoreCase))
                return raiz;

            for (int i = 0; i < raiz.childCount; i++)
            {
                Transform achado = BuscarRecursivo(raiz.GetChild(i), nome);
                if (achado != null)
                    return achado;
            }

            return null;
        }

        private static float EaseInOutCubic(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
