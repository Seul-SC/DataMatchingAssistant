using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace StudentScoreTool;

public partial class OverwriteWindow : Window
{
    private readonly ObservableCollection<OverwriteRow> _rows;

    public OverwriteWindow(IReadOnlyList<OverwriteCandidate> candidates)
    {
        InitializeComponent();
        _rows = new ObservableCollection<OverwriteRow>(candidates.Select(OverwriteRow.FromCandidate));
        foreach (var row in _rows)
        {
            row.PropertyChanged += (_, _) => RefreshHint();
        }

        ItemList.ItemsSource = _rows;
        Title = $"确认是否覆盖已有成绩（{candidates.Count} 项）";
        SummaryTextBlock.Text =
            "勾选后才会用新成绩覆盖原来的值。可以全选，也可以只覆盖其中几条。关掉窗口或点「全部跳过」，这些格子都会保持原样。";
        RefreshHint();
    }

    public IReadOnlyList<string> SelectedKeys =>
        _rows.Where(row => row.IsSelected).Select(row => row.Key).ToList();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.IsSelected = true;
        }
    }

    private void SkipAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.IsSelected = false;
        }

        DialogResult = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void RefreshHint()
    {
        var selected = _rows.Count(row => row.IsSelected);
        SelectionHintText.Text = $"已勾选 {selected} / {_rows.Count} 项";
    }
}

public sealed class OverwriteRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Key { get; init; }
    public required string StudentName { get; init; }
    public required string StudentId { get; init; }
    public required string ScoreColumn { get; init; }
    public required string CurrentValue { get; init; }
    public required string IncomingValue { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static OverwriteRow FromCandidate(OverwriteCandidate candidate)
    {
        return new OverwriteRow
        {
            Key = candidate.Key,
            StudentName = candidate.StudentName,
            StudentId = string.IsNullOrWhiteSpace(candidate.StudentId) ? "无学号" : candidate.StudentId,
            ScoreColumn = candidate.ScoreColumn,
            CurrentValue = candidate.CurrentValue,
            IncomingValue = candidate.IncomingValue
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
