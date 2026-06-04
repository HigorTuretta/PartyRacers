using UnityEngine;

public class GolfBallImpact : MonoBehaviour
{
    public GameObject impactEffectPrefab;
    public float effectLifetime = 3f;
    public float impactCooldown = 0.1f;

    private float lastImpactTime;

    private void OnCollisionStay(Collision collision)
    {
        // Evita criar muitos efeitos seguidos
        if (Time.time - lastImpactTime < impactCooldown)
            return;

        // Só ignora se realmente não houver movimento
        if (GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < 0.0001f)
            return;

        lastImpactTime = Time.time;

        ContactPoint contact = collision.contacts[0];

        GameObject effect = Instantiate(
            impactEffectPrefab,
            contact.point,
            Quaternion.LookRotation(contact.normal)
        );

        Destroy(effect, effectLifetime);
    }
}