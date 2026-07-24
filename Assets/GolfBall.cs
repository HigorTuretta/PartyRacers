using UnityEngine;

public class GolfBall : MonoBehaviour
{
    private bool destroyed = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (destroyed) return;

        GolfBall other = collision.gameObject.GetComponent<GolfBall>();

        if (other == null || other.destroyed)
            return;

        destroyed = true;
        other.destroyed = true;

        Destroy(other.gameObject);
        Destroy(gameObject);
    }
}