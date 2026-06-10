using UnityEngine;

public static class PowerVFXUtility
{
    public static GameObject SpawnOneShot(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float fallbackLifetime,
        float extraDelay = 0f,
        float scale = 1f)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        if (!Mathf.Approximately(scale, 1f))
            instance.transform.localScale *= scale;

        float lifetime = Mathf.Max(0.05f, fallbackLifetime);
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);

            ps.Clear(true);
            ps.Play(true);
        }

        foreach (TrailRenderer trail in instance.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trail == null)
                continue;

            lifetime = Mathf.Max(lifetime, trail.time);
            trail.Clear();
            trail.emitting = true;
        }

        Object.Destroy(instance, lifetime + Mathf.Max(0f, extraDelay));
        return instance;
    }

    public static void StopAndDestroyTrail(Transform trailInstance, float lingerSeconds)
    {
        if (trailInstance == null)
            return;

        foreach (ParticleSystem ps in trailInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (TrailRenderer trail in trailInstance.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trail == null)
                continue;

            trail.emitting = false;
        }

        Object.Destroy(trailInstance.gameObject, Mathf.Max(0.05f, lingerSeconds));
    }
}
