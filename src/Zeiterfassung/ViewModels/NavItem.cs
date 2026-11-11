using CommunityToolkit.Mvvm.ComponentModel;

namespace Zeiterfassung.ViewModels;

public sealed partial class NavItem(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private bool _isActive;
}
