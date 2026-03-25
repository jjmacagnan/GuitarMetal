using Godot;

/// <summary>
/// Wrapper fino sobre TranslationServer do Godot.
/// Mantém a API Locale.Tr() idêntica para compatibilidade com os 92 call sites existentes.
/// As traduções são carregadas do CSV em Translations/translations.csv pelo editor Godot.
/// </summary>
public static class Locale
{
	public enum Language { PT, EN }

	private const string CsvPath = "res://Translations/translations.csv";
	private static bool _loaded;

	public static Language Current
	{
		get => TranslationServer.GetLocale() == "pt" ? Language.PT : Language.EN;
		set
		{
			TranslationServer.SetLocale(value == Language.PT ? "pt" : "en");
		}
	}

	/// <summary>Retorna a string traduzida para o idioma ativo.</summary>
	public static string Tr(string key)
	{
		EnsureLoaded();
		string result = TranslationServer.Translate(key);
		// TranslationServer retorna a própria key se não encontrar tradução
		return result;
	}

	/// <summary>Retorna a string formatada com args (usa string.Format).</summary>
	public static string Tr(string key, params object[] args)
	{
		string fmt = Tr(key);
		return string.Format(fmt, args);
	}

	/// <summary>
	/// Carrega as traduções do CSV diretamente se os .translation binários
	/// ainda não foram gerados pelo editor Godot.
	/// Isso garante que o jogo funcione mesmo sem ter aberto o editor após o refactor.
	/// </summary>
	private static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;

		// Define PT como locale padrão (comportamento anterior do campo estático)
		string currentLocale = TranslationServer.GetLocale();
		if (currentLocale != "pt" && currentLocale != "en")
			TranslationServer.SetLocale("pt");

		// Se o TranslationServer já tem traduções (editor gerou os .translation), não precisa carregar
		string test = TranslationServer.Translate("PLAY");
		if (test != "PLAY") return;

		// Fallback: carrega do CSV manualmente
		if (!FileAccess.FileExists(CsvPath))
		{
			GD.PushWarning("[Locale] translations.csv não encontrado — usando keys como fallback.");
			return;
		}

		LoadFromCsv();
	}

	private static void LoadFromCsv()
	{
		using var file = FileAccess.Open(CsvPath, FileAccess.ModeFlags.Read);
		if (file == null) return;

		// Primeira linha: key,pt,en
		string header = file.GetLine();
		string[] columns = header.Split(',');
		if (columns.Length < 3) return;

		// Identifica índices das colunas de locale
		int ptIdx = -1, enIdx = -1;
		for (int i = 1; i < columns.Length; i++)
		{
			string col = columns[i].Trim().ToLower();
			if (col == "pt") ptIdx = i;
			else if (col == "en") enIdx = i;
		}

		if (ptIdx < 0 || enIdx < 0)
		{
			GD.PushError("[Locale] CSV header inválido — esperado: key,pt,en");
			return;
		}

		var ptTranslation = new Translation();
		ptTranslation.Locale = "pt";
		var enTranslation = new Translation();
		enTranslation.Locale = "en";

		while (!file.EofReached())
		{
			string line = file.GetLine();
			if (string.IsNullOrWhiteSpace(line)) continue;

			var fields = ParseCsvLine(line);
			if (fields.Length < 3) continue;

			string key = fields[0].Trim();
			if (string.IsNullOrEmpty(key)) continue;

			if (ptIdx < fields.Length) ptTranslation.AddMessage(key, fields[ptIdx]);
			if (enIdx < fields.Length) enTranslation.AddMessage(key, fields[enIdx]);
		}

		TranslationServer.AddTranslation(ptTranslation);
		TranslationServer.AddTranslation(enTranslation);
		GD.Print("[Locale] Traduções carregadas do CSV (fallback).");
	}

	/// <summary>
	/// Parser CSV simples que respeita campos entre aspas.
	/// </summary>
	private static string[] ParseCsvLine(string line)
	{
		var fields = new System.Collections.Generic.List<string>();
		int i = 0;

		while (i < line.Length)
		{
			if (line[i] == '"')
			{
				// Campo entre aspas
				i++; // pula aspas iniciais
				int start = i;
				var field = new System.Text.StringBuilder();
				while (i < line.Length)
				{
					if (line[i] == '"')
					{
						if (i + 1 < line.Length && line[i + 1] == '"')
						{
							field.Append('"');
							i += 2;
						}
						else
						{
							i++; // pula aspas finais
							break;
						}
					}
					else
					{
						field.Append(line[i]);
						i++;
					}
				}
				fields.Add(field.ToString());
				if (i < line.Length && line[i] == ',') i++; // pula vírgula
			}
			else
			{
				// Campo sem aspas
				int start = i;
				while (i < line.Length && line[i] != ',') i++;
				fields.Add(line[start..i]);
				if (i < line.Length) i++; // pula vírgula
			}
		}

		return fields.ToArray();
	}
}
