using UnityEngine;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Coordena a HUD de corrida. NÃO constrói a interface — apenas lê o snapshot do
    /// RaceHUDDataProvider e empurra para os widgets (refs serializadas do prefab).
    /// Substitui o antigo HUDRootUI + KartHUDOverlay (sem instanciar HUD por script).
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceHUDController : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider provider;

        [Header("Conteúdo (escondido no lobby / sem kart local)")]
        [Tooltip("CanvasGroup que envolve a HUD de corrida. Esconde sem desabilitar os widgets.")]
        [SerializeField] private CanvasGroup contentGroup;

        [Header("Widgets")]
        [SerializeField] private LapHUDUI lapHud;
        [SerializeField] private PositionLeaderboardUI leaderboard;
        [SerializeField] private PowerUpHUDUI powerUp;
        [SerializeField] private SpeedNitroHUDUI speedNitro;
        [SerializeField] private AlertNotificationHUDUI alerts;
        [Tooltip("Auto-dirigido pelos eventos do RaceManager; aqui apenas para referência.")]
        [SerializeField] private CountdownHUDUI countdown;

        private bool wasBoosting;
        private bool usePowerWired;

        private void Awake()
        {
            if (provider == null)
                provider = GetComponent<RaceHUDDataProvider>();

            WireUsePower();
        }

        private void OnEnable()
        {
            RaceHudEvents.Raised += OnHudEvent;
            WireUsePower();
        }

        private void OnDisable()
        {
            RaceHudEvents.Raised -= OnHudEvent;
        }

        private void WireUsePower()
        {
            if (usePowerWired || powerUp == null)
                return;

            powerUp.onUsePower.AddListener(OnUsePowerRequested);
            usePowerWired = true;
        }

        private void OnUsePowerRequested()
        {
            if (provider != null)
                provider.RequestUsePower();
        }

        private void Update()
        {
            if (provider == null)
                return;

            provider.Refresh();

            bool active = provider.HasLocalKart && !provider.InLobby;
            SetContentVisible(active);
            if (!active)
            {
                wasBoosting = false;
                return;
            }

            if (lapHud != null)
            {
                lapHud.SetLap(provider.CurrentLap, provider.TotalLaps, provider.RaceFinished);
                lapHud.SetLapTime(provider.CurrentLapTime);
            }

            leaderboard?.SetEntries(provider.Standings, provider.LocalPosition);

            powerUp?.SetPower(provider.CurrentPower, provider.PowerName, provider.HasPower);

            if (speedNitro != null)
            {
                speedNitro.SetSpeed(provider.SpeedKmh);
                speedNitro.SetNitro(provider.Boost01, provider.IsBoosting);
            }

            DetectNitroEdge();
        }

        private void DetectNitroEdge()
        {
            bool boosting = provider.IsBoosting;
            if (boosting && !wasBoosting)
                alerts?.Show(RaceHudEventKind.Nitro);
            wasBoosting = boosting;
        }

        private void SetContentVisible(bool visible)
        {
            if (contentGroup == null)
                return;

            contentGroup.alpha = visible ? 1f : 0f;
            contentGroup.interactable = visible;
            contentGroup.blocksRaycasts = visible;
        }

        private void OnHudEvent(RaceHudEvents.EventData data)
        {
            if (alerts == null || provider == null || provider.LocalKart == null)
                return;

            // Só alertas do jogador local (ator do evento é o kart local).
            if (data.Actor == provider.LocalKart.gameObject)
                alerts.Show(data.Kind);
        }
    }
}
