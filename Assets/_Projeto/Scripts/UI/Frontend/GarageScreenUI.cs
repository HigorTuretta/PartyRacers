using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ithappy;
using PartyRacers.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 04 (Garagem). As linhas de categoria já estão montadas na cena; este
    /// script só descobre quais existem, lê do carro quantas variantes cada uma realmente tem
    /// e escreve nos campos. Nenhum contador é inventado: a fonte de verdade é o rig do carro
    /// (<see cref="KartVisualCustomizer"/>), por isso não aparece opção que o carro não tem.
    ///
    /// Cada linha se chama <c>Cat_&lt;id&gt;</c>, onde id é "Cor" ou o nome de um
    /// <see cref="CarElementName"/>. Assim o designer pode reordenar, remover ou acrescentar
    /// linhas na cena sem tocar em script.
    /// </summary>
    [DisallowMultipleComponent]
    public class GarageScreenUI : MonoBehaviour
    {
        [Header("Fonte de verdade")]
        [SerializeField] private KartVisualCustomizer carro;
        [Tooltip("Palco 3D: dá o arraste para girar e a animação de troca de carro.")]
        [SerializeField] private CarStage palco;

        [Header("Lista de customização (montada na cena)")]
        [SerializeField] private Transform containerCategorias;
        [SerializeField] private TextMeshProUGUI contagemDeCategorias;
        [Tooltip("Rodapé do painel: mostra as categorias que ficam abaixo da área visível.")]
        [SerializeField] private TextMeshProUGUI categoriasRestantes;
        [SerializeField] private int categoriasVisiveis = 6;

        [Header("Seletor de carro")]
        [SerializeField] private TextMeshProUGUI nomeDoCarro;
        [SerializeField] private Button btnCarroAnterior;
        [SerializeField] private Button btnCarroProximo;
        [SerializeField] private Transform containerIndicadores;
        [Tooltip("Cor do indicador do carro atual (âmbar do PLACA).")]
        [SerializeField] private Color pontoAtivo = new Color(1f, 0.690f, 0.125f);
        [SerializeField] private Color pontoInativo = new Color(0.294f, 0.329f, 0.659f);

        [Header("Ações da garagem")]
        [Tooltip("Botão principal: confirma a estilização e devolve o jogador ao lobby.")]
        [FormerlySerializedAs("btnCorrer")]
        [SerializeField] private Button btnSalvarEVoltar;
        [Tooltip("Botão secundário: confirma a estilização sem sair da garagem.")]
        [FormerlySerializedAs("btnJogarLocalmente")]
        [SerializeField] private Button btnSalvarEstilo;

        [Header("Rótulos dos botões")]
        [Tooltip("A garagem não larga corrida — quem inicia a partida é o lobby. Os rótulos são " +
                 "aplicados em Awake para que a cena não fique dizendo 'CORRER'.")]
        [SerializeField] private string rotuloSalvarEVoltar = "SALVAR E VOLTAR";
        [SerializeField] private string rotuloSalvarEstilo = "SALVAR ESTILO";
        [Tooltip("Texto mostrado por alguns segundos no botão secundário ao confirmar.")]
        [SerializeField] private string rotuloSalvo = "ESTILO SALVO";
        [SerializeField, Min(0.2f)] private float segundosDoAvisoSalvo = 1.4f;

        [Header("Eventos")]
        public UnityEngine.Events.UnityEvent<string, int> aoTrocarCosmetico;
        public UnityEngine.Events.UnityEvent<int> aoTrocarCarro;

        [FormerlySerializedAs("aoCorrer")]
        public UnityEngine.Events.UnityEvent aoSalvarEVoltar;

        [FormerlySerializedAs("aoJogarLocalmente")]
        public UnityEngine.Events.UnityEvent aoSalvarEstilo;

        /// <summary>Uma linha da lista, resolvida a partir do nome do objeto na cena.</summary>
        private class Linha
        {
            public GameObject objeto;
            public string id;
            public string rotulo;
            public CarElementName elemento;
            public bool ehCor;
        }

        private readonly List<Linha> linhas = new List<Linha>();
        private string selecionada;
        private Coroutine pulsoDoNome;
        private Coroutine avisoSalvo;

        private void Awake()
        {
            Descobrir();

            foreach (Linha l in linhas)
            {
                Linha capturada = l;
                var prev = l.objeto.transform.Find("Btn_Prev")?.GetComponent<Button>();
                var next = l.objeto.transform.Find("Btn_Next")?.GetComponent<Button>();
                if (prev != null) prev.onClick.AddListener(() => Avancar(capturada, -1));
                if (next != null) next.onClick.AddListener(() => Avancar(capturada, +1));
            }

            if (btnCarroAnterior != null) btnCarroAnterior.onClick.AddListener(() => TrocarCarro(-1));
            if (btnCarroProximo != null) btnCarroProximo.onClick.AddListener(() => TrocarCarro(+1));
            if (btnSalvarEVoltar != null) btnSalvarEVoltar.onClick.AddListener(() => aoSalvarEVoltar?.Invoke());
            if (btnSalvarEstilo != null) btnSalvarEstilo.onClick.AddListener(() => aoSalvarEstilo?.Invoke());

            AplicarRotulo(btnSalvarEVoltar, rotuloSalvarEVoltar);
            AplicarRotulo(btnSalvarEstilo, rotuloSalvarEstilo);

            if (carro != null) carro.CarRebuilt += Redesenhar;
        }

        private void OnDestroy()
        {
            if (carro != null) carro.CarRebuilt -= Redesenhar;
        }

        /// <summary>Feedback visual de que a estilização foi gravada (chamado pelo FrontendFlow).</summary>
        public void ConfirmarSalvamento()
        {
            if (btnSalvarEstilo == null || !isActiveAndEnabled)
                return;

            if (avisoSalvo != null)
                StopCoroutine(avisoSalvo);
            avisoSalvo = StartCoroutine(PiscarAvisoSalvo());
        }

        private IEnumerator PiscarAvisoSalvo()
        {
            AplicarRotulo(btnSalvarEstilo, rotuloSalvo);
            yield return new WaitForSecondsRealtime(segundosDoAvisoSalvo);
            AplicarRotulo(btnSalvarEstilo, rotuloSalvarEstilo);
            avisoSalvo = null;
        }

        private static void AplicarRotulo(Button botao, string texto)
        {
            if (botao == null || string.IsNullOrWhiteSpace(texto))
                return;

            TextMeshProUGUI label = botao.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = texto;
        }

        private void OnEnable() => Redesenhar();

        /// <summary>Lê os objetos Cat_* que existem na cena e resolve o que cada um controla.</summary>
        private void Descobrir()
        {
            linhas.Clear();
            if (containerCategorias == null)
                return;

            foreach (Transform t in containerCategorias)
            {
                if (!t.name.StartsWith("Cat_"))
                    continue;

                string id = t.name.Substring(4);
                var linha = new Linha
                {
                    objeto = t.gameObject,
                    id = id,
                    ehCor = id.Equals("Cor", StringComparison.OrdinalIgnoreCase),
                    rotulo = t.Find("Nome")?.GetComponent<TextMeshProUGUI>()?.text ?? id,
                };

                if (!linha.ehCor && !Enum.TryParse(id, true, out linha.elemento))
                {
                    Debug.LogWarning($"[Garagem] '{t.name}' não corresponde a nenhum CarElementName — linha ignorada.", t);
                    continue;
                }

                linhas.Add(linha);
            }

            // a primeira categoria com variação começa destacada, como no mockup
            selecionada ??= linhas.FirstOrDefault(l => Contagem(l) > 1)?.id;
        }

        // ------------------------------------------------------------------ leitura do carro
        private int Contagem(Linha l)
        {
            if (carro == null) return 0;
            return l.ehCor ? carro.ColorCount : carro.GetElementVariantCount(l.elemento);
        }

        private int Indice(Linha l)
        {
            if (carro == null) return 0;
            return l.ehCor ? carro.ColorIndex : carro.GetElementIndex(l.elemento);
        }

        // ------------------------------------------------------------------ desenho
        /// <summary>Redesenha a lista inteira e o seletor a partir do estado real do carro.</summary>
        public void Redesenhar()
        {
            if (linhas.Count == 0)
                Descobrir();

            foreach (Linha l in linhas)
                RedesenharLinha(l);

            if (contagemDeCategorias != null)
            {
                int n = linhas.Count(l => Contagem(l) > 1);
                contagemDeCategorias.text = n == 1 ? "1 CATEGORIA" : $"{n} CATEGORIAS";
            }

            if (categoriasRestantes != null)
            {
                var abaixo = linhas.Skip(Mathf.Max(0, categoriasVisiveis)).Select(l => l.rotulo).ToArray();
                categoriasRestantes.text = abaixo.Length > 0 ? string.Join(", ", abaixo) : string.Empty;
            }

            RedesenharSeletor();
        }

        private void RedesenharLinha(Linha l)
        {
            int total = Contagem(l);
            bool semVariacao = total <= 1;
            bool sel = !semVariacao && l.id == selecionada;

            Ligar(l.objeto, "State_Locked", semVariacao);
            Ligar(l.objeto, "State_Selected", sel);
            Ligar(l.objeto, "State_Idle", !semVariacao && !sel);
            Ligar(l.objeto, "Btn_Prev", !semVariacao);
            Ligar(l.objeto, "Btn_Next", !semVariacao);
            Ligar(l.objeto, "Valor", !semVariacao && !l.ehCor);
            Ligar(l.objeto, "AmostraCor", !semVariacao && l.ehCor);

            if (semVariacao)
                return;

            int i = Mathf.Clamp(Indice(l), 0, total - 1);

            if (l.ehCor)
            {
                var amostra = l.objeto.transform.Find("AmostraCor")?.GetComponent<Image>();
                var paleta = carro != null ? carro.PaintPalette : null;
                if (amostra != null && paleta != null && paleta.Length > 0)
                    amostra.color = paleta[i % paleta.Length];
            }
            else
            {
                var valor = l.objeto.transform.Find("Valor")?.GetComponent<TextMeshProUGUI>();
                if (valor != null) valor.text = $"{i + 1}/{total}";
            }
        }

        private void RedesenharSeletor()
        {
            int total = carro != null ? carro.CarCount : 0;
            int atual = carro != null ? carro.CarIndex : 0;

            if (nomeDoCarro != null)
                nomeDoCarro.text = total > 0 ? $"CARRO {(atual + 1):00}" : "SEM CARRO";

            if (containerIndicadores == null)
                return;

            int k = 0;
            foreach (Transform ponto in containerIndicadores)
            {
                bool existe = k < total;
                if (ponto.gameObject.activeSelf != existe)
                    ponto.gameObject.SetActive(existe);

                var img = ponto.GetComponent<Image>();
                if (existe && img != null)
                    img.color = k == atual ? pontoAtivo : pontoInativo;
                k++;
            }
        }

        // ------------------------------------------------------------------ interação
        private void Avancar(Linha l, int passo)
        {
            int total = Contagem(l);
            if (carro == null || total <= 1)
                return;

            int i = ((Indice(l) + passo) % total + total) % total;

            if (l.ehCor) carro.SetColor(i);
            else carro.SetElement(l.elemento, i);

            selecionada = l.id;
            Redesenhar();
            aoTrocarCosmetico?.Invoke(l.id, i);
        }

        private void TrocarCarro(int passo)
        {
            if (carro == null || carro.CarCount == 0)
                return;

            int total = carro.CarCount;
            int destino = ((carro.CarIndex + passo) % total + total) % total;

            // o palco anima a saída, troca no ponto mais apagado e devolve o carro novo entrando.
            // A troca e o aviso acontecem juntos, no instante em que o carro está escondido:
            // assim a câmera já reenquadra antes de o carro novo aparecer, sem salto.
            void Aplicar()
            {
                carro.SetCar(destino);           // dispara CarRebuilt -> Redesenhar
                aoTrocarCarro?.Invoke(destino);
            }

            if (palco != null) palco.Trocar(Aplicar, passo);
            else Aplicar();

            PulsarNome();
        }

        /// <summary>Estica e volta o nome do carro, para a troca não parecer um corte seco.</summary>
        private void PulsarNome()
        {
            if (nomeDoCarro == null)
                return;
            if (pulsoDoNome != null)
                StopCoroutine(pulsoDoNome);
            pulsoDoNome = StartCoroutine(Pulsando(nomeDoCarro.rectTransform, 0.22f, 0.14f));
        }

        private IEnumerator Pulsando(RectTransform alvo, float duracao, float forca)
        {
            for (float t = 0f; t < duracao; t += Time.unscaledDeltaTime)
            {
                float k = UIEase.PingPong(t / duracao);
                alvo.localScale = Vector3.one * (1f + forca * UIEase.OutQuad(k));
                yield return null;
            }
            alvo.localScale = Vector3.one;
            pulsoDoNome = null;
        }

        private static void Ligar(GameObject raiz, string caminho, bool ativo)
        {
            Transform t = raiz.transform.Find(caminho);
            if (t != null && t.gameObject.activeSelf != ativo)
                t.gameObject.SetActive(ativo);
        }
    }
}
