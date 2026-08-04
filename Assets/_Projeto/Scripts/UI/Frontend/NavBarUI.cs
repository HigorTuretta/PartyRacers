using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// A barra de topo: LOBBY · GARAGEM · LOJA · PASSE.
    ///
    /// Ela existe uma vez por tela (cada Screen_* tem a sua cópia montada) e todas apontam para o
    /// mesmo <see cref="ScreenRouter"/>. É o que permite trocar de destino de qualquer lugar com um
    /// movimento só — a regra estrutural nº 1 da proposta.
    ///
    /// Como todo binder deste projeto: o estado de cada aba são dois GameObjects irmãos já
    /// estilizados; aqui só se faz <c>SetActive</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavBarUI : MonoBehaviour
    {
        [System.Serializable]
        public class Aba
        {
            [Tooltip("Id registrado no ScreenRouter.")]
            public string id;
            public Button botao;
            public GameObject estadoAtivo;
            public GameObject estadoOcioso;
        }

        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private List<Aba> abas = new List<Aba>();

        [Tooltip("Id desta tela. A aba correspondente nasce ativa.")]
        [SerializeField] private string idDestaTela = "Lobby";

        private void Awake()
        {
            foreach (Aba aba in abas)
            {
                if (aba?.botao == null)
                    continue;

                string destino = aba.id;
                aba.botao.onClick.AddListener(() => Ir(destino));
            }
        }

        private void OnEnable() => Realcar(roteador != null && !string.IsNullOrEmpty(roteador.TelaAtual)
                                           ? roteador.TelaAtual : idDestaTela);

        private void Ir(string id)
        {
            if (roteador == null)
                return;

            roteador.Ir(id);
            Realcar(id);
        }

        private void Realcar(string id)
        {
            foreach (Aba aba in abas)
            {
                if (aba == null)
                    continue;

                bool ativa = aba.id == id;

                if (aba.estadoAtivo != null && aba.estadoAtivo.activeSelf != ativa)
                    aba.estadoAtivo.SetActive(ativa);

                if (aba.estadoOcioso != null && aba.estadoOcioso.activeSelf == ativa)
                    aba.estadoOcioso.SetActive(!ativa);
            }
        }

        /// <summary>Usado pelo instalador da cena, que só conhece o roteador depois de abrir a cena.</summary>
        public void DefinirRoteador(ScreenRouter r) => roteador = r;
    }
}
