using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>
/// What a setting exposes without naming its value type. SettingManager stores
/// these, so shared work (persistence, reset, reading the apply policy) needs no
/// type switch at the call site.
/// </summary>
public interface ISettingValue
{
    SettingType Type { get; }
    string Key { get; }

    bool UsePlayerPrefs { get; }
    bool ApplyImmediately { get; }

    void Load();
    void Save();
    void Reset();
}

/// <summary>
/// One setting: its default, its live value, whether it persists, and whether a
/// change applies at once or waits for the Apply button. Views bind to
/// ValueNotifier, so this object is the setting. Nothing downstream keeps a copy
/// that can drift from it.
/// </summary>
public class SettingValue<T> : ISettingValue
{
    public SettingType Type { get; }
    public string Key { get; }
    public T DefaultValue { get; }

    public ReactiveProperty<T> ValueNotifier { get; }

    public bool UsePlayerPrefs { get; }
    public bool ApplyImmediately { get; }

    public T Value
    {
        get => ValueNotifier.Value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(ValueNotifier.Value, value))
                return;

            ValueNotifier.Value = value;

            if (ApplyImmediately)
                Save();
        }
    }

    public SettingValue(
        SettingType type,
        T defaultValue,
        bool usePlayerPrefs,
        bool applyImmediately)
    {
        Type = type;
        DefaultValue = defaultValue;
        Key = $"Setting_{type}";
        ValueNotifier = new(defaultValue);

        UsePlayerPrefs = usePlayerPrefs;
        ApplyImmediately = applyImmediately;
    }

    public void Load()
    {
        if (!UsePlayerPrefs) return;

        // An enum turns into an int only at the storage boundary. Everywhere else
        // the setting keeps its real type.
        if (typeof(T).IsEnum)
        {
            var saved = PlayerPrefs.GetInt(Key, Convert.ToInt32(DefaultValue));

            Value = Enum.IsDefined(typeof(T), saved)
                ? (T)Enum.ToObject(typeof(T), saved)
                : DefaultValue;
            return;
        }

        if (typeof(T) == typeof(float))
            Value = (T)(object)PlayerPrefs.GetFloat(Key, (float)(object)DefaultValue);
        else if (typeof(T) == typeof(int))
            Value = (T)(object)PlayerPrefs.GetInt(Key, (int)(object)DefaultValue);
        else if (typeof(T) == typeof(bool))
            Value = (T)(object)(PlayerPrefs.GetInt(Key, (bool)(object)DefaultValue ? 1 : 0) == 1);
    }

    public void Save()
    {
        if (!UsePlayerPrefs) return;

        if (typeof(T).IsEnum)
        {
            PlayerPrefs.SetInt(Key, Convert.ToInt32(Value));
            PlayerPrefs.Save();
            return;
        }

        if (typeof(T) == typeof(float))
            PlayerPrefs.SetFloat(Key, (float)(object)Value);
        else if (typeof(T) == typeof(int))
            PlayerPrefs.SetInt(Key, (int)(object)Value);
        else if (typeof(T) == typeof(bool))
            PlayerPrefs.SetInt(Key, (bool)(object)Value ? 1 : 0);

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Goes through Value, so a default takes the same path as a user edit:
    /// subscribers update and persistence follows the same rule.
    /// </summary>
    public void Reset()
    {
        Value = DefaultValue;
    }
}
