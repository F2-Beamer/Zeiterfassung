using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

public sealed class CustomerService(IDbContextFactory<AppDbContext> factory, TimeProvider time)
{
    public IReadOnlyList<Customer> GetCustomers(bool includeArchived)
    {
        using var db = factory.CreateDbContext();
        var query = db.Customers.AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return query.OrderBy(c => c.Name).ToList();
    }

    public Customer AddCustomer(string name)
    {
        var trimmed = ValidateName(name);
        using var db = factory.CreateDbContext();

        var customer = new Customer
        {
            Name = trimmed,
            CreatedAt = time.GetLocalNow().DateTime
        };
        db.Customers.Add(customer);
        db.SaveChanges();
        return customer;
    }

    public void RenameCustomer(int id, string name)
    {
        var trimmed = ValidateName(name);
        using var db = factory.CreateDbContext();

        var customer = db.Customers.Find(id)
            ?? throw new DomainException("Der Kunde existiert nicht.");
        customer.Name = trimmed;
        db.SaveChanges();
    }

    public void SetArchived(int id, bool archived)
    {
        using var db = factory.CreateDbContext();
        var customer = db.Customers.Find(id)
            ?? throw new DomainException("Der Kunde existiert nicht.");
        customer.IsArchived = archived;
        db.SaveChanges();
    }

    /// <summary>Löscht den Kunden nur, wenn er keine Buchungen und keinen laufenden Timer hat. Sonst archivieren.</summary>
    public void DeleteCustomer(int id)
    {
        using var db = factory.CreateDbContext();
        var customer = db.Customers.Find(id)
            ?? throw new DomainException("Der Kunde existiert nicht.");

        var hasEntries = db.TimeEntries.Any(e => e.CustomerId == id);
        var usedByTimer = db.ActiveTimers.Any(t => t.CustomerId == id);
        if (hasEntries || usedByTimer)
        {
            throw new DomainException(
                "Der Kunde hat Buchungen oder einen laufenden Timer und kann nicht gelöscht werden. Bitte stattdessen archivieren.");
        }

        db.Customers.Remove(customer);
        db.SaveChanges();
    }

    private static string ValidateName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("Der Kundenname darf nicht leer sein.");
        }

        return trimmed;
    }
}
