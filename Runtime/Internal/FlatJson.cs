using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AdfactenAds.Internal
{
    /// <summary>
    /// Reads and writes the flat JSON objects passed between C# and the native bridges
    /// (string, number, bool and null values only). Unknown or nested values are skipped.
    /// </summary>
    internal static class FlatJson
    {
        public static Dictionary<string, object> Parse(string json)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json)) return result;
            var p = new Parser(json);
            try
            {
                p.ReadObject(result);
            }
            catch (FormatException)
            {
                // Keep whatever was read before the bad input.
            }
            return result;
        }

        public static string GetString(Dictionary<string, object> map, string key)
        {
            object v;
            if (map == null || !map.TryGetValue(key, out v) || v == null) return null;
            var s = v as string;
            if (s != null) return s;
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            if (v is bool) return (bool)v ? "true" : "false";
            return null;
        }

        public static double GetDouble(Dictionary<string, object> map, string key, double fallback = 0)
        {
            object v;
            if (map == null || !map.TryGetValue(key, out v) || v == null) return fallback;
            if (v is double) return (double)v;
            double d;
            var s = v as string;
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        public static int GetInt(Dictionary<string, object> map, string key, int fallback = 0)
        {
            var d = GetDouble(map, key, double.NaN);
            if (double.IsNaN(d) || d > int.MaxValue || d < int.MinValue) return fallback;
            return (int)Math.Round(d);
        }

        public static bool GetBool(Dictionary<string, object> map, string key, bool fallback = false)
        {
            object v;
            if (map == null || !map.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            if (v is double) return (double)v != 0;
            var s = v as string;
            if (s == "true" || s == "1") return true;
            if (s == "false" || s == "0") return false;
            return fallback;
        }

        /// <summary>Writes a flat object. Values may be string, bool, int, long, float, double or null.</summary>
        public static string Write(IEnumerable<KeyValuePair<string, object>> values)
        {
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in values)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, kv.Key);
                sb.Append(':');
                WriteValue(sb, kv.Value);
            }
            return sb.Append('}').ToString();
        }

        private static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int) { sb.Append(((int)v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is long) { sb.Append(((long)v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is uint) { sb.Append(((uint)v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is float || v is double)
            {
                var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public void ReadObject(Dictionary<string, object> into)
            {
                SkipWs();
                Expect('{');
                SkipWs();
                if (Peek() == '}') { _i++; return; }
                while (true)
                {
                    SkipWs();
                    var key = ReadString();
                    SkipWs();
                    Expect(':');
                    SkipWs();
                    object value;
                    if (TryReadScalar(out value)) into[key] = value;
                    SkipWs();
                    var c = Next();
                    if (c == ',') continue;
                    if (c == '}') return;
                    throw new FormatException("expected , or }");
                }
            }

            private bool TryReadScalar(out object value)
            {
                var c = Peek();
                switch (c)
                {
                    case '"': value = ReadString(); return true;
                    case 't': ReadLiteral("true"); value = true; return true;
                    case 'f': ReadLiteral("false"); value = false; return true;
                    case 'n': ReadLiteral("null"); value = null; return true;
                    case '{': case '[': SkipNested(); value = null; return false;
                    default: value = ReadNumber(); return true;
                }
            }

            private void SkipNested()
            {
                var depth = 0;
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (c == '"') { ReadString(); continue; }
                    _i++;
                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                        if (depth == 0) return;
                    }
                }
                throw new FormatException("unterminated value");
            }

            private double ReadNumber()
            {
                var start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                double d;
                if (_i == start || !double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    throw new FormatException("bad number");
                return d;
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new FormatException("unterminated string");
                    var c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw new FormatException("bad escape");
                    var e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new FormatException("bad unicode escape");
                            int code;
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw new FormatException("bad unicode escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw new FormatException("bad escape");
                    }
                }
            }

            private void ReadLiteral(string lit)
            {
                if (string.CompareOrdinal(_s, _i, lit, 0, lit.Length) != 0) throw new FormatException("bad literal");
                _i += lit.Length;
            }

            private void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private char Peek()
            {
                if (_i >= _s.Length) throw new FormatException("unexpected end");
                return _s[_i];
            }

            private char Next()
            {
                var c = Peek();
                _i++;
                return c;
            }

            private void Expect(char c)
            {
                if (Next() != c) throw new FormatException("expected " + c);
            }
        }
    }
}
