using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelStunEffect : MonoBehaviour
{
    private KartController kart;
    private GameObject auraInstance;
    private float stunEndTime;
    private bool controlWasEnabled;
    private bool stunActive;

    public static ElectroGelStunEffect ApplyTo(GameObject target, float duration, GameObject auraPrefab)
    {
        if (target == null)
            return null;

        ElectroGelStunEffect stunEffect = target.GetComponent<ElectroGelStunEffect>();
        if (stunEffect == null)
            stunEffect = target.AddComponent<ElectroGelStunEffect>();

        stunEffect.Begin(duration, auraPrefab);
        return stunEffect;
    }

    private void Awake()
    {
        kart = GetComponent<KartController>();
    }

    private void Begin(float duration, GameObject auraPrefab)
    {
        if (!stunActive)
        {
            controlWasEnabled = kart == null || kart.CanControl;

            if (kart != null)
                kart.SetControlEnabled(false);
        }

        stunActive = true;
        stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);

        if (auraInstance == null && auraPrefab != null)
        {
            auraInstance = Instantiate(auraPrefab, transform);
            auraInstance.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            auraInstance.transform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (!stunActive)
            return;

        if (Time.time < stunEndTime)
            return;

        End();
    }

    private void End()
    {
        stunActive = false;

        if (kart != null && controlWasEnabled)
            kart.SetControlEnabled(true);

        if (auraInstance != null)
            Destroy(auraInstance);

        auraInstance = null;
    }

    private void OnDisable()
    {
        if (stunActive)
            End();
    }
}
