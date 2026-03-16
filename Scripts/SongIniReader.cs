using Godot;
using System.Globalization;

/// <summary>
/// Lê o arquivo song.ini presente em pastas no formato Clone Hero / Enchor.
///
/// Campos suportados:
///   name   → título da música
///   artist → artista
///   delay  → atraso do áudio em milissegundos
/// </summary>
public static class SongIniReader
{
    public struct SongInfo
    {
        public string Name;
        public string Artist;
        /// <summary>Atraso do áudio em milissegundos. Positivo = atrasa o áudio.</summary>
        public float DelayMs;
    }

    public static SongInfo Read(string iniPath)
    {
        var info = new SongInfo { Name = "", Artist = "", DelayMs = 0f };

        if (!FileAccess.FileExists(iniPath)) return info;

        using var file = FileAccess.Open(iniPath, FileAccess.ModeFlags.Read);
        foreach (string raw in file.GetAsText().Split('\n'))
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
    /// Retorna "Artista - Título", só "Título", ou <paramref name="fallback"/> se vazio.
    /// </summary>
    public static string BuildDisplayName(SongInfo info, string fallback)
    {
        if (string.IsNullOrEmpty(info.Name)) return fallback;
        // Se não há artista, usa o nome da pasta (fallback) que já pode ter "Artista - Título"
        return string.IsNullOrEmpty(info.Artist) ? fallback : $"{info.Artist} - {info.Name}";
    }
}
