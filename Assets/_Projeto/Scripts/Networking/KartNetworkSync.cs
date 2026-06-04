using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PartyRacers.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(KartController))]
    [RequireComponent(typeof(KartNetworkIdentity))]
    public class KartNetworkSync : NetworkBehaviour, IKartInputSource
    {
        private readonly NetworkVariable<int> carIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> colorIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString512Bytes> elementData = new NetworkVariable<FixedString512Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private KartController kart;
        private KartNetworkIdentity identity;
        private KartLocalRig localRig;
        private KartVisualCustomizer visualCustomizer;
        private RaceManager raceManager;
        private Rigidbody body;
        private bool originalKinematic;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetection;
        private bool visualEventsSubscribed;

        public KartInputState Read() => KartInputState.Neutral;

        private void Awake()
        {
            kart = GetComponent<KartController>();
            identity = GetComponent<KartNetworkIdentity>();
            localRig = GetComponent<KartLocalRig>();
            visualCustomizer = GetComponent<KartVisualCustomizer>();
            body = kart != null ? kart.Rigidbody : GetComponent<Rigidbody>();

            if (visualCustomizer != null)
                visualCustomizer.SetLoadSelectionOnStart(false);

            if (body != null)
            {
                originalKinematic = body.isKinematic;
                originalInterpolation = body.interpolation;
                originalCollisionDetection = body.collisionDetectionMode;
            }
        }

        public override void OnNetworkSpawn()
        {
            SubscribeNetworkVisualEvents();
            ApplyNetworkRole();

            if (IsOwner)
                SubmitLocalPlayerData();
            else
                ApplyNetworkVisual();

            raceManager = FindAnyObjectByType<RaceManager>();
            raceManager?.RegisterKart(kart);
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeNetworkVisualEvents();
            raceManager?.UnregisterKart(kart);
            raceManager = null;
            RestoreLocalControl();
        }

        private void OnDisable()
        {
            if (!IsSpawned)
                RestoreLocalControl();
        }

        private void SubscribeNetworkVisualEvents()
        {
            if (visualEventsSubscribed)
                return;

            carIndex.OnValueChanged += OnCarIndexChanged;
            colorIndex.OnValueChanged += OnColorIndexChanged;
            elementData.OnValueChanged += OnElementDataChanged;
            displayName.OnValueChanged += OnDisplayNameChanged;
            visualEventsSubscribed = true;
        }

        private void UnsubscribeNetworkVisualEvents()
        {
            if (!visualEventsSubscribed)
                return;

            carIndex.OnValueChanged -= OnCarIndexChanged;
            colorIndex.OnValueChanged -= OnColorIndexChanged;
            elementData.OnValueChanged -= OnElementDataChanged;
            displayName.OnValueChanged -= OnDisplayNameChanged;
            visualEventsSubscribed = false;
        }

        private void OnCarIndexChanged(int previousValue, int newValue) => ApplyNetworkVisual();
        private void OnColorIndexChanged(int previousValue, int newValue) => ApplyNetworkVisual();
        private void OnElementDataChanged(FixedString512Bytes previousValue, FixedString512Bytes newValue) => ApplyNetworkVisual();
        private void OnDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue) => ApplyNetworkVisual();

        private void ApplyNetworkRole()
        {
            if (localRig != null)
                localRig.IsLocalPlayer = IsOwner;

            if (kart == null)
                return;

            if (IsOwner)
            {
                identity?.SetKind(PlayerKind.Local);
                kart.SetInputSource(null);
                RestoreBody();
                return;
            }

            identity?.SetKind(PlayerKind.Remote);
            kart.SetInputSource(this);

            if (body == null)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void SubmitLocalPlayerData()
        {
            KartVisualSelection selection = KartGarageSelection.Capture();
            string localName = GetLocalDisplayName();

            if (RacePlayerRegistry.Instance != null)
            {
                RacePlayerRegistry.Instance.SetLocalPlayerVisual(selection);
                if (RacePlayerRegistry.Instance.LocalPlayer != null)
                    RacePlayerRegistry.Instance.LocalPlayer.DisplayName = localName;
            }

            ApplyVisual(selection, localName, PlayerKind.Local);

            FixedString512Bytes fixedElements = ToFixed512(selection.ElementData);
            FixedString64Bytes fixedDisplayName = ToFixed64(localName);

            if (IsServer)
            {
                carIndex.Value = Mathf.Max(0, selection.CarIndex);
                colorIndex.Value = Mathf.Max(0, selection.ColorIndex);
                elementData.Value = fixedElements;
                displayName.Value = fixedDisplayName;
            }
            else
            {
                SubmitPlayerDataServerRpc(
                    Mathf.Max(0, selection.CarIndex),
                    Mathf.Max(0, selection.ColorIndex),
                    fixedElements,
                    fixedDisplayName);
            }
        }

        [ServerRpc]
        private void SubmitPlayerDataServerRpc(
            int selectedCarIndex,
            int selectedColorIndex,
            FixedString512Bytes selectedElementData,
            FixedString64Bytes selectedDisplayName)
        {
            carIndex.Value = Mathf.Max(0, selectedCarIndex);
            colorIndex.Value = Mathf.Max(0, selectedColorIndex);
            elementData.Value = selectedElementData;
            displayName.Value = selectedDisplayName;
        }

        private void ApplyNetworkVisual()
        {
            string resolvedName = displayName.Value.ToString();
            if (string.IsNullOrWhiteSpace(resolvedName))
                resolvedName = $"Player {OwnerClientId}";

            ApplyVisual(
                new KartVisualSelection(carIndex.Value, colorIndex.Value, elementData.Value.ToString()),
                resolvedName,
                IsOwner ? PlayerKind.Local : PlayerKind.Remote);
        }

        private void ApplyVisual(KartVisualSelection selection, string resolvedDisplayName, PlayerKind kind)
        {
            visualCustomizer?.ApplySelection(selection);

            if (identity == null)
                return;

            identity.Configure(new RacePlayerInfo(OwnerClientId.ToString(), resolvedDisplayName, kind)
            {
                CarIndex = selection.CarIndex,
                ColorIndex = selection.ColorIndex,
                ElementData = selection.ElementData
            });
        }

        private string GetLocalDisplayName()
        {
            string name = RacePlayerRegistry.Instance != null && RacePlayerRegistry.Instance.LocalPlayer != null
                ? RacePlayerRegistry.Instance.LocalPlayer.DisplayName
                : string.Empty;

            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name, "Player", System.StringComparison.OrdinalIgnoreCase) ||
                (name.Length <= 5 && name.StartsWith("Voc", System.StringComparison.OrdinalIgnoreCase)))
            {
                return $"Player {OwnerClientId}";
            }

            return name;
        }

        private static FixedString512Bytes ToFixed512(string value)
        {
            value ??= string.Empty;
            if (value.Length > 500)
                value = value.Substring(0, 500);

            return new FixedString512Bytes(value);
        }

        private static FixedString64Bytes ToFixed64(string value)
        {
            value ??= string.Empty;
            if (value.Length > 60)
                value = value.Substring(0, 60);

            return new FixedString64Bytes(value);
        }

        private void RestoreLocalControl()
        {
            if (localRig != null)
                localRig.IsLocalPlayer = true;

            if (kart != null)
                kart.SetInputSource(null);

            RestoreBody();
        }

        private void RestoreBody()
        {
            if (body == null)
                return;

            body.isKinematic = originalKinematic;
            body.interpolation = originalInterpolation;
            body.collisionDetectionMode = originalCollisionDetection;
        }
    }
}
