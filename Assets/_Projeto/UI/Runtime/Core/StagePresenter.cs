using PartyRacers.UI.Frontend;
using PartyRacers.UI.Garage;
using UnityEngine;
using UnityEngine.UIElements;

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

        [Header("Integracao com o palco existente")]
        [SerializeField] KartVisualCustomizer kart;
        [SerializeField] GarageCameraRig garageCamera;
        [SerializeField] FrontendFlow flow;

        [Header("Enquadramento 3D")]
        [SerializeField] Vector3 lobbyDirection = new Vector3(1f, 0.45f, 1f);
        [SerializeField] float lobbyDistance = 1.95f;
        [SerializeField] float overlayDistance = 2.45f;
        [SerializeField] float lobbyFov = 34f;
        [SerializeField] float verticalBias = 0.30f;
        [SerializeField] float blendSeconds = 0.45f;

        public Transform PlayerKartAnchor => playerKartAnchor;
        public Transform[] MateKartAnchors => mateKartAnchors;
        public Camera StageCamera => stageCamera;

        StagePose currentPose;
        Vector3 fromPosition;
        Quaternion fromRotation;
        float fromFov;
        float blend01 = 1f;

        public StagePose CurrentPose => currentPose;

        public void Hide()
        {
            garageCamera?.Ativar(false);
            if (stageRoot != null)
                stageRoot.gameObject.SetActive(false);
        }

        public void SetPose(StagePose pose)
        {
            currentPose = pose;

            if (stageRoot != null && !stageRoot.gameObject.activeSelf)
                stageRoot.gameObject.SetActive(true);

            if (stageCamera != null)
            {
                fromPosition = stageCamera.transform.position;
                fromRotation = stageCamera.transform.rotation;
                fromFov = stageCamera.fieldOfView;
            }

            blend01 = 0f;
            garageCamera?.Ativar(pose == StagePose.GarageOrbit);

            if (pose == StagePose.GarageOrbit)
                garageCamera?.IrPara("MODELO");
        }

        public void SetGarageCategory(string category)
        {
            if (currentPose != StagePose.GarageOrbit)
                SetPose(StagePose.GarageOrbit);

            garageCamera?.Ativar(true);
            garageCamera?.IrPara(category);
            garageCamera?.NotificarInteracao();
        }

        void LateUpdate()
        {
            if (currentPose == StagePose.GarageOrbit || stageCamera == null || kart == null)
                return;

            kart.EnsureBuilt();
            if (!kart.TryGetCarShape(out Vector3 center, out float radius))
                return;

            float distanceFactor = currentPose == StagePose.OrbitSlowFar
                ? overlayDistance
                : lobbyDistance;
            Vector3 direction = lobbyDirection.sqrMagnitude > 0.001f
                ? lobbyDirection.normalized
                : Vector3.forward;
            float distance = radius / Mathf.Tan(lobbyFov * 0.5f * Mathf.Deg2Rad) * distanceFactor;
            Vector3 targetPosition = center + direction * distance;
            Vector3 targetLook = center + Vector3.up * (radius * verticalBias);
            Quaternion targetRotation = Quaternion.LookRotation(targetLook - targetPosition, Vector3.up);

            blend01 = Mathf.Clamp01(blend01 + Time.unscaledDeltaTime / Mathf.Max(0.05f, blendSeconds));
            float eased = EaseInOutCubic(blend01);
            stageCamera.transform.position = Vector3.Lerp(fromPosition, targetPosition, eased);
            stageCamera.transform.rotation = Quaternion.Slerp(fromRotation, targetRotation, eased);
            stageCamera.fieldOfView = Mathf.Lerp(fromFov, lobbyFov, eased);
        }

        static float EaseInOutCubic(float value)
            => value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;

        /// <summary>Projeta um ponto 3D para a posicao de tela usada pela UI.</summary>
        public Vector2 ToPanelPosition(Vector3 world, UnityEngine.UIElements.IPanel panel)
        {
            var screen = stageCamera.WorldToScreenPoint(world);
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
        }
    }
}
