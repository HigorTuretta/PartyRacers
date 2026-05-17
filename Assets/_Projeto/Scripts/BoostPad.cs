using UnityEngine;

public class BoostPad : MonoBehaviour
{
    [Header("Boost")]
    [SerializeField] private float boostDuration = 1.2f;
    [SerializeField] private float speedMultiplier = 1.45f;
    [SerializeField] private float accelerationMultiplier = 1.8f;
    [SerializeField] private float instantPush = 7f;

    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponentInParent<KartController>();

        if (kart == null)
            return;

        kart.ApplyBoost(boostDuration, speedMultiplier, accelerationMultiplier, instantPush);
    }
}