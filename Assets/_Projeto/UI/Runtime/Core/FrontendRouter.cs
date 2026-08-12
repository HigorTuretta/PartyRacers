using PartyRacers.UI.Frontend;
using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// UNICO GameObject de UI do frontend: um UIDocument com a Shell.
    /// As telas sao UXML carregados dentro de Screen_Host / Overlay_Host.
    /// Nao instancie Canvas, nao crie GameObject por tela.
    /// </summary>
    public enum ScreenId { Lobby, Garage, CustomMatch, Matchmaking }

    [RequireComponent(typeof(UIDocument))]
    public sealed class FrontendRouter : MonoBehaviour
    {
        [Header("Telas (UXML)")]
        [SerializeField] VisualTreeAsset lobby;
        [SerializeField] VisualTreeAsset garage;
        [SerializeField] VisualTreeAsset customMatch;
        [SerializeField] VisualTreeAsset matchmaking;

        [Header("Controllers")]
        [SerializeField] LobbyController lobbyController;
        [SerializeField] GarageController garageController;
        [SerializeField] CustomMatchController customController;
        [SerializeField] MatchmakingController mmController;

        [Header("Camera do palco 3D")]
        [SerializeField] StagePresenter stage;

        [Header("Fluxo do jogo")]
        [SerializeField] FrontendFlow flow;

        VisualElement _screenHost, _overlayHost, _topBar;
        Button _lobbyTab, _garageTab, _privateTab;
        ScreenId _current;

        public ScreenId Current => _current;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _screenHost  = root.Q<VisualElement>("Screen_Host");
            _overlayHost = root.Q<VisualElement>("Overlay_Host");
            _topBar      = root.Q<VisualElement>("TopBar");

            _lobbyTab = root.Q<Button>("Tab_Lobby_Btn");
            _garageTab = root.Q<Button>("Tab_Garage_Btn");
            _privateTab = root.Q<Button>("Tab_Private_Btn");
            _lobbyTab.clicked += OpenLobby;
            _garageTab.clicked += OpenGarage;
            _privateTab.clicked += OpenPrivateRoom;

            flow?.UsarUiToolkit(true);
            RefreshShell(root);

            Go(ScreenId.Lobby);
        }

        void OnDisable()
        {
            UnbindCurrent();
            if (_lobbyTab != null) _lobbyTab.clicked -= OpenLobby;
            if (_garageTab != null) _garageTab.clicked -= OpenGarage;
            if (_privateTab != null) _privateTab.clicked -= OpenPrivateRoom;
            flow?.UsarUiToolkit(false);
        }

        void OpenLobby() => Go(ScreenId.Lobby);
        void OpenGarage() => Go(ScreenId.Garage);

        public void Go(ScreenId id)
        {
            if (_screenHost == null || _overlayHost == null)
                return;

            UnbindCurrent();
            SetTabState(id);

            // Matchmaking e modal e cobre inclusive a TopBar. Sala privada e
            // uma tela do frontend: preserva as abas clicaveis sobre o palco.
            if (id == ScreenId.Matchmaking)
            {
                _overlayHost.Clear();
                var el = matchmaking.Instantiate();
                el.AddToClassList("pr-fill");
                _overlayHost.Add(el);
                _overlayHost.pickingMode = PickingMode.Position;
                UiStates.Show(_screenHost, false);

                mmController.Bind(el);

                stage.SetPose(StagePose.OrbitSlowFar);
                _current = id;
                return;
            }

            _overlayHost.Clear();
            _overlayHost.pickingMode = PickingMode.Ignore;
            UiStates.Show(_screenHost, true);
            _screenHost.Clear();

            VisualTreeAsset asset = id switch
            {
                ScreenId.Garage      => garage,
                ScreenId.CustomMatch => customMatch,
                _                    => lobby
            };

            var screen = asset.Instantiate();
            screen.AddToClassList("pr-fill");
            _screenHost.Add(screen);

            switch (id)
            {
                case ScreenId.Lobby:       lobbyController.Bind(screen);   stage.SetPose(StagePose.LobbyLine);   break;
                case ScreenId.Garage:      garageController.Bind(screen);  stage.SetPose(StagePose.GarageOrbit); break;
                case ScreenId.CustomMatch: customController.Bind(screen);  stage.Hide(); break;
            }

            _current = id;
        }

        public void OpenPrivateRoom() => Go(ScreenId.CustomMatch);

        [ContextMenu("Abrir Sala Privada")]
        void OpenPrivateRoomFromInspector() => OpenPrivateRoom();

        void UnbindCurrent()
        {
            switch (_current)
            {
                case ScreenId.Lobby: lobbyController?.Unbind(); break;
                case ScreenId.Garage: garageController?.Unbind(); break;
                case ScreenId.CustomMatch: customController?.Unbind(); break;
                case ScreenId.Matchmaking: mmController?.Unbind(); break;
            }
        }

        void SetTabState(ScreenId id)
        {
            bool lobbyOn = id == ScreenId.Lobby || id == ScreenId.Matchmaking;
            SetTab("Lobby", lobbyOn);
            SetTab("Garage", id == ScreenId.Garage);
            SetTab("Private", id == ScreenId.CustomMatch);
        }

        void SetTab(string name, bool on)
        {
            var wrap = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("Tab_" + name);
            var button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("Tab_" + name + "_Btn");
            UiStates.SetVariant(wrap, on ? "shell__tab--on" : "shell__tab--off",
                "shell__tab--on", "shell__tab--off");
            UiStates.SetVariant(button, on ? "pr-tab--on" : "pr-tab--off", "pr-tab--on", "pr-tab--off");
        }

        static void RefreshShell(VisualElement root)
        {
            string player = PlayerPrefs.GetString("jogador.nome", "HIGOR").Trim();
            root.Q<Label>("Profile_Name").text = string.IsNullOrEmpty(player)
                ? "JOGADOR"
                : player.ToUpperInvariant();
            root.Q<Label>("Wallet_Coins_Value").text = PlayerPrefs.GetInt("carteira.moedas", 12480).ToString("N0");
            root.Q<Label>("Wallet_Gems_Value").text = PlayerPrefs.GetInt("carteira.fichas", 340).ToString("N0");
            UiStates.SetAvatarTint(root.Q<VisualElement>("Profile_Avatar"), player);
        }
    }
}
