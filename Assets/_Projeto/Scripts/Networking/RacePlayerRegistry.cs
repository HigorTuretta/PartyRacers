using System;
using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.Networking
{
    // Registro central dos jogadores da partida (local, remotos e bots). Sobrevive entre cenas
    // (garagem/lobby -> corrida) para que a corrida saiba quem participa. Funciona offline:
    // por padrão registra apenas o jogador local. A camada online popula remotos/bots.
    public class RacePlayerRegistry : MonoBehaviour
    {
        public static RacePlayerRegistry Instance { get; private set; }

        [SerializeField] private string localPlayerName = "Você";

        private readonly List<RacePlayerInfo> players = new List<RacePlayerInfo>();

        public IReadOnlyList<RacePlayerInfo> Players => players;
        public int Count => players.Count;
        public bool IsFull => players.Count >= RaceConstants.MaxPlayers;
        public RacePlayerInfo LocalPlayer { get; private set; }

        // Disparado sempre que a lista muda (entrada/saída/ready) — UI do lobby escuta isto.
        public event Action Changed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureLocalPlayer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EnsureLocalPlayer()
        {
            if (LocalPlayer != null)
                return;

            KartVisualSelection selection = KartGarageSelection.Capture();
            LocalPlayer = new RacePlayerInfo(Guid.NewGuid().ToString(), localPlayerName, PlayerKind.Local)
            {
                IsHost = true,
                IsReady = false,
                CarIndex = selection.CarIndex,
                ColorIndex = selection.ColorIndex,
                ElementData = selection.ElementData
            };

            players.Insert(0, LocalPlayer);
            RaiseChanged();
        }

        public RacePlayerInfo AddPlayer(string id, string displayName, PlayerKind kind)
        {
            if (IsFull)
            {
                Debug.LogWarning($"Lobby cheio ({RaceConstants.MaxPlayers}). Jogador '{displayName}' não adicionado.");
                return null;
            }

            RacePlayerInfo existing = Find(id);
            if (existing != null)
                return existing;

            RacePlayerInfo info = new RacePlayerInfo(id, displayName, kind);
            players.Add(info);
            RaiseChanged();
            return info;
        }

        public void RemovePlayer(string id)
        {
            int index = players.FindIndex(p => p.Id == id);
            if (index < 0)
                return;

            if (players[index] == LocalPlayer)
                return;

            players.RemoveAt(index);
            RaiseChanged();
        }

        public void SetReady(string id, bool ready)
        {
            RacePlayerInfo info = Find(id);
            if (info == null || info.IsReady == ready)
                return;

            info.IsReady = ready;
            RaiseChanged();
        }

        public void ToggleLocalReady()
        {
            if (LocalPlayer == null)
                return;

            SetReady(LocalPlayer.Id, !LocalPlayer.IsReady);
        }

        public void SetLocalPlayerIdentity(string id, string displayName, bool isHost)
        {
            EnsureLocalPlayer();

            if (LocalPlayer == null)
                return;

            LocalPlayer.Id = string.IsNullOrWhiteSpace(id) ? LocalPlayer.Id : id;
            LocalPlayer.DisplayName = string.IsNullOrWhiteSpace(displayName) ? LocalPlayer.DisplayName : displayName;
            LocalPlayer.Kind = PlayerKind.Local;
            LocalPlayer.IsHost = isHost;

            RaiseChanged();
        }

        public void SetLocalPlayerVisual(KartVisualSelection selection)
        {
            EnsureLocalPlayer();

            if (LocalPlayer == null)
                return;

            LocalPlayer.CarIndex = selection.CarIndex;
            LocalPlayer.ColorIndex = selection.ColorIndex;
            LocalPlayer.ElementData = selection.ElementData ?? string.Empty;

            RaiseChanged();
        }

        public void ApplyNetworkSnapshot(IReadOnlyList<RacePlayerInfo> snapshot, string localPlayerId)
        {
            players.Clear();
            LocalPlayer = null;

            if (snapshot != null)
            {
                foreach (RacePlayerInfo source in snapshot)
                {
                    if (source == null)
                        continue;

                    RacePlayerInfo copy = new RacePlayerInfo(
                        source.Id,
                        source.DisplayName,
                        source.Id == localPlayerId ? PlayerKind.Local : source.Kind
                    )
                    {
                        IsReady = source.IsReady,
                        IsHost = source.IsHost,
                        CarIndex = source.CarIndex,
                        ColorIndex = source.ColorIndex,
                        ElementData = source.ElementData ?? string.Empty,
                        SpawnIndex = source.SpawnIndex
                    };

                    if (copy.Kind == PlayerKind.Local)
                        LocalPlayer = copy;

                    players.Add(copy);
                }
            }

            if (LocalPlayer == null)
                EnsureLocalPlayer();
            else
                RaiseChanged();
        }

        public void ResetToLocalPlayer()
        {
            RacePlayerInfo previousLocal = LocalPlayer;

            players.Clear();
            LocalPlayer = null;

            if (previousLocal != null)
            {
                previousLocal.Kind = PlayerKind.Local;
                previousLocal.IsHost = true;
                previousLocal.IsReady = false;
                players.Add(previousLocal);
                LocalPlayer = previousLocal;
                RaiseChanged();
                return;
            }

            EnsureLocalPlayer();
        }

        public bool AllReady()
        {
            if (players.Count < RaceConstants.MinPlayersToStart)
                return false;

            foreach (RacePlayerInfo info in players)
            {
                if (!info.IsReady)
                    return false;
            }

            return true;
        }

        public RacePlayerInfo Find(string id) => players.Find(p => p.Id == id);

        public void Clear()
        {
            players.Clear();
            LocalPlayer = null;
            RaiseChanged();
        }

        public void RaiseChanged() => Changed?.Invoke();
    }
}
