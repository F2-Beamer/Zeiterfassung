namespace Zeiterfassung.ViewModels;

/// <summary>Öffnet das Tagesfenster. Die konkrete Umsetzung als Fenster liegt in der View-Schicht.</summary>
public interface IDayDialog
{
    /// <param name="date">Tag, dessen Details angezeigt werden.</param>
    /// <param name="changed">Wird nach jeder Änderung (Abwesenheit angelegt oder gelöscht) aufgerufen.</param>
    void Show(DateOnly date, Action changed);
}
