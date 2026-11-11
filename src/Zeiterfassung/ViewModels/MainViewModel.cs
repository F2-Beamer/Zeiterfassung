using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Zeiterfassung.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, object> _views;

    public MainViewModel(
        HeuteViewModel heute,
        MonatViewModel monat,
        EinstellungenViewModel einstellungen)
    {
        _views = new Dictionary<string, object>
        {
            ["Heute"] = heute,
            ["Monat"] = monat,
            ["Einstellungen"] = einstellungen
        };

        NavItems = new ObservableCollection<NavItem>(_views.Keys.Select(k => new NavItem(k)));
        Navigate("Heute");
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    private object? _currentView;

    [RelayCommand]
    private void Navigate(string tab)
    {
        var view = _views[tab];
        if (view is IRefreshable refreshable)
        {
            refreshable.Refresh();
        }

        CurrentView = view;
        foreach (var item in NavItems)
        {
            item.IsActive = item.Name == tab;
        }
    }
}
