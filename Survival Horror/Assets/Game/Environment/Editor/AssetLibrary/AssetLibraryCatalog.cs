using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.AssetLibrary
{
    [Serializable] public sealed class LibraryEntry
    {
        public string guid, name, category, status, note, path, source, original, classification;
        public string[] tags = Array.Empty<string>();
        public float[] size = Array.Empty<float>();
    }

    [Serializable] public sealed class LibrarySynonym
    {
        public string canonical;
        public string[] aliases;
    }

    [Serializable] public sealed class LibraryCatalog
    {
        public int version;
        public string[] scope, statuses;
        public LibrarySynonym[] synonyms;
        public LibraryEntry[] entries;
    }

    [Serializable] public sealed class LibraryOverride
    {
        public string guid, name, category, status, note;
        public string[] tags;
    }

    [Serializable] public sealed class LibraryOverrides
    {
        public List<LibraryOverride> entries = new List<LibraryOverride>();
    }

    [Serializable] public sealed class LibraryFilter
    {
        public const string DefaultStatus = "All";
        public string query = "", category = "", status = DefaultStatus, collection = "";
        public bool requireAll = true, favouritesOnly;
        public List<string> include = new List<string>(), exclude = new List<string>();
        public LibraryFilter Copy() => JsonUtility.FromJson<LibraryFilter>(JsonUtility.ToJson(this));
    }

    // GUID references keep this editor-only index independent of prefab names, paths and runtime tags.
    public sealed class LibraryIndex
    {
        public const string Folder = "Assets/Game/Environment/Editor/AssetLibrary";
        public const string CatalogPath = Folder + "/Catalog.json";
        public const string OverridesPath = Folder + "/Overrides.json";
        public readonly LibraryCatalog catalog;
        public readonly LibraryOverrides overrides;
        static readonly Regex CamelCase = new Regex("([a-z])([A-Z])");
        static readonly Regex Separators = new Regex("[^a-z0-9]+");
        static readonly Regex QueryTerms = new Regex("(-?)\"([^\"]+)\"|(-?)([^\\s\"]+)");
        readonly (Regex pattern, string replacement)[] replacements;
        readonly Dictionary<string, string> documents = new Dictionary<string, string>();
        readonly Dictionary<string, LibraryEntry> entriesByGuid;

        public LibraryIndex()
        {
            catalog = JsonUtility.FromJson<LibraryCatalog>(File.ReadAllText(CatalogPath));
            if (catalog == null || catalog.version != 1 || catalog.entries == null)
                throw new InvalidDataException("Unsupported or invalid asset catalogue.");
            overrides = File.Exists(OverridesPath)
                ? JsonUtility.FromJson<LibraryOverrides>(File.ReadAllText(OverridesPath)) : new LibraryOverrides();
            entriesByGuid = catalog.entries.ToDictionary(entry => entry.guid);
            foreach (var edit in overrides.entries)
            {
                if (!entriesByGuid.TryGetValue(edit.guid, out var entry)) continue;
                entry.name = edit.name; entry.category = edit.category; entry.status = edit.status;
                entry.note = edit.note; entry.tags = edit.tags ?? Array.Empty<string>();
            }
            replacements = catalog.synonyms
                .SelectMany(s => s.aliases.Append(s.canonical).Select(alias =>
                    (alias: Normalize(alias), replacement: Normalize(s.canonical).Replace(' ', '_'))))
                .Where(pair => pair.alias.Length > 0)
                .OrderByDescending(pair => pair.alias.Length)
                .Select(pair => (new Regex(@"(?<![a-z0-9_])" + Regex.Escape(pair.alias) + @"(?![a-z0-9_])"), pair.replacement))
                .ToArray();
            var canonicalFields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in catalog.entries)
                documents[entry.guid] = string.Join(" ", new[] {entry.name, entry.category, entry.original}
                    .Concat(entry.tags).Select(field =>
                    {
                        if (!canonicalFields.TryGetValue(field, out string value))
                            canonicalFields[field] = value = Canonicalize(field);
                        return value;
                    }));
        }

        public LibraryEntry Find(string guid) => guid != null && entriesByGuid.TryGetValue(guid, out var entry) ? entry : null;

        public static string Normalize(string input)
        {
            input = CamelCase.Replace(input ?? "", "$1 $2");
            return Separators.Replace(input.ToLowerInvariant(), " ").Trim();
        }

        public string Canonicalize(string input)
        {
            string value = Normalize(input);
            foreach (var pair in replacements)
                value = pair.pattern.Replace(value, pair.replacement);
            return value;
        }

        public bool Matches(LibraryEntry entry, LibraryFilter filter, ISet<string> favourites = null)
            => CreateMatcher(filter, favourites)(entry);

        public Func<LibraryEntry, bool> CreateMatcher(LibraryFilter filter, ISet<string> favourites = null)
        {
            var clauses = ParseQuery(filter.query);
            string categoryPrefix = filter.category + "/";
            return entry =>
            {
                if (filter.favouritesOnly && (favourites == null || !favourites.Contains(entry.guid))) return false;
                if (!string.IsNullOrEmpty(filter.collection) && !entry.tags.Contains(filter.collection)) return false;
                if (!string.IsNullOrEmpty(filter.category) && entry.category != filter.category &&
                    !entry.category.StartsWith(categoryPrefix, StringComparison.Ordinal)) return false;
                if (filter.status == "Placeable")
                {
                    if (entry.status != "Candidate" && entry.status != "Unreviewed") return false;
                }
                else if (!string.IsNullOrEmpty(filter.status) && filter.status != LibraryFilter.DefaultStatus && entry.status != filter.status) return false;
                foreach (string tag in filter.exclude)
                    if (entry.tags.Contains(tag)) return false;
                if (filter.include.Count > 0)
                {
                    bool matched = filter.requireAll;
                    foreach (string tag in filter.include)
                    {
                        if (entry.tags.Contains(tag) == filter.requireAll) continue;
                        matched = !filter.requireAll;
                        break;
                    }
                    if (!matched) return false;
                }
                if (clauses.Count == 0) return true;
                string text = documents[entry.guid];
                foreach (var clause in clauses)
                    if ((text.IndexOf(clause.value, StringComparison.Ordinal) >= 0) == clause.negative) return false;
                return true;
            };
        }

        List<(bool negative, string value)> ParseQuery(string query)
        {
            var clauses = new List<(bool negative, string value)>();
            if (string.IsNullOrWhiteSpace(query)) return clauses;
            // Preserve negative terms and quoted phrases while resolving synonyms inside each clause.
            var terms = QueryTerms.Matches(query);
            // Runs of positive words are canonicalized together (e.g. "cold storage").
            var positive = new List<string>();
            void Flush()
            {
                if (positive.Count == 0) return;
                foreach (string token in Canonicalize(string.Join(" ", positive)).Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries))
                    clauses.Add((false, token));
                positive.Clear();
            }
            foreach (Match term in terms)
            {
                bool quoted = term.Groups[2].Success;
                bool negative = (quoted ? term.Groups[1].Value : term.Groups[3].Value) == "-";
                string value = quoted ? term.Groups[2].Value : term.Groups[4].Value;
                if (!quoted && !negative) { positive.Add(value); continue; }
                Flush();
                string canonical = Canonicalize(value);
                if (canonical.Length > 0) clauses.Add((negative, canonical));
            }
            Flush();
            return clauses;
        }

        public GameObject Resolve(LibraryEntry entry) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.guid));

        public void SaveOverride(LibraryEntry entry, string title, string category, string status, string note, IEnumerable<string> tags)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Name and category are required.");
            var known = catalog.entries.SelectMany(e => e.tags).Distinct().ToArray();
            var resolvedTags = tags.Select(t => t.Trim()).Where(t => Normalize(t).Length > 0)
                .Select(t => known.FirstOrDefault(k => Canonicalize(k) == Canonicalize(t)) ?? t);
            var edit = new LibraryOverride { guid = entry.guid, name = title.Trim(), category = category.Trim(),
                status = status, note = note.Trim(), tags = resolvedTags.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray() };
            overrides.entries.RemoveAll(e => e.guid == entry.guid);
            overrides.entries.Add(edit);
            overrides.entries.Sort((a, b) => string.CompareOrdinal(a.guid, b.guid));
            File.WriteAllText(OverridesPath, JsonUtility.ToJson(overrides, true) + "\n");
            AssetDatabase.ImportAsset(OverridesPath);
        }
    }
}
