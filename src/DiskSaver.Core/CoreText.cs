using System.Globalization;
using System.Resources;

namespace DiskSaver.Core;

/// <summary>Строки ядра на языке текущего UI-потока (CultureInfo.CurrentUICulture).</summary>
public static class CoreText
{
    private static readonly ResourceManager Resources =
        new("DiskSaver.Core.Resources.CoreStrings", typeof(CoreText).Assembly);

    private static readonly CultureInfo[] KnownCultures = [new("en"), new("ru")];

    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Название стандартной категории на текущем языке или null, если ключ не стандартный.</summary>
    public static string? BuiltInCategoryName(string key) =>
        Resources.GetString("Cat_" + key, CultureInfo.CurrentUICulture);

    /// <summary>Совпадает ли название со стандартным для этого ключа на любом из поддерживаемых языков.</summary>
    public static bool IsBuiltInCategoryName(string key, string name) =>
        KnownCultures.Any(c => string.Equals(Resources.GetString("Cat_" + key, c), name.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
