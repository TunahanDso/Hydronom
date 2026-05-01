using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Config içinden double değer okur.
    /// Önce invariant culture, sonra mevcut culture denenir.
    /// Okunamazsa fallback döner.
    /// </summary>
    private static double ReadDouble(IConfiguration config, string key, double fallback)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var invariantResult))
        {
            return invariantResult;
        }

        if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out var currentCultureResult))
        {
            return currentCultureResult;
        }

        return fallback;
    }

    /// <summary>
    /// Config içinden nullable double değer okur.
    /// Değer yoksa veya parse edilemezse null döner.
    /// </summary>
    private static double? ReadNullableDouble(IConfiguration config, string key)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var invariantResult))
        {
            return invariantResult;
        }

        if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out var currentCultureResult))
        {
            return currentCultureResult;
        }

        return null;
    }

    /// <summary>
    /// Config içinden int değer okur.
    /// Okunamazsa fallback döner.
    /// </summary>
    private static int ReadInt(IConfiguration config, string key, int fallback)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var invariantResult))
        {
            return invariantResult;
        }

        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var currentCultureResult))
        {
            return currentCultureResult;
        }

        return fallback;
    }

    /// <summary>
    /// Config içinden nullable bool değer okur.
    /// Değer yoksa veya parse edilemezse null döner.
    /// </summary>
    private static bool? ReadNullableBool(IConfiguration config, string key)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (bool.TryParse(value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Config içinden bool değer okur.
    /// Okunamazsa fallback döner.
    /// </summary>
    private static bool ReadBool(IConfiguration config, string key, bool fallback)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (bool.TryParse(value, out var result))
            return result;

        return fallback;
    }

    /// <summary>
    /// Config içinden string değer okur.
    /// Boşsa fallback döner.
    /// </summary>
    private static string ReadString(IConfiguration config, string key, string fallback)
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    /// <summary>
    /// Config içinden enum değer okur.
    /// Okunamazsa fallback döner.
    /// </summary>
    private static TEnum ReadEnum<TEnum>(IConfiguration config, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        var value = config[key];

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
            return result;

        return fallback;
    }

    /// <summary>
    /// Double değeri güvenli aralığa çeker.
    /// NaN/Infinity gelirse fallback döner.
    /// </summary>
    private static double ClampDouble(double value, double min, double max, double fallback)
    {
        if (!double.IsFinite(value))
            return fallback;

        return Math.Clamp(value, min, max);
    }

    /// <summary>
    /// Int değeri güvenli aralığa çeker.
    /// </summary>
    private static int ClampInt(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }
}