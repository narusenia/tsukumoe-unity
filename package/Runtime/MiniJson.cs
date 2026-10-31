// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tsukumoe
{
    /// <summary>
    /// A small JSON reader for the parity case files (JsonUtility cannot read maps).
    /// Objects are <c>Dictionary&lt;string, object&gt;</c>, arrays <c>List&lt;object&gt;</c>, numbers <c>double</c>.
    /// </summary>
    static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            var v = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("trailing text at " + i);
            return v;
        }

        static void Skip(string t, ref int i)
        {
            while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
        }

        static object Value(string t, ref int i)
        {
            Skip(t, ref i);
            if (i >= t.Length) throw new FormatException("unexpected end");
            switch (t[i])
            {
                case '{':
                {
                    var d = new Dictionary<string, object>();
                    i++;
                    Skip(t, ref i);
                    if (t[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Skip(t, ref i);
                        var key = Str(t, ref i);
                        Skip(t, ref i);
                        Expect(t, ref i, ':');
                        d[key] = Value(t, ref i);
                        Skip(t, ref i);
                        if (t[i] == ',') { i++; continue; }
                        Expect(t, ref i, '}');
                        return d;
                    }
                }
                case '[':
                {
                    var l = new List<object>();
                    i++;
                    Skip(t, ref i);
                    if (t[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(t, ref i));
                        Skip(t, ref i);
                        if (t[i] == ',') { i++; continue; }
                        Expect(t, ref i, ']');
                        return l;
                    }
                }
                case '"': return Str(t, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default:
                {
                    int s = i;
                    while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
                    return double.Parse(t.Substring(s, i - s), CultureInfo.InvariantCulture);
                }
            }
        }

        static void Expect(string t, ref int i, char c)
        {
            if (i >= t.Length || t[i] != c) throw new FormatException($"expected '{c}' at {i}");
            i++;
        }

        // Case files hold plain names, so only the simple escapes are handled.
        static string Str(string t, ref int i)
        {
            Expect(t, ref i, '"');
            var sb = new StringBuilder();
            while (t[i] != '"')
            {
                if (t[i] == '\\') { i++; sb.Append(t[i] == 'n' ? '\n' : t[i]); }
                else sb.Append(t[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
