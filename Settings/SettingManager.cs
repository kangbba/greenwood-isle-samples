using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every setting in the game lives here and only here. Values of different types
/// share one registry, and the type each SettingType must carry is declared once
/// in _typeMap and checked on both registration and lookup.
/// </summary>
public class SettingManager : SingletonMono<SettingManager>
{
    protected override bool UseDontDestroyOnLoad => true;

    /// <summary>
    /// Raised only when the stored settings structure changes in a way old data
    /// cannot satisfy. Deliberately not the build number.
    /// </summary>
    private const int CurrentSettingsSchemaVersion = 2;
    private const string SettingsSchemaVersionKey = "Settings_SchemaVersion";

    private readonly Dictionary<SettingType, ISettingValue> _settings = new();

    private static readonly Dictionary<SettingType, Type> _typeMap = new()
    {
        { SettingType.MasterVolume, typeof(float) },
        { SettingType.BGMVolume, typeof(float) },
        { SettingType.SFXVolume, typeof(float) },
        { SettingType.UIVolume, typeof(float) },

        { SettingType.DialogueSpeed, typeof(float) },
        { SettingType.FontSize, typeof(float) },
        { SettingType.AutoModePagingSpeed, typeof(float) },
        { SettingType.EffectSpeed, typeof(float) },

        { SettingType.ResolutionIndex, typeof(int) },
        { SettingType.IsFullScreen, typeof(bool) },

        { SettingType.Language, typeof(GameLanguage) },
    };

    protected override void Awake()
    {
        base.Awake();

        // Audio
        Register(SettingType.MasterVolume, defaultValue: 1f,
            usePlayerPrefs: true, applyImmediately: true);
        Register(SettingType.BGMVolume, defaultValue: 1f,
            usePlayerPrefs: true, applyImmediately: true);
        Register(SettingType.SFXVolume, defaultValue: 1f,
            usePlayerPrefs: true, applyImmediately: true);
        Register(SettingType.UIVolume, defaultValue: 1f,
            usePlayerPrefs: true, applyImmediately: true);

        // Text & Dialogue
        Register(SettingType.FontSize, defaultValue: 0.5f,
            usePlayerPrefs: true, applyImmediately: false);
        Register(SettingType.DialogueSpeed, defaultValue: 0.5f,
            usePlayerPrefs: true, applyImmediately: false);
        Register(SettingType.AutoModePagingSpeed, defaultValue: 0.5f,
            usePlayerPrefs: true, applyImmediately: false);
        Register(SettingType.EffectSpeed, defaultValue: 0.5f,
            usePlayerPrefs: true, applyImmediately: false);

        // Display
        Register(SettingType.ResolutionIndex, defaultValue: 0,
            usePlayerPrefs: true, applyImmediately: false);
        Register(SettingType.IsFullScreen, defaultValue: true,
            usePlayerPrefs: true, applyImmediately: false);

        // Language
        Register(SettingType.Language, defaultValue: GameLanguage.Korean,
            usePlayerPrefs: true, applyImmediately: false);

        ValidateSettingsVersion();
        Load();
    }

    private void Register<T>(
        SettingType type,
        T defaultValue,
        bool usePlayerPrefs,
        bool applyImmediately)
    {
        ValidateType<T>(type);

        _settings[type] = new SettingValue<T>(
            type, defaultValue, usePlayerPrefs, applyImmediately);
    }

    /// <summary>
    /// Hands back the setting itself, not a copy, so a caller binds to the same
    /// ReactiveProperty everything else observes. Asking for the wrong type throws
    /// at the call site instead of quietly returning null.
    /// </summary>
    public SettingValue<T> GetSetting<T>(SettingType type)
    {
        ValidateType<T>(type);

        if (_settings[type] is not SettingValue<T> setting)
            throw new InvalidOperationException(
                $"Setting type mismatch: {type}");

        return setting;
    }

    /// <summary>Declared-type check shared by registration and lookup.</summary>
    private static void ValidateType<T>(SettingType type)
    {
        if (!_typeMap.TryGetValue(type, out var expectedType)
            || expectedType != typeof(T))
            throw new InvalidOperationException(
                $"SettingType {type} carries {expectedType}, requested {typeof(T)}");
    }

    /// <summary>Metadata that does not depend on the value type.</summary>
    public ISettingValue GetSettingInfo(SettingType type) => _settings[type];

    public void Load()
    {
        foreach (var setting in _settings.Values)
            setting.Load();
    }

    public void Save()
    {
        foreach (var setting in _settings.Values)
        {
            if (!setting.ApplyImmediately)
                setting.Save();
        }

        PlayerPrefs.Save();
    }

    public void ResetAll()
    {
        foreach (var setting in _settings.Values)
            setting.Reset();
    }

    /// <summary>
    /// Clears stored settings when the schema they were written against no longer
    /// matches this build, so old local data cannot put the game in a state the
    /// new build does not expect.
    /// </summary>
    private void ValidateSettingsVersion()
    {
        var savedVersion = PlayerPrefs.GetInt(SettingsSchemaVersionKey, 0);

        if (savedVersion == CurrentSettingsSchemaVersion)
            return;

        // DeleteAll is avoided: it would take other systems' keys with it.
        foreach (var setting in _settings.Values)
        {
            if (setting.UsePlayerPrefs)
                PlayerPrefs.DeleteKey(setting.Key);
        }

        PlayerPrefs.SetInt(SettingsSchemaVersionKey, CurrentSettingsSchemaVersion);
        PlayerPrefs.Save();
    }

    protected override void Release()
    {
        _settings.Clear();
    }
}
