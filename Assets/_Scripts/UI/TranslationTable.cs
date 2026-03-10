using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Localization/Translation Table", fileName = "TranslationTable")]
public class TranslationTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        [TextArea] public string[] translations;
    }

    [Tooltip("All translations. Each entry maps a key -> translations[] (match LanguageSwitcher order).")]
    public List<Entry> entries = new();

    private Dictionary<string, string[]> _cache;

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    private void RebuildCache()
    {
        _cache = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.key)) continue;
            _cache[e.key.Trim()] = e.translations;
        }
    }

    public bool TryGetTranslations(string key, out string[] translations)
    {
        translations = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (_cache == null) RebuildCache();
        return _cache.TryGetValue(key.Trim(), out translations);
    }
}

