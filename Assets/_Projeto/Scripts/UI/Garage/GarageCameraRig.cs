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
        [System.Serializable]
        public class Pose
        {
            [Tooltip("Chave da categoria: modelo, cor, rodas, frente, traseira, teto, adesivos.")]
            public string categoria;
            [Tooltip("Posição da câmera em espaço LOCAL do palco.")]
            public Vector3 posicao;
            [Tooltip("Nome do transform do carro para onde olhar (Body, Wheel_FL, Bumper_R...). " +
                     "Não encontrado, olha para o centro do carro.")]
            public string alvo = "Body";
            public float fov = 34f;
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
            new Pose { categoria = "modelo",   posicao = new Vector3( 3.2f, 1.6f,  4.4f), alvo = "Body",       fov = 34f },
            new Pose { categoria = "cor",      posicao = new Vector3( 3.2f, 1.6f,  4.4f), alvo = "Body",       fov = 34f },
            new Pose { categoria = "rodas",    posicao = new Vector3( 2.1f, 0.55f, 2.4f), alvo = "Wheel_FL",   fov = 28f },
            new Pose { categoria = "frente",   posicao = new Vector3( 0f,   0.8f,  3.6f), alvo = "Bumper_F",   fov = 30f },
            new Pose { categoria = "traseira", posicao = new Vector3(-0.4f, 1.2f, -3.8f), alvo = "Bumper_R",   fov = 32f },
            new Pose { categoria = "teto",     posicao = new Vector3( 2.4f, 3.1f,  3.0f), alvo = "RoofSocket", fov = 32f },
            new Pose { categoria = "adesivos", posicao = new Vector3( 4.4f, 1.1f,  0f),   alvo = "Door_L",     fov = 30f },
        };

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

            origemPos = camera3D.transform.position;
            origemRot = camera3D.transform.rotation;
            origemFov = camera3D.fieldOfView;

            destinoPos = palco.TransformPoint(pose.posicao);
            destinoFov = pose.fov;

            Vector3 alvo = ResolverAlvo(pose.alvo);
            destinoRot = Quaternion.LookRotation((alvo - destinoPos).normalized, Vector3.up);

            t = 0f;
            ultimaInteracao = Time.time;
        }

        /// <summary>Avisa que o jogador mexeu — a respiração para e reinicia o atraso.</summary>
        public void NotificarInteracao() => ultimaInteracao = Time.time;

        private void LateUpdate()
        {
            if (camera3D == null)
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
