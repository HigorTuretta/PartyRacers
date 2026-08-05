using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Uma linha da lista de amigos (`Items/Row_Friend`). Único item da UI que é instanciado em
    /// runtime — a lista de amigos é a única cujo tamanho não se conhece de antemão.
    ///
    /// Amigo que já está no grupo mostra "NO GRUPO" no lugar do botão de convidar. Deixar o botão
    /// ali, mesmo desabilitado, convida a clicar de novo.
    /// </summary>
    [DisallowMultipleComponent]
    public class FriendRowUI : MonoBehaviour
    {
        [Header("Peças já montadas no prefab")]
        [SerializeField] private TextMeshProUGUI nome;
        [SerializeField] private TextMeshProUGUI estado;
        [SerializeField] private Button btnConvidar;
        [SerializeField] private GameObject rotuloIndisponivel;
        [SerializeField] private TextMeshProUGUI textoIndisponivel;

        [Tooltip("Quadrado de identidade. A cor vem do nome, para o mesmo amigo ter sempre a mesma.")]
        [SerializeField] private Graphic avatar;

        [Header("Pontos de presença (filhos de estado)")]
        [SerializeField] private GameObject pontoOnline;
        [SerializeField] private GameObject pontoEmJogo;
        [SerializeField] private GameObject pontoOffline;

        private FriendEntry amigo;
        private PartyController controlador;

        public void Bind(FriendEntry entrada, PartyController dono)
        {
            amigo = entrada;
            controlador = dono;

            if (amigo == null)
                return;

            if (nome != null)
                nome.text = amigo.DisplayName;

            if (estado != null)
                estado.text = TextoDePresenca(amigo.Presence);

            // Sem isto a lista inteira sai da cor com que o prefab foi salvo, e as sete linhas
            // viram sete retângulos iguais — o avatar deixa de informar qualquer coisa.
            if (avatar != null)
                avatar.color = PlayerTint.De(amigo.DisplayName);

            bool online = amigo.Presence != FriendPresence.Offline;
            bool emCorrida = amigo.Presence == FriendPresence.InRace;

            Ligar(pontoOnline, online && !emCorrida);
            Ligar(pontoEmJogo, emCorrida);
            Ligar(pontoOffline, !online);

            bool podeConvidar = amigo.CanInvite;
            Ligar(btnConvidar != null ? btnConvidar.gameObject : null, podeConvidar);
            Ligar(rotuloIndisponivel, !podeConvidar);

            if (!podeConvidar && textoIndisponivel != null)
            {
                textoIndisponivel.text = amigo.Presence switch
                {
                    FriendPresence.InThisParty => "NO GRUPO",
                    FriendPresence.InRace => "EM JOGO",
                    _ => "OFFLINE",
                };
            }

            if (btnConvidar == null)
                return;

            btnConvidar.onClick.RemoveAllListeners();
            btnConvidar.onClick.AddListener(Convidar);
        }

        private void Convidar()
        {
            if (controlador == null || amigo == null)
                return;

            if (controlador.Convidar(amigo))
                Bind(amigo, controlador);
        }

        private static string TextoDePresenca(FriendPresence presenca) => presenca switch
        {
            FriendPresence.InThisParty => "no seu grupo",
            FriendPresence.InGarage => "na garagem",
            FriendPresence.InLobby => "no lobby",
            FriendPresence.InRace => "em corrida",
            FriendPresence.Online => "online",
            _ => "offline",
        };

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
