using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace DiskSaver.Localization;

/// <summary>
/// Строки интерфейса. XAML привязывается к индексатору, поэтому при смене языка
/// все подписи обновляются сразу, без перезапуска.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static readonly string[] SupportedLanguages = ["en", "ru"];

    private static readonly ResourceManager Resources =
        new("DiskSaver.Resources.Strings", typeof(Loc).Assembly);

    private Loc()
    {
    }

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Срабатывает после смены языка — для текстов, которые собираются в коде.</summary>
    public event EventHandler? LanguageChanged;

    public string Language { get; private set; } = "en";

    public string this[string key] => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string T(string key) => Instance[key];

    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Язык Windows, если он поддерживается, иначе английский.</summary>
    public static string SystemLanguage =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";

    public void SetLanguage(string language)
    {
        if (!SupportedLanguages.Contains(language))
            language = "en";
        var culture = new CultureInfo(language);
        // Default* — чтобы фоновые задачи (сканирование, копирование) тоже писали на нужном языке.
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Language = language;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Разметка <c>{loc:L Scan}</c> — привязка к строке, которая обновляется при смене языка.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
