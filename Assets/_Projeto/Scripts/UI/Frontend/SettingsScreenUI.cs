using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 10. Cinco grupos, três tipos de controle (slider, interruptor, seletor).
    /// Todo slider mostra o número — nada de valor só por cor.
    /// Persistência em PlayerPrefs; nada de layout por código.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsScreenUI : MonoBehaviour
    {
        [System.Serializable]
        public class Grupo
        {
            public string id;
            [Tooltip("Botão da lista da esquerda, com os 2 estados irmãos já montados.")]
            public GameObject botao;
            public GameObject botaoIdle;
            public GameObject botaoAtivo;
            [Tooltip("Painel correspondente à direita.")]
            public GameObject painel;
        }

        [System.Serializable]
        public class Ajuste
        {
            public string chave;
            public Slider slider;
            [Tooltip("TMP obrigatório à direita do slider — todo slider mostra o número.")]
            public TextMeshProUGUI valor;
            public Toggle interruptor;
            public GameObject interruptorOn;
            public GameObject interruptorOff;
            public int padrao = 70;
        }

        [Header("Grupos (5): Áudio, Vídeo, Controles, Jogo, Conta")]
        [SerializeField] private List<Grupo> grupos = new List<Grupo>();

        [Header("Ajustes")]
        [SerializeField] private List<Ajuste> ajustes = new List<Ajuste>();

        [Header("Ações")]
        [SerializeField] private Button btnVoltar;
        [SerializeField] private Button btnRestaurar;
        [SerializeField] private Button btnCancelar;
        [SerializeField] private Button btnAplicar;

        [Header("Navegação")]
        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private string telaAoVoltar = "Lobby";

        private readonly Dictionary<string, float> valoresSalvos = new Dictionary<string, float>();

        private void Awake()
        {
            foreach (Grupo g in grupos)
            {
                if (g?.botao == null)
                    continue;
                Grupo capturado = g;
                Button b = g.botao.GetComponent<Button>();
                if (b != null)
                    b.onClick.AddListener(() => Selecionar(capturado.id));
            }

            foreach (Ajuste a in ajustes)
            {
                if (a == null)
                    continue;
                Ajuste capturado = a;

                if (a.slider != null)
                    a.slider.onValueChanged.AddListener(v => AoMudarSlider(capturado, v));

                if (a.interruptor != null)
                    a.interruptor.onValueChanged.AddListener(v => AoMudarToggle(capturado, v));
            }

            if (btnVoltar != null) btnVoltar.onClick.AddListener(Voltar);
            if (btnCancelar != null) btnCancelar.onClick.AddListener(Cancelar);
            if (btnRestaurar != null) btnRestaurar.onClick.AddListener(RestaurarPadroes);
            if (btnAplicar != null) btnAplicar.onClick.AddListener(Aplicar);
        }

        private void OnEnable()
        {
            Carregar();
            if (grupos.Count > 0)
                Selecionar(grupos[0].id);
        }

        public void Selecionar(string id)
        {
            // Dois passos porque os 4 grupos de jogo dividem o mesmo painel (a grade 2×2 do PLACA)
            // e só CONTA tem painel próprio: apagar tudo primeiro evita um grupo desligar o do outro.
            foreach (Grupo g in grupos)
            {
                if (g == null)
                    continue;
                Ligar(g.painel, false);
                Ligar(g.botaoAtivo, false);
                Ligar(g.botaoIdle, true);
            }

            foreach (Grupo g in grupos)
            {
                if (g == null || g.id != id)
                    continue;
                Ligar(g.painel, true);
                Ligar(g.botaoAtivo, true);
                Ligar(g.botaoIdle, false);
            }
        }

        public void Carregar()
        {
            valoresSalvos.Clear();
            foreach (Ajuste a in ajustes)
            {
                if (a == null || string.IsNullOrEmpty(a.chave))
                    continue;

                float v = PlayerPrefs.GetFloat("cfg." + a.chave, a.padrao);
                valoresSalvos[a.chave] = v;
                Escrever(a, v);
            }
        }

        public void Aplicar()
        {
            foreach (Ajuste a in ajustes)
            {
                if (a == null || string.IsNullOrEmpty(a.chave))
                    continue;
                float v = Ler(a);
                valoresSalvos[a.chave] = v;
                PlayerPrefs.SetFloat("cfg." + a.chave, v);
            }
            PlayerPrefs.Save();
        }

        public void Cancelar()
        {
            foreach (Ajuste a in ajustes)
            {
                if (a == null || string.IsNullOrEmpty(a.chave))
                    continue;
                if (valoresSalvos.TryGetValue(a.chave, out float v))
                    Escrever(a, v);
            }
            Voltar();
        }

        public void RestaurarPadroes()
        {
            foreach (Ajuste a in ajustes)
            {
                if (a != null)
                    Escrever(a, a.padrao);
            }
        }

        public void Voltar()
        {
            if (roteador != null)
                roteador.Ir(telaAoVoltar);
        }

        private void AoMudarSlider(Ajuste a, float v)
        {
            if (a.valor != null)
                a.valor.text = Mathf.RoundToInt(v).ToString();
        }

        private void AoMudarToggle(Ajuste a, bool ligado)
        {
            Ligar(a.interruptorOn, ligado);
            Ligar(a.interruptorOff, !ligado);
        }

        private static float Ler(Ajuste a)
        {
            if (a.slider != null) return a.slider.value;
            if (a.interruptor != null) return a.interruptor.isOn ? 1f : 0f;
            return 0f;
        }

        private void Escrever(Ajuste a, float v)
        {
            if (a.slider != null)
            {
                a.slider.SetValueWithoutNotify(v);
                AoMudarSlider(a, v);
            }

            if (a.interruptor != null)
            {
                bool ligado = v > 0.5f;
                a.interruptor.SetIsOnWithoutNotify(ligado);
                AoMudarToggle(a, ligado);
            }
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
