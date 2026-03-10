using System;
using UnityEngine;

public static class LocalizationManager
{
    public const string SelectedLanguagePrefKey = "SelectedLanguage";

    public static event Action<int> LanguageChanged;

    public static int CurrentLanguageIndex { get; private set; } = 0;

    public static TranslationTable Table { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // Ensure we load any saved language as early as possible.
        var saved = PlayerPrefs.GetInt(SelectedLanguagePrefKey, 0);
        SetLanguage(saved, save: false, notify: false);
    }

    public static void SetTable(TranslationTable table, bool notify = true)
    {
        if (ReferenceEquals(Table, table))
            return;

        Table = table;
        if (notify)
            LanguageChanged?.Invoke(CurrentLanguageIndex);
    }

    public static void SetLanguage(int index, bool save = true, bool notify = true)
    {
        if (index < 0) index = 0;

        if (CurrentLanguageIndex == index)
            return;

        CurrentLanguageIndex = index;

        if (save)
        {
            PlayerPrefs.SetInt(SelectedLanguagePrefKey, CurrentLanguageIndex);
            PlayerPrefs.Save();
        }

        if (notify)
            LanguageChanged?.Invoke(CurrentLanguageIndex);
    }

    public static bool TryGet(string key, int languageIndex, out string value)
    {
        value = null;
        if (Table == null) return false;

        if (!Table.TryGetTranslations(key, out var translations) || translations == null || translations.Length == 0)
            return false;

        if (languageIndex < 0 || languageIndex >= translations.Length)
            languageIndex = 0;

        value = translations[languageIndex];
        return !string.IsNullOrEmpty(value);
    }
}

