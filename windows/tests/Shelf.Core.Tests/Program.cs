using System.Security.Cryptography;
using Shelf.Core;

// Dependency-free executable tests. Exit 1 on any failure; no WPF or real AppData access.
int passed = 0, failed = 0;
string root = Path.Combine(Path.GetTempPath(), "Shelf.Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

void Check(bool condition, string message = "Assertion failed")
{
    if (!condition) throw new Exception(message);
}

void Test(string name, Action<string> body)
{
    string directory = Path.Combine(root, (passed + failed).ToString());
    Directory.CreateDirectory(directory);
    try { body(directory); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}

JsonStore<ShelfItemsState> ItemsStore(string dir) => new(Path.Combine(dir, "items.json"), () => new(), ShelfItemsState.IsValid);
JsonStore<ShelfSettings> SettingsStore(string dir) => new(Path.Combine(dir, "settings.json"), () => new(), ShelfSettings.IsValid);
ShelfItemsState State(string dir, string name = "文書.txt") => new() { Paths = [Path.Combine(dir, name)] };

Test("first launch uses empty defaults without writing", dir =>
{
    var store = ItemsStore(dir);
    var load = store.Load();
    Check(load.Value.Paths.Count == 0 && load.Warning is null && store.CanSave);
    Check(!Directory.EnumerateFileSystemEntries(dir).Any());
});
Test("multiple Unicode and space-containing paths round trip", dir =>
{
    var state = new ShelfItemsState { Paths = [Path.Combine(dir, "日本語.txt"), Path.Combine(dir, "a b.txt")] };
    ItemsStore(dir).Save(state);
    Check(ItemsStore(dir).Load().Value.Paths.SequenceEqual(state.Paths));
});
Test("saving references does not change source content, path or timestamp", dir =>
{
    string source = Path.Combine(dir, "original.txt");
    File.WriteAllText(source, "unchanged source");
    byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
    DateTime time = File.GetLastWriteTimeUtc(source);
    var store = ItemsStore(dir);
    store.Save(new() { Paths = [source] });
    store.Save(new()); // Explicit removal is metadata-only too.
    Check(File.Exists(source) && hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))));
    Check(File.GetLastWriteTimeUtc(source) == time);
});
Test("replacement preserves last valid backup and cleans temporary files", dir =>
{
    var store = ItemsStore(dir);
    store.Save(State(dir, "first.txt"));
    byte[] first = File.ReadAllBytes(Path.Combine(dir, "items.json"));
    store.Save(State(dir, "second.txt"));
    store.Save(State(dir, "third.txt"));
    Check(ItemsStore(dir).Load().Value.Paths.Single().EndsWith("third.txt"));
    Check(File.ReadAllText(Path.Combine(dir, "items.json.bak")).Contains("second.txt"));
    Check(!Directory.EnumerateFiles(dir, "*.tmp").Any());
});
Test("corrupt primary recovers backup without overwriting either file", dir =>
{
    var store = ItemsStore(dir);
    store.Save(State(dir, "first.txt"));
    store.Save(State(dir, "second.txt"));
    string file = Path.Combine(dir, "items.json");
    File.WriteAllText(file, "broken");
    byte[] backup = File.ReadAllBytes(file + ".bak");
    var recovery = ItemsStore(dir);
    var load = recovery.Load();
    Check(load.Value.Paths.Single().EndsWith("first.txt") && load.Warning is not null && !recovery.CanSave);
    bool rejected = false;
    try { recovery.Save(new()); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected && File.ReadAllText(file) == "broken");
    Check(backup.SequenceEqual(File.ReadAllBytes(file + ".bak")));
});
Test("corrupt primary and backup remain protected", dir =>
{
    File.WriteAllText(Path.Combine(dir, "items.json"), "{");
    File.WriteAllText(Path.Combine(dir, "items.json.bak"), "[");
    var store = ItemsStore(dir);
    var result = store.Load();
    Check(result.Warning is not null && result.Value.Paths.Count == 0 && !store.CanSave);
    Check(File.ReadAllText(Path.Combine(dir, "items.json")) == "{");
});
foreach (string invalid in new[] { "{\"Version\":99,\"Paths\":[]}", "{\"Paths\":[]}", "{\"Version\":1}",
    "{\"Version\":1,\"Paths\":null}", "{\"Version\":1,\"Paths\":[\"relative.txt\"]}", "null", "42" })
    Test("reject invalid/incomplete/unknown state: " + invalid, dir =>
    {
        string path = Path.Combine(dir, "items.json");
        File.WriteAllText(path, invalid);
        var store = ItemsStore(dir);
        Check(store.Load().Warning is not null && !store.CanSave);
        Check(File.ReadAllText(path) == invalid);
    });
Test("missing referenced file is retained during restore", dir =>
{
    var state = State(dir, "does-not-exist.txt");
    ItemsStore(dir).Save(state);
    Check(ItemsStore(dir).Load().Value.Paths.SequenceEqual(state.Paths));
});
Test("collapsed state and monitor device ID round trip", dir =>
{
    var settings = new ShelfSettings { MonitorId = @"\\.\DISPLAY3", IsCollapsed = true };
    SettingsStore(dir).Save(settings);
    Check(SettingsStore(dir).Load().Value == settings);
});
Test("incomplete settings are protected", dir =>
{
    File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"Version\":1}");
    var store = SettingsStore(dir);
    Check(store.Load().Warning is not null && !store.CanSave);
});
Test("invalid save leaves last valid state intact", dir =>
{
    var store = ItemsStore(dir);
    store.Save(State(dir));
    byte[] before = File.ReadAllBytes(Path.Combine(dir, "items.json"));
    bool rejected = false;
    try { store.Save(new() { Version = 99 }); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && before.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "items.json"))));
});
Test("storage failure is reported instead of pretending to save", dir =>
{
    string blocker = Path.Combine(dir, "not-a-directory");
    File.WriteAllText(blocker, "keep");
    bool failedSave = false;
    try { ItemsStore(blocker).Save(State(dir)); }
    catch (IOException) { failedSave = true; }
    Check(failedSave && File.ReadAllText(blocker) == "keep");
});
Test("left monitor negative coordinates at 150 percent DPI", _ =>
{
    var rect = ShelfPlacement.Calculate(new(-2560, -200, 0, 1200), 144, false);
    Check(rect.Left == -2560 && rect.Width == 480 && rect.Height == 750);
    Check(rect.Top >= -200 && rect.Bottom <= 1200);
});
Test("top monitor and taskbar work area at 125 percent DPI", _ =>
{
    var rect = ShelfPlacement.Calculate(new(40, -1440, 2560, -40), 120, true);
    Check(rect.Left == 40 && rect.Width == 45 && rect.Height == 160);
    Check(rect.Top >= -1440 && rect.Bottom <= -40);
});
Test("200 percent DPI preserves logical expanded size", _ =>
{
    var rect = ShelfPlacement.Calculate(new(1920, 0, 5760, 2080), 192, false);
    Check(rect.Left == 1920 && rect.Width == 640 && rect.Height == 1000);
});
Test("tiny work area clamps shelf fully on screen", _ =>
{
    var area = new PixelRect(-100, -100, 100, 100);
    Check(ShelfPlacement.Calculate(area, 192, false) == area);
});
Test("invalid geometry is rejected", _ =>
{
    bool rejected = false;
    try { ShelfPlacement.Calculate(new(0, 0, 0, 0), 96, false); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected);
});

Directory.Delete(root, recursive: true); // Only this test run's unique temporary directory.
Console.WriteLine($"{passed} passed; {failed} failed");
return failed == 0 ? 0 : 1;
