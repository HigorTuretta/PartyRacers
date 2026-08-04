using System.Collections.Generic;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>Modo do grupo. Define quantas vagas a tela de lobby mostra.</summary>
    public enum PartyMode
    {
        Solo = 1,
        Duo = 2,
        Squad = 4,
    }

    /// <summary>Estado de um integrante do grupo, lido pela linha do lobby.</summary>
    public enum MemberState
    {
        /// <summary>Entrou e confirmou. Só com todos assim a busca libera.</summary>
        Ready,
        /// <summary>Entrou mas ainda não confirmou.</summary>
        Waiting,
        /// <summary>Convite enviado, resposta pendente.</summary>
        Invited,
    }

    /// <summary>De onde veio o amigo — muda o rótulo e a ação disponível na lista.</summary>
    public enum FriendSource
    {
        InGame,
        Steam,
    }

    public enum FriendPresence
    {
        Offline,
        Online,
        InGarage,
        InLobby,
        InRace,
        InThisParty,
    }

    /// <summary>Um integrante do grupo.</summary>
    public class PartyMember
    {
        public string Id;
        public string DisplayName;
        public int Level;
        public int PingMs;
        public bool IsLeader;
        public bool IsLocal;
        public MemberState State = MemberState.Waiting;
    }

    /// <summary>Uma entrada da lista de amigos (aba NO JOGO ou aba STEAM).</summary>
    public class FriendEntry
    {
        public string Id;
        public string DisplayName;
        public FriendSource Source;
        public FriendPresence Presence = FriendPresence.Offline;

        /// <summary>Convidável = está online, não está em corrida e não é do grupo.</summary>
        public bool CanInvite =>
            Presence != FriendPresence.Offline &&
            Presence != FriendPresence.InRace &&
            Presence != FriendPresence.InThisParty;
    }

    /// <summary>
    /// O grupo do jogador. É o estado que o lobby público desenha e a única fonte da verdade sobre
    /// quem está pronto — a tela nunca guarda estado próprio.
    ///
    /// Vive fora da cena (não é MonoBehaviour) para sobreviver à troca Frontend → pista e voltar.
    /// Quando o NGO entrar, este é o objeto que a camada de rede passa a alimentar; a tela não
    /// muda nada por causa disso.
    /// </summary>
    public class PartyState
    {
        public PartyMode Mode { get; private set; } = PartyMode.Solo;

        private readonly List<PartyMember> members = new List<PartyMember>();
        public IReadOnlyList<PartyMember> Members => members;

        /// <summary>Vagas do modo atual. SOLO 1, DUO 2, SQUAD 4.</summary>
        public int Capacity => (int)Mode;

        public int FilledSlots => members.Count;
        public int FreeSlots => UnityEngine.Mathf.Max(0, Capacity - members.Count);

        public PartyMember Local
        {
            get
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].IsLocal)
                        return members[i];
                }

                return null;
            }
        }

        public PartyMember Leader
        {
            get
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].IsLeader)
                        return members[i];
                }

                return members.Count > 0 ? members[0] : null;
            }
        }

        /// <summary>O jogador local comanda o grupo? Só o líder inicia a busca.</summary>
        public bool LocalIsLeader => Local != null && Local.IsLeader;

        /// <summary>Quantos integrantes ainda não confirmaram.</summary>
        public int WaitingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].State != MemberState.Ready)
                        n++;
                }

                return n;
            }
        }

        /// <summary>
        /// A busca só libera com TODO MUNDO pronto. Um grupo com vaga vazia pode buscar: quem
        /// escolheu DUO e está sozinho não fica preso — o matchmaking completa a sala.
        /// </summary>
        public bool CanSearch => members.Count > 0 && WaitingCount == 0;

        /// <summary>Frase curta do motivo do bloqueio, para o botão desabilitado explicar-se.</summary>
        public string SearchBlockReason
        {
            get
            {
                if (members.Count == 0)
                    return "GRUPO VAZIO";

                int esperando = WaitingCount;
                if (esperando == 1)
                    return "FALTA 1 CONFIRMAR";
                if (esperando > 1)
                    return $"FALTAM {esperando} CONFIRMAR";

                return string.Empty;
            }
        }

        // ---------------------------------------------------------------- Mutação

        public event System.Action Changed;

        public void SetMode(PartyMode mode)
        {
            if (Mode == mode)
                return;

            Mode = mode;

            // Encolher o grupo remove os últimos que entraram, nunca o líder nem o jogador local:
            // trocar SQUAD → SOLO não pode expulsar quem está com o controle na mão.
            while (members.Count > Capacity)
            {
                int alvo = -1;
                for (int i = members.Count - 1; i >= 0; i--)
                {
                    if (!members[i].IsLocal && !members[i].IsLeader)
                    {
                        alvo = i;
                        break;
                    }
                }

                if (alvo < 0)
                    break;

                members.RemoveAt(alvo);
            }

            Changed?.Invoke();
        }

        public PartyMember EnsureLocal(string displayName, int level)
        {
            PartyMember local = Local;
            if (local != null)
                return local;

            local = new PartyMember
            {
                Id = "local",
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "VOCÊ" : displayName,
                Level = level,
                PingMs = 0,
                IsLeader = true,
                IsLocal = true,
                State = MemberState.Ready,
            };

            members.Insert(0, local);
            Changed?.Invoke();
            return local;
        }

        public bool TryAdd(PartyMember member)
        {
            if (member == null || members.Count >= Capacity)
                return false;

            members.Add(member);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string id)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Id != id || members[i].IsLocal)
                    continue;

                members.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Alterna o pronto do jogador local (botão PRONTO da barra de ação).</summary>
        public void ToggleLocalReady()
        {
            PartyMember local = Local;
            if (local == null)
                return;

            local.State = local.State == MemberState.Ready ? MemberState.Waiting : MemberState.Ready;
            Changed?.Invoke();
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
