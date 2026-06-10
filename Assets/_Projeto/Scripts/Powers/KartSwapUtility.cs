using UnityEngine;

// Troca de posição entre dois karts (power-up Swap/Disco Voador), preservando orientação
// coerente com a pista: cada kart assume o yaw do slot em que aterrissa (sem pitch/roll,
// nunca fica de lado ou de ré) e mantém a própria velocidade ao longo do novo forward —
// ambos continuam correndo normalmente após a troca.
public static class KartSwapUtility
{
    public static void SwapPositions(GameObject a, GameObject b)
    {
        if (a == null || b == null || a == b)
            return;

        Rigidbody bodyA = ResolveBody(a);
        Rigidbody bodyB = ResolveBody(b);

        Vector3 positionA = a.transform.position;
        Vector3 positionB = b.transform.position;

        Quaternion yawA = UprightYaw(a.transform.rotation);
        Quaternion yawB = UprightYaw(b.transform.rotation);

        float forwardSpeedA = bodyA != null ? Vector3.Dot(bodyA.linearVelocity, a.transform.forward) : 0f;
        float forwardSpeedB = bodyB != null ? Vector3.Dot(bodyB.linearVelocity, b.transform.forward) : 0f;

        // A vai para o slot de B (com o yaw que B tinha) e vice-versa.
        Place(bodyA, a.transform, positionB, yawB, forwardSpeedA);
        Place(bodyB, b.transform, positionA, yawA, forwardSpeedB);

        Physics.SyncTransforms();
    }

    private static Rigidbody ResolveBody(GameObject go)
    {
        KartController kart = go.GetComponent<KartController>();
        if (kart != null && kart.Rigidbody != null)
            return kart.Rigidbody;

        return go.GetComponent<Rigidbody>();
    }

    // Mantém apenas o yaw: evita que o kart apareça inclinado/capotado após o teleporte.
    private static Quaternion UprightYaw(Quaternion rotation)
    {
        return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
    }

    private static void Place(Rigidbody body, Transform kartTransform, Vector3 position, Quaternion rotation, float forwardSpeed)
    {
        // Pequena elevação evita interpenetração com o chão no frame do teleporte.
        position += Vector3.up * 0.15f;

        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = rotation * Vector3.forward * Mathf.Max(0f, forwardSpeed);
            body.angularVelocity = Vector3.zero;
        }

        kartTransform.SetPositionAndRotation(position, rotation);
    }
}
