using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Resposta tátil de um botão: cresce um tico no hover e afunda ao pressionar, com os tempos
    /// do tokens.json (movimento.botaoPressionar: 0,08 s, deslocamento 6). Não monta nada — só
    /// anima a escala e a posição do RectTransform que já existe.
    ///
    /// Os botões com sprite `_Pressed` (verde e âmbar) já afundam pela troca de sprite, como o
    /// README manda; para esses deixe <see cref="afundar"/> desligado para não somar os dois.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                           IPointerDownHandler, IPointerUpHandler, ISelectHandler,
                           IDeselectHandler, ISubmitHandler
    {
        [Header("Tempos (tokens.json → movimento.botaoPressionar)")]
        [SerializeField] private float duracao = 0.08f;

        [Header("Hover")]
        [SerializeField] private float escalaHover = 1.04f;

        [Header("Pressionar")]
        [SerializeField] private float escalaPressionado = 0.96f;
        [Tooltip("Afunda o botão. Desligue quando o sprite _Pressed já faz isso.")]
        [SerializeField] private bool afundar = true;
        [SerializeField] private float deslocamentoY = 6f;

        [Header("Destaque")]
        [SerializeField] private bool destaque;
        [SerializeField, Range(0f, 0.04f)] private float pulsoEmRepouso = 0.012f;
        [SerializeField, Range(0.5f, 5f)] private float velocidadeDoPulso = 2.2f;

        private RectTransform rect;
        private Vector2 posicaoOriginal;
        private Vector3 escalaOriginal;
        private bool temHover, temPressao, temSelecao;
        private float pressionadoAte;
        private bool repousoLido;
        private float progresso;
        private Vector3 escalaAlvo;
        private Vector2 posicaoAlvo;

        private void Awake()
        {
            rect = (RectTransform)transform;
            escalaOriginal = rect.localScale;
            escalaAlvo = escalaOriginal;
            // a posição de repouso é lida no primeiro Update, não aqui: sob um Layout Group o
            // Awake roda antes do layout posicionar o botão, e guardar (0,0) fazia todos os
            // botões da linha migrarem para o mesmo ponto — eles se empilhavam no hover.
        }

        private void OnDisable()
        {
            // volta ao repouso: um botão escondido no meio da animação não pode voltar torto
            if (rect == null) return;
            rect.localScale = escalaOriginal;
            if (afundar && repousoLido) rect.anchoredPosition = posicaoOriginal;
            temHover = temPressao = temSelecao = false;
            pressionadoAte = 0f;
        }

        private void Update()
        {
            if (rect == null)
                return;

            // só quem afunda mexe em posição — e só depois do layout ter posicionado o botão
            if (afundar && !repousoLido)
            {
                posicaoOriginal = rect.anchoredPosition;
                posicaoAlvo = posicaoOriginal;
                repousoLido = true;
            }

            bool interagivel = Interagivel();
            bool pressionando = interagivel && (temPressao || Time.unscaledTime < pressionadoAte);
            bool realcado = interagivel && (temHover || temSelecao);
            float multiplicador = pressionando ? escalaPressionado : realcado ? escalaHover : 1f;
            if (interagivel && destaque && !pressionando && !realcado)
            {
                float onda = Mathf.Sin(Time.unscaledTime * velocidadeDoPulso * Mathf.PI * 2f) * 0.5f + 0.5f;
                multiplicador += onda * pulsoEmRepouso;
            }

            Vector3 escalaDesejada = escalaOriginal * multiplicador;
            Vector2 posicaoDesejada = posicaoOriginal +
                (pressionando && afundar ? new Vector2(0f, -deslocamentoY) : Vector2.zero);

            if (escalaDesejada != escalaAlvo || (afundar && posicaoDesejada != posicaoAlvo))
            {
                escalaAlvo = escalaDesejada;
                posicaoAlvo = posicaoDesejada;
                progresso = 0f;
            }

            if (progresso >= 1f)
                return;

            progresso = duracao <= 0f ? 1f : Mathf.Min(1f, progresso + Time.unscaledDeltaTime / duracao);
            float k = UIEase.OutQuad(progresso);
            rect.localScale = Vector3.Lerp(rect.localScale, escalaAlvo, k);
            if (afundar)
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, posicaoAlvo, k);
        }

        public void OnPointerEnter(PointerEventData _) { if (Interagivel()) temHover = true; }
        public void OnPointerExit(PointerEventData _) { temHover = false; temPressao = false; }
        public void OnPointerDown(PointerEventData _) { if (Interagivel()) temPressao = true; }
        public void OnPointerUp(PointerEventData _) => temPressao = false;
        public void OnSelect(BaseEventData _) => temSelecao = true;
        public void OnDeselect(BaseEventData _) { temSelecao = false; temPressao = false; }

        public void OnSubmit(BaseEventData _)
        {
            if (Interagivel())
                pressionadoAte = Time.unscaledTime + 0.12f;
        }

        public void SetEmphasized(bool value) => destaque = value;

        private bool Interagivel()
        {
            var botao = GetComponentInChildren<Button>();
            return botao == null || botao.interactable;
        }
    }
}
