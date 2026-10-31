using System.IO;

namespace Shelf.Windows.Models;

// A reference only: registering and removing an item never modifies its source.
public sealed record ShelfItem(string FilePath)
{
    public string Name => Path.GetFileName(FilePath);
    public string Availability => File.Exists(FilePath) ? "" : "見つかりません（参照は保持しています）";
}
