using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Importa charts de arquivos MIDI (notes.mid) no formato Rock Band / Harmonix.
///
/// Tracks suportadas:
///   PART GUITAR  → notas de guitarra
///   PART BASS    → notas de baixo (futuro)
///
/// Mapeamento de notas MIDI → lanes (5 frets):
///   Expert : 96–100  →  lanes 0–4
///   Hard   : 84–88   →  lanes 0–4
///   Medium : 72–76   →  lanes 0–4
///   Easy   : 60–64   →  lanes 0–4
///
/// Hold notes são determinadas pela duração entre note-on e note-off.
/// O tempo é convertido de ticks para segundos usando o mapa de tempo (Set Tempo events).
/// </summary>
public static class MidiImporter
{
    // Mapeamento de dificuldades: nome padrão (compatível com ChartImporter) → nota MIDI base
    private static readonly (string name, int baseNote)[] Difficulties =
    {
        ("ExpertSingle", 96),
        ("HardSingle",   84),
        ("MediumSingle", 72),
        ("EasySingle",   60),
    };

    /// <summary>
    /// Escaneia um arquivo MIDI e retorna quais dificuldades estão presentes
    /// na track PART GUITAR. Retorna nomes compatíveis com ChartImporter
    /// (ex: "ExpertSingle", "HardSingle").
    /// </summary>
    public static List<string> ScanDifficulties(string midiPath)
    {
        var result = new List<string>();
        if (!FileAccess.FileExists(midiPath)) return result;

        try
        {
            byte[] data = ReadAllBytes(midiPath);
            var midi = ParseHeader(data);
            if (midi == null) return result;

            // Encontra a track PART GUITAR
            byte[] guitarTrack = FindTrackByName(data, midi, "PART GUITAR");
            if (guitarTrack == null) return result;

            // Coleta quais notas MIDI existem na track
            var notesPresent = new HashSet<int>();
            ParseTrackNotes(guitarTrack, (note, vel, tick) =>
            {
                if (vel > 0) notesPresent.Add(note);
            });

            // Verifica quais dificuldades têm notas
            foreach (var (name, baseNote) in Difficulties)
            {
                for (int lane = 0; lane < 5; lane++)
                {
                    if (notesPresent.Contains(baseNote + lane))
                    {
                        result.Add(name);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[MidiImporter] Erro ao escanear dificuldades: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Importa notas de um arquivo MIDI. Se <paramref name="difficulty"/> for informado,
    /// usa essa dificuldade; caso contrário pega a mais alta disponível.
    /// Retorna um SongChart compatível com o restante do sistema.
    /// </summary>
    public static SongChart Import(string midiPath, string difficulty = null)
    {
        if (!FileAccess.FileExists(midiPath))
        {
            GD.PushError($"[MidiImporter] Arquivo não encontrado: {midiPath}");
            return null;
        }

        try
        {
            byte[] data = ReadAllBytes(midiPath);
            var midi = ParseHeader(data);
            if (midi == null) return null;

            // Extrai mapa de tempo da track 0
            byte[] tempoTrack = GetTrackData(data, midi, 0);
            var tempoMap = ParseTempoMap(tempoTrack);
            if (tempoMap.Count == 0) tempoMap[0] = 500000; // 120 BPM default

            // Encontra a track PART GUITAR
            byte[] guitarTrack = FindTrackByName(data, midi, "PART GUITAR");
            if (guitarTrack == null)
            {
                GD.PushWarning("[MidiImporter] Track PART GUITAR não encontrada no MIDI.");
                return null;
            }

            // Determina a dificuldade a usar
            int baseNote = ResolveBaseNote(guitarTrack, difficulty);
            if (baseNote < 0)
            {
                GD.PushWarning("[MidiImporter] Nenhuma dificuldade encontrada no MIDI.");
                return null;
            }

            // Parseia as notas
            var noteList = ExtractNotes(guitarTrack, baseNote, tempoMap, midi.Division);

            // Calcula BPM inicial
            float initialBpm = 60_000_000f / tempoMap[0];

            // Lê o nome da música da track 0 (meta event FF 03)
            string songName = ReadTrackName(tempoTrack);

            var chart = new SongChart
            {
                BPM         = initialBpm,
                StartOffset = 0f,
                SongName    = songName ?? "",
            };

            noteList.Sort((a, b) => a.Time.CompareTo(b.Time));
            foreach (var nd in noteList)
                chart.Notes.Add(nd);

            GD.Print($"[MidiImporter] '{songName}' — {chart.Notes.Count} notas (base={baseNote}), BPM={initialBpm:F1}");
            return chart;
        }
        catch (Exception ex)
        {
            GD.PushError($"[MidiImporter] Erro ao importar MIDI: {ex.Message}");
            return null;
        }
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private class MidiHeader
    {
        public int Format;
        public int NumTracks;
        public int Division; // ticks per quarter note
    }

    private static MidiHeader ParseHeader(byte[] data)
    {
        if (data.Length < 14) return null;

        // MThd
        if (data[0] != 'M' || data[1] != 'T' || data[2] != 'h' || data[3] != 'd')
        {
            GD.PushError("[MidiImporter] Não é um arquivo MIDI válido.");
            return null;
        }

        return new MidiHeader
        {
            Format    = ReadInt16BE(data, 8),
            NumTracks = ReadInt16BE(data, 10),
            Division  = ReadInt16BE(data, 12),
        };
    }

    // ── Track data extraction ─────────────────────────────────────────────────

    /// <summary>Retorna os bytes de dados de uma track pelo índice.</summary>
    private static byte[] GetTrackData(byte[] data, MidiHeader midi, int trackIndex)
    {
        int offset = 14; // Após o header MThd
        for (int t = 0; t < midi.NumTracks && offset < data.Length - 8; t++)
        {
            // MTrk
            if (data[offset] != 'M' || data[offset + 1] != 'T' ||
                data[offset + 2] != 'r' || data[offset + 3] != 'k')
                return null;

            int chunkLen = ReadInt32BE(data, offset + 4);
            int dataStart = offset + 8;

            if (t == trackIndex)
            {
                byte[] trackData = new byte[chunkLen];
                Array.Copy(data, dataStart, trackData, 0, Math.Min(chunkLen, data.Length - dataStart));
                return trackData;
            }

            offset = dataStart + chunkLen;
        }
        return null;
    }

    /// <summary>Encontra uma track pelo nome (meta event FF 03 = Track Name).</summary>
    private static byte[] FindTrackByName(byte[] data, MidiHeader midi, string targetName)
    {
        int offset = 14;
        for (int t = 0; t < midi.NumTracks && offset < data.Length - 8; t++)
        {
            if (data[offset] != 'M' || data[offset + 1] != 'T' ||
                data[offset + 2] != 'r' || data[offset + 3] != 'k')
                return null;

            int chunkLen = ReadInt32BE(data, offset + 4);
            int dataStart = offset + 8;
            int dataEnd = Math.Min(dataStart + chunkLen, data.Length);

            // Procura o nome da track nos primeiros bytes
            string name = FindTrackNameInData(data, dataStart, dataEnd);
            if (name != null && name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                byte[] trackData = new byte[chunkLen];
                Array.Copy(data, dataStart, trackData, 0, Math.Min(chunkLen, data.Length - dataStart));
                return trackData;
            }

            offset = dataStart + chunkLen;
        }
        return null;
    }

    private static string FindTrackNameInData(byte[] data, int start, int end)
    {
        int pos = start;
        while (pos < end - 3)
        {
            // Pula delta time
            while (pos < end && (data[pos] & 0x80) != 0) pos++;
            if (pos >= end) break;
            pos++; // último byte do delta

            if (pos >= end) break;

            // Meta event?
            if (data[pos] == 0xFF)
            {
                pos++;
                if (pos >= end) break;
                int metaType = data[pos]; pos++;
                if (pos >= end) break;

                int len = ReadVarLen(data, ref pos, end);
                if (metaType == 0x03 && len > 0 && pos + len <= end) // Track Name
                {
                    return Encoding.ASCII.GetString(data, pos, len);
                }
                pos += len;
                continue;
            }

            // Não é meta — para de procurar nome nos primeiros eventos
            break;
        }
        return null;
    }

    // ── Tempo map ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parseia o mapa de tempo (Set Tempo events) de uma track.
    /// Retorna tick → microseconds per quarter note.
    /// </summary>
    private static SortedDictionary<long, int> ParseTempoMap(byte[] trackData)
    {
        var map = new SortedDictionary<long, int>();
        if (trackData == null) return map;

        int pos = 0;
        long absTick = 0;
        int runningStatus = 0;

        while (pos < trackData.Length)
        {
            // Delta time
            long delta = ReadVarLenLong(trackData, ref pos, trackData.Length);
            absTick += delta;

            if (pos >= trackData.Length) break;
            int b = trackData[pos];

            if (b == 0xFF) // Meta event
            {
                pos++;
                if (pos >= trackData.Length) break;
                int metaType = trackData[pos]; pos++;
                int len = ReadVarLen(trackData, ref pos, trackData.Length);

                if (metaType == 0x51 && len == 3 && pos + 3 <= trackData.Length) // Set Tempo
                {
                    int tempo = (trackData[pos] << 16) | (trackData[pos + 1] << 8) | trackData[pos + 2];
                    map[absTick] = tempo;
                }

                pos += len;
                continue;
            }

            if (b == 0xF0 || b == 0xF7) // SysEx
            {
                pos++;
                int len = ReadVarLen(trackData, ref pos, trackData.Length);
                pos += len;
                continue;
            }

            // Channel message
            if ((b & 0x80) != 0)
            {
                runningStatus = b;
                pos++;
            }

            int ch = runningStatus & 0xF0;
            if (ch == 0x80 || ch == 0x90 || ch == 0xA0 || ch == 0xB0 || ch == 0xE0)
                pos += 2;
            else if (ch == 0xC0 || ch == 0xD0)
                pos += 1;
            else
                pos++; // segurança
        }

        return map;
    }

    // ── Note parsing ──────────────────────────────────────────────────────────

    /// <summary>Callback para cada nota encontrada na track.</summary>
    private delegate void NoteCallback(int note, int velocity, long absTick);

    /// <summary>Parseia todos os note-on/off de uma track.</summary>
    private static void ParseTrackNotes(byte[] trackData, NoteCallback callback)
    {
        if (trackData == null) return;

        int pos = 0;
        long absTick = 0;
        int runningStatus = 0;

        while (pos < trackData.Length)
        {
            long delta = ReadVarLenLong(trackData, ref pos, trackData.Length);
            absTick += delta;

            if (pos >= trackData.Length) break;
            int b = trackData[pos];

            if (b == 0xFF) // Meta event
            {
                pos++;
                if (pos >= trackData.Length) break;
                pos++; // meta type
                int len = ReadVarLen(trackData, ref pos, trackData.Length);
                pos += len;
                continue;
            }

            if (b == 0xF0 || b == 0xF7) // SysEx
            {
                pos++;
                int len = ReadVarLen(trackData, ref pos, trackData.Length);
                pos += len;
                continue;
            }

            if ((b & 0x80) != 0)
            {
                runningStatus = b;
                pos++;
            }

            int ch = runningStatus & 0xF0;
            if (ch == 0x90) // Note On
            {
                if (pos + 1 < trackData.Length)
                {
                    int note = trackData[pos];
                    int vel  = trackData[pos + 1];
                    callback(note, vel, absTick);
                }
                pos += 2;
            }
            else if (ch == 0x80) // Note Off
            {
                if (pos + 1 < trackData.Length)
                {
                    int note = trackData[pos];
                    callback(note, 0, absTick); // vel=0 → note off
                }
                pos += 2;
            }
            else if (ch == 0xA0 || ch == 0xB0 || ch == 0xE0)
                pos += 2;
            else if (ch == 0xC0 || ch == 0xD0)
                pos += 1;
            else
                pos++;
        }
    }

    /// <summary>
    /// Extrai NoteData da track de guitarra para uma dificuldade específica.
    /// </summary>
    private static List<NoteData> ExtractNotes(byte[] guitarTrack, int baseNote,
        SortedDictionary<long, int> tempoMap, int division)
    {
        // Primeiro, coleta note-on e note-off com ticks absolutos
        // Chave: note number → lista de (tick, isOn)
        var events = new Dictionary<int, List<(long tick, bool isOn)>>();

        ParseTrackNotes(guitarTrack, (note, vel, tick) =>
        {
            if (note < baseNote || note > baseNote + 4) return;

            if (!events.ContainsKey(note))
                events[note] = new List<(long, bool)>();

            events[note].Add((tick, vel > 0));
        });

        // Converte em NoteData: emparelha note-on com note-off
        var result = new List<NoteData>();

        foreach (var (midiNote, evts) in events)
        {
            int lane = midiNote - baseNote;

            for (int i = 0; i < evts.Count; i++)
            {
                if (!evts[i].isOn) continue; // pula note-off sem par

                long onTick = evts[i].tick;
                long offTick = onTick;

                // Procura o note-off correspondente
                for (int j = i + 1; j < evts.Count; j++)
                {
                    if (!evts[j].isOn) // note-off encontrado
                    {
                        offTick = evts[j].tick;
                        break;
                    }
                }

                double timeOn  = TicksToSeconds(onTick, tempoMap, division);
                double timeOff = TicksToSeconds(offTick, tempoMap, division);
                float  duration = (float)(timeOff - timeOn);

                // Notas com duração muito curta (<0.1s) são tratadas como taps
                bool isLong = duration > 0.1f;

                result.Add(new NoteData
                {
                    Time     = timeOn,
                    Lane     = lane,
                    IsLong   = isLong,
                    Duration = isLong ? duration : 0f,
                });
            }
        }

        return result;
    }

    /// <summary>Determina a nota base para a dificuldade solicitada.</summary>
    private static int ResolveBaseNote(byte[] guitarTrack, string difficulty)
    {
        // Se uma dificuldade específica foi solicitada, usa ela
        if (!string.IsNullOrEmpty(difficulty))
        {
            foreach (var (name, baseNote) in Difficulties)
            {
                if (name == difficulty) return baseNote;
            }
        }

        // Caso contrário, encontra a mais alta disponível
        var notesPresent = new HashSet<int>();
        ParseTrackNotes(guitarTrack, (note, vel, tick) =>
        {
            if (vel > 0) notesPresent.Add(note);
        });

        foreach (var (name, baseNote) in Difficulties)
        {
            for (int lane = 0; lane < 5; lane++)
            {
                if (notesPresent.Contains(baseNote + lane))
                    return baseNote;
            }
        }

        return -1; // nenhuma dificuldade encontrada
    }

    // ── Time conversion ───────────────────────────────────────────────────────

    /// <summary>Converte ticks absolutos em segundos usando o mapa de tempo.</summary>
    private static double TicksToSeconds(long ticks, SortedDictionary<long, int> tempoMap, int division)
    {
        double time = 0;
        long prevTick = 0;
        int prevTempo = 500000; // 120 BPM default

        foreach (var (mapTick, tempo) in tempoMap)
        {
            if (mapTick >= ticks) break;
            time += (mapTick - prevTick) / (double)division * (prevTempo / 1_000_000.0);
            prevTick = mapTick;
            prevTempo = tempo;
        }

        time += (ticks - prevTick) / (double)division * (prevTempo / 1_000_000.0);
        return time;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string ReadTrackName(byte[] trackData)
    {
        if (trackData == null) return null;
        int pos = 0;
        int end = trackData.Length;

        // Procura o primeiro meta event 03 (Track Name)
        while (pos < end)
        {
            // Delta
            ReadVarLenLong(trackData, ref pos, end);
            if (pos >= end) break;

            if (trackData[pos] == 0xFF)
            {
                pos++;
                if (pos >= end) break;
                int metaType = trackData[pos]; pos++;
                int len = ReadVarLen(trackData, ref pos, end);
                if (metaType == 0x03 && len > 0 && pos + len <= end)
                    return Encoding.ASCII.GetString(trackData, pos, len);
                pos += len;
                continue;
            }

            // Qualquer outro evento — pula
            if ((trackData[pos] & 0x80) != 0) pos++;
            int ch2 = trackData[pos > 0 ? pos - 1 : pos] & 0xF0;
            if (ch2 == 0x80 || ch2 == 0x90 || ch2 == 0xA0 || ch2 == 0xB0 || ch2 == 0xE0)
                pos += 2;
            else if (ch2 == 0xC0 || ch2 == 0xD0)
                pos += 1;
            else
                break;
        }
        return null;
    }

    private static byte[] ReadAllBytes(string godotPath)
    {
        using var file = FileAccess.Open(godotPath, FileAccess.ModeFlags.Read);
        if (file == null) return Array.Empty<byte>();
        long length = (long)file.GetLength();
        return file.GetBuffer(length);
    }

    private static int ReadInt16BE(byte[] data, int offset)
        => (data[offset] << 8) | data[offset + 1];

    private static int ReadInt32BE(byte[] data, int offset)
        => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static int ReadVarLen(byte[] data, ref int pos, int end)
    {
        int val = 0;
        while (pos < end)
        {
            int b = data[pos]; pos++;
            val = (val << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return val;
    }

    private static long ReadVarLenLong(byte[] data, ref int pos, int end)
    {
        long val = 0;
        while (pos < end)
        {
            int b = data[pos]; pos++;
            val = (val << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return val;
    }
}
