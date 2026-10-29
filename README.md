<p align="center"><img src="docs/icon.png" width="96" alt="Clawd"></p>

# Blox Code

Um bootstrapper de Roblox com a cara do **Claude desktop** e estrelando o **Clawd**, o mascote
do Claude Code. O fluxo de atualização/launch foi obtido por engenharia reversa do Bloxstrap 2.11.4
com o REA MCP e reescrito do zero em C#/WPF — veja [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md).

## O que ele faz

- Escolhe o espelho de CDN mais rápido do Roblox, consulta a versão do canal e baixa os pacotes
  do `rbxPkgManifest`, com cache por MD5 (e reaproveita o cache do Bloxstrap quando o hash bate).
- Extrai tudo no mesmo layout do Bloxstrap em `%LocalAppData%\BloxCode\Versions\<versão>`,
  grava `AppSettings.xml`, instala o WebView2 se faltar e limpa versões antigas.
- **Fast Flags**: presets (fullscreen exclusivo, DPI, MSAA, texturas) + um **Fast Flag Editor** no
  estilo do Bloxstrap — Add new (nome único ou Import JSON colado/de arquivo), Delete selected,
  Export JSON, busca, "Show preset flags", validação de prefixo/valor e chave liga/desliga para flags booleanas.
- **Mods**: tudo em `Modifications\` é copiado por cima do Roblox; os originais ficam guardados e
  voltam quando você apaga o mod.
- Registra `roblox://` e `roblox-player://`, atalhos e uma entrada em Aplicativos — tudo por usuário, sem admin.

## A interface

- Paleta do Claude (papel marfim / carvão, acento argila `#D97757`), títulos serifados, sidebar e
  "composer" iguais aos do app: cole um link de jogo ou um place ID e aperte Enter.
- Tela de launch no estilo do Claude Code: `✻ Downloading RobloxApp.zip… (12s · esc to cancel)`,
  com o Clawd andando em cima da barra de progresso, trabalhando ao extrair e pulando no final.
- Tema Sistema / Claro / Escuro.

O que mudou em cada versão está no [log de atualizações](CHANGELOG.md).

## Rodar e compilar

Requer o .NET SDK 10.

```bash
dotnet run --project BloxCode
```

```bash
dotnet publish BloxCode/BloxCode.csproj -c Release -o dist
```

O publish gera um único `dist/BloxCode.exe` self-contained (não precisa de .NET instalado).
Abra-o e clique em **Install** para que os links do Roblox passem a abrir o Blox Code.

Argumentos: `-player [uri]`, `-menu`/`-settings`, `-uninstall [-quiet]`, `-nolaunch`, `-force`, `-channel <nome>`, `-noupdate`.

## Atualização automática

Toda vez que a cópia instalada abre (menu ou link do Roblox), ela pergunta ao GitHub Releases se
há versão nova (no máximo 4 s). Se houver, baixa o `BloxCode.exe` novo para `%TEMP%\BloxCode\Updates`,
confere o SHA-256, reinicia com `-upgrade`, se copia por cima da instalação e continua o que você
tinha pedido (abrir o jogo, o menu…). Mesmo fluxo do Bloxstrap. Dá para desligar em Behaviour.

Para publicar uma atualização:

1. Uma vez: coloque seu repositório em `<UpdateRepository>dono/repo</UpdateRepository>` no
   [BloxCode.csproj](BloxCode/BloxCode.csproj) e reinstale esse build.
2. A cada versão: aumente `<Version>` (ex.: `1.0.1`), rode o `dotnet publish` e crie um release
   no GitHub com a tag `v1.0.1` anexando o `dist/BloxCode.exe`.

## Estrutura

```
BloxCode/
  Core/      Deployment, PackageManifest, Bootstrapper, Installer, Settings, FastFlagPresets…
  UI/        MainWindow (estilo Claude desktop), BootstrapperWindow, ClawdMascot, ClaudeSpinner
  UI/Pages/  Home, Fast Flags, Mods, Appearance, Behaviour, About
  UI/Themes/ Light.xaml, Dark.xaml, Styles.xaml
tools/
  extract-bundle.js  extrai um .NET single-file bundle (usado no Bloxstrap.exe)
  make-icon.js       gera Resources/BloxCode.ico a partir do pixel art do Clawd
```

Fora do escopo por enquanto: Roblox Studio, Discord Rich Presence, auto-update do próprio Blox Code.

Projeto de fã — sem afiliação com Roblox Corporation, Anthropic ou a equipe do Bloxstrap (MIT).
