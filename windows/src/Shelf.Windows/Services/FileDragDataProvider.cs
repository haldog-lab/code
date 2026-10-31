using System.Windows;
using Shelf.Windows.Models;

namespace Shelf.Windows.Services;

public sealed class FileDragDataProvider : IDragDataProvider
{
    public IDataObject CreateData(ShelfItem item)
    {
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { item.FilePath });
        return data;
    }
}
