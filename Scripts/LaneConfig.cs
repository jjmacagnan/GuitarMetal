using Godot;

/// <summary>
/// Constantes centralizadas das lanes do jogo.
/// Fonte única da verdade para posições, cores, teclas e ações.
/// </summary>
public static class LaneConfig
{
	public const int LaneCount = 5;

	public static readonly float[] LaneX = { -4f, -2f, 0f, 2f, 4f };

	public static readonly Color[] LaneColors =
	{
		new Color(0.1f,  0.95f, 0.2f),   // Verde
		new Color(0.95f, 0.1f,  0.1f),   // Vermelho
		new Color(1.0f,  0.85f, 0.0f),   // Amarelo
		new Color(0.2f,  0.4f,  1.0f),   // Azul
		new Color(1.0f,  0.5f,  0.0f),   // Laranja
	};

	public static readonly Key[] LaneKeys =
		{ Key.A, Key.S, Key.J, Key.K, Key.L };

	public static readonly string[] LaneActions =
		{ "lane_0", "lane_1", "lane_2", "lane_3", "lane_4" };

	public static readonly string[] LaneNameKeys =
		{ "LANE_GREEN", "LANE_RED", "LANE_YELLOW", "LANE_BLUE", "LANE_ORANGE" };
}
