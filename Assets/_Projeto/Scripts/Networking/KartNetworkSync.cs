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
        private KartController kart;
        private KartNetworkIdentity identity;
        private RaceManager raceManager;
        private Rigidbody body;
        private bool originalKinematic;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetection;

        public KartInputState Read() => KartInputState.Neutral;

        private void Awake()
        {
            kart = GetComponent<KartController>();
            identity = GetComponent<KartNetworkIdentity>();
            body = kart != null ? kart.Rigidbody : GetComponent<Rigidbody>();

            if (body != null)
            {
                originalKinematic = body.isKinematic;
                originalInterpolation = body.interpolation;
                originalCollisionDetection = body.collisionDetectionMode;
            }
        }

        public override void OnNetworkSpawn()
        {
            ApplyNetworkRole();
            raceManager = FindAnyObjectByType<RaceManager>();
            raceManager?.RegisterKart(kart);
        }

        public override void OnNetworkDespawn()
        {
            raceManager?.UnregisterKart(kart);
            raceManager = null;
            RestoreLocalControl();
        }

        private void OnDisable()
        {
            if (!IsSpawned)
                RestoreLocalControl();
        }

        private void ApplyNetworkRole()
        {
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

        private void RestoreLocalControl()
        {
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
