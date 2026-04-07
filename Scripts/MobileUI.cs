using Godot;

/// <summary>
/// Autoload que aplica escala de UI adequada para dispositivos móveis (Android / iOS).
///
/// Em desktop, o design resolution 1920×1080 preenche o monitor e os elementos têm
/// tamanho confortável. Em celulares de alta densidade (400+ DPI), o mesmo resolution
/// produz fontes e botões minúsculos porque o scale factor fica próximo de 1.0.
///
/// A solução: reduzir o ContentScaleSize para 1280×720 no mobile. Com canvas_items
/// stretch, o Godot escala o canvas 2D (UI) automaticamente para preencher a tela.
/// O viewport 3D não é afetado.
///
/// Por que 1280×720 e não 960×540:
///   960×540 deixa apenas 540px virtuais de altura. Em phones landscape com aspect
///   ratio > 16:9 (iPhone 15 Pro ~19.5:9, Pixel 8 Pro ~20:9), a altura virtual
///   permanece 540px, que é insuficiente para o menu (VBox com ~460px de conteúdo
///   mínimo + margens). Resultado: título cortado, botões sobrepostos.
///   1280×720 fornece 720px de altura virtual → layout cabe com folga.
///
/// Exemplos de scale factor resultante com 1280×720:
///   iPhone 15 Pro landscape (2556×1179): min(2556/1280, 1179/720) = 1.64× → legível
///   Pixel 8 Pro landscape (2992×1344):   min(2992/1280, 1344/720) = 1.87× → legível
///   Pixel Tablet landscape (2560×1600):  min(2560/1280, 1600/720) = 2.00× → ótimo
/// </summary>
public partial class MobileUI : Node
{
	// Design resolution mobile: 2/3 da resolução desktop 1920×1080.
	// Garante 720px de altura virtual — suficiente para os menus — e escala
	// fontes/botões 1.6–2.0× em phones modernos.
	private static readonly Vector2I MobileDesignSize = new Vector2I(1280, 720);

	public override void _Ready()
	{
		// Inicializa buses e volumes de áudio salvos
		AudioSettings.Initialize();

		string os = OS.GetName();
		bool isMobile = os == "Android" || os == "iOS";
		if (!isMobile) return;

		var root = GetTree().Root;

		// Aplica design resolution menor → UI aparece 1.6–2.0× maior em telas 1080p+
		root.ContentScaleSize = MobileDesignSize;

		GD.Print($"[MobileUI] ContentScaleSize → {MobileDesignSize} " +
		         $"(tela física: {DisplayServer.ScreenGetSize()} @ {DisplayServer.ScreenGetDpi()} DPI)");
	}
}
