namespace Zeiterfassung.Domain;

/// <summary>Urlaubskonto eines Jahres.</summary>
public sealed record VacationStatus(
    int Year,
    decimal EntitlementDays,
    decimal CorrectionDays,
    decimal CarryOverDays,
    decimal TakenDays)
{
    public decimal RemainingDays => CarryOverDays + EntitlementDays + CorrectionDays - TakenDays;
}
