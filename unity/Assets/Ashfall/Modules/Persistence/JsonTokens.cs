using System;
using System.Collections.Generic;
using System.Text;

namespace Ashfall.Modules.Persistence
{
    // Retains raw JSON values: migration never rounds numbers or drops unfamiliar objects.
    internal sealed class JsonTokens
    {
        private readonly string text;
        private int at;
        private JsonTokens(string value) { text = value; }
        internal static Dictionary<string, string> Object(string text)
        {
            if (text == null || text.Length > 4194304) throw new FormatException("JSON size");
            var reader = new JsonTokens(text);
            var result = reader.ReadObject(0);
            reader.Space();
            if (reader.at != text.Length) throw new FormatException("Trailing JSON");
            return result;
        }
        internal static string String(string token)
        {
            var reader = new JsonTokens(token);
            string result = reader.ReadString(); reader.Space();
            if (reader.at != token.Length) throw new FormatException("Expected string");
            return result;
        }
        internal static string Quote(string value)
        {
            ValidateUnicode(value);
            var b = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < 32 || char.IsSurrogate(c)) b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
        internal static void ValidateUnicode(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            for (int i = 0; i < value.Length; i++)
            {
                if (Char.IsHighSurrogate(value[i]))
                {
                    if (++i >= value.Length || !Char.IsLowSurrogate(value[i])) throw new FormatException("Unpaired high surrogate");
                }
                else if (Char.IsLowSurrogate(value[i])) throw new FormatException("Unpaired low surrogate");
            }
        }
        internal static string Encode(IDictionary<string, string> raw)
        {
            var b = new StringBuilder("{"); bool first = true;
            foreach (string key in new SortedSet<string>(raw.Keys, StringComparer.Ordinal))
            { if (!first) b.Append(','); first = false; b.Append(Quote(key)).Append(':').Append(raw[key]); }
            return b.Append('}').ToString();
        }
        private void Space() { while (at < text.Length && (text[at] == ' ' || text[at] == '\r' || text[at] == '\n' || text[at] == '\t')) at++; }
        private char Peek() { Space(); return at < text.Length ? text[at] : '\0'; }
        private void Need(char c) { if (Peek() != c) throw new FormatException("Expected " + c); at++; }
        private Dictionary<string, string> ReadObject(int depth)
        {
            if (depth > 64) throw new FormatException("JSON depth");
            Need('{'); var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Peek() == '}') { at++; return result; }
            do
            {
                string key = ReadString(); Need(':'); Space(); int start = at;
                Value(depth + 1);
                if (result.ContainsKey(key)) throw new FormatException("Duplicate key");
                result.Add(key, text.Substring(start, at - start));
                if (Peek() == '}') { at++; return result; }
                Need(',');
            } while (true);
        }
        private string ReadString()
        {
            Need('"'); var b = new StringBuilder();
            while (at < text.Length)
            {
                char c = text[at++]; if (c == '"') { string value = b.ToString(); ValidateUnicode(value); return value; }
                if (c < 32) throw new FormatException("String control");
                if (c != '\\') { b.Append(c); continue; }
                if (at == text.Length) break;
                c = text[at++];
                switch (c)
                {
                    case '"': case '\\': case '/': b.Append(c); break;
                    case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                    case 'u':
                        if (at + 4 > text.Length) throw new FormatException("Unicode escape");
                        int code;
                        if (!Int32.TryParse(text.Substring(at, 4), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out code)) throw new FormatException("Unicode escape");
                        b.Append((char)code); at += 4; break;
                    default: throw new FormatException("Escape");
                }
            }
            throw new FormatException("Unterminated string");
        }
        private void Value(int depth)
        {
            if (depth > 64) throw new FormatException("JSON depth");
            char c = Peek();
            if (c == '{') { ReadObject(depth); return; }
            if (c == '"') { ReadString(); return; }
            if (c == '[')
            {
                at++; if (Peek() == ']') { at++; return; }
                do { Value(depth + 1); if (Peek() == ']') { at++; return; } Need(','); } while (true);
            }
            foreach (string literal in new[] { "true", "false", "null" })
                if (at + literal.Length <= text.Length && text.Substring(at, literal.Length) == literal) { at += literal.Length; return; }
            if (c == '-') at++;
            if (at >= text.Length) throw new FormatException("Number");
            if (text[at] == '0') at++;
            else { if (text[at] < '1' || text[at] > '9') throw new FormatException("Number"); Digits(); }
            if (at < text.Length && text[at] == '.') { at++; RequireDigits(); }
            if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
            { at++; if (at < text.Length && (text[at] == '+' || text[at] == '-')) at++; RequireDigits(); }
        }
        private void Digits() { while (at < text.Length && text[at] >= '0' && text[at] <= '9') at++; }
        private void RequireDigits() { int start = at; Digits(); if (start == at) throw new FormatException("Digits"); }
    }
}
