using System.Text.Json;

namespace Shelf.Core;

public sealed record StoreLoad<T>(T Value, string? Warning);

/// <summary>Only writes application-owned JSON/backup/temp files, never referenced files.</summary>
public sealed class JsonStore<T>(string filePath, Func<T> createDefault, Func<T, bool> validate)
    where T : class
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public bool CanSave { get; private set; } = true;

    public StoreLoad<T> Load()
    {
        try { return new(Read(filePath), null); }
        catch (FileNotFoundException) { return new(createDefault(), null); }
        catch (DirectoryNotFoundException) { return new(createDefault(), null); }
        catch (Exception ex) when (IsDataError(ex))
        {
            // Never silently replace unreadable, corrupt, or newer-version user data.
            CanSave = false;
            try
            {
                return new(Read(filePath + ".bak"),
                    $"{Path.GetFileName(filePath)}を読めないためバックアップを表示しています。元データを保護し、このファイルへの保存を停止しました。{ex.Message}");
            }
            catch (Exception backupError) when (IsDataError(backupError))
            {
                return new(createDefault(),
                    $"{Path.GetFileName(filePath)}を読めません。元データを保護し、このファイルへの保存を停止しました。{ex.Message}");
            }
        }
    }

    public void Save(T value)
    {
        if (!CanSave) throw new InvalidOperationException("読み込めなかった保存ファイルを保護しています。説明書の復旧手順を確認してください。");
        if (!validate(value)) throw new InvalidDataException("保存データの形式が正しくありません。");

        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(filePath))
                File.Replace(temporary, filePath, filePath + ".bak");
            else
                File.Move(temporary, filePath);
        }
        finally
        {
            // Only the unique temporary file created by this save may be deleted.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private T Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        T? value = JsonSerializer.Deserialize<T>(bytes, Options);
        // Version must be explicitly present, not supplied by the model's initializer.
        using var document = JsonDocument.Parse(bytes);
        if (value is null || !document.RootElement.TryGetProperty("Version", out _)
            || !validate(value)) throw new InvalidDataException("未対応の形式またはバージョンです。");
        return value;
    }

    private static bool IsDataError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException
        or JsonException or InvalidOperationException or NotSupportedException;
}
