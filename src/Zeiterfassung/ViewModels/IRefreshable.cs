namespace Zeiterfassung.ViewModels;

/// <summary>ViewModels, die beim Öffnen ihrer Ansicht Daten neu laden.</summary>
public interface IRefreshable
{
    void Refresh();
}
