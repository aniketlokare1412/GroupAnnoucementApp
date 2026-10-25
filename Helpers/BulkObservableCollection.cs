using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GroupAnnouncementApp.Helpers;

// An ObservableCollection that can swap its whole content with ONE change notification.
// Clear() + Add() x N raises N + 1 notifications and the list re-measures after each one;
// ReplaceAll raises a single Reset, so a list of hundreds of rows is rebuilt once.
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();

        Items.Clear();
        foreach (T item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
