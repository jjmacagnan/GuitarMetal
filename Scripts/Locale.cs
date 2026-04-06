using Godot;

/// <summary>
/// Wrapper fino sobre TranslationServer do Godot.
/// Mantém a API Locale.Tr() idêntica para compatibilidade com os 92 call sites existentes.
/// As traduções são carregadas do CSV em Translations/translations.csv pelo editor Godot.
/// </summary>
public static class Locale
{
	public enum Language { PT, EN, ES }

	private const string CsvPath = "res://Translations/translations.csv";
	private static bool _loaded;

	public static Language Current
	{
		get => TranslationServer.GetLocale() switch
		{
			"pt" => Language.PT,
			"es" => Language.ES,
			_    => Language.EN
		};
		set => TranslationServer.SetLocale(value switch
		{
			Language.PT => "pt",
			Language.ES => "es",
			_           => "en"
		});
	}

	/// <summary>Avança para o próximo idioma no ciclo PT → EN → ES → PT.</summary>
	public static void CycleLanguage()
	{
		Current = Current switch
		{
			Language.PT => Language.EN,
			Language.EN => Language.ES,
			Language.ES => Language.PT,
			_           => Language.PT
		};
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
		if (currentLocale != "pt" && currentLocale != "en" && currentLocale != "es")
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

		// Primeira linha: key,pt,en,es,...
		string header = file.GetLine();
		var headerFields = ParseCsvLine(header);
		if (headerFields.Length < 2) return;

		// Cria uma Translation para cada coluna de locale encontrada
		var translations = new System.Collections.Generic.Dictionary<int, Translation>();
		for (int i = 1; i < headerFields.Length; i++)
		{
			string locale = headerFields[i].Trim().ToLower();
			if (!string.IsNullOrEmpty(locale))
			{
				var t = new Translation();
				t.Locale = locale;
				translations[i] = t;
			}
		}

		if (translations.Count == 0)
		{
			GD.PushError("[Locale] CSV header sem colunas de locale.");
			return;
		}

		while (!file.EofReached())
		{
			string line = file.GetLine();
			if (string.IsNullOrWhiteSpace(line)) continue;

			var fields = ParseCsvLine(line);
			if (fields.Length < 2) continue;

			string key = fields[0].Trim();
			if (string.IsNullOrEmpty(key)) continue;

			foreach (var (idx, translation) in translations)
			{
				if (idx < fields.Length)
					translation.AddMessage(key, fields[idx]);
			}
		}

		foreach (var translation in translations.Values)
			TranslationServer.AddTranslation(translation);

		GD.Print($"[Locale] Traduções carregadas do CSV (fallback): {translations.Count} idiomas.");
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
