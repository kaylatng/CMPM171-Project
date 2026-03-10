using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedTMPText : MonoBehaviour
{
    [Header("Key (recommended)")]
    [Tooltip("Lookup key in the active TranslationTable.")]
    public string key;

    [Tooltip("If true and Key is empty, auto-generate key from hierarchy path.")]
    public bool autoKeyFromHierarchyPath = true;

    [Header("Fallback (legacy per-element translations)")]
    [Tooltip("Optional fallback if no table/key translation is found. Match LanguageSwitcher order.")]
    [TextArea] public string[] translations;

    private TextMeshProUGUI _tmp;
    private string _originalText;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _originalText = _tmp != null ? _tmp.text : null;
    }

    private void Start()
    {
        // In case a bootstrapper sets the table after scene load.
        Apply(LocalizationManager.CurrentLanguageIndex);
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Apply(LocalizationManager.CurrentLanguageIndex);
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(int languageIndex)
    {
        Apply(languageIndex);
    }

    private void Apply(int languageIndex)
    {
        if (_tmp == null)
            return;

        // Treat language index 0 as "original scene text" (English).
        if (languageIndex == 0)
        {
            if (_originalText != null)
                _tmp.text = _originalText;
            return;
        }

        var effectiveKey = key;
        if (string.IsNullOrWhiteSpace(effectiveKey) && autoKeyFromHierarchyPath)
            effectiveKey = GetHierarchyPath(transform);

        if (!string.IsNullOrWhiteSpace(effectiveKey) &&
            LocalizationManager.TryGet(effectiveKey, languageIndex, out var fromTable))
        {
            _tmp.text = fromTable;
            return;
        }

        if (translations == null || translations.Length == 0)
        {
            // No translation found: fall back to original scene text.
            if (_originalText != null)
                _tmp.text = _originalText;
            return;
        }

        if (languageIndex < 0 || languageIndex >= translations.Length)
            languageIndex = 0;

        var value = translations[languageIndex];
        if (!string.IsNullOrEmpty(value))
        {
            _tmp.text = value;
            return;
        }

        if (_originalText != null)
            _tmp.text = _originalText;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return string.Empty;

        var path = t.name;
        var p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}

