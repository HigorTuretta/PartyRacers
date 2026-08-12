using UnityEngine;

namespace PartyRacers.UI
{
    public enum StagePose { LobbyLine, GarageOrbit, OrbitSlowFar }

    /// <summary>
    /// A cena 3D do frontend. TUDO aqui e GameObject: karts, plataforma,
    /// anel, luzes, VFX. Nada disto e UI.
    ///
    /// Regra de centro:
    ///   Lobby / Sala privada -> centro do palco em x=960 (canvas 1920)
    ///   Garagem              -> centro do palco em x=1341
    /// O CHAO ACOMPANHA O KART. Se a plataforma ficar em 960 na garagem,
    /// o kart parece flutuar — foi o defeito do porte anterior.
    /// </summary>
    public sealed class StagePresenter : MonoBehaviour
    {
        [SerializeField] Transform stageRoot;
        [SerializeField] Transform playerKartAnchor;
        [SerializeField] Transform[] mateKartAnchors;
        [SerializeField] Camera stageCamera;

        [Header("Poses (Transform alvo, ja posicionado na cena)")]
        [SerializeField] Transform poseLobby;
        [SerializeField] Transform poseGarage;
        [SerializeField] Transform poseOrbitFar;

        [Header("Movimento")]
        [SerializeField] float blendSeconds = 0.45f;   // tokens: cameraGaragem

        public Transform PlayerKartAnchor => playerKartAnchor;
        public Transform[] MateKartAnchors => mateKartAnchors;
        public Camera StageCamera => stageCamera;

        public void SetPose(StagePose pose) { /* lerp easeInOutCubic para a pose */ }

        /// <summary>Projeta um ponto 3D para a posicao de tela usada pela UI.</summary>
        public Vector2 ToPanelPosition(Vector3 world, UnityEngine.UIElements.IPanel panel)
        {
            var screen = stageCamera.WorldToScreenPoint(world);
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
        }
    }
}
