# Guitar Metal — Godot 4 + C#

[![🇧🇷 Português](https://img.shields.io/badge/lang-Português-green?style=flat-square)](README.md)
[![🇺🇸 English](https://img.shields.io/badge/lang-English-blue?style=flat-square)](README.en.md)

Jogo de ritmo inspirado em Guitar Hero, construído do zero com Godot 4.6 e C#. Suporta charts no formato Clone Hero (`.chart`) e Rock Band (`.mid`), hold notes, seleção de dificuldade, controle gamepad e teclado simultâneos, leaderboard local e internacionalização PT/EN.

> Projeto desenvolvido para a disciplina de **Desenvolvimento de Jogos para Smartphones**, integrante da **Especialização em Programação para Dispositivos Móveis** oferecida pela **UTFPR — Universidade Tecnológica Federal do Paraná**.

---

## Requisitos

- Godot 4.6 (com suporte a C# / .NET)
- .NET 8 SDK

> Os arquivos de áudio (`.ogg`, `.mp3`) e charts (`.chart` / `.mid`) **não estão incluídos** no repositório. Adicione-os na pasta `Audio/` localmente.

---

## Estrutura do Projeto

```
res://
├── Scripts/
│   ├── GameManager.cs       ← Controlador principal (spawn, score, HUD, pause)
│   ├── Lane.cs              ← Lógica de pista (input, visuals, hold tracking)
│   ├── Note.cs              ← Física e visual da nota (tap e hold)
│   ├── SongChart.cs         ← Estrutura de dados + geração procedural
│   ├── ChartImporter.cs     ← Parser de arquivos .chart (Clone Hero)
│   ├── MidiImporter.cs      ← Parser de arquivos .mid (Rock Band)
│   ├── SongIniReader.cs     ← Leitor de song.ini (nome, artista, delay)
│   ├── GameData.cs          ← Dados estáticos entre cenas
│   ├── LoadingScreen.cs     ← State machine de carregamento
│   ├── SongSelectMenu.cs    ← Seleção de música (scan da pasta Audio/)
│   ├── DifficultySelect.cs  ← Seleção de dificuldade
│   ├── MainMenu.cs          ← Menu principal
│   ├── NameInput.cs         ← Teclado virtual (gamepad-friendly)
│   ├── ResultsScreen.cs     ← Tela de resultado
│   ├── Leaderboard.cs       ← Top 10 scores por música
│   ├── ScoreStorage.cs      ← Persistência de scores (JSON)
│   ├── KeybindingStorage.cs ← Persistência e aplicação de keybindings customizados
│   ├── Locale.cs            ← Internacionalização PT/EN
│   ├── SettingsMenu.cs      ← Tela de configurações (remapeamento de teclas)
│   ├── Credits.cs           ← Tela de créditos e licença
│   └── MobileUI.cs          ← Autoload: escala UI para Android e iOS
├── Scenes/
│   ├── MainMenu.tscn
│   ├── NameInput.tscn
│   ├── SongSelect.tscn
│   ├── DifficultySelect.tscn
│   ├── Loading.tscn
│   ├── Game.tscn
│   ├── Results.tscn
│   ├── Leaderboard.tscn
│   ├── Settings.tscn
│   ├── Credits.tscn
│   ├── Lane.tscn            ← Componente de pista (instanciado pelo Game)
│   └── Note.tscn            ← Componente de nota (tap e hold)
├── Audio/               ← Coloque seus .ogg/.mp3 e .chart/.mid aqui (ignorados pelo git)
├── SFX/                 ← Efeitos sonoros do jogo
├── LICENSE
└── project.godot
```

---

## Fluxo do Jogo

```
MainMenu → NameInput → SongSelect → [DifficultySelect] → Loading → Game → Results
    ↕           ↕                                                           ↕
 Settings   Leaderboard                                                 MainMenu
                ↕
            Credits
```

---

## Controles

> Os controles abaixo são os **padrões**. Todas as teclas e botões de lane podem ser remapeados na tela de **Configurações** (Menu Principal → Configurações).

### Teclado (padrão)

| Tecla | Lane | Cor      |
|-------|------|----------|
| A     | 0    | Verde    |
| S     | 1    | Vermelho |
| J     | 2    | Amarelo  |
| K     | 3    | Azul     |
| L     | 4    | Laranja  |
| ESC   | —    | Pausar   |

### Gamepad (padrão — Switch Pro / Xbox)

| Botão       | Lane | Cor      |
|-------------|------|----------|
| ZL / LT     | 0    | Verde    |
| L / LB      | 1    | Vermelho |
| R / RB      | 2    | Amarelo  |
| ZR / RT     | 3    | Azul     |
| X (topo)    | 4    | Laranja  |
| Start / +   | —    | Pausar   |

### Touch (Android / iOS)

Em dispositivos móveis, o jogo detecta automaticamente zonas de toque baseadas na projeção 3D dos botões na tela. Cada lane possui uma área de toque correspondente. Suporta multi-touch para hold notes com segurança contra toques simultâneos.

Teclado e gamepad funcionam simultaneamente. Navegação de menus pelo D-pad + A (confirmar) / B (voltar).

---

## Configurações

Acesse **Menu Principal → Configurações** para remapear as teclas de cada lane.

- **Aba Teclado** — clique em uma lane e pressione a tecla desejada. `ESC` cancela.
- **Aba Controle** — clique em uma lane e pressione o botão ou gatilho desejado.
- **Restaurar Padrões** — volta para o mapeamento original.
- As configurações são salvas automaticamente ao sair da tela (`user://keybindings.cfg`).
- As legendas no Menu Principal e no HUD do jogo refletem sempre os bindings ativos.

---

## Pontuação

| Timing  | Janela  | Tempo  | Pontos base |
|---------|---------|--------|-------------|
| PERFECT | < 0.90u | < 25ms | 100         |
| GREAT   | < 2.16u | < 60ms | 75          |
| GOOD    | < 3.24u | < 90ms | 50          |
| HOLD    | completo | —     | 150         |
| MISS    | —       | —      | 0 + reset combo |

**Multiplicadores:**

| Combo | Multiplicador |
|-------|--------------|
| < 10  | 1x           |
| >= 10 | 2x           |
| >= 20 | 4x           |
| >= 30 | 8x           |

**Grades:** S >= 95% · A >= 85% · B >= 70% · C >= 55% · D < 55%

As janelas de timing são ajustadas por dificuldade: Easy (1.5×), Medium (1.2×), Hard (1.0×), Expert (0.85×).

---

## Sincronização

As notas são posicionadas diretamente pelo clock do áudio (`GameData.SongTime`), não por acúmulo de delta por frame. Isso garante sincronização perfeita independente de variações de frame rate.

O campo `AudioLatencyOffset` (Export no GameManager) permite compensação manual de latência se necessário.

---

## Adicionando Músicas

### Formato Clone Hero / Enchor (recomendado)

Crie uma subpasta em `Audio/` com a estrutura:

```
Audio/
└── Metallica - Master of Puppets/
    ├── notes.chart    ← chart de notas
    ├── song.ini       ← metadados (nome, artista, delay)
    └── song.ogg       ← áudio da música
```

O jogo lê `song.ini` para exibir "Artista - Título" na lista de seleção.

> 🎸 Encontre milhares de charts gratuitos em **[Enchor](https://www.enchor.us)** — maior repositório de músicas compatíveis com Clone Hero.

### Formato Rock Band (MIDI)

Coloque o arquivo `.mid` junto com o áudio:

```
Audio/
└── NomeMusica/
    ├── notes.mid      ← chart no formato Rock Band
    └── song.ogg
```

Dificuldades MIDI suportadas (notas MIDI): Expert (96–100), Hard (84–88), Medium (72–76), Easy (60–64).

### Formato solto (arquivo único)

Coloque o áudio e o `.chart` na pasta `Audio/` com o mesmo nome base:

```
Audio/
├── MinhaMusica.ogg
└── MinhaMusica.chart
```

### Formatos de áudio suportados

| Formato | Suportado |
|---------|-----------|
| `.ogg`  | ✅ Recomendado |
| `.mp3`  | ✅ |
| `.wav`  | ✅ |
| `.opus` | ❌ Não suportado pelo Godot 4 |

> **Dica:** Converta `.opus` para `.ogg` com `ffmpeg -i song.opus song.ogg`

### Fallback: chart procedural

Se não houver `.chart` ou `.mid`, o jogo gera um chart automático baseado no BPM e duração do áudio.

---

## Leaderboard

Os scores são salvos localmente em `user://scores.json` (pasta de dados do Godot no sistema operacional). O leaderboard exibe os top 10 por música, com nome do jogador, score, grade, precisão e combo máximo. É possível limpar os scores de uma música individualmente.

---

## Idiomas

O jogo suporta **Português (BR)** e **Inglês**. O idioma pode ser alterado pelo botão de idioma no menu principal. A preferência é aplicada em tempo real, sem necessidade de reiniciar.

---

## Plataformas

### Desktop

Funciona em Windows, macOS e Linux com Godot 4.6. Controle por teclado e/ou gamepad.

### Android

- Pacote: `br.app.jbit.guitarmetal`
- Arquiteturas: armeabi-v7a + arm64-v8a
- Renderização: `gl_compatibility` (otimizado para mobile)
- Entrada por toque com zonas projetadas da posição 3D dos botões
- Modo imersivo em tela cheia com suporte a rotação
- MobileUI autoload escala a interface para resolução 1280×720

### iOS

- Bundle: `br.app.jbit.guitarmetal`
- Exporta projeto Xcode pronto para build
- Assinatura Apple Development (Team ID configurável em `export_presets.cfg`)
- MobileUI autoload escala a interface para resolução 1280×720

---

## Licença

Distribuído sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE) para mais detalhes.
