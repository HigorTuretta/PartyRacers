using UnityEngine;

// Toggles the local-player-only sub-hierarchy (camera, HUD, input handler) on/off.
// Hosts the IsLocalPlayer flag the netcode layer will flip later — for now stays true.
public class KartLocalRig : MonoBehaviour
{
    [SerializeField] private GameObject localPlayerRig;
    [SerializeField] private bool isLocalPlayer = true;

    public bool IsLocalPlayer
    {
        get => isLocalPlayer;
        set
        {
            isLocalPlayer = value;
            Apply();
        }
    }

    private void Awake() => Apply();

    private void Apply()
    {
        if (localPlayerRig != null)
            localPlayerRig.SetActive(isLocalPlayer);
    }
}
