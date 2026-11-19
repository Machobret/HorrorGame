using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    // The project catalog covers authored English text and legacy display code that
    // does not already point to a Unity Localization string table.
    public static class GameText
    {
        private sealed class Binding
        {
            public string Source;
            public string Displayed;
            public bool Korean;
        }

        private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
        private static readonly List<(Regex pattern, string replacement)> Formats = new();
        private static readonly Dictionary<TMP_Text, Binding> Bindings = new();
        private static readonly HashSet<TMP_Text> Pending = new();
        private static Font koreanFont;
        private static TMP_FontAsset fallback;
        private static bool korean;

        public static Font DisplayFont(Font original) => korean && koreanFont ? koreanFont : original;

        public static GUIStyle DisplayStyle(GUIStyle original) =>
            korean && koreanFont ? new GUIStyle(original) { font = koreanFont } : original;

        public static string Translate(string source)
        {
            if (!korean || string.IsNullOrEmpty(source))
                return source;

            if (Strings.TryGetValue(source, out string translation))
                return translation;

            foreach (var format in Formats)
            {
                Match match = format.pattern.Match(source);
                if (match.Success)
                {
                    string result = match.Result(format.replacement);
                    if (match.Groups["label"].Success)
                        result = result.Replace("{label}", Translate(match.Groups["label"].Value));
                    if (match.Groups["log"].Success)
                        result = result.Replace("{log}", string.Join("\n", Array.ConvertAll(
                            match.Groups["log"].Value.Split('\n'), Translate)));
                    return result;
                }
            }

            return source;
        }

        public static void SetLabel(TMP_Text label, string source)
        {
            bool tracked = Bindings.TryGetValue(label, out var binding);
            if (!tracked)
                Bindings[label] = binding = new Binding();
            if (!tracked || binding.Source != source || binding.Korean != korean)
            {
                binding.Source = source;
                binding.Displayed = Translate(source);
                binding.Korean = korean;
            }

            if (label.text != binding.Displayed)
                label.text = binding.Displayed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Strings.Clear();
            Formats.Clear();
            Bindings.Clear();
            Pending.Clear();

            foreach (var entry in ReadCatalog("KoreanGameText"))
                Strings.Add(entry.source, entry.target.Replace(@"\n", "\n"));
            foreach (var entry in ReadCatalog("KoreanGameTextFormats", false))
                Formats.Add((new Regex(entry.source, RegexOptions.CultureInvariant), entry.target.Replace(@"\n", "\n")));

            koreanFont = Resources.Load<Font>("NotoSansKR");
            if (!fallback && koreanFont)
                fallback = TMP_FontAsset.CreateFontAsset(koreanFont);
            if (fallback && TMP_Settings.instance)
            {
                TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
                if (!TMP_Settings.fallbackFontAssets.Contains(fallback))
                    TMP_Settings.fallbackFontAssets.Add(fallback);
            }

            LocalizationSettings.SelectedLocaleChanged -= ChangeLocale;
            LocalizationSettings.SelectedLocaleChanged += ChangeLocale;
            SceneManager.sceneLoaded -= RegisterScene;
            SceneManager.sceneLoaded += RegisterScene;
            SceneManager.sceneUnloaded -= UnregisterScene;
            SceneManager.sceneUnloaded += UnregisterScene;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
            Canvas.preWillRenderCanvases -= Flush;
            Canvas.preWillRenderCanvases += Flush;
            korean = LocalizationSettings.SelectedLocale?.Identifier.Code == "ko";
            // Keep archived locale assets available to the Localization editor.
            LocalizationSettings.AvailableLocales.Locales.RemoveAll(
                locale => locale.Identifier.Code != "en" && locale.Identifier.Code != "ko");
        }

        private static IEnumerable<(string source, string target)> ReadCatalog(string name, bool decodeSource = true)
        {
            var file = Resources.Load<TextAsset>(name);
            if (!file)
                yield break;

            foreach (string row in file.text.TrimStart('\uFEFF').Split('\n'))
            {
                string line = row.TrimEnd('\r');
                int delimiter = line.IndexOf('|');
                if (delimiter < 0)
                    continue;
                string source = line.Substring(0, delimiter);
                yield return (decodeSource ? source.Replace(@"\n", "\n") : source, line.Substring(delimiter + 1));
            }
        }

        private static void RegisterScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    Refresh(label);
        }

        private static void UnregisterScene(Scene scene)
        {
            var labels = new List<TMP_Text>(Bindings.Keys);
            foreach (var label in labels)
                if (!label || label.gameObject.scene == scene)
                    Bindings.Remove(label);
        }

        private static void TextChanged(UnityEngine.Object changed)
        {
            if (changed is TMP_Text label && label && label.gameObject.scene.IsValid())
                Pending.Add(label);
        }

        private static void Flush()
        {
            foreach (var label in Pending)
                if (label)
                    Refresh(label);
            Pending.Clear();
        }

        private static void ChangeLocale(Locale locale)
        {
            korean = locale?.Identifier.Code == "ko";
            var labels = new List<TMP_Text>(Bindings.Keys);
            foreach (var label in labels)
            {
                if (label)
                    Refresh(label);
                else
                    Bindings.Remove(label);
            }
        }

        private static void Refresh(TMP_Text label)
        {
            if (fallback && !TMP_Settings.instance && label.font && label.font != fallback)
            {
                var fonts = label.font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!fonts.Contains(fallback))
                    fonts.Add(fallback);
            }

            string source = label.text;
            bool tracked = Bindings.TryGetValue(label, out var binding);
            if (tracked && source == binding.Displayed)
                source = binding.Source;

            bool localizable = Strings.ContainsKey(source ?? "");
            if (!localizable && !string.IsNullOrEmpty(source))
                localizable = Formats.Exists(format => format.pattern.IsMatch(source));
            // Retain the English source while it is displayed so a locale switch
            // can refresh the label in place even if nothing else changes it.
            if (localizable)
                SetLabel(label, source);
            else if (tracked)
            {
                Bindings.Remove(label);
            }
        }
    }
}
