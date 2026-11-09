using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AdfactenAds.Editor
{
    /// <summary>Text edits on Unity's generated Gradle project (Groovy DSL). No Unity APIs, so they are unit tested.</summary>
    internal static class GradlePatcher
    {
        /// <summary>Whether <paramref name="gradle"/> already declares <paramref name="artifact"/> (group:name) with any version.</summary>
        public static bool HasDependency(string gradle, string artifact)
        {
            if (string.IsNullOrEmpty(gradle)) return false;
            return Regex.IsMatch(gradle, "['\"]" + Regex.Escape(artifact) + ":[^'\"]+['\"]");
        }

        /// <summary>Adds <c>implementation '…'</c> lines to the top-level <c>dependencies</c> block (created if missing).</summary>
        public static string AddDependencies(string gradle, IEnumerable<string> specs)
        {
            var lines = new StringBuilder();
            foreach (var spec in specs) lines.Append("    implementation '").Append(spec).Append("' // Adfacten\n");
            if (lines.Length == 0) return gradle;
            var open = FindBlock(gradle, new[] { "dependencies" });
            if (open < 0) return gradle.TrimEnd() + "\n\ndependencies {\n" + lines + "}\n";
            return gradle.Insert(open + 1, "\n" + lines.ToString().TrimEnd('\n'));
        }

        /// <summary>
        /// Adds the Maven repository to <c>dependencyResolutionManagement { repositories { … } }</c> (settings.gradle,
        /// Unity 2022.2+) or <c>allprojects { repositories { … } }</c> (build.gradle, older Unity). Returns null when
        /// the file has neither block.
        /// </summary>
        public static string AddRepository(string gradle, string url)
        {
            if (gradle.Contains(url)) return gradle;
            var open = FindBlock(gradle, new[] { "dependencyResolutionManagement", "repositories" });
            if (open < 0) open = FindBlock(gradle, new[] { "allprojects", "repositories" });
            if (open < 0) return null;
            return gradle.Insert(open + 1, "\n        maven { url '" + url + "' } // Adfacten");
        }

        /// <summary>Marks the AD_ID permission for removal in the manifest merge (child-directed apps).</summary>
        public static string RemoveAdIdPermission(string manifest)
        {
            const string permission = "com.google.android.gms.permission.AD_ID";
            if (Regex.IsMatch(manifest, Regex.Escape(permission) + "\"[^>]*tools:node=\"remove\"")) return manifest;
            var m = Regex.Match(manifest, "<manifest\\b[^>]*>");
            if (!m.Success) return manifest;
            var tag = m.Value;
            var newTag = tag;
            if (!tag.Contains("xmlns:tools"))
                newTag = tag.Substring(0, tag.Length - 1).TrimEnd() + " xmlns:tools=\"http://schemas.android.com/tools\">";
            var line = "\n    <uses-permission android:name=\"" + permission + "\" tools:node=\"remove\" />";
            return manifest.Substring(0, m.Index) + newTag + line + manifest.Substring(m.Index + tag.Length);
        }

        /// <summary>
        /// Index of the opening brace of the nested block <paramref name="path"/> (each name directly inside the
        /// previous block), or -1. Comments and strings are skipped.
        /// </summary>
        internal static int FindBlock(string text, string[] path)
        {
            var depth = 0;
            var level = 0; // how many names of the path are matched
            var matchedDepth = new int[path.Length];
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    i = text.IndexOf('\n', i);
                    if (i < 0) break;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    i = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (i < 0) break;
                    i += 2;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    i = SkipString(text, i);
                    continue;
                }
                if (c == '{')
                {
                    depth++;
                    i++;
                    continue;
                }
                if (c == '}')
                {
                    depth--;
                    while (level > 0 && depth < matchedDepth[level - 1]) level--;
                    i++;
                    continue;
                }
                if (char.IsLetter(c) && (i == 0 || !IsIdent(text[i - 1])))
                {
                    var end = i;
                    while (end < text.Length && IsIdent(text[end])) end++;
                    var word = text.Substring(i, end - i);
                    if (level < path.Length && word == path[level] && depth == (level == 0 ? 0 : matchedDepth[level - 1]))
                    {
                        var j = end;
                        while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                        if (j < text.Length && text[j] == '{')
                        {
                            level++;
                            if (level == path.Length) return j;
                            matchedDepth[level - 1] = depth + 1;
                            depth++;
                            i = j + 1;
                            continue;
                        }
                    }
                    i = end;
                    continue;
                }
                i++;
            }
            return -1;
        }

        private static bool IsIdent(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.';
        }

        private static int SkipString(string text, int start)
        {
            var quote = text[start];
            var i = start + 1;
            while (i < text.Length)
            {
                if (text[i] == '\\') { i += 2; continue; }
                if (text[i] == quote) return i + 1;
                i++;
            }
            return text.Length;
        }
    }
}
