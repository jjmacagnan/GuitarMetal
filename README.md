# Guitar Metal — Godot 4 + C#

Jogo de ritmo estilo Guitar Hero construído do zero com Godot 4.6 e C#. Suporta charts no formato Clone Hero (`.chart`), hold notes, seleção de dificuldade, controle gamepad e teclado simultâneos.

---

## Requisitos

- Godot 4.6 (com suporte a C# / .NET)
- .NET 8 SDK

> Os arquivos de áudio (`.ogg`, `.mp3`) e charts (`.chart`) **não estão incluídos** no repositório. Adicione-os na pasta `Audio/` localmente.

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
│   ├── SongIniReader.cs     ← Leitor de song.ini (nome, artista, delay)
│   ├── GameData.cs          ← Dados estáticos entre cenas
│   ├── LoadingScreen.cs     ← State machine de carregamento
│   ├── SongSelectMenu.cs    ← Seleção de música (scan da pasta Audio/)
│   ├── DifficultySelect.cs  ← Seleção de dificuldade
│   ├── MainMenu.cs          ← Menu principal
│   └── ResultsScreen.cs     ← Tela de resultado
├── Scenes/
│   ├── MainMenu.tscn
│   ├── SongSelect.tscn
│   ├── DifficultySelect.tscn
│   ├── Loading.tscn
│   ├── Game.tscn
│   └── Results.tscn
├── Audio/               ← Coloque seus .ogg/.mp3 e .chart aqui (ignorados pelo git)
└── project.godot
```

---

## Fluxo do Jogo

```
MainMenu → SongSelect → [DifficultySelect] → Loading → Game → Results
```

---

## Controles

### Teclado

| Tecla | Lane | Cor      |
|-------|------|----------|
| A     | 0    | Verde    |
| S     | 1    | Vermelho |
| J     | 2    | Amarelo  |
| K     | 3    | Azul     |
| L     | 4    | Laranja  |
| ESC   | —    | Pause    |

### Gamepad (Switch Pro / Xbox)

| Botão       | Lane | Cor      |
|-------------|------|----------|
| ZL / LT     | 0    | Verde    |
| L / LB      | 1    | Vermelho |
| R / RB      | 2    | Amarelo  |
| ZR / RT     | 3    | Azul     |
| X (topo)    | 4    | Laranja  |
| Start / +   | —    | Pause    |

Teclado e gamepad funcionam simultaneamente. Navegação de menus pelo D-pad + A (confirmar) / B (voltar).

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

### Formato `.chart` suportado

Compatível com o formato Clone Hero. Dificuldades suportadas:
`ExpertSingle`, `HardSingle`, `MediumSingle`, `EasySingle`

Suporta mudanças de BPM (múltiplos eventos `B` no `[SyncTrack]`).

### Fallback: chart procedural

Se não houver `.chart`, o jogo gera um chart automático baseado no BPM e duração do áudio.

---

## Licença

MIT
