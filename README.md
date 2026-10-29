<p align="center"><img src="docs/icon.png" width="96" alt="Bit, o mascote do Blox Code"></p>

# Blox Code

Um launcher de Roblox com a cara do **Claude desktop** e estrelando o **Bit**, nosso mascote: um
bloco de montar com um terminal no lugar do rosto. Fast Flags com editor, mods e links `roblox://`,
no **Windows** e no **Linux**.

## Baixar

| Sistema | Arquivo | Como o Roblox roda |
| --- | --- | --- |
| **Windows** 10/11 | [**BloxCode.exe**](https://github.com/huginhohenriquerl-ops/Blox-Code/releases/latest/download/BloxCode.exe) | O Blox Code baixa e abre o Roblox para Windows |
| **Linux** — Arch, CachyOS, Manjaro, Ubuntu, Mint, Debian… | [**BloxCode-x86_64.AppImage**](https://github.com/huginhohenriquerl-ops/Blox-Code/releases/latest/download/BloxCode-x86_64.AppImage) | Pelo [Sober](https://sober.vinegarhq.org/), que o Blox Code instala para você |

Os dois arquivos saem juntos em cada [release](https://github.com/huginhohenriquerl-ops/Blox-Code/releases/latest)
e têm o mesmo número de versão. Cada um se atualiza sozinho, sempre com o arquivo do próprio sistema.
O que mudou em cada versão está no [log de atualizações](CHANGELOG.md).

## Nos dois sistemas

- Paleta do Claude (papel marfim / carvão, acento argila `#D97757`), títulos serifados, sidebar e
  "composer" iguais aos do app: cole um link de jogo ou um place ID e aperte Enter.
- Tela de launch com status ao vivo (`▖ … (12s · esc to cancel)`) e o Bit andando em cima da barra
  de progresso, digitando código e pulando quando o Roblox abre.
- **Fast Flag Editor** no estilo do Bloxstrap: Add new (nome único ou Import JSON colado/de arquivo),
  Delete selected, Export JSON, busca, validação de prefixo/valor e chave liga/desliga para booleanas.
- **Mods** que voltam ao original quando você apaga o arquivo.
- Abre os links `roblox://` do navegador, instala sem pedir senha/admin e se atualiza sozinho pelo
  GitHub Releases.
- Tema Sistema / Claro / Escuro.

## Windows

Baixe o `BloxCode.exe`, abra e clique em **Install**. Ele vai para `%LocalAppData%\BloxCode`, ganha
atalhos e uma entrada em Aplicativos, e passa a abrir os links do Roblox.

- Escolhe o espelho de CDN mais rápido, consulta a versão do canal e baixa os pacotes do
  `rbxPkgManifest`, com cache por MD5 (reaproveita o cache do Bloxstrap quando o hash bate).
- Extrai no mesmo layout do Bloxstrap em `%LocalAppData%\BloxCode\Versions\<versão>`, instala o
  WebView2 se faltar e limpa versões antigas.
- Presets de Fast Flags: fullscreen exclusivo, DPI, MSAA, texturas. As flags vão para o
  `ClientAppSettings.json` do Roblox.
- Mods: tudo em `Modifications\` é copiado por cima do Roblox; os originais ficam guardados.

O fluxo de atualização e launch foi obtido por engenharia reversa do Bloxstrap 2.11.4 com o REA MCP e
reescrito do zero em C#/WPF — veja [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md).

## Linux

Distros baseadas no **Arch** (Arch, CachyOS, EndeavourOS, Manjaro) e no **Debian/Ubuntu** (Ubuntu
22.04+, Linux Mint 21+, Pop!_OS, Zorin, Debian 12+). O Roblox para Windows não roda mais no Wine,
então o Blox Code usa o [Sober](https://sober.vinegarhq.org/), que roda a versão Android do Roblox
nativamente e se atualiza sozinho.

1. Baixe o `BloxCode-x86_64.AppImage`, deixe executável e abra:

   ```bash
   chmod +x BloxCode-x86_64.AppImage && ./BloxCode-x86_64.AppImage
   ```

2. Na tela inicial, clique em **Install** (vai para o menu de aplicativos, em `~/.local/bin/bloxcode`,
   e passa a abrir os links `roblox://`) e em **Install Sober** se ele ainda não estiver instalado.

O que muda em relação ao Windows:

- Fast Flags vão para o `config.json` do Sober, como valores JSON de verdade, assim que você muda.
- A tela **Mods** lista os cursores, sons, fontes e texturas do pacote do Roblox para você escolher o
  que trocar; os arquivos vão para o `asset_overlay` do Sober.
- A tela **Sober** explica as opções do próprio Sober (gráficos, OpenGL/Vulkan, HiDPI, Discord…) e
  tem botões para restaurá-lo.
- Avisa quando o driver da NVIDIA não bate com a extensão GL do Flatpak, e exporta um zip de
  diagnóstico em Behaviour.

Se o AppImage não abrir, falta o FUSE: `sudo pacman -S --needed fuse3` (Arch) ou
`sudo apt install fuse3` (Debian/Ubuntu); ou rode com `--appimage-extract-and-run`. O Sober precisa
do Flatpak (`sudo pacman -S flatpak` ou `sudo apt install flatpak`); o Blox Code mostra o comando
certo se ele faltar e nunca roda `sudo` sozinho. Detalhes em [docs/LINUX_PLAN.md](docs/LINUX_PLAN.md);
roteiro de teste em [docs/LINUX_TESTING.md](docs/LINUX_TESTING.md).

## Atualização automática

Toda vez que a cópia instalada abre (menu ou link do Roblox), ela pergunta ao GitHub Releases se há
versão nova (no máximo 4 s). Se houver, baixa o arquivo do seu sistema, confere o SHA-256, reinicia
com `-upgrade`, se substitui e continua o que você tinha pedido (abrir o jogo, o menu…). Mesmo fluxo
do Bloxstrap. Dá para desligar em Behaviour.

| | Windows | Linux |
| --- | --- | --- |
| Baixa | `BloxCode.exe` para `%TEMP%\BloxCode\Updates` | `BloxCode-x86_64.AppImage` para `~/.cache/bloxcode/updates` |
| Substitui | `%LocalAppData%\BloxCode\BloxCode.exe` | `~/.local/bin/bloxcode` |

## Desenvolvimento

Requer o .NET SDK 10.

```bash
dotnet run --project windows
```

```bash
dotnet publish windows/BloxCode.csproj -c Release -o dist
```

O publish gera um único `dist/BloxCode.exe` self-contained (não precisa de .NET instalado). O AppImage
do Linux é gerado no Linux (o GitHub Actions usa um contêiner Ubuntu 22.04):

```bash
bash linux/packaging/build-appimage.sh
```

Para mexer na versão Linux pelo Windows, rode o `BloxCode.Linux` com `BLOXCODE_LAYOUT=xdg` e um `HOME`
temporário (as pastas seguem o padrão do Linux dentro dele). `BloxCode --self-test` roda os testes
sem tela numa pasta temporária, em qualquer sistema.

Argumentos: `-player [uri]`, `-menu`/`-settings`, `-uninstall [-quiet]`, `-nolaunch`, `-force`,
`-channel <nome>` (só Windows), `-noupdate`.

Para publicar uma versão: aumente `<Version>` no [Directory.Build.props](Directory.Build.props)
(é o mesmo número para os dois sistemas), escreva a seção no [CHANGELOG.md](CHANGELOG.md) e envie uma
tag `vX.Y.Z`. O [GitHub Actions](.github/workflows/build.yml) compila o `.exe` e o `.AppImage`, testa
o AppImage no Ubuntu, Debian e Arch e publica o release com os dois arquivos e as notas do CHANGELOG.
O repositório do feed de atualização também fica no `Directory.Build.props` (`<UpdateRepository>`).

```
windows/         Blox Code para Windows (WPF, BloxCode.csproj)
  Core/          Deployment, PackageManifest, Bootstrapper, Installer, ShellLink
  UI/            MainWindow (estilo Claude desktop), BootstrapperWindow, BitMascot, BlockSpinner
  UI/Pages/      Home, Fast Flags, Mods, Appearance, Behaviour, About
linux/           Blox Code para Linux (Avalonia, BloxCode.Linux.csproj)
  Runtime/       Sober, SoberConfig, ModSync, ApkAssets, SoberLauncher
  Platform/      DesktopIntegration (.desktop, ícones, xdg-mime), Distro, Diagnostics, Proc
  UI/            MainWindow, LaunchWindow, BitMascot, páginas (incluindo Sober)
  SelfTest.cs    `--self-test`
  packaging/     AppRun, bloxcode.desktop, build-appimage.sh (gera o AppImage)
shared/          usado pelos dois (BloxCode.Core.csproj): Settings, Logger, Paths, Fast Flags, Updater
docs/            engenharia reversa do Bloxstrap, plano e roteiro de teste do Linux
tools/
  extract-bundle.js  extrai um .NET single-file bundle (usado no Bloxstrap.exe)
  make-icon.js       gera o ícone do Bit (.ico do Windows e PNGs do Linux)
```

Fora do escopo por enquanto: Roblox Studio, e Discord Rich Presence no Windows.

Projeto de fã — sem afiliação com Roblox Corporation, Anthropic, a equipe do Bloxstrap ou a
VinegarHQ (Sober) (MIT).
