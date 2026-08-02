using System;
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

        // Bots são karts do SERVIDOR: sem esta flag replicada o cliente não teria como distinguir
        // um bot de um jogador remoto, e trataria o kart como se alguém o estivesse dirigindo.
        private readonly NetworkVariable<bool> isBotKart = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Item em posse. Quem sorteia é sempre o servidor (ver ItemBox); os clientes só leem.
        // Antes cada máquina sorteava o seu, e os jogadores viam poderes diferentes no mesmo kart.
        private readonly NetworkVariable<KartPowerType> currentPower = new NetworkVariable<KartPowerType>(
            KartPowerType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<DriftEffectState> driftEffectState = new NetworkVariable<DriftEffectState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private const float DriftEffectStateSyncInterval = 0.05f;
        private const float DriftEffectStateEpsilon = 0.015f;
        private const float PowerResyncInterval = 0.5f;

        private KartController kart;
        private KartNetworkIdentity identity;
        private KartLocalRig localRig;
        private KartVisualCustomizer visualCustomizer;
        private KartPowerInventory powerInventory;
        private KartPowerUser powerUser;
        private KartRespawn kartRespawn;
        private RaceManager raceManager;
        private Rigidbody body;
        private bool originalKinematic;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetection;
        private bool visualEventsSubscribed;
        private bool inventoryEventSubscribed;
        private bool hasSubmittedDriftEffectState;
        private float nextDriftEffectStateSyncTime;
        private float nextPowerResyncTime;
        private DriftEffectState lastSubmittedDriftEffectState;

        public KartInputState Read() => KartInputState.Neutral;
        public bool UseSyncedEffectState => IsSpawned && !IsOwner;
        public bool EffectIsGrounded => UseSyncedEffectState ? driftEffectState.Value.IsGrounded : kart != null && kart.IsGrounded;
        public bool EffectIsBurningOut => UseSyncedEffectState ? driftEffectState.Value.IsBurningOut : kart != null && kart.IsBurningOut;
        public float EffectSpeedKmh => UseSyncedEffectState ? driftEffectState.Value.SpeedKmh : kart != null ? kart.SpeedKmh : 0f;
        public float EffectSpeed01 => UseSyncedEffectState ? driftEffectState.Value.Speed01 : kart != null ? kart.Speed01 : 0f;
        public float EffectDriftBlend => UseSyncedEffectState ? driftEffectState.Value.DriftBlend : kart != null ? kart.DriftBlend : 0f;
        public float EffectTireStress01 => UseSyncedEffectState ? driftEffectState.Value.TireStress01 : kart != null ? kart.TireStress01 : 0f;
        public float EffectLaunchSlip01 => UseSyncedEffectState ? driftEffectState.Value.LaunchSlip01 : kart != null ? kart.LaunchSlip01 : 0f;
        public float EffectBrakeSlip01 => UseSyncedEffectState ? driftEffectState.Value.BrakeSlip01 : kart != null ? kart.BrakeSlip01 : 0f;

        /// <summary>True quando este kart é um bot conduzido pelo servidor.</summary>
        public bool IsBot => (IsSpawned && isBotKart.Value) || (identity != null && identity.IsBot);

        /// <summary>
        /// Esta máquina comanda este kart: o dono, no caso de um jogador; o servidor, no caso de um
        /// bot. Só quem comanda pode gastar poderes e dirigir com física própria.
        /// </summary>
        public bool CanCommandThisKart => IsBot ? IsServer : IsOwner;

        private void Awake()
        {
            kart = GetComponent<KartController>();
            identity = GetComponent<KartNetworkIdentity>();
            localRig = GetComponent<KartLocalRig>();
            visualCustomizer = GetComponent<KartVisualCustomizer>();
            powerInventory = GetComponent<KartPowerInventory>();
            powerUser = GetComponent<KartPowerUser>();
            kartRespawn = GetComponent<KartRespawn>();
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
            SubscribeInventoryEvents();
            ApplyNetworkRole();

            if (IsOwner && !IsBot)
            {
                SubmitLocalPlayerData();
                SubmitDriftEffectState(force: true);
            }
            else if (!(IsServer && IsBot))
            {
                // O bot no servidor já foi vestido pelo BotKartCustomizer. Todo o resto (jogadores
                // remotos e, nos clientes, também os bots) recebe o visual pelas NetworkVariables.
                ApplyNetworkVisual();
            }

            raceManager = FindAnyObjectByType<RaceManager>();
            raceManager?.RegisterKart(kart);
        }

        private void Update()
        {
            SubmitDriftEffectState(force: false);
            ResyncPowerFromNetwork();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeNetworkVisualEvents();
            UnsubscribeInventoryEvents();
            raceManager?.UnregisterKart(kart);
            raceManager = null;
            RestoreLocalControl();
        }

        private void OnDisable()
        {
            if (!IsSpawned)
                RestoreLocalControl();
        }

        // ------------------------------------------------------------------ Configuração de bot

        /// <summary>
        /// Publica um kart de bot para os clientes: papel, nome e visual. Chamado pelo
        /// RaceBotManager no servidor logo depois do Spawn. Sem isto os clientes recebiam o kart
        /// mas o tratavam como um jogador remoto sem nome e com o carro padrão.
        /// </summary>
        public void ConfigureAsBot(string botName, KartVisualSelection selection)
        {
            if (!IsServer || !IsSpawned)
                return;

            isBotKart.Value = true;
            displayName.Value = ToFixed64(botName);
            carIndex.Value = Mathf.Max(0, selection.CarIndex);
            colorIndex.Value = Mathf.Max(0, selection.ColorIndex);
            elementData.Value = ToFixed512(selection.ElementData);

            identity?.SetKind(PlayerKind.Bot);
            identity?.SetDisplayName(botName);
            ApplyNetworkRole();
        }

        // ------------------------------------------------------------------ Poderes

        /// <summary>
        /// Anuncia que este kart acabou de gastar um poder, para que TODAS as máquinas reproduzam o
        /// mesmo efeito com o mesmo alvo. Quem chama já executou o efeito localmente.
        /// </summary>
        public void ReportPowerUsed(KartPowerType power, GameObject target)
        {
            if (!IsSpawned || power == KartPowerType.None)
                return;

            ulong targetId = ResolveNetworkObjectId(target);

            if (IsServer)
            {
                currentPower.Value = KartPowerType.None;
                PlayPowerClientRpc(power, targetId, NetworkManager.ServerClientId);
                return;
            }

            UsePowerServerRpc(power, targetId);
        }

        [ServerRpc]
        private void UsePowerServerRpc(KartPowerType power, ulong targetId, ServerRpcParams rpcParams = default)
        {
            // O servidor é a fonte de verdade do inventário: se ele não tinha o poder, o pedido cai.
            if (currentPower.Value != power)
                return;

            currentPower.Value = KartPowerType.None;
            powerInventory?.ApplyNetworkPower(KartPowerType.None);

            ulong initiator = rpcParams.Receive.SenderClientId;
            PlayPowerClientRpc(power, targetId, initiator);

            // O servidor também executa o efeito: é a máquina que hospeda a simulação e, no modo
            // host, é onde o dono da sala precisa ver o foguete do adversário saindo.
            GameObject target = ResolveTargetObject(targetId);
            powerUser?.PlayNetworkPower(power, target);
        }

        [ClientRpc]
        private void PlayPowerClientRpc(KartPowerType power, ulong targetId, ulong initiatorClientId)
        {
            if (IsServer)
                return;

            // Quem disparou já reproduziu o efeito na hora do clique — repetir causaria dois
            // foguetes saindo do mesmo carro.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == initiatorClientId)
                return;

            GameObject target = ResolveTargetObject(targetId);
            powerUser?.PlayNetworkPower(power, target);
        }

        private void SubscribeInventoryEvents()
        {
            if (inventoryEventSubscribed || powerInventory == null)
                return;

            if (IsServer)
                powerInventory.PowerChangedLocally += OnPowerChangedLocally;

            currentPower.OnValueChanged += OnNetworkPowerChanged;
            inventoryEventSubscribed = true;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (!inventoryEventSubscribed)
                return;

            if (powerInventory != null)
                powerInventory.PowerChangedLocally -= OnPowerChangedLocally;

            currentPower.OnValueChanged -= OnNetworkPowerChanged;
            inventoryEventSubscribed = false;
        }

        private void OnPowerChangedLocally(KartPowerType power)
        {
            if (IsServer && IsSpawned)
                currentPower.Value = power;
        }

        private void OnNetworkPowerChanged(KartPowerType previousValue, KartPowerType newValue)
        {
            if (IsServer)
                return;

            powerInventory?.ApplyNetworkPower(newValue);
        }

        /// <summary>
        /// Rede de segurança do inventário: o dono consome o item na hora do clique para o comando
        /// não parecer travado, mas se o servidor recusar o uso a NetworkVariable não muda e o
        /// evento de alteração nunca chega. Esta varredura devolve o item nesse caso.
        /// </summary>
        private void ResyncPowerFromNetwork()
        {
            if (!IsSpawned || IsServer || powerInventory == null)
                return;

            if (Time.unscaledTime < nextPowerResyncTime)
                return;

            nextPowerResyncTime = Time.unscaledTime + PowerResyncInterval;

            if (powerInventory.CurrentPower != currentPower.Value)
                powerInventory.ApplyNetworkPower(currentPower.Value);
        }

        private static ulong ResolveNetworkObjectId(GameObject target)
        {
            if (target == null)
                return 0UL;

            NetworkObject netObj = target.GetComponentInParent<NetworkObject>();
            return netObj != null && netObj.IsSpawned ? netObj.NetworkObjectId : 0UL;
        }

        private static GameObject ResolveTargetObject(ulong networkObjectId)
        {
            if (networkObjectId == 0UL)
                return null;

            NetworkObject netObj = RaceAuthority.FindSpawned(networkObjectId);
            return netObj != null ? netObj.gameObject : null;
        }

        // ------------------------------------------------------------------ Visual e papéis

        private void SubscribeNetworkVisualEvents()
        {
            if (visualEventsSubscribed)
                return;

            carIndex.OnValueChanged += OnCarIndexChanged;
            colorIndex.OnValueChanged += OnColorIndexChanged;
            elementData.OnValueChanged += OnElementDataChanged;
            displayName.OnValueChanged += OnDisplayNameChanged;
            isBotKart.OnValueChanged += OnIsBotChanged;
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
            isBotKart.OnValueChanged -= OnIsBotChanged;
            visualEventsSubscribed = false;
        }

        private void OnCarIndexChanged(int previousValue, int newValue) => ApplyNetworkVisualIfRemote();
        private void OnColorIndexChanged(int previousValue, int newValue) => ApplyNetworkVisualIfRemote();
        private void OnElementDataChanged(FixedString512Bytes previousValue, FixedString512Bytes newValue) => ApplyNetworkVisualIfRemote();
        private void OnDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue) => ApplyNetworkVisualIfRemote();

        private void OnIsBotChanged(bool previousValue, bool newValue)
        {
            ApplyNetworkRole();
            ApplyNetworkVisualIfRemote();
        }

        // O dono monta o próprio carro a partir da garagem; reaplicar o que veio da rede em cima
        // disso só destruiria e reconstruiria o mesmo rig por nada.
        private void ApplyNetworkVisualIfRemote()
        {
            if (CanCommandThisKart && !IsBot)
                return;

            if (IsServer && IsBot)
                return;

            ApplyNetworkVisual();
        }

        private void ApplyNetworkRole()
        {
            bool bot = IsBot;
            bool authoritative = bot ? IsServer : IsOwner;

            if (localRig != null)
                localRig.IsLocalPlayer = IsOwner && !bot;

            SetNonAuthoritativeComponentsEnabled(authoritative);

            if (kart == null)
                return;

            if (authoritative)
            {
                // Bots são dirigidos pelo BotDriverController — não podemos limpar a fonte de input.
                identity?.SetKind(bot ? PlayerKind.Bot : PlayerKind.Local);
                if (!bot)
                    kart.SetInputSource(null);

                RestoreBody();
                return;
            }

            identity?.SetKind(bot ? PlayerKind.Bot : PlayerKind.Remote);
            kart.SetInputSource(this);

            if (body == null)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>
        /// Karts que esta máquina não comanda recebem a posição pelo NetworkTransform. Deixar o
        /// respawn automático ligado neles fazia a cópia local teleportar o carro por conta própria
        /// e brigar com a posição que chegava da rede.
        /// </summary>
        private void SetNonAuthoritativeComponentsEnabled(bool authoritative)
        {
            if (kartRespawn != null)
                kartRespawn.enabled = authoritative;
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

            PlayerKind kind = IsBot
                ? PlayerKind.Bot
                : IsOwner ? PlayerKind.Local : PlayerKind.Remote;

            ApplyVisual(
                new KartVisualSelection(carIndex.Value, colorIndex.Value, elementData.Value.ToString()),
                resolvedName,
                kind);
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

        private void SubmitDriftEffectState(bool force)
        {
            if (!IsSpawned || !IsOwner || kart == null)
                return;

            float time = Time.unscaledTime;
            if (!force && time < nextDriftEffectStateSyncTime)
                return;

            nextDriftEffectStateSyncTime = time + DriftEffectStateSyncInterval;

            DriftEffectState current = DriftEffectState.FromKart(kart);
            if (!force
                && hasSubmittedDriftEffectState
                && !current.HasMeaningfulChange(lastSubmittedDriftEffectState, DriftEffectStateEpsilon))
            {
                return;
            }

            driftEffectState.Value = current;
            lastSubmittedDriftEffectState = current;
            hasSubmittedDriftEffectState = true;
        }

        private void RestoreLocalControl()
        {
            bool bot = identity != null && identity.IsBot;
            if (localRig != null)
                localRig.IsLocalPlayer = !bot;

            if (kart != null && !bot)
                kart.SetInputSource(null);

            SetNonAuthoritativeComponentsEnabled(true);
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

        private struct DriftEffectState : INetworkSerializable, IEquatable<DriftEffectState>
        {
            public bool IsGrounded;
            public bool IsBurningOut;
            public float SpeedKmh;
            public float Speed01;
            public float DriftBlend;
            public float TireStress01;
            public float LaunchSlip01;
            public float BrakeSlip01;

            public static DriftEffectState FromKart(KartController kart)
            {
                return new DriftEffectState
                {
                    IsGrounded = kart.IsGrounded,
                    IsBurningOut = kart.IsBurningOut,
                    SpeedKmh = kart.SpeedKmh,
                    Speed01 = kart.Speed01,
                    DriftBlend = kart.DriftBlend,
                    TireStress01 = kart.TireStress01,
                    LaunchSlip01 = kart.LaunchSlip01,
                    BrakeSlip01 = kart.BrakeSlip01
                };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref IsGrounded);
                serializer.SerializeValue(ref IsBurningOut);
                serializer.SerializeValue(ref SpeedKmh);
                serializer.SerializeValue(ref Speed01);
                serializer.SerializeValue(ref DriftBlend);
                serializer.SerializeValue(ref TireStress01);
                serializer.SerializeValue(ref LaunchSlip01);
                serializer.SerializeValue(ref BrakeSlip01);
            }

            public bool Equals(DriftEffectState other)
            {
                return IsGrounded == other.IsGrounded
                    && IsBurningOut == other.IsBurningOut
                    && SpeedKmh.Equals(other.SpeedKmh)
                    && Speed01.Equals(other.Speed01)
                    && DriftBlend.Equals(other.DriftBlend)
                    && TireStress01.Equals(other.TireStress01)
                    && LaunchSlip01.Equals(other.LaunchSlip01)
                    && BrakeSlip01.Equals(other.BrakeSlip01);
            }

            public override bool Equals(object obj)
            {
                return obj is DriftEffectState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = IsGrounded.GetHashCode();
                    hashCode = (hashCode * 397) ^ IsBurningOut.GetHashCode();
                    hashCode = (hashCode * 397) ^ SpeedKmh.GetHashCode();
                    hashCode = (hashCode * 397) ^ Speed01.GetHashCode();
                    hashCode = (hashCode * 397) ^ DriftBlend.GetHashCode();
                    hashCode = (hashCode * 397) ^ TireStress01.GetHashCode();
                    hashCode = (hashCode * 397) ^ LaunchSlip01.GetHashCode();
                    hashCode = (hashCode * 397) ^ BrakeSlip01.GetHashCode();
                    return hashCode;
                }
            }

            public bool HasMeaningfulChange(DriftEffectState previous, float epsilon)
            {
                return IsGrounded != previous.IsGrounded
                    || IsBurningOut != previous.IsBurningOut
                    || Mathf.Abs(SpeedKmh - previous.SpeedKmh) > epsilon
                    || Mathf.Abs(Speed01 - previous.Speed01) > epsilon
                    || Mathf.Abs(DriftBlend - previous.DriftBlend) > epsilon
                    || Mathf.Abs(TireStress01 - previous.TireStress01) > epsilon
                    || Mathf.Abs(LaunchSlip01 - previous.LaunchSlip01) > epsilon
                    || Mathf.Abs(BrakeSlip01 - previous.BrakeSlip01) > epsilon;
            }
        }
    }
}
