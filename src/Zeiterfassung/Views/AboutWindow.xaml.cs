using System.Reflection;
using System.Windows;

namespace Zeiterfassung.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + GetVersion();
    }

    /// <summary>Versionsnummer aus der Assembly. Build-Metadaten nach dem „+“ werden weggelassen.</summary>
    public static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "unbekannt";
    }
}
