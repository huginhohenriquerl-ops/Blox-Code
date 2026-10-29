# Testar o Blox Code no Linux

Roteiro para quem testa num PC com Linux (pensado para o CachyOS com GTX 1050, mas serve para
qualquer distro baseada no Arch ou no Debian/Ubuntu). Cada etapa diz o que fazer e o que deve
acontecer. Se algo sair diferente, pare ali e mande o diagnóstico (etapa 7).

## 0. Antes de começar

No terminal:

```bash
flatpak --version
```

- Se disser que o comando não existe: `sudo pacman -S flatpak` (Arch/CachyOS) ou
  `sudo apt install flatpak` (Ubuntu/Mint/Debian). Depois, saia e entre de novo na sessão.

NVIDIA (GTX 1050 é Pascal, precisa do driver legado da série 580):

```bash
cat /proc/driver/nvidia/version
flatpak list --runtime | grep -i nvidia
```

A versão do driver (ex.: `580.95.05`) deve aparecer também na extensão do Flatpak como
`nvidia-580-95-05`. Se não aparecer, rode `flatpak update`.

## 1. O AppImage abre

Baixe `BloxCode-x86_64.AppImage` do release (ou do artefato `linux` do GitHub Actions) e rode:

```bash
chmod +x BloxCode-x86_64.AppImage
./BloxCode-x86_64.AppImage --self-test
```

**Esperado:** uma lista de `ok` terminando em `all checks passed`. O teste roda numa pasta
temporária e não mexe em nada seu.

Depois abra normalmente:

```bash
./BloxCode-x86_64.AppImage
```

**Esperado:** a janela do Blox Code com o Bit e "Good morning/afternoon/evening".

- Se aparecer erro de FUSE: `sudo pacman -S --needed fuse3` ou `sudo apt install fuse3`. Para seguir
  sem isso: `./BloxCode-x86_64.AppImage --appimage-extract-and-run`.

## 2. Instalar

Na tela inicial, clique em **Install** no aviso "Blox Code isn't installed yet".

**Esperado:** a mensagem "Installed"; "Blox Code" aparece no menu de aplicativos com o ícone do Bit.
Pode apagar o AppImage baixado; a cópia instalada fica em `~/.local/bin/bloxcode`.

Feche e abra pelo menu de aplicativos para conferir.

## 3. Sober

- Sem o Sober: a tela inicial mostra **Install Sober**. Clique, confirme e espere a barra (o
  primeiro download é grande). **Esperado:** "Sober is installed" e o aviso some.
- Com o Sober: a linha de baixo da tela inicial mostra `sober <versão> · N fast flags · N mods`.
- NVIDIA: se aparecer o aviso "Sober may run without your NVIDIA GPU", rode o `flatpak update` que
  ele mostra e clique em Home de novo.

## 4. Jogar

1. Clique em **Play Roblox** (barra lateral). **Esperado:** a tela de launch com o Bit, "Roblox is
   running", e o Sober abre. Na primeira vez o Sober pede login no Roblox.
2. Feche o Roblox. No site do Roblox, num navegador, clique em **Jogar** em algum jogo. **Esperado:**
   o navegador pergunta se pode abrir o link; o Blox Code mostra a tela de launch e o jogo abre.
3. Cole o link de um jogo (`https://www.roblox.com/games/...`) na caixa da tela inicial e aperte
   Enter. **Esperado:** entra direto nesse jogo.

## 5. Fast Flags e opções do Sober

1. Em **Fast Flags**, escolha Texture quality → Lowest. Abra o Roblox. **Esperado:** texturas
   visivelmente piores. Volte para Auto.
2. Em **Fast Flags → Fast Flag Editor → Add new**, adicione `DFIntTaskSchedulerTargetFps` = `30`.
   Abra um jogo. **Esperado:** limite de 30 FPS (se a flag estiver na allowlist do Roblox). Apague a
   flag depois.
3. Em **Sober**, troque Graphics para Performance e ligue "Use OpenGL instead of Vulkan". Abra o
   Roblox. **Esperado:** abre normalmente. Depois volte para Vulkan.
4. Confira o arquivo: `cat ~/.var/app/org.vinegarhq.Sober/config/sober/config.json`. **Esperado:**
   as flags dentro de `"fflags"` e as opções mudadas.

## 6. Mods

1. Em **Mods**, filtre por **Cursors**, escolha `ArrowCursor.png` e clique em **Replace with file…**.
   Escolha qualquer PNG pequeno. **Esperado:** aparece "modded" e "Applied to Sober".
2. Abra o Roblox. **Esperado:** o cursor trocado.
3. Clique em **Restore original** e abra o Roblox de novo. **Esperado:** o cursor normal.

## 7. Se algo der errado: diagnóstico

Em **Behaviour → Export diagnostics**. Ele cria `BloxCode-diagnostics-<data>.zip` na sua pasta
pessoal (com versões, configurações e logs, sem senhas nem dados da conta) e abre a pasta. Mande o
zip junto com o que você fez e o que aconteceu.

## 8. Atualização (quando sair uma versão nova)

Abra o Blox Code pelo menu. **Esperado:** a tela "Updating Blox Code to vX", depois o menu com o
aviso "Updated to vX".

## 9. Desinstalar

**Behaviour → Uninstall Blox Code.** **Esperado:** some do menu de aplicativos; os links `roblox://`
voltam a abrir o Sober direto. O Sober e as configurações dele continuam.
