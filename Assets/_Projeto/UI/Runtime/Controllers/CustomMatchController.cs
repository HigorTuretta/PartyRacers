using System;
using System.Collections.Generic;
using PartyRacers.Networking;
using PartyRacers.Race;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Sala privada com 16 linhas fixas. Usa o registro de rede quando ha Relay
    /// e conserva uma sala local honesta quando o online ainda nao foi criado.
    /// </summary>
    public sealed class CustomMatchController : MonoBehaviour
    {
        const int SlotCount = 16;
        const string RoomCodeKey = "sala.codigo";
        static readonly int[] LapOptions = { 1, 2, 3, 5, 7 };

        [SerializeField] TrackDefinition[] tracks;
        [SerializeField] FrontendFlow flow;
        [SerializeField] PartyRacers.UI.Garage.PreviewStudio previewStudio;

        readonly List<string> localBots = new List<string>();
        readonly List<TrackDefinition> playableTracks = new List<TrackDefinition>();
        readonly Dictionary<string, Texture2D> playerPreviews = new Dictionary<string, Texture2D>();
        readonly HashSet<string> pendingPreviews = new HashSet<string>();

        VisualElement root;
        Label roomCode;
        Label roomCount;
        VisualElement mapPreview;
        Label mapName;
        Label mapDescription;
        Label mapRules;
        List<VisualElement> mapDots;
        RacePlayerRegistry registry;
        NetworkBootstrap bootstrap;
        int trackIndex;
        int lapIndex;
        bool subscribed;

        public void Bind(VisualElement value)
        {
            Unbind();
            root = value;
            if (flow == null)
                flow = FindAnyObjectByType<FrontendFlow>(FindObjectsInactive.Include);
            if (previewStudio == null)
                previewStudio = FindAnyObjectByType<PartyRacers.UI.Garage.PreviewStudio>(FindObjectsInactive.Include);

            registry = RacePlayerRegistry.Instance;
            bootstrap = NetworkBootstrap.Instance;
            registry?.EnsureLocalPlayer();

            roomCode = root.Q<Label>("Room_Code");
            roomCount = root.Q<Label>("Room_Count");
            mapPreview = root.Q<VisualElement>("Map_Preview");
            mapName = root.Q<Label>("Map_Name");
            mapDescription = root.Q<Label>("Map_Desc");
            mapRules = root.Q<Label>("Map_Rules");
            mapDots = new List<VisualElement>(root.Q<VisualElement>("Map_Dots").Children());

            root.Q<Button>("Btn_Copy").clicked += CreateOrCopyRoom;
            root.Q<Button>("Btn_AddBots_Face").clicked += AddBot;
            root.Q<Button>("Btn_Start_Face").clicked += StartRace;
            root.Q<Button>("Laps_Minus").clicked += () => ChangeLaps(-1);
            root.Q<Button>("Laps_Plus").clicked += () => ChangeLaps(1);
            root.Q<Button>("Items_Minus").clicked += ToggleItems;
            root.Q<Button>("Items_Plus").clicked += ToggleItems;
            root.Q<Button>("Bots_Minus").clicked += ToggleFillBots;
            root.Q<Button>("Bots_Plus").clicked += ToggleFillBots;
            root.Q<Button>("Map_Prev").clicked += () => ChangeTrack(-1);
            root.Q<Button>("Map_Next").clicked += () => ChangeTrack(1);

            for (int i = 0; i < mapDots.Count; i++)
            {
                int dotIndex = i;
                mapDots[i].RegisterCallback<ClickEvent>(_ => SelectTrack(dotIndex));
            }

            for (int i = 0; i < SlotCount; i++)
            {
                int slotIndex = i;
                VisualElement slot = root.Q<VisualElement>("Slot_" + i);
                slot.Q<VisualElement>("State_Empty").RegisterCallback<ClickEvent>(_ => CreateOrCopyRoom());
                slot.Q<VisualElement>("State_Player").RegisterCallback<ClickEvent>(_ => ToggleLocalReady(slotIndex));
                slot.Q<Button>("Btn_Kick").clicked += () => RemoveAt(slotIndex);
            }

            RaceRules.Carregar();
            BuildTracks();
            lapIndex = Array.IndexOf(LapOptions, RaceRules.Voltas);
            if (lapIndex < 0) lapIndex = 2;
            trackIndex = Mathf.Max(0, playableTracks.FindIndex(track => track.cena == RaceRules.Pista));

            if (registry != null)
            {
                registry.Changed += Refresh;
                subscribed = true;
            }
            if (bootstrap != null)
            {
                bootstrap.StatusChanged += OnNetworkStatus;
                subscribed = true;
            }

            Refresh();
        }

        public void Unbind()
        {
            if (subscribed)
            {
                if (registry != null) registry.Changed -= Refresh;
                if (bootstrap != null) bootstrap.StatusChanged -= OnNetworkStatus;
            }

            subscribed = false;
            root = null;
            registry = null;
            bootstrap = null;
        }

        void BuildTracks()
        {
            playableTracks.Clear();
            if (tracks != null)
                foreach (TrackDefinition track in tracks)
                    if (track != null && track.Jogavel)
                        playableTracks.Add(track);
            playableTracks.Sort((a, b) => a.ordem.CompareTo(b.ordem));
        }

        void OnNetworkStatus(string _)
        {
            if (bootstrap != null && bootstrap.IsOnline && bootstrap.HasJoinCode)
                GUIUtility.systemCopyBuffer = bootstrap.CurrentJoinCode;
            Refresh();
        }

        void CreateOrCopyRoom()
        {
            if (bootstrap != null && bootstrap.IsOnline && bootstrap.HasJoinCode)
            {
                GUIUtility.systemCopyBuffer = bootstrap.CurrentJoinCode;
                Refresh();
                return;
            }

            GUIUtility.systemCopyBuffer = LocalRoomCode();
            flow?.Convidar();
            Refresh();
        }

        void AddBot()
        {
            if (!IsHost() || OccupantCount() >= SlotCount)
                return;

            localBots.Add("BOT " + (localBots.Count + 1));
            Refresh();
        }

        void RemoveAt(int slotIndex)
        {
            if (!IsHost()) return;
            int humans = HumanCount();
            int botIndex = slotIndex - humans;
            if (botIndex < 0 || botIndex >= localBots.Count) return;
            localBots.RemoveAt(botIndex);
            Refresh();
        }

        void ToggleLocalReady(int slotIndex)
        {
            if (registry?.LocalPlayer == null || slotIndex >= HumanCount())
                return;

            RacePlayerInfo player = PlayerAt(slotIndex);
            if (player == null || !player.IsLocal)
                return;

            if (bootstrap != null && bootstrap.IsOnline)
                bootstrap.SetLocalReady(!player.IsReady);
            else
                registry.ToggleLocalReady();
        }

        void ChangeLaps(int direction)
        {
            lapIndex = ((lapIndex + direction) % LapOptions.Length + LapOptions.Length) % LapOptions.Length;
            SaveRules();
            RefreshRules();
            RefreshMap();
        }

        void ToggleItems()
        {
            RaceRules.Itens = !RaceRules.Itens;
            SaveRules();
            RefreshRules();
        }

        void ToggleFillBots()
        {
            RaceRules.BotsPreenchem = !RaceRules.BotsPreenchem;
            SaveRules();
            RefreshRules();
            RefreshStart();
        }

        void SelectTrack(int index)
        {
            if (index < 0 || index >= playableTracks.Count)
                return;
            trackIndex = index;
            SaveRules();
            RefreshMap();
        }

        void ChangeTrack(int direction)
        {
            if (playableTracks.Count == 0)
                return;

            trackIndex = ((trackIndex + direction) % playableTracks.Count + playableTracks.Count)
                % playableTracks.Count;
            SaveRules();
            RefreshMap();
        }

        void StartRace()
        {
            if (!CanStart() || flow == null)
                return;

            SaveRules();
            TrackDefinition track = CurrentTrack();
            if (track != null)
                flow.CorrerEm(track.cena);
            else
                flow.Correr();
        }

        void SaveRules()
        {
            RaceRules.Voltas = LapOptions[Mathf.Clamp(lapIndex, 0, LapOptions.Length - 1)];
            TrackDefinition track = CurrentTrack();
            RaceRules.Pista = track != null ? track.cena : string.Empty;
            RaceRules.Salvar();
        }

        void Refresh()
        {
            if (root == null) return;
            roomCode.text = CurrentRoomCode();
            roomCount.text = $"{OccupantCount()} / {SlotCount}";
            RefreshSlots();
            RefreshMap();
            RefreshRules();
            RefreshStart();
        }

        void RefreshSlots()
        {
            int humans = HumanCount();
            for (int i = 0; i < SlotCount; i++)
            {
                VisualElement slot = root.Q<VisualElement>("Slot_" + i);
                if (i < humans)
                {
                    RacePlayerInfo player = PlayerAt(i);
                    SetPlayer(slot, i, player);
                }
                else if (i - humans < localBots.Count)
                {
                    SetBot(slot, i, localBots[i - humans]);
                }
                else
                {
                    UiStates.ShowOnly(slot, "State_Empty", "State_Player", "State_Bot", "State_Empty");
                    slot.Q<Label>("Empty_Index").text = (i + 1).ToString("00");
                }
            }
        }

        void SetPlayer(VisualElement slot, int index, RacePlayerInfo player)
        {
            UiStates.ShowOnly(slot, "State_Player", "State_Player", "State_Bot", "State_Empty");
            string name = string.IsNullOrWhiteSpace(player?.DisplayName) ? "JOGADOR" : player.DisplayName.ToUpperInvariant();
            slot.Q<Label>("Player_Index").text = (index + 1).ToString("00");
            slot.Q<Label>("Player_Name").text = player != null && player.IsLocal ? name + " (VOCE)" : name;
            UiStates.Show(slot.Q<Label>("Badge_Host"), player != null && player.IsHost);
            UiStates.Show(slot.Q<VisualElement>("Mark_Ready"), player == null || player.IsReady || !IsOnline());
            UiStates.Show(slot.Q<VisualElement>("Mark_Waiting"), player != null && !player.IsReady && IsOnline());
            UiStates.Show(slot.Q<Button>("Btn_Kick"), false);

            VisualElement avatar = slot.Q<VisualElement>("Player_Avatar");
            UiStates.SetAvatarTint(avatar, name);
            SetPlayerPreview(avatar, player, index);
        }

        void SetPlayerPreview(VisualElement target, RacePlayerInfo player, int index)
        {
            if (target == null || player == null)
                return;

            string key = $"{player.Id ?? index.ToString()}:{player.CarIndex}:{player.ColorIndex}:{player.ElementData ?? string.Empty}";
            if (playerPreviews.TryGetValue(key, out Texture2D texture) && texture != null)
            {
                target.style.backgroundImage = new StyleBackground(texture);
                return;
            }

            target.style.backgroundImage = StyleKeyword.None;
            if (previewStudio == null || !pendingPreviews.Add(key))
                return;

            var selection = new KartVisualSelection(player.CarIndex, player.ColorIndex, player.ElementData);
            previewStudio.PedirCarro(selection, ready =>
            {
                pendingPreviews.Remove(key);
                if (ready == null)
                    return;

                playerPreviews[key] = ready;
                if (root != null)
                    Refresh();
            });
        }

        static void SetBot(VisualElement slot, int index, string name)
        {
            UiStates.ShowOnly(slot, "State_Bot", "State_Player", "State_Bot", "State_Empty");
            slot.Q<Label>("Bot_Index").text = (index + 1).ToString("00");
            slot.Q<Label>("Bot_Name").text = name;
        }

        void RefreshMap()
        {
            TrackDefinition track = CurrentTrack();
            if (track == null)
            {
                mapName.text = "SEM PISTA";
                mapDescription.text = "Nenhuma pista jogavel foi configurada.";
                UiStates.Show(mapPreview.Q<Label>(className: "custom__map-placeholder"), true);
                return;
            }

            mapName.text = track.nome.ToUpperInvariant();
            mapDescription.text = track.descricao;
            int laps = LapOptions[Mathf.Clamp(lapIndex, 0, LapOptions.Length - 1)];
            mapRules.text = laps == 1 ? "1 VOLTA" : laps + " VOLTAS";
            if (track.miniatura != null)
            {
                mapPreview.style.backgroundImage = new StyleBackground(track.miniatura);
                UiStates.Show(mapPreview.Q<Label>(className: "custom__map-placeholder"), false);
            }

            for (int i = 0; i < mapDots.Count; i++)
            {
                UiStates.Show(mapDots[i], i < playableTracks.Count);
                UiStates.SetVariant(mapDots[i], i == trackIndex ? "custom__dot--on" : "custom__dot--off",
                    "custom__dot--on", "custom__dot--off");
            }
        }

        void RefreshRules()
        {
            root.Q<Label>("Laps_Value").text = LapOptions[Mathf.Clamp(lapIndex, 0, LapOptions.Length - 1)].ToString();
            root.Q<Label>("Items_Value").text = RaceRules.Itens ? "TODOS" : "NENHUM";
            root.Q<Label>("Bots_Value").text = RaceRules.BotsPreenchem ? "SIM" : "NAO";
        }

        void RefreshStart()
        {
            bool canStart = CanStart();
            UiStates.Show(root.Q<VisualElement>("Btn_Start"), canStart);
            UiStates.Show(root.Q<VisualElement>("Btn_Start_Blocked"), !canStart);
            UiStates.Show(root.Q<VisualElement>("Btn_AddBots"), IsHost());
            root.Q<Label>("Start_Block_Reason").text = StartBlockReason();
        }

        bool CanStart()
        {
            if (!IsHost()) return false;
            if (IsOnline() && registry != null && !registry.AllReady()) return false;
            return OccupantCount() >= 2 || RaceRules.BotsPreenchem;
        }

        string StartBlockReason()
        {
            if (!IsHost()) return "SO O ANFITRIAO INICIA";
            if (IsOnline() && registry != null && !registry.AllReady()) return "AGUARDANDO PRONTOS";
            if (OccupantCount() < 2 && !RaceRules.BotsPreenchem) return "CONVIDE ALGUEM OU LIGUE OS BOTS";
            return string.Empty;
        }

        bool IsOnline() => bootstrap != null && bootstrap.IsOnline;
        bool IsHost() => !IsOnline() || bootstrap.Mode == NetworkBootstrap.SessionMode.Host;
        int HumanCount() => registry?.Players.Count ?? 1;
        int OccupantCount() => Mathf.Min(SlotCount, HumanCount() + localBots.Count);

        RacePlayerInfo PlayerAt(int index)
        {
            if (registry != null && index >= 0 && index < registry.Players.Count)
                return registry.Players[index];
            if (index == 0)
                return new RacePlayerInfo("local", PlayerPrefs.GetString("jogador.nome", "JOGADOR"), PlayerKind.Local)
                {
                    IsHost = true,
                    IsReady = true,
                };
            return null;
        }

        TrackDefinition CurrentTrack()
            => playableTracks.Count == 0 ? null : playableTracks[Mathf.Clamp(trackIndex, 0, playableTracks.Count - 1)];

        string CurrentRoomCode()
            => bootstrap != null && bootstrap.IsOnline && bootstrap.HasJoinCode
                ? bootstrap.CurrentJoinCode
                : LocalRoomCode();

        static string LocalRoomCode()
        {
            string saved = PlayerPrefs.GetString(RoomCodeKey, string.Empty).Trim().ToUpperInvariant();
            if (saved.Length == 6) return saved;

            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[6];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
            saved = new string(chars);
            PlayerPrefs.SetString(RoomCodeKey, saved);
            PlayerPrefs.Save();
            return saved;
        }
    }
}
