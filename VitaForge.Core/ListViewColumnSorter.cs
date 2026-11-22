using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace VitaForge.Core;

public sealed class ListViewColumnSorter
{
    private string? _lastSortColumn;
    private ListSortDirection _lastSortDirection;

    public void HandleHeaderClick(RoutedEventArgs e, ListView listView)
    {
        HandleHeaderClick(e, (sortBy, direction) =>
        {
            var dataView = CollectionViewSource.GetDefaultView(listView.ItemsSource);

            if (dataView == null)
                return false;

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();

            return true;
        });
    }

    public void HandleHeaderClick(RoutedEventArgs e, Func<string, ListSortDirection, bool> applySort)
    {
        if (e.OriginalSource is not GridViewColumnHeader header)
            return;

        if (header.Tag is not string sortBy)
            return;

        var direction = _lastSortColumn == sortBy && _lastSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

        if (!applySort(sortBy, direction))
            return;

        _lastSortColumn = sortBy;
        _lastSortDirection = direction;
    }
}