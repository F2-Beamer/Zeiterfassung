using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Models;

namespace Zeiterfassung.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<PauseEntry> PauseEntries => Set<PauseEntry>();

    public DbSet<ActiveTimer> ActiveTimers => Set<ActiveTimer>();

    public DbSet<WorkTimeProfile> WorkTimeProfiles => Set<WorkTimeProfile>();

    public DbSet<WorkTimeDayOverride> WorkTimeDayOverrides => Set<WorkTimeDayOverride>();

    public DbSet<Absence> Absences => Set<Absence>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<VacationEntitlement> VacationEntitlements => Set<VacationEntitlement>();

    public DbSet<OvertimeOpening> OvertimeOpenings => Set<OvertimeOpening>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(t => t.Date);
            entity.Property(t => t.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<PauseEntry>(entity =>
        {
            entity.HasIndex(p => p.Date);
        });

        modelBuilder.Entity<ActiveTimer>(entity =>
        {
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkTimeProfile>(entity =>
        {
            entity.HasIndex(p => p.ValidFrom).IsUnique();
            entity.HasMany(p => p.Overrides)
                .WithOne()
                .HasForeignKey(o => o.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkTimeDayOverride>(entity =>
        {
            entity.HasIndex(o => new { o.ProfileId, o.Day }).IsUnique();
        });

        modelBuilder.Entity<Absence>(entity =>
        {
            entity.HasIndex(a => a.Date);
            entity.Property(a => a.Portion).HasPrecision(3, 2);
            entity.Property(a => a.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasIndex(h => new { h.State, h.Date }).IsUnique();
        });

        modelBuilder.Entity<VacationEntitlement>(entity =>
        {
            entity.HasIndex(v => v.Year).IsUnique();
            entity.Property(v => v.Days).HasPrecision(5, 2);
            entity.Property(v => v.CorrectionDays).HasPrecision(5, 2);
            entity.Property(v => v.OpeningCarryOverDays).HasPrecision(5, 2);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
        });
    }
}
