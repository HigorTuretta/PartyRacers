namespace PartyRacers.UI.HUD
{
    /// <summary>Helpers de formatação compartilhados pela HUD de corrida.</summary>
    public static class HUDFormat
    {
        /// <summary>Formata um tempo de volta em mm:ss.mmm. Valores negativos viram traços.</summary>
        public static string LapTime(float seconds)
        {
            if (seconds < 0f)
                return "--:--.---";

            int totalMs = UnityEngine.Mathf.RoundToInt(seconds * 1000f);
            int minutes = totalMs / 60000;
            int secs = (totalMs / 1000) % 60;
            int millis = totalMs % 1000;
            return $"{minutes:00}:{secs:00}.{millis:000}";
        }

        /// <summary>Formato curto mm:ss.mm para cards de leaderboard.</summary>
        public static string LapTimeShort(float seconds)
        {
            if (seconds < 0f)
                return "--:--";

            int totalMs = UnityEngine.Mathf.RoundToInt(seconds * 1000f);
            int minutes = totalMs / 60000;
            int secs = (totalMs / 1000) % 60;
            int hundredths = (totalMs % 1000) / 10;
            return $"{minutes:0}:{secs:00}.{hundredths:00}";
        }
    }
}
