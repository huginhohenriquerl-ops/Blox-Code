# Log de atualizações do Blox Code

Todas as versões do Blox Code, da mais nova para a mais antiga. Quem tem o Blox Code instalado
recebe cada versão automaticamente na próxima vez que abrir.

## 1.1.1 — Versão certa em Aplicativos

- **Windows:** depois de se atualizar sozinho para a 1.1.0, o Blox Code continuava aparecendo como
  1.0.3 em Configurações → Aplicativos (o programa em si já era o novo). Agora a versão mostrada
  acompanha cada atualização. Nada muda no Linux.

## 1.1.0 — Blox Code no Linux

O Blox Code agora roda no Linux, nas distros baseadas no **Arch** (Arch, CachyOS, EndeavourOS,
Manjaro) e no **Debian/Ubuntu** (Ubuntu, Linux Mint, Pop!_OS, Zorin, Debian). Vem num único arquivo,
o `BloxCode-x86_64.AppImage`, com o mesmo tema do Claude desktop e o Bit.

> **Primeira versão para Linux.** O AppImage passa nos testes automáticos no Ubuntu, no Debian e no
> Arch, mas jogar de verdade pelo Sober ainda está sendo testado num PC real. Se algo não funcionar,
> use **Behaviour → Export diagnostics** e abra uma issue com o zip.

### Como funciona no Linux

- **Via Sober.** O Roblox para Windows não roda mais no Wine (o anti-cheat bloqueia), então o
  caminho no Linux é o [Sober](https://sober.vinegarhq.org/), que roda a versão Android do Roblox
  nativamente. O Sober baixa e atualiza o Roblox sozinho; o Blox Code cuida do resto.
- **Instala o Sober para você.** Se ele faltar, a tela inicial mostra **Install Sober**: o Blox Code
  adiciona o Flathub e instala o Sober só para o seu usuário, sem pedir senha, com o Bit andando na
  barra de progresso. Se faltar o próprio Flatpak, aparece o comando certo para a sua distro
  (`pacman` ou `apt`) com um botão de copiar. O Blox Code nunca roda `sudo` sozinho.
- **Links do navegador.** Instalado, o Blox Code entra no menu de aplicativos e passa a abrir os links
  `roblox://`: aplica suas Fast Flags e mods e entrega o link ao Sober.

### O que dá para fazer

- **Fast Flags:** o mesmo editor do Windows (busca, validação, chave para booleanas, Import JSON,
  Export JSON). As flags vão direto para o `config.json` do Sober assim que você muda, como valores
  JSON de verdade (`true`, `144`), então valem até se você abrir o Sober direto. As flags que você já
  tinha colocado no Sober são importadas na primeira vez, não apagadas. Presets de anti-aliasing e
  qualidade de textura.
- **Mods com a lista dos arquivos do Roblox:** a tela Mods lê o pacote do Roblox que o Sober baixou e
  lista cursores, sons, fontes e texturas, com busca e filtros. Escolha um arquivo, clique em
  **Replace with file…** e ele vai para o lugar certo do `asset_overlay` do Sober na hora.
  **Restore original** desfaz. Se você já tinha colocado um arquivo no overlay na mão, ele é guardado
  e volta quando o mod sai.
- **Configurações do Sober explicadas:** qualidade gráfica (Quality, Balanced, Performance), OpenGL
  ou Vulkan, HiDPI, GameMode, fechar ao sair do jogo, localização do servidor, Discord e controles,
  cada uma com uma frase dizendo para que serve.
- **Restaurar o Sober:** volta o `config.json` do backup que o Blox Code faz antes da primeira mudança,
  reseta o Sober para o padrão ou tira todos os mods do Blox Code de dentro dele.
- **Aviso de NVIDIA:** se o driver da NVIDIA não bate com a extensão GL do Flatpak (o motivo mais
  comum do Sober rodar sem a placa de vídeo), a tela inicial avisa e mostra `flatpak update`.
- **Exportar diagnóstico:** em Behaviour, um `.zip` com versões (distro, Flatpak, Sober, driver),
  configurações e logs para mandar a quem estiver ajudando. Sem senhas nem dados da conta do Roblox.

### Robustez

- O `config.json` do Sober é gravado num arquivo temporário, validado e trocado de uma vez: um JSON
  quebrado impediria o Sober de abrir. As chaves que o Blox Code não conhece ficam intactas. Se o
  arquivo já estiver quebrado, o Blox Code não mexe nele e explica o que fazer.
- A saída do Sober vai para `sober.log`, e não para o Blox Code, então fechar o Blox Code nunca
  derruba o jogo. Se o Sober fechar logo ao abrir, a tela de launch mostra o erro.
- A atualização automática troca o AppImage sem estragar a cópia que está rodando, e baixa para
  `~/.cache` (o `/tmp` pode não deixar executar programas em algumas distros).
- Uma janela de menu e um launch por vez, como no Windows.
- O AppImage é testado automaticamente a cada versão no Ubuntu 22.04 e 24.04, no Debian 12 e no Arch,
  com um autoteste (`BloxCode-x86_64.AppImage --self-test`) que roda numa pasta temporária, sem mexer
  nas suas configurações.

### Windows

- Nada muda no uso. Por dentro, a parte comum (configurações, Fast Flags, atualizador) virou uma
  biblioteca compartilhada com a versão Linux.
- As versões agora são compiladas e publicadas pelo GitHub Actions, com o `.exe` e o `.AppImage` no
  mesmo release.

## 1.0.3 — Conheça o Bit

O Blox Code ganhou um mascote próprio. O **Bit** substitui o Clawd, que era o mascote do Claude Code
(da Anthropic). Agora a identidade visual do Blox Code é nossa, e a versão para Linux já nasce com ela.

- **Quem é o Bit:** um bloco de montar, como o "Blox" do nome, com um terminal no lugar do rosto,
  como o "Code". Na cabeça, uma antena com uma faísca que pisca como sinal. É pixel art, igual ao
  estilo do resto do app.
- **Animações:**
  - **Parado:** respira, pisca, e a boca é um cursor de terminal piscando.
  - **Baixando o Roblox:** anda em cima da barra de progresso.
  - **Extraindo:** digita com os bracinhos enquanto a tela rola linhas de código.
  - **Terminou:** dá um pulinho com os olhos em ^ ^ e a antena acesa.
  - **Deu erro:** antena apagada, olhar baixo e boca triste.
  - Clicar nele na tela About também faz ele pular.
- **Ícone novo:** o Bit no lugar do Clawd, na barra de tarefas, no menu Iniciar e nos atalhos. Os
  atalhos que já existem passam a mostrar o ícone novo depois da atualização (o Windows pode demorar
  um pouco para atualizar o cache de ícones).
- **Indicador de carregamento novo:** o asterisco que piscava na tela de launch (que também vinha do
  Claude Code) virou um bloco girando em volta de um quadrado (▖ ▌ ▘ ▀ ▝ ▐ ▗ ▄). O ✻ da barra de título
  e da lista de mods também saiu.
- O tema continua o mesmo: as cores e a tipografia no estilo do Claude desktop não mudam.

## 1.0.2 — Robustez antes do port para Linux

Última varredura no código antes de começar a versão para Linux. Nada muda na aparência; o foco é
o Blox Code não travar nem fechar sozinho em situações comuns.

### Erros que podiam fechar o app

- **Área de transferência ocupada.** Se outro programa estivesse usando a área de transferência
  (gerenciadores de clipboard, o próprio navegador), o **Export JSON** do Fast Flag Editor dava erro.
  Agora ele tenta de novo por um instante e, se não der, avisa "The clipboard is busy — try again".
- **Abrir pastas e links.** "Mods folder", "Open logs", "Open install folder" e os links do app
  davam erro se a pasta não existisse ou não houvesse navegador padrão. Agora a pasta é criada se
  faltar, e qualquer outro problema vira uma mensagem.
- **Erro logo na abertura.** Se a pasta de dados (`%LocalAppData%\BloxCode`) não pudesse ser criada,
  o app fechava antes de conseguir mostrar ou registrar o erro. Agora o tratamento de erros entra
  primeiro, e você vê a mensagem.
- **Erros fora da janela principal.** Falhas em tarefas de fundo não eram registradas em lugar
  nenhum. Agora vão para o log, o que facilita achar a causa de qualquer problema.
- **Mensagem de erro que falha.** Se a própria janela de erro falhasse ao abrir, o app podia entrar
  num ciclo de erros. Agora isso é registrado e o ciclo é interrompido.

### Abrir o Roblox

- **Links com espaços ou aspas.** O link do jogo agora é repassado ao Roblox como um único argumento
  entre aspas quando precisa, então não pode ser quebrado em pedaços.
- **Canal inválido.** Um canal digitado com espaço, barra ou outro caractere estranho em
  Behaviour virava um endereço errado. Agora é ignorado (fica registrado no log) e o Blox Code usa
  o `production`.
- **Instalador do WebView2 travado.** Se o instalador do WebView2 (que o Roblox usa) travasse, o
  launch ficava esperando para sempre. Agora ele espera no máximo 3 minutos e segue; o Roblox abre
  mesmo assim.
- **Conexão que não responde.** Um servidor que não aceita a conexão agora falha em 10 segundos, em
  vez dos ~21 segundos do Windows, e o Blox Code passa para o próximo.

### Instalação e Fast Flags

- **Atalhos.** Se um atalho do menu Iniciar ou da área de trabalho não pudesse ser criado, a
  instalação parava no meio. Agora o atalho com problema é pulado (fica no log) e a instalação termina.
- **Importar arquivo JSON.** Abrir um arquivo que não pode ser lido, ou um arquivo enorme por engano,
  dava erro ou travava o editor. Agora aparece uma mensagem; arquivos acima de 2 MB são recusados.

### Novidade pequena

- O aviso **"Updated to vX"** na tela inicial agora tem o botão **What's new**, que abre as notas
  daquela versão no GitHub.

## 1.0.1 — Estabilidade

### Rede e downloads

- **Travamentos de até 10 minutos.** A escolha do servidor de download, a consulta da versão do
  Roblox e o manifesto de pacotes agora têm tempo-limite curto (8, 15 e 30 segundos).
- **Download parado.** Um download que parasse de receber dados ficava travado para sempre. Agora,
  sem dados por 30 segundos, ele é tentado de novo, até 3 vezes.
- **Sem internet.** Aparecia como "Channel 'production' is not available". Agora aparece como erro
  de rede.

### Abertura e atualização

- **Processo invisível.** Um erro antes da primeira janela deixava o Blox Code rodando sem janela.
  Agora ele mostra o erro e fecha.
- **Atualização que falha.** Se o Blox Code não conseguisse se substituir, o que você pediu (entrar
  no jogo) se perdia. Agora acontece mesmo assim, na versão atual, e a atualização tenta de novo depois.
- **Uma janela de menu por vez.** Abrir o Blox Code de novo traz o menu já aberto para a frente,
  como no Bloxstrap, em vez de abrir um segundo que sobrescreveria as configurações do primeiro.
- **Launch na fila.** Um segundo launch ao mesmo tempo mostra "Waiting for another Blox Code launch
  to finish".
- **Tema do sistema.** No tema "System", o Blox Code troca de claro para escuro junto com o Windows.

### Arquivos e configurações

- **Configurações sem corromper.** Settings e estado são gravados num arquivo temporário e trocados
  de uma vez, então desligar o PC no meio não estraga nada. Um arquivo já estragado é guardado como
  `.corrupt` em vez de ser substituído pelas configurações padrão.
- **Desinstalação.** O comando de desinstalação não apagava a pasta de instalação (o `timeout` do
  Windows falha sem console). Agora apaga.
- **Roblox aberto.** Reinstalar com o Roblox aberto quebrava a instalação pela metade. Agora o Blox
  Code avisa "Close Roblox and try again" antes de apagar qualquer coisa.
- **Mods em uso.** Um mod que o Roblox estivesse usando impedia o jogo de abrir. Agora é pulado e
  aplicado no próximo launch.

### Fast Flag Editor

- **Flags sumindo.** Com duas linhas de mesmo nome, as duas saíam das configurações salvas,
  inclusive a original. Agora só a cópia repetida fica marcada.
- **Mais leve.** Marcar caixas não salva mais a lista inteira a cada clique.

## 1.0.0 — Primeira versão

- Bootstrapper do Roblox com a cara do Claude desktop, estrelando o Clawd.
- Fluxo de atualização e launch obtido por engenharia reversa do Bloxstrap 2.11.4 com o REA e
  reescrito do zero em C#/WPF.
- Fast Flag Editor no estilo do Bloxstrap (Add single / Import JSON, validação, presets).
- Mods com backup e restauração dos arquivos originais.
- Instalação por usuário, sem precisar de administrador.
- Atualização automática pelo GitHub Releases.
