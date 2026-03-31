using Godot;

/// <summary>
/// Gerencia score, combo, multiplicador e timing windows.
/// Classe pura (não-Node) — sem dependência de cena.
/// </summary>
public class ScoreManager
{
	public int Score          { get; private set; }
	public int Combo          { get; private set; }
	public int Multiplier     { get; private set; } = 1;
	public int NotesHit       { get; private set; }
	public int ResolvedNotes  { get; private set; }
	public int TotalNotes     { get; }
	public bool AllResolved   => ResolvedNotes >= TotalNotes;

	public ScoreManager(int totalNotes)
	{
		TotalNotes = totalNotes;
	}

	/// <summary>
	/// Processa um acerto de nota. Retorna (pontos ganhos, label de feedback, cor de feedback).
	/// </summary>
	public (int score, string label, Color color) ProcessHit(Note note, string difficulty, float noteSpeed)
	{
		float dist = Mathf.Abs(note.GlobalPosition.Z - Note.HitLineZ);

		float diffMult = difficulty switch
		{
			"EasySingle"   => 1.5f,
			"MediumSingle" => 1.2f,
			"HardSingle"   => 1.0f,
			"ExpertSingle" => 0.85f,
			_              => 1.0f
		};

		float perfectThreshold = 0.025f * noteSpeed * diffMult;
		float greatThreshold   = 0.06f  * noteSpeed * diffMult;
		float goodThreshold    = 0.09f  * noteSpeed * diffMult;

		// Hit fora da janela válida → tratar como miss
		if (dist > goodThreshold)
		{
			ProcessMiss();
			return (0, Locale.Tr("MISS"), Colors.Red);
		}

		// Hit válido — incrementa combo
		Combo++;
		UpdateMultiplier();
		if (Combo > GameData.MaxCombo) GameData.MaxCombo = Combo;

		int    baseScore;
		string label;
		Color  color;

		if      (dist <= perfectThreshold) { baseScore = 100; label = Locale.Tr("PERFECT"); color = Colors.Cyan;   }
		else if (dist <= greatThreshold)   { baseScore =  75; label = Locale.Tr("GREAT");   color = Colors.Yellow; }
		else                                { baseScore =  50; label = Locale.Tr("GOOD");    color = Colors.White;  }

		int points = baseScore * Multiplier;
		Score += points;
		NotesHit++;
		GameData.NotesHit++;

		// Hold notes: não conta como resolvida agora — será resolvida no HoldComplete ou miss
		if (!note.IsLong)
			ResolvedNotes++;

		return (points, label, color);
	}

	/// <summary>
	/// Processa a conclusão de uma hold note. Retorna os pontos ganhos.
	/// </summary>
	public int ProcessHoldComplete()
	{
		Combo++;
		UpdateMultiplier();
		if (Combo > GameData.MaxCombo) GameData.MaxCombo = Combo;

		int points = 150 * Multiplier;
		Score += points;
		GameData.HoldsComplete++;
		ResolvedNotes++;

		return points;
	}

	/// <summary>
	/// Processa um miss de nota.
	/// </summary>
	public void ProcessMiss()
	{
		Combo      = 0;
		Multiplier = 1;
		GameData.NotesMissed++;
		ResolvedNotes++;
	}

	/// <summary>
	/// Conta notas não-resolvidas como miss (fim de música).
	/// Retorna a quantidade de notas não-resolvidas.
	/// </summary>
	public int FinalizeUnresolved()
	{
		int unresolved = TotalNotes - ResolvedNotes;
		if (unresolved > 0)
		{
			GameData.NotesMissed += unresolved;
			ResolvedNotes = TotalNotes;
		}
		return unresolved;
	}

	private void UpdateMultiplier()
	{
		Multiplier = Combo switch { >= 30 => 8, >= 20 => 4, >= 10 => 2, _ => 1 };
	}
}
