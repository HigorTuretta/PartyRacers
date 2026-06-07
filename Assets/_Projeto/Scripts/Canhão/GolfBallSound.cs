using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GolfBallSound : MonoBehaviour
{
    public AudioClip bounceSound;

    [Range(0.1f, 10f)]
    public float minImpactVelocity = 1f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Ignora impactos muito fracos
        if (collision.relativeVelocity.magnitude < minImpactVelocity)
            return;

        // Volume proporcional à força da batida
        float volume = Mathf.Clamp01(
            collision.relativeVelocity.magnitude / 10f
        );

        audioSource.PlayOneShot(bounceSound, volume);
    }
}