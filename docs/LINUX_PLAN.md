# Blox Code para Linux — plano

Status: **implementado na v1.1.0** (outubro de 2026). O que já foi verificado: o autoteste
(`--self-test`) e todas as telas rodando com um Sober simulado; o CI testa o AppImage no Ubuntu
22.04/24.04, Debian 12 e Arch. **Ainda não verificado num PC real:** jogar pelo Sober, links do
navegador, mods e flags fazendo efeito no jogo, e o atualizador. O roteiro para isso está em
[LINUX_TESTING.md](LINUX_TESTING.md). As decisões abaixo continuam valendo; onde a implementação
mudou algo, a seção diz.

Mudanças em relação ao plano:

- **FUSE:** o AppImage usa o runtime estático novo, que precisa só do `fusermount` (pacotes `fuse3`
  ou `fuse2`), não da biblioteca `libfuse2`.
- **Abrir o jogo:** `flatpak run org.vinegarhq.Sober <link>`, com a saída do Sober em `sober.log`.
- **Etapa 0 (spike) e `linux-check.sh`:** substituídos pelo `--self-test` do próprio AppImage e pelo
  "Export diagnostics" (zip com distro, sessão, Flatpak, Sober, driver e extensão GL da NVIDIA).
- **Tipos das flags:** gravadas como JSON de verdade (`true`, números); flags `FString` e com filtro
  continuam texto.

## 1. Como o Roblox roda no Linux hoje

- **Não existe cliente oficial para Linux.** E o Roblox Player para Windows **não roda mais no
  Wine** desde março de 2024: o anti-cheat Hyperion bloqueia o Wine. O Vinegar (o "Bloxstrap do
  Linux") hoje só serve para o Roblox Studio.
- **O caminho atual é o [Sober](https://sober.vinegarhq.org/)** (`org.vinegarhq.Sober`, só no
  Flathub). Ele roda a versão Android do Roblox nativamente, sem Wine e sem emulador. O próprio
  Sober baixa e atualiza o Roblox.
- O Sober já expõe o que o Blox Code precisa ([documentação](https://vinegarhq.org/Sober/Configuration/index.html)):
  - configuração em `~/.var/app/org.vinegarhq.Sober/config/sober/config.json`, com um objeto
    `fflags` e chaves como `graphics_optimization_mode`, `use_opengl`, `discord_rpc_enabled`,
    `enable_hidpi`, `close_on_leave`, `server_location_indicator_enabled`;
  - mods em `~/.var/app/org.vinegarhq.Sober/data/sober/asset_overlay`, com a mesma estrutura de
    pastas dos assets do Roblox (por exemplo `content/textures/Cursors/KeyboardMouse/ArrowCursor.png`);
  - JSON inválido no `config.json` impede o Sober de abrir.
- Desde setembro de 2025 o Roblox só aceita Fast Flags da allowlist, no Sober também.
- Existe o **Cordial** (GPL-3.0), uma alternativa open source ao Sober lançada em agosto de 2026,
  mas ainda experimental.

**Consequência:** no Windows o Blox Code é um *bootstrapper* (baixa, extrai e abre o Roblox).
No Linux ele vira um **frontend do Sober**: o Sober cuida do Roblox; o Blox Code instala e
configura o Sober, aplica Fast Flags e mods, abre os links `roblox://` e mostra a tela de launch
com o Bit.

## 2. Decisões tomadas

| Tema | Decisão |
| --- | --- |
| Runtime | Sober primeiro, atrás de uma interface `IRobloxRuntime` para o Cordial poder entrar depois |
| Interface | **Avalonia só no Linux**; a versão WPF do Windows continua como está |
| Distros | **Baseadas no Arch** e **baseadas no Debian/Ubuntu** (seção 5) |
| Distribuição | **AppImage** no GitHub Releases, com o atualizador automático que já existe |
| Testes | O PC Linux de um amigo, testado à distância (seção 6) |

Custo aceito da escolha de interface: são **duas interfaces para manter**. Toda tela nova ou
mudança visual precisa ser feita em WPF e em Avalonia. A lógica fica compartilhada (seção 3),
então a duplicação fica só no XAML e no code-behind.

## 3. Arquitetura

```
BloxCode.sln
├─ shared/ (BloxCode.Core) net10.0, sem APIs do Windows (compartilhado)
│   FastFlagValidator, FastFlagPresets, Settings/Store, Logger, Updater, LaunchSettings,
│   IPlatform (pastas, instalação, abrir pasta), IRobloxRuntime
├─ windows/ (BloxCode)     net10.0-windows, WPF (o app atual)
│   Deployment, PackageManifest, Bootstrapper → WindowsPlayerRuntime
│   Installer (registro, atalhos .lnk), ShellLink, ThemeManager (DWM)
└─ linux/ (BloxCode.Linux) net10.0, Avalonia, linux-x64
    SoberRuntime, LinuxPlatform (.desktop, xdg-mime, pastas XDG), UI em Avalonia
```

Pontos do código atual que precisam ser desacoplados antes:

- `Paths` usa `%LocalAppData%`: vira `IPlatform.DataDir`. No Linux: `~/.config/bloxcode`
  (configurações), `~/.local/share/bloxcode` (mods e versões) e `~/.local/state/bloxcode/logs`.
- `Updater.ApplyUpgrade` chama `Installer.RefreshAfterUpgrade` (registro): vira um método da plataforma.
- `Updater.CheckAsync` escolhe o asset `.exe`: passa a escolher pelo sistema (`BloxCode.exe` no
  Windows, `BloxCode-x86_64.AppImage` no Linux).
- `Store` usa `Deployment.DefaultChannel`: a constante vai para o Core.

### `IRobloxRuntime`

```csharp
interface IRobloxRuntime
{
    string Name { get; }                        // "Roblox (Windows)", "Sober"
    Task<RuntimeStatus> DetectAsync();          // instalado? versão?
    Task InstallAsync(IProgress<...>, ct);      // Windows: baixa o Roblox; Sober: flatpak install
    Task ApplyAsync(Settings settings, ct);     // Fast Flags + mods
    Task LaunchAsync(string robloxArgs, ct);    // abre o jogo
}
```

O `Bootstrapper` atual vira `WindowsPlayerRuntime`, sem mudança de comportamento.

## 4. O que muda de recurso para recurso

| Recurso | Windows (hoje) | Linux (plano) |
| --- | --- | --- |
| Baixar/atualizar o Roblox | Blox Code (CDN + manifesto) | O Sober faz sozinho |
| Fast Flags | `ClientSettings\ClientAppSettings.json` (valores texto) | Objeto `fflags` do `config.json` do Sober, preservando as outras chaves |
| Presets | Fullscreen, DPI, MSAA, texturas | Presets do Sober: modo gráfico, OpenGL/Vulkan, HiDPI, fechar ao sair, indicador de servidor, Discord |
| Mods | Copia `Modifications\` para a pasta da versão | Sincroniza `Modifications/` para `asset_overlay` (mesma estrutura `content/...`) |
| Links `roblox://` | Registro do Windows | Arquivo `.desktop` + `xdg-mime default` para `x-scheme-handler/roblox` e `roblox-player` |
| Instalação | Copia o exe + registro + atalhos | Copia o AppImage para `~/.local/bin` + `.desktop` + ícone em `hicolor` |
| Tema escuro | Registro do Windows | Portal `org.freedesktop.appearance` `color-scheme` (fallback: `gsettings`) |
| Atualização | GitHub Releases, `-upgrade` | Igual, substituindo o arquivo AppImage (mais simples que no Windows: dá para trocar um executável em uso) |
| Uma janela de menu por vez | Mutex nomeado | Mutex nomeado (funciona no .NET para Linux) |
| Roblox Studio | Fora do escopo | Fora do escopo |

### O que o Blox Code resolve no Linux

Mexer no Sober direto é ruim: Fast Flags exigem editar JSON na mão (e um erro impede o Sober de
abrir), mods exigem abrir o APK num gerenciador de arquivos e recriar a estrutura de pastas, e o
menu do Sober cobre só o básico. O Blox Code entra exatamente aí:

- **Editor de Fast Flags** igual ao do Windows (busca, validação, chave para booleanas, importar
  JSON), gravando o `config.json` com segurança.
- **Gerenciador de mods com lista de assets:** lê o APK que o próprio Sober baixa
  (`~/.var/app/org.vinegarhq.Sober/data/sober/packages/com.roblox.client/base.apk`, um zip) e lista
  os arquivos de `assets/` (cursores, sons, texturas, fontes) numa árvore com busca. Você escolhe um
  asset, arrasta o arquivo novo e o Blox Code grava no caminho certo de `asset_overlay`. Desfazer um
  mod ou todos de uma vez. Só lemos os nomes e arquivos de mídia do APK; nada de código do Roblox.
- **Opções do Sober em telas explicadas:** qualidade gráfica, OpenGL/Vulkan, HiDPI, Discord, fechar
  ao sair, indicador de servidor.
- **Restaurar o Sober:** volta o `config.json` do backup que o Blox Code fez antes da primeira
  alteração, ou apaga o arquivo para o Sober recriar o padrão; opção de limpar o `asset_overlay`.

### Detalhes da integração com o Sober

- **Detectar:** `flatpak info org.vinegarhq.Sober`. Se não estiver instalado, oferecer instalar
  com `flatpak install --user flathub org.vinegarhq.Sober`, sempre com confirmação. Se o próprio
  Flatpak faltar, mostrar o comando da distro em vez de tentar instalar.
- **Configurar:** ler o `config.json`, alterar só `fflags` e as chaves dos presets, gravar de forma
  atômica e validar o JSON antes de trocar o arquivo. Fazer backup do original na primeira vez.
- **Tipos das flags:** no Windows o Blox Code grava tudo como texto. O Sober parece usar tipos JSON
  (`true`/`false`, números), então converter pelo prefixo da flag (`FFlag` → bool, `FInt` → número).
- **Abrir o jogo:** `flatpak run org.vinegarhq.Sober <uri>` (a confirmar no spike).
- **Links:** o próprio Sober registra o esquema `roblox`. Se o Blox Code assumir o esquema, ele
  precisa repassar o link ao Sober depois de aplicar as configurações.

## 5. Distros suportadas e distribuição

| Família | Exemplos | Versões mínimas |
| --- | --- | --- |
| Arch | Arch, CachyOS, EndeavourOS, Manjaro | rolling (sempre a atual) |
| Debian/Ubuntu | Ubuntu, Linux Mint, Pop!_OS, Zorin, Debian | Ubuntu 22.04, Mint 21, Debian 12 |

O **mesmo AppImage** serve para as duas famílias; o que muda é o que cada uma precisa ter instalado.
O Blox Code lê o `/etc/os-release` (`ID` e `ID_LIKE`) para saber a família e mostrar os comandos
certos. Ele **nunca roda `sudo` sozinho**: mostra o comando com um botão de copiar. A instalação do
Sober em si (`flatpak install --user`) não precisa de `sudo`.

| Pré-requisito | Arch | Debian/Ubuntu |
| --- | --- | --- |
| FUSE 2 (para o AppImage abrir) | `sudo pacman -S --needed fuse2` | Ubuntu 24.04+ e Debian 13+: `sudo apt install libfuse2t64`; Ubuntu 22.04 e Debian 12: `sudo apt install libfuse2` |
| Flatpak | `sudo pacman -S flatpak` | `sudo apt install flatpak` (o Ubuntu não traz; o Mint já traz) |
| Flathub | `flatpak remote-add --if-not-exists --user flathub https://dl.flathub.org/repo/flathub.flatpakrepo` | igual |
| Driver NVIDIA (Pascal, ex. GTX 1050) | série legada 580 (`580xx`; no CachyOS, perfil legado do `chwd`) | `ubuntu-drivers` / Gerenciador de Drivers (Mint) com a série 580; no Debian, pacote `nvidia-driver` do repositório `non-free` |

Diferenças que afetam o app:

- **Compatibilidade do binário:** o AppImage é gerado no runner `ubuntu-22.04` do GitHub Actions,
  a base mais antiga suportada. Assim ele roda nas distros mais novas das duas famílias (a regra do
  Linux é: binário feito em sistema antigo roda em sistema novo, não o contrário).
- **Ambiente gráfico:** Ubuntu usa GNOME, Mint usa Cinnamon (X11), CachyOS usa KDE (Wayland). O
  Blox Code desenha a própria barra de título, como no Windows, então fica igual em todos. Tema
  escuro via portal `org.freedesktop.appearance`, que GNOME, KDE e Cinnamon implementam.
- **Links `roblox://`:** `.desktop` + `xdg-mime` funciona igual nas duas famílias.

Formatos extras para depois da v1.1.0, se fizer falta: pacote **AUR** `bloxcode-bin` (o jeito
natural de instalar no Arch) e um **.deb** para Debian/Ubuntu. Os dois só embrulham o mesmo binário.

### Build e releases

- `dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true`, empacotado com o
  `appimagetool` em `BloxCode-x86_64.AppImage`.
- Cada release passa a ter os dois arquivos: `BloxCode.exe` e `BloxCode-x86_64.AppImage`.
- Um workflow do GitHub Actions gera os dois quando uma tag `v*` é enviada: `windows-latest` para
  o WPF, `ubuntu-22.04` para o AppImage. Isso também tira a publicação manual do meu PC.

## 6. Testes

| Família | Onde | O que cobre |
| --- | --- | --- |
| Arch | PC do amigo (CachyOS, abaixo) | Tudo, inclusive jogar de verdade com o Sober |
| Debian/Ubuntu | Máquina virtual com Ubuntu 24.04 e Linux Mint 22 no seu PC Windows | Instalar, links `roblox://`, interface, Fast Flags e mods gravados nos arquivos certos, atualizador. **Não cobre jogar:** VM tem 3D fraco e o Sober provavelmente não roda bem nela |
| Debian/Ubuntu | GitHub Actions (`ubuntu-22.04` e `ubuntu-24.04`, contêiner `debian:12`) | A cada commit: o AppImage abre e passa num autoteste sem tela (`--self-test`), nas versões mais antigas e mais novas |
| Debian/Ubuntu | Alguém com Ubuntu/Mint num PC real (a encontrar) | Jogar de verdade nessa família |

Até aparecer um PC real com Ubuntu/Mint, "jogar no Debian/Ubuntu" fica **não verificado**. O risco
é baixo, porque o Sober é o mesmo Flatpak nas duas famílias, mas o release deve dizer isso.

O `linux-check.sh` detecta a família pelo `/etc/os-release` e checa os pré-requisitos certos
(`pacman` ou `apt`, `fuse2` ou `libfuse2`/`libfuse2t64`).

### PC do amigo

O PC de testes é de um amigo que já usa Linux, então os testes são **à distância**: eu não tenho
acesso à máquina, e cada rodada depende dele.

**Hardware:** GTX 1050 2 GB, kit Xeon, 16 GB de RAM. **Sistema:** CachyOS.

**CachyOS** é baseado no Arch, com kernel e pacotes otimizados, e usa KDE Plasma no Wayland por
padrão. O que isso muda:

- **AppImage precisa do FUSE 2** (pacote `fuse2`), que distros baseadas no Arch nem sempre trazem.
  Sem ele o AppImage não abre. O roteiro de teste começa com `sudo pacman -S --needed fuse2`, e o
  `linux-check.sh` verifica.
- **Flatpak:** conferir se está instalado e com o Flathub ativo (`flatpak remotes`); se faltar,
  `sudo pacman -S flatpak`. O Sober só existe no Flathub, mesmo no Arch.
- **Driver NVIDIA:** no Arch e no CachyOS os pacotes `nvidia` atuais deixaram de suportar Pascal;
  a GTX 1050 precisa da série legada 580 (no CachyOS, pelo perfil de driver legado do `chwd` ou
  pelo pacote `580xx`). Confirmar no `linux-check.sh` qual está instalado.
- **Rolling release:** o sistema atualiza tudo o tempo todo. Bom para pegar problemas cedo, mas
  uma atualização do kernel ou do driver pode quebrar o Sober entre uma rodada de teste e outra;
  o diagnóstico sempre registra as versões.
- **Wayland por padrão:** os testes cobrem o Wayland primeiro; o X11 fica como teste secundário,
  se a sessão estiver disponível.

- **NVIDIA GTX 1050 (Pascal):** precisa do driver proprietário da NVIDIA. O Nouveau/NVK roda muito
  devagar nessa geração. Segundo o anúncio da NVIDIA, a série 580 é a última que suporta
  Maxwell/Pascal/Volta, então a máquina tem que ficar no driver 580 (legado) — confirmar a versão instalada.
- **Flatpak + NVIDIA:** o Sober usa a extensão `org.freedesktop.Platform.GL.nvidia-<versão>` do
  Flatpak, que precisa ter a **mesma versão** do driver instalado. Versões diferentes fazem o Sober
  rodar sem aceleração ou não abrir; é o problema mais comum com NVIDIA. O Blox Code deve detectar
  isso e mostrar o comando para corrigir (`flatpak update`).
- **2 GB de VRAM:** pouco para a qualidade máxima. Bom teste para o preset de qualidade gráfica do
  Sober (`balanced`/`performance`).
- **Xeon + 16 GB:** sem preocupação; kits Xeon desse tipo costumam ter AVX2.

**Como testar à distância:**

- O GitHub Actions gera o AppImage; o amigo baixa do release (ou de um artefato de build) e roda.
- Um script `tools/linux-check.sh` que ele roda uma vez e me manda a saída: distro, sessão
  (Wayland/X11), versão do driver NVIDIA, extensão GL do Flatpak, Sober instalado, conteúdo do
  `config.json` e o teste de abrir um jogo por link.
- Um botão **"Exportar diagnóstico"** no Blox Code que junta logs, configuração e versões num `.zip`
  para ele me mandar quando algo der errado.
- Um roteiro curto de teste por etapa (o que clicar, o que deve acontecer), para ele não precisar
  entender o código.

## 7. Etapas

| # | Etapa | Pronto quando |
| --- | --- | --- |
| 0 | **Spike no PC do amigo** (1–2 dias, via `linux-check.sh`) | Confirmados: abrir um jogo com `flatpak run org.vinegarhq.Sober <uri>`; o Sober relê o `config.json` a cada abertura; tipos aceitos em `fflags`; `asset_overlay` funciona; o APK em `packages/` é legível; driver e extensão GL da NVIDIA batem; um "olá mundo" em Avalonia abre na sessão dele |
| 1 | **Extrair o BloxCode.Core** | O Windows funciona igual, só reorganizado; sai como v1.0.2 para provar que nada quebrou |
| 2 | **Esqueleto Linux em Avalonia** | Janela principal com o tema Claude, o Bit e a tela inicial |
| 3 | **SoberRuntime + Fast Flags + mods** | Abre um jogo pelo Blox Code com flags e mods aplicados; gerenciador de mods lista os assets do APK; "Restaurar o Sober" funciona |
| 4 | **Integração com o desktop + AppImage + atualizador** | Instala, links do navegador abrem o Blox Code, atualização automática funciona |
| 5 | **Teste completo e release v1.1.0** | Roteiro inteiro passa no CachyOS do amigo e nas VMs Ubuntu/Mint; autoteste passa no CI (22.04, 24.04, Debian 12); release com `.exe` e `.AppImage` |

## 8. Riscos

- **O Sober é de terceiros** e pode mudar o formato do `config.json` ou o comportamento a qualquer
  momento. Mitigação: alterar só as chaves conhecidas, nunca apagar as desconhecidas, e mostrar um
  erro claro se o arquivo não tiver o formato esperado.
- **Disputa pelos links `roblox://`** entre o Blox Code e o próprio Sober.
- **Allowlist de Fast Flags:** a maioria das flags personalizadas não tem efeito, igual no Windows.
- **Diferenças do Avalonia em relação ao WPF:** estilos, templates e triggers são escritos de outro
  jeito, então a interface é reescrita, não copiada. O `BitMascot` é simples de portar (desenho próprio, só retângulos).
- **Duas interfaces para manter** (decisão consciente, seção 2).
- **Testes dependem do amigo:** cada rodada de teste tem o ritmo dele. O script de checagem e o
  "Exportar diagnóstico" existem para cada rodada render o máximo.
- **Driver legado da NVIDIA:** a GTX 1050 fica presa à série 580; atualizações da distro ou do
  Flatpak que mudem o driver podem quebrar o Sober independentemente do Blox Code.

## 9. Em aberto

- Qual versão do driver NVIDIA está instalada no CachyOS do amigo (580 legado ou outra)?
- O `fuse2` e o Flatpak/Flathub já estão instalados?
- Alguém com Ubuntu/Mint num PC real para testar o jogo nessa família?
- Qual programa de VM usar no seu PC (VirtualBox ou Hyper-V)? Com 8 GB de RAM, uma VM por vez,
  com 3–4 GB.
- Os itens do spike (etapa 0).

## Fontes

- [Sober — site oficial](https://sober.vinegarhq.org/)
- [Sober — configuração](https://vinegarhq.org/Sober/Configuration/index.html)
- [Sober — Tips and Tricks (asset_overlay, allowlist)](https://vinegarhq.org/Sober/Configuration/TipsAndTricks.html)
- [Sober no Flathub](https://flathub.org/apps/org.vinegarhq.Sober)
- [ArchWiki — Sober](https://wiki.archlinux.org/title/Sober)
- [Vinegar (bloqueio do Wine em março de 2024)](https://github.com/vinegarhq/vinegar)
- [OMG! Ubuntu — Roblox no Linux com Sober (out/2026)](https://www.omgubuntu.co.uk/2026/10/play-roblox-linux-sober)
- [DevForum — Roblox on Linux, resumo](https://devforum.roblox.com/t/roblox-on-linux-everything-summarized-in-1-post-updated/3236696)
- [Cordial no AlternativeTo](https://alternativeto.net/software/cordial/about/)
