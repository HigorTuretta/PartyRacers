using PartyRacers.AI;
using UnityEngine;

// Piloto automático acionado quando o PLAYER termina a corrida: o carro continua correndo sozinho
// de forma segura (segue o traçado da IA), sem aceitar mais input do jogador e sem parar bruscamente.
// Reutiliza o BotDriverController (mesma física, mesmo seguidor de traçado) em modo calmo, com a
// trava anti-local liberada explicitamente. Se não houver traçado pronto, o BotDriverController
// devolve input neutro e o carro apenas desacelera de forma cinematográfica.
[DisallowMultipleComponent]
public class KartFinishAutopilot : MonoBehaviour
{
    [Header("Condução automática (calma)")]
    [SerializeField, Range(0.3f, 1f)] private float throttleScale = 0.72f;
    [SerializeField, Range(0f, 1f)] private float corneringCaution = 0.65f;
    [SerializeField] private float steerSharpness = 1.4f;
    [SerializeField] private float lookAheadDistance = 13f;

    private bool engaged;

    public static KartFinishAutopilot Engage(KartController kart)
    {
        if (kart == null)
            return null;

        KartFinishAutopilot autopilot = kart.GetComponent<KartFinishAutopilot>();
        if (autopilot == null)
            autopilot = kart.gameObject.AddComponent<KartFinishAutopilot>();

        autopilot.EngageInternal(kart);
        return autopilot;
    }

    private void EngageInternal(KartController kart)
    {
        if (engaged || kart == null)
            return;

        engaged = true;

        BotPathFollower follower = kart.GetComponent<BotPathFollower>();
        if (follower == null)
            follower = kart.gameObject.AddComponent<BotPathFollower>();

        BotDriverController driver = kart.GetComponent<BotDriverController>();
        if (driver == null)
            driver = kart.gameObject.AddComponent<BotDriverController>();

        BotDifficultyProfile profile = new BotDifficultyProfile
        {
            label = "Autopilot",
            throttleScale = throttleScale,
            corneringCaution = corneringCaution,
            steerSharpness = steerSharpness,
            lookAheadDistance = lookAheadDistance,
            steerWander = 0f,
            mistakeChance = 0f
        };

        // allowLocalPlayer = true: é o único caso em que a IA pode assumir o kart do player.
        driver.Initialize(kart, profile, Random.Range(1, 99999), true);
    }
}
