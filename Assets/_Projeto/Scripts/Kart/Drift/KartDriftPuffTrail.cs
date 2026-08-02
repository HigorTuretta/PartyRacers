using PartyRacers.Networking;
using UnityEngine;

public class KartDriftPuffTrail : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private DriftPuffBubble puffPrefab;
    [SerializeField] private Transform rearLeftPoint;
    [SerializeField] private Transform rearRightPoint;

    [Header("Ativação - Drift")]
    [SerializeField] private float minSpeedKmh = 20f;
    [SerializeField] private float minDriftBlend = 0.15f;

    [Header("Ativação - Burnout")]
    [SerializeField] private float burnoutMaxSpeedKmh = 18f;
    [SerializeField] private float burnoutSpawnInterval = 0.055f;
    [SerializeField] private int burnoutPuffsPerWheel = 3;

    [Header("Ativacao - Stress de Pneu")]
    [SerializeField] private float minStressFactor = 0.32f;
    [SerializeField] private float stressSpawnInterval = 0.075f;
    [SerializeField] private int stressPuffsPerWheel = 1;

    [Header("Densidade do Trail")]
    [SerializeField] private float lowSpeedSpacing = 0.75f;
    [SerializeField] private float highSpeedSpacing = 0.28f;
    [SerializeField] private int puffsPerWheel = 3;
    [SerializeField] private int maxSpawnStepsPerFrame = 4;

    [Header("Tamanho dos Puffs")]
    [SerializeField] private float minStartScale = 0.22f;
    [SerializeField] private float maxStartScale = 0.45f;
    [SerializeField] private float minEndScale = 0.85f;
    [SerializeField] private float maxEndScale = 1.45f;

    [Header("Vida dos Puffs")]
    [SerializeField] private float minLifetime = 0.55f;
    [SerializeField] private float maxLifetime = 0.95f;

    [Header("Volume")]
    [SerializeField] private float horizontalScatter = 0.38f;
    [SerializeField] private float forwardScatter = 0.28f;
    [SerializeField] private float verticalScatter = 0.12f;

    [Header("Movimento do Puff")]
    [SerializeField] private float backwardVelocity = 0.45f;
    [SerializeField] private float upwardVelocity = 0.22f;
    [SerializeField] private float sideVelocity = 0.28f;

    [Header("Multiplicadores Burnout")]
    [SerializeField] private float burnoutSizeMultiplier = 1.15f;
    [SerializeField] private float burnoutUpwardMultiplier = 1.35f;
    [SerializeField] private float burnoutScatterMultiplier = 1.2f;

    private Vector3 lastPosition;
    private float distanceAccumulator;
    private bool wasEmittingDrift;
    private float burnoutTimer;
    private float stressTimer;
    private KartNetworkSync networkSync;

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        networkSync = GetComponent<KartNetworkSync>();
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (kart == null || puffPrefab == null || rearLeftPoint == null || rearRightPoint == null)
            return;

        bool shouldEmitBurnout = ShouldEmitBurnout();

        if (shouldEmitBurnout)
        {
            UpdateBurnoutTrail();
            ResetDriftDistanceTracking();
            return;
        }

        burnoutTimer = 0f;

        bool shouldEmitStress = ShouldEmitStress();

        if (shouldEmitStress)
        {
            UpdateStressTrail();
            ResetDriftDistanceTracking();
            return;
        }

        stressTimer = 0f;

        bool shouldEmitDrift = ShouldEmitDrift();

        if (!shouldEmitDrift)
        {
            wasEmittingDrift = false;
            distanceAccumulator = 0f;
            lastPosition = transform.position;
            return;
        }

        UpdateDriftTrail();
    }

    private bool ShouldEmitDrift()
    {
        if (!EffectIsGrounded)
            return false;

        if (EffectSpeedKmh < minSpeedKmh)
            return false;

        if (EffectDriftBlend < minDriftBlend)
            return false;

        return true;
    }

    private bool ShouldEmitBurnout()
    {
        if (!EffectIsGrounded)
            return false;

        return EffectIsBurningOut && EffectSpeedKmh <= burnoutMaxSpeedKmh;
    }

    private bool ShouldEmitStress()
    {
        if (!EffectIsGrounded)
            return false;

        if (EffectIsBurningOut || EffectDriftBlend >= minDriftBlend)
            return false;

        bool abruptSlip = EffectLaunchSlip01 >= minStressFactor || EffectBrakeSlip01 >= minStressFactor;
        if (EffectSpeedKmh < 8f && !abruptSlip)
            return false;

        return EffectTireStress01 >= minStressFactor;
    }

    private void UpdateBurnoutTrail()
    {
        burnoutTimer += Time.deltaTime;

        while (burnoutTimer >= burnoutSpawnInterval)
        {
            SpawnBurnoutStep();
            burnoutTimer -= burnoutSpawnInterval;
        }
    }

    private void UpdateStressTrail()
    {
        stressTimer += Time.deltaTime;

        while (stressTimer >= stressSpawnInterval)
        {
            SpawnStressStep();
            stressTimer -= stressSpawnInterval;
        }
    }

    private void UpdateDriftTrail()
    {
        if (!wasEmittingDrift)
        {
            wasEmittingDrift = true;
            distanceAccumulator = 0f;
            lastPosition = transform.position;

            SpawnDriftStep();
            return;
        }

        Vector3 currentPosition = transform.position;

        Vector3 flatLast = new Vector3(lastPosition.x, 0f, lastPosition.z);
        Vector3 flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);

        float distanceMoved = Vector3.Distance(flatLast, flatCurrent);

        distanceAccumulator += distanceMoved;
        lastPosition = currentPosition;

        float spacing = Mathf.Lerp(lowSpeedSpacing, highSpeedSpacing, EffectSpeed01);

        int safety = 0;

        while (distanceAccumulator >= spacing && safety < maxSpawnStepsPerFrame)
        {
            SpawnDriftStep();
            distanceAccumulator -= spacing;
            safety++;
        }
    }

    private void ResetDriftDistanceTracking()
    {
        wasEmittingDrift = false;
        distanceAccumulator = 0f;
        lastPosition = transform.position;
    }

    private void SpawnDriftStep()
    {
        SpawnPuffCluster(rearLeftPoint, puffsPerWheel, false);
        SpawnPuffCluster(rearRightPoint, puffsPerWheel, false);
    }

    private void SpawnBurnoutStep()
    {
        SpawnPuffCluster(rearLeftPoint, burnoutPuffsPerWheel, true);
        SpawnPuffCluster(rearRightPoint, burnoutPuffsPerWheel, true);
    }

    private void SpawnStressStep()
    {
        SpawnPuffCluster(rearLeftPoint, stressPuffsPerWheel, false);
        SpawnPuffCluster(rearRightPoint, stressPuffsPerWheel, false);
    }

    private void SpawnPuffCluster(Transform spawnPoint, int count, bool isBurnout)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnSinglePuff(spawnPoint, isBurnout);
        }
    }

    private void SpawnSinglePuff(Transform spawnPoint, bool isBurnout)
    {
        float speedFactor = Mathf.Clamp01(EffectSpeed01);
        float driftFactor = Mathf.Clamp01(EffectDriftBlend);
        float stressFactor = Mathf.Clamp01(EffectTireStress01);

        float scatterMultiplier = isBurnout ? burnoutScatterMultiplier : 1f;

        Vector3 randomOffset =
            transform.right * Random.Range(-horizontalScatter, horizontalScatter) * scatterMultiplier +
            transform.forward * Random.Range(-forwardScatter, forwardScatter) * scatterMultiplier +
            Vector3.up * Random.Range(0f, verticalScatter) * scatterMultiplier;

        Vector3 spawnPosition = spawnPoint.position + randomOffset;
        // Só yaw aleatório: a base plana da nuvem permanece voltada para o chão.
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        DriftPuffBubble puff = DriftPuffPool.Obter(puffPrefab, spawnPosition, spawnRotation);

        if (puff == null)
            return;

        float startScale = Random.Range(minStartScale, maxStartScale);
        float endScale = Random.Range(minEndScale, maxEndScale);

        float scaleBoost = Mathf.Lerp(0.9f, 1.25f, speedFactor);
        scaleBoost *= Mathf.Lerp(0.95f, 1.2f, driftFactor);
        scaleBoost *= Mathf.Lerp(1f, 1.15f, stressFactor);

        if (isBurnout)
            scaleBoost *= burnoutSizeMultiplier;

        startScale *= scaleBoost;
        endScale *= scaleBoost;

        float lifetime = Random.Range(minLifetime, maxLifetime);

        Vector3 velocity =
            -transform.forward * Random.Range(backwardVelocity * 0.25f, backwardVelocity) +
            Vector3.up * Random.Range(upwardVelocity * 0.5f, upwardVelocity) * (isBurnout ? burnoutUpwardMultiplier : 1f) +
            transform.right * Random.Range(-sideVelocity, sideVelocity);

        if (isBurnout)
        {
            velocity += -transform.forward * Random.Range(0.05f, 0.25f);
            velocity += transform.right * Random.Range(-0.2f, 0.2f);
        }

        puff.Initialize(
            lifetime,
            startScale,
            endScale,
            velocity,
            isBurnout
        );
    }

    private bool UseSyncedEffectState => networkSync != null && networkSync.UseSyncedEffectState;
    private bool EffectIsGrounded => UseSyncedEffectState ? networkSync.EffectIsGrounded : kart.IsGrounded;
    private bool EffectIsBurningOut => UseSyncedEffectState ? networkSync.EffectIsBurningOut : kart.IsBurningOut;
    private float EffectSpeedKmh => UseSyncedEffectState ? networkSync.EffectSpeedKmh : kart.SpeedKmh;
    private float EffectSpeed01 => UseSyncedEffectState ? networkSync.EffectSpeed01 : kart.Speed01;
    private float EffectDriftBlend => UseSyncedEffectState ? networkSync.EffectDriftBlend : kart.DriftBlend;
    private float EffectTireStress01 => UseSyncedEffectState ? networkSync.EffectTireStress01 : kart.TireStress01;
    private float EffectLaunchSlip01 => UseSyncedEffectState ? networkSync.EffectLaunchSlip01 : kart.LaunchSlip01;
    private float EffectBrakeSlip01 => UseSyncedEffectState ? networkSync.EffectBrakeSlip01 : kart.BrakeSlip01;
}
