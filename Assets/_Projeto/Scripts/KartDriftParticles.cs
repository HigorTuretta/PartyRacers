using UnityEngine;

public class KartDriftParticles : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;

    [Header("Particles")]
    [SerializeField] private ParticleSystem rearLeftSmoke;
    [SerializeField] private ParticleSystem rearRightSmoke;

    [Header("Configuração")]
    [SerializeField] private float minSpeedKmh = 18f;
    [SerializeField] private float minDriftBlend = 0.12f;

    [SerializeField] private float maxRateOverDistance = 38f;
    [SerializeField] private float maxRateOverTime = 16f;

    [SerializeField] private float smokeSmooth = 10f;

    [Header("Escala Visual")]
    [SerializeField] private float minStartSize = 0.6f;
    [SerializeField] private float maxStartSize = 1.2f;

    [SerializeField] private float minStartSpeed = 0.02f;
    [SerializeField] private float maxStartSpeed = 0.18f;

    private float currentRateDistance;
    private float currentRateTime;

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        InitializeParticleSystem(rearLeftSmoke);
        InitializeParticleSystem(rearRightSmoke);
    }

    private void Update()
    {
        if (kart == null)
            return;

        float targetFactor = CalculateTargetFactor();

        float targetRateDistance = maxRateOverDistance * targetFactor;
        float targetRateTime = maxRateOverTime * targetFactor;

        currentRateDistance = Mathf.Lerp(currentRateDistance, targetRateDistance, Time.deltaTime * smokeSmooth);
        currentRateTime = Mathf.Lerp(currentRateTime, targetRateTime, Time.deltaTime * smokeSmooth);

        float sizeFactor = Mathf.Clamp01((kart.TireStress01 * 0.85f) + (kart.Speed01 * 0.35f));
        float currentSize = Mathf.Lerp(minStartSize, maxStartSize, sizeFactor);
        float currentSpeed = Mathf.Lerp(minStartSpeed, maxStartSpeed, sizeFactor);

        UpdateParticleSystem(rearLeftSmoke, currentRateTime, currentRateDistance, currentSize, currentSpeed);
        UpdateParticleSystem(rearRightSmoke, currentRateTime, currentRateDistance, currentSize, currentSpeed);
    }

    private float CalculateTargetFactor()
    {
        if (!kart.IsGrounded)
            return 0f;

        if (kart.SpeedKmh < minSpeedKmh && !kart.IsBurningOut)
            return 0f;

        float tireStress = kart.TireStress01;

        if (tireStress < minDriftBlend)
            return 0f;

        float speedFactor = kart.Speed01;

        return Mathf.Clamp01((tireStress * 0.9f) + (speedFactor * 0.25f));
    }

    private void UpdateParticleSystem(ParticleSystem ps, float rateTime, float rateDistance, float startSize, float startSpeed)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.rateOverTime = rateTime;
        emission.rateOverDistance = rateDistance;

        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.75f, startSize);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed * 0.7f, startSpeed);

        if (rateTime > 0.5f || rateDistance > 0.5f)
        {
            if (!ps.isPlaying)
                ps.Play();
        }
        else
        {
            if (ps.isPlaying)
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void InitializeParticleSystem(ParticleSystem ps)
    {
        if (ps == null)
            return;

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
    }
}
