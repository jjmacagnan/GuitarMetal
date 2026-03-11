using Godot;
using System.Globalization;

/// <summary>
/// Lê o arquivo song.ini presente em pastas no formato Clone Hero.
///
/// Campos suportados:
///   name   → título da música
///   artist → artista
///   delay  → atraso do áudio em milissegundos (pode ser negativo)
/// </summary>
public static class SongIniReader
{
    public struct SongInfo
    {
        public string Name;
        public string Artist;
        /// <summary>Atraso do áudio em milissegundos (mesmo semântica do Offset em .chart).</summary>
        public float DelayMs;
    }

    public static SongInfo Read(string iniPath)
    {
        var info = new SongInfo { Name = "", Artist = "", DelayMs = 0f };

        if (!FileAccess.FileExists(iniPath)) return info;

        using var file = FileAccess.Open(iniPath, FileAccess.ModeFlags.Read);
        string[] lines = file.GetAsText().Split('\n');

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.StartsWith("[") || line.Length == 0) continue;

            int eq = line.IndexOf('=');
            if (eq < 0) continue;

            string key = line[..eq].Trim().ToLowerInvariant();
            string val = line[(eq + 1)..].Trim();

            switch (key)
            {
                case "name":   info.Name   = val; break;
                case "artist": info.Artist = val; break;
                case "delay":
                    float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out info.DelayMs);
                    break;
            }
        }

        return info;
    }

    /// <summary>
    /// Monta o nome de exibição no formato "Artista - Título" ou só o título se não houver artista.
    /// </summary>
    public static string BuildDisplayName(SongInfo info, string fallback)
    {
        if (string.IsNullOrEmpty(info.Name)) return fallback;
        return string.IsNullOrEmpty(info.Artist) ? info.Name : $"{info.Artist} - {info.Name}";
    }
}
