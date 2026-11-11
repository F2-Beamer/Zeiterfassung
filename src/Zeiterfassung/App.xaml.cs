using Serilog.Events;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Zeiterfassung.Data;
using Zeiterfassung.Integration;
using Zeiterfassung.Services;
using Zeiterfassung.ViewModels;
using Zeiterfassung.Views;

namespace Zeiterfassung;

public partial class App : Application
{
    private ServiceProvider? _services;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CultureInfo.DefaultThreadCurrentCulture = TimeFormat.German;
        CultureInfo.DefaultThreadCurrentUICulture = TimeFormat.German;

        var paths = StoragePathResolver.CreateDefault();
        var logDirectory = Path.Combine(paths.DataDirectory, "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(logDirectory, "zeiterfassung-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unbehandelter UI-Fehler");
            MessageBox.Show(
                "Ein unerwarteter Fehler ist aufgetreten. Details stehen im Logbuch.\n\n" + args.Exception.Message,
                "Zeiterfassung",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: true));
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath}"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TimeTrackingService>();
        services.AddSingleton<CustomerService>();
        services.AddSingleton<AbsenceService>();
        services.AddSingleton<MasterDataService>();
        services.AddSingleton<BalanceService>();
        services.AddSingleton<BackupService>();
        services.AddHttpClient<HolidayApiClient>(client => client.BaseAddress = new Uri(HolidayApiClient.BaseUrl));
        services.AddSingleton<HolidayService>();
        services.AddSingleton<HeuteViewModel>();
        services.AddSingleton<IDayDialog, WindowDayDialog>();
        services.AddSingleton<MonatViewModel>();
        services.AddSingleton<ErfassungViewModel>();
        services.AddSingleton<EinstellungenViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        _services = services.BuildServiceProvider();
        Services = _services;

        using (var scope = _services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();
        }

        _services.GetRequiredService<BackupService>().AutoBackupOnStart();

        Log.Information("Start. Datenordner: {DataDirectory}", paths.DataDirectory);

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Beenden");
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
