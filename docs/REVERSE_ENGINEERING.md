# Bloxstrap → Blox Code: notas de engenharia reversa

Alvo analisado: `%LocalAppData%\Bloxstrap\Bloxstrap.exe` (12 589 224 bytes,
SHA-256 `9ad77580…46b2`). Ferramenta: REA MCP (`inspect_managed_artifact`,
`inspect_managed_members`, `inspect_analysis_view`). Nada do alvo foi executado.

## 1. Identificação do binário

| Observação | Evidência |
| --- | --- |
| O `.exe` é um apphost PE x64 **sem diretório CLI** – não é um assembly gerenciado direto. | `ev_8bb0dab3…` |
| É um *.NET single-file bundle* v6.0 com 49 arquivos (extraídos com `tools/extract-bundle.js`). O assembly principal é `Bloxstrap.dll`. | saída do extrator |
| `Bloxstrap.dll`: assembly **Bloxstrap 2.11.4.0**, `.NETCoreApp v6.0`, IL-only, 337 tipos, 2 626 métodos, MVID `5e97addc-2e46-44d2-8f93-cd1db1e367c4`. | `ev_4b0cca4c…` / `ev_316cc6cb…` |
| Dependências embutidas: Wpf.Ui, CommunityToolkit.Mvvm, DiscordRPC, Markdig, Newtonsoft.Json, SharpZipLib, AvalonEdit, XamlAnimatedGif, securifybv.ShellLink. | lista do bundle |

## 2. Fluxo de inicialização do Roblox (o "bootstrapper")

Tipos-chave inspecionados (`ev_e8a9f740…`): `Bootstrapper`, `RobloxInterfaces.Deployment`,
`LaunchHandler`, `LaunchSettings`, `Installer`, `Paths`, `FastFlagManager`,
`AppData.RobloxPlayerData`, `Models.Manifest.*`.

1. **Escolha de espelho (mirror)** – `Deployment.TestConnection` / `InitializeConnectivity`.
   Literais observados: `https://setup.rbxcdn.com`, `https://setup-aws.rbxcdn.com`,
   `https://setup-ak.rbxcdn.com`, `https://roblox-setup.cachefly.net`,
   `https://s3.amazonaws.com/setup.roblox.com`, `/versionStudio` e o valor esperado
   `version-012732894899482c` (mensagem: *"versionStudio response does not match"*).
2. **Versão mais recente** – `Deployment.GetInfo`: `https://clientsettingscdn.roblox.com`
   + `/v2/client-version/` + `WindowsPlayer` + `/channel/` + canal; em falha,
   *"Falling back to clientsettings..."* → `https://clientsettings.roblox.com`.
   Modelo `ClientVersion`: `Version`, `VersionGuid`, `BootstrapperVersion`, `Timestamp`.
   *Verificado em rede (2026-10-24):* para `production` o segmento `/channel/…` deve ser
   omitido — `/channel/production` responde 401 igual a um canal inexistente; `get_IsDefaultChannel`
   (compara com `"production"`) é o que decide isso no Bloxstrap.
3. **Canal** – regex `channel:([a-zA-Z0-9-_]+)` sobre a URI de launch, depois registro
   `SOFTWARE\ROBLOX Corporation\Environments\…\Channel`, padrão `production`.
4. **URL dos pacotes** – `Deployment.GetLocation` tem 6 instruções IL, sem desvio:
   `get_BaseUrl` + `ldstr "/channel/common"` + recurso. *Inferência:* todo download usa
   `{mirror}/channel/common/{versionGuid}-{arquivo}`.
5. **Manifesto** – `{version}-rbxPkgManifest.txt`; primeira linha precisa ser `v0`
   (*"Unexpected package manifest version: … (expected v0!)"*). Modelo `Package`:
   `Name`, `Signature` (MD5), `PackedSize`, `Size`, `DownloadPath`.
6. **Cache** – `Downloads\{md5}` (confirmado: o diretório Downloads do Bloxstrap contém
   arquivos nomeados pelo MD5 e `PlayerState.json` → `PackageHashes`). Verificação via
   `Utility.MD5Hash`.
7. **Mapa pacote → pasta** (literais de `RobloxPlayerData`/`CommonAppData`):
   `RobloxApp.zip`→raiz, `shaders.zip`→`shaders\`, `ssl.zip`→`ssl\`,
   `WebView2RuntimeInstaller.zip`→`WebView2RuntimeInstaller\`, `content-*.zip`→`content\…`,
   `content-textures3/terrain/platform-fonts/platform-dictionaries` → `PlatformContent\pc\…`,
   `extracontent-*.zip` → `ExtraContent\…`. Pacotes fora do mapa: *"was not found in the
   package map!"*.
8. **Pós-extração** – grava `AppSettings.xml` (`<ContentFolder>content</ContentFolder>`,
   `<BaseUrl>http://www.roblox.com</BaseUrl>`), instala WebView2 se necessário
   (`MicrosoftEdgeWebview2Setup.exe`), aplica `Modifications\` com `ModManifest`,
   escreve FastFlags em `ClientSettings\ClientAppSettings.json`, limpa `Versions\` antigos.
9. **Launch** – `Bootstrapper.StartRoblox` inicia `RobloxPlayerBeta.exe` com os
   argumentos da URI (*"Got Roblox launch arguments"*) e espera o log em `logs\*.log`.

## 3. Integração com o Windows

* Protocolos: `Software\Classes\roblox` e `roblox-player`, com `URL Protocol`,
  `DefaultIcon` e `shell\open\command` = `"exe" -player "%1"`.
* Desinstalação: `Software\Microsoft\Windows\CurrentVersion\Uninstall\Bloxstrap`
  (`DisplayName`, `DisplayIcon`, `DisplayVersion`, `InstallDate`, `UninstallString`
  `"exe" -uninstall`, `QuietUninstallString`, …); remoção da pasta via
  `cmd.exe /c timeout 5 && del … && rmdir …`.
* Argumentos: `-player`, `-studio`, `-settings`/`-menu`/`-preferences`, `-uninstall`,
  `-quiet`, `-nolaunch`, `-watcher`, `-backgroundupdater`, `-channel`, `-version`, `-force`.
* Mutex de instância: `Bloxstrap-Bootstrapper`; o Roblox usa `ROBLOX_singletonMutex`.
* Tema do sistema: `SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize`.

## 4. Presets de FastFlags observados

| Preset | Flag |
| --- | --- |
| `Rendering.ManualFullscreen` | `FFlagHandleAltEnterFullscreenManually` |
| `Rendering.DisableScaling` | `DFFlagDisableDPIScale` |
| `Rendering.MSAA` | `FIntDebugForceMSAASamples` |
| `Rendering.TextureQuality.*` | `DFFlagTextureQualityOverrideEnabled`, `DFIntTextureQualityOverride` |

## 5. O que o Blox Code reimplementa / não reimplementa

Reimplementado do zero (sem copiar código do Bloxstrap): itens 1–9 da seção 2, integração
com o Windows (seção 3, sem Studio), editor de FastFlags + presets, mods com backup/restauração.

Fora do escopo desta versão: Roblox Studio, Discord Rich Presence/activity watcher,
background updater, temas customizados XML, auto-update do próprio bootstrapper.

## Desconhecidos

* A lógica exata de `InitializeConnectivity` (ordem/paralelismo dos testes) está num state
  machine assíncrono; o Blox Code testa os espelhos em paralelo e usa o primeiro válido.
* Não foi observado em execução nenhum comportamento — tudo acima é evidência estática.
