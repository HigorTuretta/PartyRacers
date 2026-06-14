using PartyRacers.Networking;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class KartTireMarks : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartController kart;
    [SerializeField] private Transform rearLeftPoint;
    [SerializeField] private Transform rearRightPoint;

    [Header("Ativacao")]
    [SerializeField] private float minIntensity = 0.25f;
    [SerializeField] private float minSpeedKmh = 8f;

    [Header("Marca")]
    [SerializeField] private float markWidth = 0.16f;
    [SerializeField] private float markLifetime = 8f;
    [SerializeField] private float groundOffset = 0.025f;
    [SerializeField] private Color markColor = new Color(0.02f, 0.018f, 0.015f, 0.55f);

    [Header("Chao")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayDistance = 1.4f;
    [SerializeField] private float validGroundNormalY = 0.25f;

    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private readonly WheelMarkState leftMark = new WheelMarkState();
    private readonly WheelMarkState rightMark = new WheelMarkState();
    private Material markMaterial;
    private KartNetworkSync networkSync;

    private class WheelMarkState
    {
        public TrailRenderer Trail;
        public GameObject Segment;
    }

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        networkSync = GetComponent<KartNetworkSync>();

        if (rearLeftPoint == null)
            rearLeftPoint = FindChildByName("SmokePoint_RearLeft");

        if (rearRightPoint == null)
            rearRightPoint = FindChildByName("SmokePoint_RearRight");

        markMaterial = CreateMarkMaterial();
    }

    private void LateUpdate()
    {
        if (kart == null || rearLeftPoint == null || rearRightPoint == null)
        {
            StopMark(leftMark);
            StopMark(rightMark);
            return;
        }

        float intensity = Mathf.Clamp01(EffectTireStress01);
        bool abruptSlip = EffectLaunchSlip01 >= minIntensity || EffectBrakeSlip01 >= minIntensity;
        bool shouldMark = EffectIsGrounded
            && intensity >= minIntensity
            && (EffectSpeedKmh >= minSpeedKmh || EffectIsBurningOut || abruptSlip);

        if (!shouldMark)
        {
            StopMark(leftMark);
            StopMark(rightMark);
            return;
        }

        UpdateWheelMark(leftMark, rearLeftPoint, intensity);
        UpdateWheelMark(rightMark, rearRightPoint, intensity);
    }

    private void UpdateWheelMark(WheelMarkState state, Transform wheelPoint, float intensity)
    {
        Vector3 markPosition = GetGroundPosition(wheelPoint);
        TrailRenderer trail = EnsureTrail(state, markPosition);

        trail.transform.position = markPosition;
        trail.emitting = true;

        float width = markWidth * Mathf.Lerp(0.55f, 1.1f, intensity);
        trail.startWidth = width;
        trail.endWidth = width;

        Color currentColor = markColor;
        currentColor.a *= Mathf.Lerp(0.45f, 1f, intensity);
        trail.startColor = currentColor;
        trail.endColor = currentColor;
    }

    private TrailRenderer EnsureTrail(WheelMarkState state, Vector3 startPosition)
    {
        if (state.Trail != null)
            return state.Trail;

        GameObject segment = new GameObject("Kart_TireMarkSegment");
        segment.transform.position = startPosition;

        TrailRenderer trail = segment.AddComponent<TrailRenderer>();
        trail.material = markMaterial;
        trail.time = markLifetime;
        trail.minVertexDistance = 0.035f;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 0;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;
        trail.emitting = false;

        state.Segment = segment;
        state.Trail = trail;

        return trail;
    }

    private void StopMark(WheelMarkState state)
    {
        if (state.Trail == null)
            return;

        state.Trail.emitting = false;

        if (state.Segment != null)
            Destroy(state.Segment, markLifetime + 0.35f);

        state.Trail = null;
        state.Segment = null;
    }

    private Vector3 GetGroundPosition(Transform wheelPoint)
    {
        Vector3 origin = wheelPoint.position + Vector3.up * 0.35f;
        Ray ray = new Ray(origin, Vector3.down);
        int hitCount = Physics.RaycastNonAlloc(ray, groundHits, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        Vector3 bestPoint = wheelPoint.position - transform.up * 0.2f;
        Vector3 bestNormal = Vector3.up;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null)
                continue;

            if (hit.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y < validGroundNormalY)
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            bestPoint = hit.point;
            bestNormal = hit.normal;
        }

        return bestPoint + bestNormal * groundOffset;
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private Material CreateMarkMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader);
        material.name = "Runtime_TireMark_Material";
        material.color = Color.white;
        material.renderQueue = 3000;

        return material;
    }

    private void OnDestroy()
    {
        StopMark(leftMark);
        StopMark(rightMark);

        if (markMaterial != null)
            Destroy(markMaterial);
    }

    private bool UseSyncedEffectState => networkSync != null && networkSync.UseSyncedEffectState;
    private bool EffectIsGrounded => UseSyncedEffectState ? networkSync.EffectIsGrounded : kart.IsGrounded;
    private bool EffectIsBurningOut => UseSyncedEffectState ? networkSync.EffectIsBurningOut : kart.IsBurningOut;
    private float EffectSpeedKmh => UseSyncedEffectState ? networkSync.EffectSpeedKmh : kart.SpeedKmh;
    private float EffectTireStress01 => UseSyncedEffectState ? networkSync.EffectTireStress01 : kart.TireStress01;
    private float EffectLaunchSlip01 => UseSyncedEffectState ? networkSync.EffectLaunchSlip01 : kart.LaunchSlip01;
    private float EffectBrakeSlip01 => UseSyncedEffectState ? networkSync.EffectBrakeSlip01 : kart.BrakeSlip01;
}
