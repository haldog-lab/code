using System.Windows;
using Shelf.Windows.Models;

namespace Shelf.Windows.Services;

// Future providers may also advertise UnicodeText or URL formats.
// The file provider deliberately exposes only file references.
public interface IDragDataProvider
{
    IDataObject CreateData(ShelfItem item);
}
