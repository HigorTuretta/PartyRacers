using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Leitor de JSON mínimo, só para o importador de layout.
    ///
    /// Por que não JsonUtility: os `especificacao/layout/*.json` são árvores heterogêneas — o mesmo
    /// campo `states` ora é uma lista de nomes, ora um objeto de descrições — e JsonUtility exige
    /// uma classe por formato. Por que não Newtonsoft: não está no manifest do projeto e puxar um
    /// pacote só para uma ferramenta descartável de editor sai mais caro que estas 150 linhas.
    /// </summary>
    public class JsonValue
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type { get; private set; } = Kind.Null;

        private bool boolValue;
        private double numberValue;
        private string stringValue;
        private List<JsonValue> arrayValue;
        private Dictionary<string, JsonValue> objectValue;

        public static readonly JsonValue Null = new JsonValue();

        public bool IsNull => Type == Kind.Null;
        public bool IsObject => Type == Kind.Object;
        public bool IsArray => Type == Kind.Array;
        public bool IsString => Type == Kind.String;
        public bool IsNumber => Type == Kind.Number;

        public int Count => Type == Kind.Array ? arrayValue.Count
                          : Type == Kind.Object ? objectValue.Count
                          : 0;

        /// <summary>Item do array. Fora de faixa devolve Null em vez de estourar.</summary>
        public JsonValue this[int index] =>
            Type == Kind.Array && index >= 0 && index < arrayValue.Count ? arrayValue[index] : Null;

        /// <summary>Campo do objeto. Ausente devolve Null — o chamador testa com IsNull.</summary>
        public JsonValue this[string key] =>
            Type == Kind.Object && objectValue.TryGetValue(key, out JsonValue v) ? v : Null;

        public bool Has(string key) => Type == Kind.Object && objectValue.ContainsKey(key);

        public IEnumerable<KeyValuePair<string, JsonValue>> Fields =>
            Type == Kind.Object ? (IEnumerable<KeyValuePair<string, JsonValue>>)objectValue
                                : new Dictionary<string, JsonValue>();

        public IEnumerable<JsonValue> Items =>
            Type == Kind.Array ? (IEnumerable<JsonValue>)arrayValue : new List<JsonValue>();

        public string AsString(string fallback = null) =>
            Type == Kind.String ? stringValue
          : Type == Kind.Number ? numberValue.ToString(CultureInfo.InvariantCulture)
          : fallback;

        public float AsFloat(float fallback = 0f) =>
            Type == Kind.Number ? (float)numberValue
          : Type == Kind.String && float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float p) ? p
          : fallback;

        public int AsInt(int fallback = 0) => Type == Kind.Number ? (int)numberValue : fallback;

        public bool AsBool(bool fallback = false) =>
            Type == Kind.Bool ? boolValue
          : Type == Kind.Number ? numberValue != 0d
          : fallback;

        // ------------------------------------------------------------------ Parser

        public static JsonValue Parse(string text)
        {
            int i = 0;
            JsonValue v = ParseValue(text, ref i);
            return v ?? Null;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
                return Null;

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return new JsonValue { Type = Kind.String, stringValue = ParseString(s, ref i) };
                case 't': i += 4; return new JsonValue { Type = Kind.Bool, boolValue = true };
                case 'f': i += 5; return new JsonValue { Type = Kind.Bool, boolValue = false };
                case 'n': i += 4; return Null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, JsonValue>();
            i++; // {

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; break; }

                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ':') i++;

                obj[key] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }

            return new JsonValue { Type = Kind.Object, objectValue = obj };
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var arr = new List<JsonValue>();
            i++; // [

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; break; }

                arr.Add(ParseValue(s, ref i));

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }

            return new JsonValue { Type = Kind.Array, arrayValue = arr };
        }

        private static string ParseString(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length || s[i] != '"')
                return string.Empty;

            i++; // "
            var sb = new StringBuilder();

            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 < s.Length)
                            {
                                sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber));
                                i += 4;
                            }
                            break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }

                i++;
            }

            i++; // "
            return sb.ToString();
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
                i++;

            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
            return new JsonValue { Type = Kind.Number, numberValue = d };
        }
    }
}
