using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zeiterfassung.ViewModels;

namespace Zeiterfassung.Views;

public partial class DateField : UserControl
{
    private const string DisplayFormat = "dd.MM.yyyy";

    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate),
        typeof(DateTime?),
        typeof(DateField),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedDateChanged));

    public DateField()
    {
        InitializeComponent();
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateField field)
        {
            field.ShowDate(e.NewValue as DateTime?);
        }
    }

    private void ShowDate(DateTime? value)
    {
        Input.Text = value?.ToString(DisplayFormat, TimeFormat.German) ?? string.Empty;
    }

    private DateTime CurrentOrToday() => SelectedDate ?? DateTime.Today;

    private void OnPrevious(object sender, RoutedEventArgs e) => SelectedDate = CurrentOrToday().AddDays(-1);

    private void OnNext(object sender, RoutedEventArgs e) => SelectedDate = CurrentOrToday().AddDays(1);

    private void OnToday(object sender, RoutedEventArgs e) => SelectedDate = DateTime.Today;

    private void OnCommit(object sender, RoutedEventArgs e)
    {
        if (DateTime.TryParseExact(Input.Text.Trim(), DisplayFormat, TimeFormat.German, DateTimeStyles.None, out var parsed))
        {
            SelectedDate = parsed;
        }
        else
        {
            // Ungültige Eingabe verwerfen und den bisherigen Wert wieder anzeigen.
            ShowDate(SelectedDate);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnCommit(sender, e);
            e.Handled = true;
        }
    }
}
