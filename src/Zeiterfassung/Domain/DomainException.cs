namespace Zeiterfassung.Domain;

/// <summary>Fachlicher Fehler, der dem Nutzer als Hinweis angezeigt wird.</summary>
public sealed class DomainException(string message) : Exception(message);
