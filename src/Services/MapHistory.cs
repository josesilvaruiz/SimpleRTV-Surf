using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SimpleRTV;

/// <summary>
/// Últimos mapas jugados (claves del pool, el más reciente al final). Se guarda en disco para que
/// un reinicio del servidor o una recarga del plugin no vuelva a ofrecer el mapa que se acaba de jugar.
/// </summary>
public class MapHistory
{
    private const int MaxStored = 50;

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly List<string> _played = new();

    public MapHistory(string path, ILogger logger)
    {
        _path = path;
        _logger = logger;
        Load();
    }

    public IReadOnlyList<string> Played => _played;

    /// <summary>Apunta el mapa que acaba de empezar. Si ya estaba en la lista pasa a ser el más reciente.</summary>
    public void Record(string mapKey)
    {
        if (string.IsNullOrWhiteSpace(mapKey)) return;
        if (_played.Count > 0 && _played[^1].Equals(mapKey, StringComparison.OrdinalIgnoreCase)) return;

        _played.RemoveAll(k => k.Equals(mapKey, StringComparison.OrdinalIgnoreCase));
        _played.Add(mapKey);
        if (_played.Count > MaxStored)
            _played.RemoveRange(0, _played.Count - MaxStored);
        Save();
    }

    /// <summary>
    /// Los <paramref name="count"/> mapas más recientes (0 = ninguno), sin contar los que cumplan
    /// <paramref name="skip"/> (el mapa actual: ya está apuntado y no debe gastar una plaza).
    /// </summary>
    public HashSet<string> Recent(int count, Func<string, bool>? skip = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = _played.Count - 1; i >= 0 && set.Count < count; i--)
        {
            if (skip != null && skip(_played[i])) continue;
            set.Add(_played[i]);
        }
        return set;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path));
            if (parsed == null) return;
            foreach (var k in parsed.Where(k => !string.IsNullOrWhiteSpace(k)).TakeLast(MaxStored))
                _played.Add(k);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SimpleRTV] Could not read map history ({Path}): {Error}", _path, ex.Message);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_played));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SimpleRTV] Could not save map history ({Path}): {Error}", _path, ex.Message);
        }
    }
}
