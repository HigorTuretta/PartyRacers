using System.Collections.Generic;
using PartyRacers.UI.Frontend.Party;
using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Binder do lobby publico. A fonte da verdade continua sendo PartyState;
    /// este componente apenas escreve texto, escolhe estados irmaos e encaminha cliques.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] StagePresenter stage;
        [SerializeField] FrontendRouter router;
        [SerializeField] PartyController partyController;
        [SerializeField] VisualTreeAsset friendTemplate;

        VisualElement root;
        Label groupCount;
        Label groupStatus;
        FriendSource friendSource = FriendSource.InGame;
        bool subscribed;

        static readonly PartyMode[] Modes = { PartyMode.Solo, PartyMode.Duo, PartyMode.Squad };
        static readonly string[] ModeNames = { "Solo", "Duo", "Squad" };
        static readonly string[] ModeLabels = { "SOLO", "DUO", "SQUAD" };

        public void Bind(VisualElement value)
        {
            Unbind();
            root = value;
            if (partyController == null)
                partyController = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);

            groupCount = root.Q<Label>("Group_Count");
            groupStatus = root.Q<Label>("Group_Status");

            root.Q<Button>("Btn_Search_Face").clicked += Search;
            root.Q<Button>("Btn_Ready_Face").clicked += ToggleReady;
            root.Q<Button>("Btn_Cancel_Face").clicked += ToggleReady;
            root.Q<Button>("Friend_Tab_Game").clicked += () => SelectFriends(FriendSource.InGame);
            root.Q<Button>("Friend_Tab_Steam").clicked += () => SelectFriends(FriendSource.Steam);

            for (int i = 0; i < Modes.Length; i++)
            {
                PartyMode mode = Modes[i];
                var card = root.Q<VisualElement>("Card_Mode_" + ModeNames[i]);
                card.Q<Button>("State_On_Btn").clicked += () => SetMode(mode);
                card.Q<Button>("State_Off_Btn").clicked += () => SetMode(mode);

                string pips = new string('\u25CF', (int)mode);
                card.Q<Label>("On_Pips").text = pips;
                card.Q<Label>("Off_Pips").text = pips;
                card.Q<Label>("On_Label").text = ModeLabels[i];
                card.Q<Label>("Off_Label").text = ModeLabels[i];
                card.Q<Label>("On_Cap").text = "ate " + (int)mode;
                card.Q<Label>("Off_Cap").text = "ate " + (int)mode;
            }

            if (partyController?.Party != null)
            {
                partyController.Party.Changed += Refresh;
                subscribed = true;
            }

            Refresh();
        }

        public void Unbind()
        {
            if (subscribed && partyController?.Party != null)
                partyController.Party.Changed -= Refresh;

            subscribed = false;
            root = null;
        }

        void Search()
        {
            PartyState party = partyController?.Party;
            if (party == null || !party.CanSearch || !party.LocalIsLeader)
                return;

            partyController.IniciarBusca();
            if (partyController.Matchmaking != null && partyController.Matchmaking.Running)
                router.Go(ScreenId.Matchmaking);
        }

        void ToggleReady()
        {
            partyController?.Party?.ToggleLocalReady();
        }

        void SetMode(PartyMode mode)
        {
            partyController?.EscolherModo(mode);
        }

        void SelectFriends(FriendSource source)
        {
            friendSource = source;
            RefreshFriendTabs();
            RefreshFriends();
        }

        void Refresh()
        {
            if (root == null || partyController?.Party == null)
                return;

            PartyState party = partyController.Party;
            RefreshModes(party);
            RefreshGroup(party);
            RefreshActions(party);
            RefreshFriendTabs();
            RefreshFriends();
        }

        void RefreshModes(PartyState party)
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                var card = root.Q<VisualElement>("Card_Mode_" + ModeNames[i]);
                UiStates.ShowOnly(card, party.Mode == Modes[i] ? "State_On" : "State_Off",
                    "State_On", "State_Off");
            }
        }

        void RefreshGroup(PartyState party)
        {
            IReadOnlyList<PartyMember> members = party.Members;
            groupCount.text = $"{party.FilledSlots}/{party.Capacity}";

            for (int i = 0; i < 4; i++)
            {
                var slot = root.Q<VisualElement>("Group_Slot_" + i);
                if (i < members.Count)
                {
                    PartyMember member = members[i];
                    UiStates.ShowOnly(slot, "State_Player", "State_Player", "State_Empty", "State_Locked");
                    slot.Q<Label>("Name").text = member.IsLocal
                        ? member.DisplayName.ToUpperInvariant() + " (VOCE)"
                        : member.DisplayName.ToUpperInvariant();
                    slot.Q<Label>("Meta").text = member.IsLocal
                        ? $"nivel {member.Level} \u00B7 local"
                        : $"nivel {member.Level} \u00B7 {member.PingMs}ms";
                    UiStates.Show(slot.Q<Label>("Badge_Leader"), member.IsLeader);
                    UiStates.Show(slot.Q<Label>("Badge_Ready"), member.State == MemberState.Ready);
                    UiStates.Show(slot.Q<Label>("Badge_Waiting"), member.State != MemberState.Ready);
                    UiStates.SetAvatarTint(slot.Q<VisualElement>("Avatar"), member.DisplayName);
                }
                else if (i < party.Capacity)
                {
                    UiStates.ShowOnly(slot, "State_Empty", "State_Player", "State_Empty", "State_Locked");
                }
                else
                {
                    UiStates.ShowOnly(slot, "State_Locked", "State_Player", "State_Empty", "State_Locked");
                    slot.Q<Label>("Lock_Text").text = "MODO " + party.Mode.ToString().ToUpperInvariant();
                }
            }
        }

        void RefreshActions(PartyState party)
        {
            PartyMember local = party.Local;
            bool ready = local != null && local.State == MemberState.Ready;
            bool canSearch = party.CanSearch && party.LocalIsLeader;

            UiStates.Show(root.Q<VisualElement>("Btn_Ready"), !ready);
            UiStates.Show(root.Q<VisualElement>("Btn_Cancel"), ready);
            UiStates.Show(root.Q<VisualElement>("Btn_Search"), canSearch);
            UiStates.Show(root.Q<VisualElement>("Btn_Search_Blocked"), !canSearch);

            if (canSearch)
            {
                groupStatus.text = "TODOS PRONTOS \u00B7 PODE BUSCAR";
                UiStates.SetVariant(groupStatus, "lobby__group-status--ready",
                    "lobby__group-status--ready", "lobby__group-status--waiting");
            }
            else
            {
                int waiting = party.WaitingCount;
                groupStatus.text = waiting == 1
                    ? "AGUARDANDO 1 JOGADOR"
                    : $"AGUARDANDO {waiting} JOGADORES";
                UiStates.SetVariant(groupStatus, "lobby__group-status--waiting",
                    "lobby__group-status--ready", "lobby__group-status--waiting");

                string reason = !party.LocalIsLeader ? "SO O LIDER PODE BUSCAR" : party.SearchBlockReason;
                root.Q<Label>("Block_Reason").text = string.IsNullOrEmpty(reason)
                    ? "AGUARDANDO O GRUPO"
                    : reason;
            }
        }

        void RefreshFriendTabs()
        {
            if (root == null) return;
            SetFriendTab(root.Q<Button>("Friend_Tab_Game"), friendSource == FriendSource.InGame);
            SetFriendTab(root.Q<Button>("Friend_Tab_Steam"), friendSource == FriendSource.Steam);
        }

        static void SetFriendTab(Button tab, bool on)
        {
            UiStates.SetVariant(tab, on ? "lobby__friend-tab--on" : "lobby__friend-tab--off",
                "lobby__friend-tab--on", "lobby__friend-tab--off");
        }

        void RefreshFriends()
        {
            if (root == null || partyController == null || friendTemplate == null)
                return;

            IReadOnlyList<FriendEntry> friends = partyController.AmigosDe(friendSource);
            ScrollView scroll = root.Q<ScrollView>("Friend_List");
            List<TemplateContainer> rows = UiStates.Fill(scroll.contentContainer, friendTemplate, friends.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                FriendEntry friend = friends[i];
                TemplateContainer row = rows[i];
                row.Q<Label>("Name").text = friend.DisplayName.ToUpperInvariant();
                Label state = row.Q<Label>("State");
                state.text = PresenceText(friend.Presence);
                UiStates.SetVariant(state, PresenceClass(friend.Presence),
                    "friend__state--online", "friend__state--away",
                    "friend__state--steam", "friend__state--offline");
                UiStates.SetAvatarTint(row.Q<VisualElement>("Avatar"), friend.DisplayName);

                VisualElement invite = row.Q<VisualElement>("Btn_Invite");
                Label cant = row.Q<Label>("Badge_Cant");
                UiStates.Show(invite, friend.CanInvite && partyController.Party.FreeSlots > 0);
                UiStates.Show(cant, !friend.CanInvite || partyController.Party.FreeSlots == 0);
                cant.text = friend.Presence == FriendPresence.InThisParty ? "NO GRUPO" : "INDISPONIVEL";
                row.Q<Button>("Btn_Invite_Face").clicked += () =>
                {
                    partyController.Convidar(friend);
                    Refresh();
                };
            }
        }

        static string PresenceText(FriendPresence presence) => presence switch
        {
            FriendPresence.InGarage => "na garagem",
            FriendPresence.InLobby => "no lobby",
            FriendPresence.InRace => "em corrida",
            FriendPresence.InThisParty => "no seu grupo",
            FriendPresence.Offline => "offline",
            _ => "online",
        };

        string PresenceClass(FriendPresence presence)
        {
            if (presence == FriendPresence.Offline) return "friend__state--offline";
            if (friendSource == FriendSource.Steam) return "friend__state--steam";
            if (presence == FriendPresence.InGarage || presence == FriendPresence.InLobby)
                return "friend__state--away";
            return "friend__state--online";
        }
    }
}
