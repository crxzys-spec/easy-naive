using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyNaive.App.Infrastructure;

internal sealed class JsonFileStore<T>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly string _path;
    private readonly IJsonFileStoreTransform<T>? _transform;

    public JsonFileStore(string path, IJsonFileStoreTransform<T>? transform = null)
    {
        _path = path;
        _transform = transform;
    }

    public T LoadOrCreate(Func<T> factory)
    {
        if (File.Exists(_path))
        {
            var json = File.ReadAllText(_path);
            var value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (value is not null)
            {
                return _transform is null ? value : _transform.AfterLoad(value);
            }
        }

        var created = factory();
        Save(created);
        return created;
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var valueToSave = _transform is null ? value : _transform.BeforeSave(value);
        var json = JsonSerializer.Serialize(valueToSave, SerializerOptions);
        File.WriteAllText(_path, json);
    }
}
