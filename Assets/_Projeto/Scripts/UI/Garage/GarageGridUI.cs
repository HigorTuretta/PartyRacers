using System.Collections.Generic;
using ithappy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Garage
{
    /// <summary>
    /// Binder da GARAGEM v2: abas de categoria + grade visual de cards, no lugar das setinhas
    /// `◄ 11/15 ►`.
    ///
    /// Por que a grade: com setas, ver o catálogo inteiro custa um clique por item e exige guardar
    /// na memória o que já passou. Não há visão de conjunto, não dá para pular e não dá para
    /// comparar. A grade resolve os três de uma vez.
    ///
    /// Os cards já estão montados na cena (um pool fixo) e a lista pagina quando o catálogo é maior
    /// — nada é instanciado por frame. Cada card tem os quatro estados como filhos irmãos já
    /// estilizados; o binder só faz SetActive.
    ///
    /// SELECIONADO e EQUIPADO são coisas diferentes e ficam visivelmente diferentes: selecionado é
    /// onde o foco está agora, equipado é o que o carro está usando. Confundir os dois é o erro
    /// mais comum desse tipo de tela.
    /// </summary>
    [DisallowMultipleComponent]
    public class GarageGridUI : MonoBehaviour
    {
        /// <summary>Uma aba de categoria, já montada com seus dois estados.</summary>
        [System.Serializable]
        public class Aba
        {
            [Tooltip("Chave da pose de câmera: modelo, cor, rodas, frente, traseira, teto, adesivos.")]
            public string categoria;
            public Button botao;
            public GameObject estadoAtivo;
            public GameObject estadoOcioso;

            [Header("Fonte dos itens")]
            [Tooltip("Modelos de carro e paleta de cores vêm do customizador; as demais categorias " +
                     "vêm dos elementos do carro atual.")]
            public Fonte fonte = Fonte.ElementoDoCarro;
            [Tooltip("Elemento do carro, quando a fonte for ElementoDoCarro.")]
            public CarElementName elemento;
        }

        public enum Fonte
        {
            ModeloDeCarro,
            Cor,
            ElementoDoCarro,
        }

        /// <summary>Um card da grade, já montado na cena com os quatro estados.</summary>
        [System.Serializable]
        public class Card
        {
            public GameObject raiz;
            public Button botao;

            [Header("Estados (filhos mutuamente exclusivos)")]
            public GameObject equipado;
            public GameObject selecionado;
            public GameObject livre;
            public GameObject bloqueado;

            [Header("Peças")]
            public TextMeshProUGUI nome;
            public Image preview;
            public GameObject seloDeNovo;
            public GameObject anelDeFoco;
        }

        [Header("Carro e câmera")]
        [SerializeField] private KartVisualCustomizer carro;
        [SerializeField] private GarageCameraRig camera3D;

        [Header("Abas")]
        [SerializeField] private List<Aba> abas = new List<Aba>();

        [Header("Grade (cards já montados na cena)")]
        [SerializeField] private List<Card> cards = new List<Card>();
        [SerializeField] private TextMeshProUGUI contagemDaCategoria;

        [Header("Paginação")]
        [SerializeField] private Button btnPaginaAnterior;
        [SerializeField] private Button btnProximaPagina;
        [SerializeField] private TextMeshProUGUI indicadorDePagina;
        [SerializeField] private GameObject blocoDePaginacao;

        [Header("Confirmação")]
        [SerializeField] private Button btnEquipar;
        [SerializeField] private Button btnDesfazer;
        [SerializeField] private TextMeshProUGUI textoDeDesbloqueio;

        private int abaAtual;
        private int pagina;
        private int selecionado = -1;
        private int equipado = -1;

        // Estado no momento em que a categoria foi aberta. DESFAZER volta para cá — sem isso,
        // "desfazer" só poderia significar "voltar ao padrão", que não é o que o jogador espera.
        private int equipadoAoAbrir = -1;

        private void Awake()
        {
            for (int i = 0; i < abas.Count; i++)
            {
                if (abas[i]?.botao == null)
                    continue;

                int indice = i;
                abas[i].botao.onClick.AddListener(() => AbrirAba(indice));
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i]?.botao == null)
                    continue;

                int indice = i;
                cards[i].botao.onClick.AddListener(() => SelecionarCard(indice));
            }

            if (btnEquipar != null)
                btnEquipar.onClick.AddListener(Equipar);

            if (btnDesfazer != null)
                btnDesfazer.onClick.AddListener(Desfazer);

            if (btnProximaPagina != null)
                btnProximaPagina.onClick.AddListener(() => TrocarPagina(1));

            if (btnPaginaAnterior != null)
                btnPaginaAnterior.onClick.AddListener(() => TrocarPagina(-1));
        }

        private void OnEnable() => AbrirAba(abaAtual);

        // ---------------------------------------------------------------- Abas

        public void AbrirAba(int indice)
        {
            if (abas.Count == 0)
                return;

            abaAtual = Mathf.Clamp(indice, 0, abas.Count - 1);
            pagina = 0;

            for (int i = 0; i < abas.Count; i++)
            {
                bool ativa = i == abaAtual;
                Ligar(abas[i]?.estadoAtivo, ativa);
                Ligar(abas[i]?.estadoOcioso, !ativa);
            }

            equipado = IndiceEquipado();
            equipadoAoAbrir = equipado;
            selecionado = equipado;

            // A câmera acompanha a categoria: é o que transforma "editar rodas" em "ver as rodas".
            camera3D?.IrPara(abas[abaAtual].categoria);

            Redesenhar();
        }

        // ---------------------------------------------------------------- Grade

        private void Redesenhar()
        {
            int total = TotalDeItens();
            int porPagina = Mathf.Max(1, cards.Count);
            int paginas = Mathf.Max(1, Mathf.CeilToInt(total / (float)porPagina));

            pagina = Mathf.Clamp(pagina, 0, paginas - 1);
            int primeiro = pagina * porPagina;

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                if (card == null || card.raiz == null)
                    continue;

                int item = primeiro + i;
                bool existe = item < total;

                if (card.raiz.activeSelf != existe)
                    card.raiz.SetActive(existe);

                if (!existe)
                    continue;

                if (card.nome != null)
                    card.nome.text = NomeDoItem(item);

                bool estaEquipado = item == equipado;
                bool estaSelecionado = item == selecionado && !estaEquipado;

                // Tudo desbloqueado por enquanto: não existe economia de cosméticos no projeto.
                // O estado Locked fica montado e o binder já sabe ligá-lo quando existir.
                Ligar(card.equipado, estaEquipado);
                Ligar(card.selecionado, estaSelecionado);
                Ligar(card.livre, !estaEquipado && !estaSelecionado);
                Ligar(card.bloqueado, false);
                Ligar(card.anelDeFoco, item == selecionado);
                Ligar(card.seloDeNovo, false);
            }

            if (contagemDaCategoria != null)
                contagemDaCategoria.text = total == 1 ? "1 ITEM" : $"{total} ITENS";

            bool paginado = paginas > 1;
            Ligar(blocoDePaginacao, paginado);

            if (indicadorDePagina != null)
                indicadorDePagina.text = $"{pagina + 1}/{paginas}";

            if (btnPaginaAnterior != null)
                btnPaginaAnterior.interactable = pagina > 0;

            if (btnProximaPagina != null)
                btnProximaPagina.interactable = pagina < paginas - 1;

            if (btnEquipar != null)
                btnEquipar.interactable = selecionado >= 0 && selecionado != equipado;

            if (btnDesfazer != null)
                btnDesfazer.interactable = equipado != equipadoAoAbrir;

            if (textoDeDesbloqueio != null)
                textoDeDesbloqueio.text = string.Empty;
        }

        private void TrocarPagina(int passo)
        {
            pagina += passo;
            camera3D?.NotificarInteracao();
            Redesenhar();
        }

        private void SelecionarCard(int indiceNaGrade)
        {
            int porPagina = Mathf.Max(1, cards.Count);
            int item = pagina * porPagina + indiceNaGrade;

            if (item >= TotalDeItens())
                return;

            selecionado = item;
            camera3D?.NotificarInteracao();

            // Pré-visualiza no carro na hora. Escolher cosmético sem ver o resultado obriga a
            // equipar para descobrir, e aí não há como comparar duas opções.
            Aplicar(item);
            Redesenhar();
        }

        private void Equipar()
        {
            if (selecionado < 0)
                return;

            equipado = selecionado;
            Aplicar(equipado);
            KartGarageSelection.Save();
            Redesenhar();
        }

        private void Desfazer()
        {
            equipado = equipadoAoAbrir;
            selecionado = equipadoAoAbrir;
            Aplicar(equipado);
            Redesenhar();
        }

        // ---------------------------------------------------------------- Fonte dos itens

        private Aba AbaAtual => abas.Count > 0 ? abas[Mathf.Clamp(abaAtual, 0, abas.Count - 1)] : null;

        private int TotalDeItens()
        {
            if (carro == null || AbaAtual == null)
                return 0;

            return AbaAtual.fonte switch
            {
                Fonte.ModeloDeCarro => carro.CarCount,
                Fonte.Cor => carro.ColorCount,
                _ => carro.GetElementVariantCount(AbaAtual.elemento),
            };
        }

        private int IndiceEquipado()
        {
            if (carro == null || AbaAtual == null)
                return -1;

            return AbaAtual.fonte switch
            {
                Fonte.ModeloDeCarro => carro.CarIndex,
                Fonte.Cor => carro.ColorIndex,
                _ => carro.GetElementIndex(AbaAtual.elemento),
            };
        }

        private void Aplicar(int item)
        {
            if (carro == null || AbaAtual == null || item < 0)
                return;

            switch (AbaAtual.fonte)
            {
                case Fonte.ModeloDeCarro:
                    carro.SetCar(item);
                    break;
                case Fonte.Cor:
                    carro.SetColor(item);
                    break;
                default:
                    carro.SetElement(AbaAtual.elemento, item);
                    break;
            }
        }

        private string NomeDoItem(int item)
        {
            if (AbaAtual == null)
                return $"ITEM {item + 1}";

            return AbaAtual.fonte switch
            {
                Fonte.ModeloDeCarro => $"KART {item + 1:00}",
                Fonte.Cor => $"COR {item + 1:00}",
                _ => $"{AbaAtual.categoria.ToUpperInvariant()} {item + 1:00}",
            };
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
