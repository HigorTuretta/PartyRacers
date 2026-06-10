using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Perfil de dificuldade/personalidade de um bot. Serializável e editável no Inspector
    /// (lista no RaceBotManager). Cada bot recebe um perfil + pequenas variações por seed,
    /// para não parecerem clones.
    /// </summary>
    [System.Serializable]
    public class BotDifficultyProfile
    {
        [Tooltip("Apenas para leitura no Inspector.")]
        public string label = "Normal";

        [Header("Velocidade")]
        [Tooltip("Multiplicador do acelerador alvo (1 = full). <1 deixa o bot mais lento.")]
        [Range(0.45f, 1.1f)] public float throttleScale = 1f;

        [Tooltip("Quanto o bot alivia/freia em curvas (0 = não freia, 1 = freia muito).")]
        [Range(0f, 1f)] public float corneringCaution = 0.5f;

        [Header("Direção")]
        [Tooltip("Responsividade da direção. Maior = vira mais forte para o alvo.")]
        [Range(0.5f, 3f)] public float steerSharpness = 1.5f;

        [Tooltip("Distância à frente que o bot mira no traçado (suaviza curvas).")]
        [Range(4f, 30f)] public float lookAheadDistance = 13f;

        [Header("Imperfeição (deixa o bot humano/variado)")]
        [Tooltip("Amplitude do erro aleatório de direção.")]
        [Range(0f, 0.35f)] public float steerWander = 0.07f;

        [Tooltip("Frequência da oscilação do erro de direção.")]
        [Range(0.1f, 3f)] public float wanderFrequency = 0.6f;

        [Tooltip("Chance por segundo de um pequeno erro (aliviar o acelerador).")]
        [Range(0f, 1f)] public float mistakeChance = 0.04f;

        public BotDifficultyProfile Clone()
        {
            return (BotDifficultyProfile)MemberwiseClone();
        }

        /// <summary>Aplica pequenas variações determinísticas (por seed) para diferenciar bots do mesmo perfil.</summary>
        public BotDifficultyProfile Varied(int seed)
        {
            var rng = new System.Random(seed);
            float Jitter(float range) => (float)(rng.NextDouble() * 2.0 - 1.0) * range;

            var p = Clone();
            p.throttleScale = Mathf.Clamp(p.throttleScale + Jitter(0.05f), 0.45f, 1.1f);
            p.steerSharpness = Mathf.Clamp(p.steerSharpness + Jitter(0.25f), 0.5f, 3f);
            p.lookAheadDistance = Mathf.Clamp(p.lookAheadDistance + Jitter(2.5f), 4f, 30f);
            p.steerWander = Mathf.Clamp(p.steerWander + Jitter(0.03f), 0f, 0.35f);
            p.corneringCaution = Mathf.Clamp01(p.corneringCaution + Jitter(0.1f));
            return p;
        }
    }
}
