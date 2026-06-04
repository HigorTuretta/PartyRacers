using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PartyRacers.Networking;

#if PARTYRACERS_ONLINE
using Unity.Netcode;
#endif

// Gerencia a largada da corrida. Generalizado de 1 para até 16 karts: a contagem regressiva
// trava/destrava TODOS os karts participantes. Continua funcionando em single-player local
// (basta o playerKart) e já está pronto para múltiplos jogadores/bots.
public class RaceManager : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Kart principal (jogador local). Opcional — karts também são descobertos na cena.")]
    [SerializeField] private KartController playerKart;
    [SerializeField] private TMP_Text countdownText;

    [Header("Descoberta de karts")]
    [Tooltip("Inclui automaticamente todos os KartControllers da cena (bots/remotos/16 jogadores).")]
    [SerializeField] private bool autoCollectKarts = true;

    [Header("Largada")]
    [Tooltip("Posiciona os karts nos pontos do RaceSpawnManager (se houver) ao iniciar.")]
    [SerializeField] private bool placeOnSpawnPoints = false;

    [Header("Configuração")]
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float goMessageDuration = 0.75f;

    [Header("Estado")]
    [SerializeField] private bool raceStarted;

    private readonly List<KartController> karts = new List<KartController>();

    public bool RaceStarted => raceStarted;
    public IReadOnlyList<KartController> Karts => karts;

    public void SetCountdownText(TMP_Text text)
    {
        if (countdownText != null && countdownText != text)
            countdownText.gameObject.SetActive(false);

        countdownText = text;
    }

    public void RegisterKart(KartController kart)
    {
        if (kart == null || karts.Contains(kart))
            return;

        karts.Add(kart);

        if (placeOnSpawnPoints && RaceSpawnManager.Instance != null)
            PlaceKartOnSpawn(kart, karts.Count - 1);

        // Karts que entram após a largada já largam liberados.
        kart.SetControlEnabled(raceStarted);
    }

    public void UnregisterKart(KartController kart)
    {
        karts.Remove(kart);
    }

    private void Start()
    {
        CollectKarts();
        StartCoroutine(StartRaceRoutine());
    }

    private void CollectKarts()
    {
        karts.Clear();

        if (playerKart != null && !ShouldIgnoreKartForOnline(playerKart))
            karts.Add(playerKart);

        if (autoCollectKarts)
        {
            KartController[] found = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
            foreach (KartController kart in found)
            {
                if (kart != null && !karts.Contains(kart) && !ShouldIgnoreKartForOnline(kart))
                    karts.Add(kart);
            }
        }

        if (placeOnSpawnPoints && RaceSpawnManager.Instance != null)
            PlaceKartsOnSpawns();
    }

    private bool ShouldIgnoreKartForOnline(KartController kart)
    {
#if PARTYRACERS_ONLINE
        if (kart == null || NetworkBootstrap.Instance == null || !NetworkBootstrap.Instance.IsOnline)
            return false;

        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            return false;

        kart.gameObject.SetActive(false);
        return true;
#else
        return false;
#endif
    }

    private void PlaceKartsOnSpawns()
    {
        for (int i = 0; i < karts.Count; i++)
            PlaceKartOnSpawn(karts[i], i);
    }

    private void PlaceKartOnSpawn(KartController kart, int fallbackIndex)
    {
        if (kart == null || !ShouldPlaceKartLocally(kart))
            return;

        Pose pose = RaceSpawnManager.Instance.GetSpawnPose(ResolveSpawnIndex(kart, fallbackIndex));

        Rigidbody body = kart.Rigidbody;
        if (body != null)
        {
            body.position = pose.position;
            body.rotation = pose.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        kart.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }

    private int ResolveSpawnIndex(KartController kart, int fallbackIndex)
    {
#if PARTYRACERS_ONLINE
        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            return Mathf.Clamp((int)networkObject.OwnerClientId, 0, RaceConstants.MaxPlayers - 1);
#endif

        return fallbackIndex;
    }

    private bool ShouldPlaceKartLocally(KartController kart)
    {
#if PARTYRACERS_ONLINE
        if (NetworkBootstrap.Instance == null || !NetworkBootstrap.Instance.IsOnline)
            return true;

        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
#else
        return true;
#endif
    }

    private void SetAllControl(bool enabled)
    {
        foreach (KartController kart in karts)
        {
            if (kart != null)
                kart.SetControlEnabled(enabled);
        }
    }

    private IEnumerator StartRaceRoutine()
    {
        raceStarted = false;
        SetAllControl(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);

            countdownText.text = "3";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "2";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "1";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "VAI!";
        }

        raceStarted = true;
        SetAllControl(true);

        yield return new WaitForSeconds(goMessageDuration);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }
}
