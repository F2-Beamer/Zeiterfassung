using System.Threading;
using System.Windows;
using FluentAssertions;
using Zeiterfassung.Views;

namespace Zeiterfassung.Tests.Ui;

/// <summary>
/// Lädt jede View einmal im STA-Thread. Fehlende Ressourcen oder defekte XAML-Dateien
/// fallen so beim Test auf und nicht erst bei der Navigation in der App.
/// </summary>
public class ViewLoadTests
{
    [Fact]
    public void Alle_Views_werden_ohne_XAML_Fehler_erzeugt()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var app = Application.Current ?? new Application();
                var theme = (ResourceDictionary)Application.LoadComponent(
                    new Uri("/Zeiterfassung;component/Resources/Theme.xaml", UriKind.Relative));
                var controls = (ResourceDictionary)Application.LoadComponent(
                    new Uri("/Zeiterfassung;component/Resources/Controls.xaml", UriKind.Relative));
                app.Resources.MergedDictionaries.Add(theme);
                app.Resources.MergedDictionaries.Add(controls);

                _ = new HeuteView();
                _ = new MonatView();
                _ = new ErfassungView();
                _ = new EinstellungenView();
                _ = new DayDetailWindow();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
    }
}
