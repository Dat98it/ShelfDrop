using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ShelfDrop.Core;

/// <summary>What is on the shelf. Everything here must be touched from the UI thread only.</summary>
public sealed class ShelfModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<ShelfItem> _items = new();
    private bool _isDropTargeted;

    public ShelfModel()
    {
        Items = new ReadOnlyObservableCollection<ShelfItem>(_items);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ShelfItem> Items { get; }

    /// <summary>True while a drag is hovering over the shelf, used to highlight it.</summary>
    public bool IsDropTargeted
    {
        get => _isDropTargeted;
        set
        {
            if (_isDropTargeted == value) return;
            _isDropTargeted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDropTargeted)));
        }
    }

    public void Add(IEnumerable<ShelfItem> newItems)
    {
        foreach (ShelfItem item in newItems) _items.Add(item);
    }

    public void Remove(Guid id)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Id != id) continue;
            _items.RemoveAt(i);
            return;
        }
    }

    public void Clear() => _items.Clear();
}
