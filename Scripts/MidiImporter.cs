using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Importa charts no formato MIDI (.mid) usado pelo Clone Hero.
///
/// Estrutura esperada:
///   Track 0         → tempo map (eventos FF 51)
///   "PART GUITAR"   → notas do jogador (outros tracks são ignorados)
///
/// Mapeamento de notas por dificuldade (PART GUITAR):
///   Expert : 96-100  (Green=96, Red=97, Yellow=98, Blue=99, Orange=100)
///   Hard   : 84-88
///   Medium : 72-76
///   Easy   : 60-64
///
/// Sustains: duração real da nota (Note Off - Note On).
/// Notas muito curtas (< 1/16) são tratadas como tap (sem hold).
/// </summary>
public static class MidiImporter
{
    private static readonly string[] TrackPriority =
        { "ExpertSingle", "HardSingle", "MediumSingle", "EasySingle" };

    private static readonly Dictionary<string, int> DiffBaseNote = new()
    {
        ["ExpertSingle"] = 96,
        ["HardSingle"]   = 84,
        ["MediumSingle"] = 72,
        ["EasySingle"]   = 60,
    };

    // Nomes de track aceitos para guitarra (Clone Hero e variantes)
    private static readonly string[] GuitarTrackNames =
        { "PART GUITAR", "T1 GEMS", "GUITAR" };

    // ── API pública ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica quais dificuldades têm notas no arquivo MIDI.
    /// Faz uma passagem leve pelo arquivo (só verifica presença de Note On).
    /// </summary>
    public static List<string> ScanDifficulties(string midiPath)
    {
        var result = new List<string>();
        if (!FileAccess.FileExists(midiPath)) return result;

        try
        {
            using var file = FileAccess.Open(midiPath, FileAccess.ModeFlags.Read);
            byte[] data = file.GetBuffer((long)file.GetLength());

            var (_, tracks) = ParseRaw(data);

            // Encontra o track de guitarra
            List<RawEvent> guitarEvents = null;
            foreach (var t in tracks)
                if (IsGuitarTrack(t.name)) { guitarEvents = t.events; break; }

            if (guitarEvents == null) return result;

            // Verifica quais faixas de notas têm eventos
            var presentNotes = new HashSet<int>();
            foreach (var ev in guitarEvents)
                if (ev.isNoteOn) presentNotes.Add(ev.note);

            foreach (string diff in TrackPriority)
            {
                if (!DiffBaseNote.TryGetValue(diff, out int baseNote)) continue;
                for (int fret = 0; fret <= 4; fret++)
                    if (presentNotes.Contains(baseNote + fret)) { result.Add(diff); break; }
            }
        }
        catch (System.Exception ex)
        {
            GD.PushError($"[MidiImporter] Erro ao escanear dificuldades: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Importa o MIDI e retorna um SongChart com as notas da dificuldade selecionada.
    /// <paramref name="audioOffsetSec"/> é o offset em segundos (delay do song.ini / 1000).
    /// </summary>
    public static SongChart Import(string midiPath, float audioOffsetSec = 0f, string difficulty = null)
    {
        if (!FileAccess.FileExists(midiPath))
        {
            GD.PushError($"[MidiImporter] Arquivo não encontrado: {midiPath}");
            return null;
        }

        try
        {
            using var file = FileAccess.Open(midiPath, FileAccess.ModeFlags.Read);
            byte[] data = file.GetBuffer((long)file.GetLength());

            var (ticksPerQuarter, tracks) = ParseRaw(data);

            // Monta tempo map a partir de todos os tracks (geralmente track 0)
            var tempoMap = new SortedDictionary<long, int>();
            tempoMap[0] = 500_000; // 120 BPM padrão
            foreach (var t in tracks)
                foreach (var ev in t.events)
                    if (ev.isTempo) tempoMap[ev.tick] = ev.tempoMicros;

            // Escolhe dificuldade
            string targetDiff = difficulty;
            if (targetDiff == null || !DiffBaseNote.ContainsKey(targetDiff))
                targetDiff = TrackPriority[0];
            int baseNote = DiffBaseNote[targetDiff];

            // Encontra o track de guitarra
            List<RawEvent> guitarEvents = null;
            foreach (var t in tracks)
                if (IsGuitarTrack(t.name)) { guitarEvents = t.events; break; }

            if (guitarEvents == null)
            {
                GD.PushWarning("[MidiImporter] Track PART GUITAR não encontrada no arquivo.");
                return new SongChart { BPM = 60_000_000f / tempoMap[0], StartOffset = audioOffsetSec };
            }

            // Converte eventos MIDI em NoteData
            // "Tap threshold": sustain < 1/16 de beat = é tap (IsLong = false)
            float tapThreshold = ticksPerQuarter / 4f;  // semínima/4 = semicolcheia

            var activeNotes = new Dictionary<int, long>(); // note → startTick
            var noteList    = new List<NoteData>();

            foreach (var ev in guitarEvents)
            {
                int fret = ev.note - baseNote;
                if (fret < 0 || fret > 4) continue;

                if (ev.isNoteOn)
                {
                    activeNotes[ev.note] = ev.tick;
                }
                else // Note Off
                {
                    if (!activeNotes.TryGetValue(ev.note, out long startTick)) continue;
                    activeNotes.Remove(ev.note);

                    long   sustainTicks = ev.tick - startTick;
                    bool   isHold       = sustainTicks >= tapThreshold;
                    double noteTime     = TicksToSeconds(startTick, tempoMap, ticksPerQuarter) + audioOffsetSec;
                    float  duration     = isHold
                        ? (float)(TicksToSeconds(ev.tick, tempoMap, ticksPerQuarter)
                                  - TicksToSeconds(startTick, tempoMap, ticksPerQuarter))
                        : 0f;

                    noteList.Add(new NoteData
                    {
                        Time     = noteTime,
                        Lane     = fret,
                        IsLong   = isHold,
                        Duration = duration,
                    });
                }
            }

            noteList.Sort((a, b) => a.Time.CompareTo(b.Time));

            var chart = new SongChart
            {
                BPM         = 60_000_000f / GetTempoAtTick(0, tempoMap),
                StartOffset = audioOffsetSec,
            };
            foreach (var nd in noteList) chart.Notes.Add(nd);

            GD.Print($"[MidiImporter] {chart.Notes.Count} notas ({targetDiff}), BPM≈{chart.BPM:F1}");
            return chart;
        }
        catch (System.Exception ex)
        {
            GD.PushError($"[MidiImporter] Erro ao importar MIDI: {ex.Message}");
            return null;
        }
    }

    // ── Parser binário ─────────────────────────────────────────────────────

    private record struct RawEvent(long tick, int note, bool isNoteOn, bool isTempo, int tempoMicros);
    private record struct TrackData(string name, List<RawEvent> events);

    private static (int ticksPerQuarter, List<TrackData> tracks) ParseRaw(byte[] data)
    {
        int pos = 0;

        // Header MThd
        if (!CheckMagic(data, pos, "MThd"))
            throw new System.Exception("Header MThd inválido");
        pos += 4;

        ReadInt32(data, ref pos); // header length (= 6)
        ReadInt16(data, ref pos); // format
        int nTracks        = ReadInt16(data, ref pos);
        int ticksPerQuarter = ReadInt16(data, ref pos);

        if ((ticksPerQuarter & 0x8000) != 0)
            throw new System.Exception("SMPTE timecode não suportado");

        var tracks = new List<TrackData>(nTracks);

        for (int t = 0; t < nTracks; t++)
        {
            if (pos + 8 > data.Length) break;
            if (!CheckMagic(data, pos, "MTrk"))
            {
                pos += 4;
                int skip = ReadInt32(data, ref pos);
                pos += skip;
                continue;
            }
            pos += 4;

            int trackLen = ReadInt32(data, ref pos);
            int trackEnd = pos + trackLen;

            string        trackName  = null;
            var           events     = new List<RawEvent>();
            long          absTick    = 0;
            byte          runStatus  = 0;

            while (pos < trackEnd)
            {
                long delta = ReadVlq(data, ref pos);
                absTick += delta;
                if (pos >= trackEnd) break;

                byte b = data[pos];

                if (b == 0xFF) // Meta event
                {
                    pos++;
                    byte metaType = data[pos++];
                    int  metaLen  = (int)ReadVlq(data, ref pos);

                    if (metaType == 0x51 && metaLen == 3) // Tempo
                    {
                        int micros = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
                        events.Add(new RawEvent(absTick, 0, false, true, micros));
                    }
                    else if (metaType == 0x03) // Track name
                    {
                        trackName = Encoding.UTF8.GetString(data, pos, metaLen).Trim();
                    }

                    pos += metaLen;
                }
                else if (b == 0xF0 || b == 0xF7) // SysEx
                {
                    pos++;
                    int sysexLen = (int)ReadVlq(data, ref pos);
                    pos += sysexLen;
                }
                else // MIDI event (com running status)
                {
                    byte status;
                    if ((b & 0x80) != 0) { status = b; runStatus = b; pos++; }
                    else                 { status = runStatus; }

                    byte type = (byte)(status & 0xF0);

                    if (type == 0x80 || type == 0x90) // Note Off / On
                    {
                        byte note = data[pos++];
                        byte vel  = data[pos++];
                        bool isOn = type == 0x90 && vel > 0;
                        events.Add(new RawEvent(absTick, note, isOn, false, 0));
                    }
                    else if (type == 0xA0 || type == 0xB0 || type == 0xE0) { pos += 2; }
                    else if (type == 0xC0 || type == 0xD0)                  { pos += 1; }
                    // 0xF0-0xF7 já tratados acima; 0xF8-0xFF são realtime (sem dados)
                }
            }

            pos = trackEnd;
            tracks.Add(new TrackData(trackName ?? $"Track{t}", events));
        }

        return (ticksPerQuarter, tracks);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool IsGuitarTrack(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var n in GuitarTrackNames)
            if (name.Equals(n, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static double TicksToSeconds(long ticks, SortedDictionary<long, int> tempoMap, int tpq)
    {
        double time     = 0;
        long   prevTick = 0;
        int    prevTempo = GetTempoAtTick(0, tempoMap);

        foreach (var (mapTick, tempo) in tempoMap)
        {
            if (mapTick >= ticks) break;
            time    += (mapTick - prevTick) / (double)tpq * (prevTempo / 1_000_000.0);
            prevTick = mapTick;
            prevTempo = tempo;
        }

        time += (ticks - prevTick) / (double)tpq * (prevTempo / 1_000_000.0);
        return time;
    }

    private static int GetTempoAtTick(long tick, SortedDictionary<long, int> tempoMap)
    {
        int tempo = 500_000;
        foreach (var (t, v) in tempoMap) { if (t > tick) break; tempo = v; }
        return tempo;
    }

    private static bool CheckMagic(byte[] data, int pos, string magic)
    {
        if (pos + 4 > data.Length) return false;
        for (int i = 0; i < 4; i++)
            if (data[pos + i] != (byte)magic[i]) return false;
        return true;
    }

    private static int ReadInt32(byte[] data, ref int pos)
    {
        int v = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
        pos += 4;
        return v;
    }

    private static int ReadInt16(byte[] data, ref int pos)
    {
        int v = (data[pos] << 8) | data[pos + 1];
        pos += 2;
        return v;
    }

    private static long ReadVlq(byte[] data, ref int pos)
    {
        long value = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            value = (value << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return value;
    }
}
