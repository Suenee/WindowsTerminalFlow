namespace WindowsTerminalFlow.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, (string Cs, string En)> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setup_title"] = ("Nastavení WindowsTerminalFlow", "WindowsTerminalFlow Setup"),
        ["language"] = ("Jazyk", "Language"),
        ["logging"] = ("Protokolování", "Logging"),
        ["shell"] = ("Shell", "Shell"),
        ["new_folders"] = ("Nově nalezené složky ve výchozím stavu otevřít", "Open newly discovered folders by default"),
        ["elevated_active"] = ("Zvýšená oprávnění: aktivní", "Elevated: active"),
        ["elevated_inactive"] = ("Zvýšená oprávnění: neaktivní", "Elevated: inactive"),
        ["repair_launcher"] = ("Opravit spouštění se zvýšenými oprávněními", "Repair elevated launcher"),
        ["launcher_registered"] = ("Spouštění se zvýšenými oprávněními bylo zaregistrováno.", "Elevated launcher registered."),
        ["save"] = ("Uložit", "Save"),
        ["cancel"] = ("Zrušit", "Cancel"),
        ["workspace_config"] = ("WTF Konfigurace workspace", "WTF Workspace configuration"),
        ["close_tab"] = ("Zavřít", "Close"),
        ["path_not_found"] = ("Zadaná cesta neexistuje nebo není složka", "The specified path does not exist or is not a directory")
    };

    public static string Get(string key, string? language = null)
    {
        if (!Texts.TryGetValue(key, out var value)) return key;
        var lang = language ?? SettingsService.Settings.Language;
        return lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? value.En : value.Cs;
    }
}
