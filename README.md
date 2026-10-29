<p align="center"><img src="docs/icon.png" width="96" alt="Bit, o mascote do Blox Code"></p>

# Blox Code

Um bootstrapper de Roblox com a cara do **Claude desktop** e estrelando o **Bit**, nosso mascote:
um bloco de montar com um terminal no lugar do rosto. O fluxo de atualização/launch foi obtido por engenharia reversa do Bloxstrap 2.11.4
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
- Tela de launch com status ao vivo: `▖ Downloading RobloxApp.zip… (12s · esc to cancel)`,
  com o Bit andando em cima da barra de progresso, digitando código na tela ao extrair e pulando no final.
- Tema Sistema / Claro / Escuro.

O que mudou em cada versão está no [log de atualizações](CHANGELOG.md).

## Linux

O Blox Code roda nas distros baseadas no **Arch** (Arch, CachyOS, EndeavourOS, Manjaro) e no
**Debian/Ubuntu** (Ubuntu 22.04+, Linux Mint 21+, Pop!_OS, Zorin, Debian 12+), através do
[Sober](https://sober.vinegarhq.org/), que roda a versão Android do Roblox nativamente.

1. Baixe `BloxCode-x86_64.AppImage` do [último release](https://github.com/huginhohenriquerl-ops/blox-code/releases/latest).
2. Deixe executável e abra:

   ```bash
   chmod +x BloxCode-x86_64.AppImage && ./BloxCode-x86_64.AppImage
   ```

3. Na tela inicial, clique em **Install** (vai para o menu de aplicativos e passa a abrir os links
   `roblox://`) e em **Install Sober** se ele ainda não estiver instalado.

Se o AppImage não abrir, falta o FUSE: `sudo pacman -S --needed fuse3` (Arch) ou
`sudo apt install fuse3` (Debian/Ubuntu); ou rode com `--appimage-extract-and-run`. O Sober precisa
do Flatpak (`sudo pacman -S flatpak` ou `sudo apt install flatpak`); o Blox Code mostra o comando
certo se ele faltar e nunca roda `sudo` sozinho.

No Linux, Fast Flags vão para o `config.json` do Sober e mods para o `asset_overlay` dele. A tela
Mods lista os arquivos do pacote do Roblox para você escolher o que trocar, e a tela Sober explica as
opções do próprio Sober. Detalhes em [docs/LINUX_PLAN.md](docs/LINUX_PLAN.md); roteiro de teste em
[docs/LINUX_TESTING.md](docs/LINUX_TESTING.md).

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

O AppImage do Linux é gerado no Linux (o GitHub Actions usa um contêiner Ubuntu 22.04):

```bash
bash packaging/linux/build-appimage.sh
```

Para mexer na versão Linux pelo Windows, rode o `BloxCode.Linux` com `BLOXCODE_LAYOUT=xdg` e um `HOME`
temporário (as pastas seguem o padrão do Linux dentro dele). `BloxCode --self-test` roda os testes
sem tela numa pasta temporária, em qualquer sistema.

Argumentos: `-player [uri]`, `-menu`/`-settings`, `-uninstall [-quiet]`, `-nolaunch`, `-force`, `-channel <nome>`, `-noupdate`.

## Atualização automática

Toda vez que a cópia instalada abre (menu ou link do Roblox), ela pergunta ao GitHub Releases se
há versão nova (no máximo 4 s). Se houver, baixa o `BloxCode.exe` novo para `%TEMP%\BloxCode\Updates`,
confere o SHA-256, reinicia com `-upgrade`, se copia por cima da instalação e continua o que você
tinha pedido (abrir o jogo, o menu…). Mesmo fluxo do Bloxstrap. Dá para desligar em Behaviour.

Para publicar uma atualização:

1. Uma vez: coloque seu repositório em `<UpdateRepository>dono/repo</UpdateRepository>` no
   [Directory.Build.props](Directory.Build.props) e reinstale esse build.
2. A cada versão: aumente `<Version>` no [Directory.Build.props](Directory.Build.props), escreva a
   seção no [CHANGELOG.md](CHANGELOG.md) e envie uma tag `vX.Y.Z`. O
   [GitHub Actions](.github/workflows/build.yml) compila o `.exe` e o `.AppImage`, testa o AppImage no
   Ubuntu, Debian e Arch e publica o release com os dois arquivos e as notas do CHANGELOG.

No Linux o fluxo é o mesmo: a cópia instalada em `~/.local/bin/bloxcode` baixa o AppImage novo para
`~/.cache/bloxcode/updates` e se substitui.

## Estrutura

```
BloxCode.Core/   compartilhado: Settings, Logger, Paths, LaunchSettings, Fast Flags, Updater
BloxCode/        Windows (WPF)
  Core/          Deployment, PackageManifest, Bootstrapper, Installer, ShellLink
  UI/            MainWindow (estilo Claude desktop), BootstrapperWindow, BitMascot, BlockSpinner
  UI/Pages/      Home, Fast Flags, Mods, Appearance, Behaviour, About
BloxCode.Linux/  Linux (Avalonia)
  Runtime/       Sober, SoberConfig, ModSync, ApkAssets, SoberLauncher
  Platform/      DesktopIntegration (.desktop, ícones, xdg-mime), Distro, Diagnostics, Proc
  UI/            MainWindow, LaunchWindow, BitMascot, páginas (incluindo Sober)
  SelfTest.cs    `--self-test`
packaging/linux/ AppRun, bloxcode.desktop, build-appimage.sh
tools/
  extract-bundle.js  extrai um .NET single-file bundle (usado no Bloxstrap.exe)
  make-icon.js       gera o ícone do Bit (.ico do Windows e PNGs do Linux)
```

Fora do escopo por enquanto: Roblox Studio e Discord Rich Presence no Windows.

Projeto de fã — sem afiliação com Roblox Corporation, Anthropic, a equipe do Bloxstrap ou a
VinegarHQ (Sober) (MIT).
