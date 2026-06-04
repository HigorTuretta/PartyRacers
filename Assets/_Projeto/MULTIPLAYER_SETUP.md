# PartyRacers — Multiplayer Setup (16 jogadores)

Este projeto já foi **estruturado** para multiplayer online de até 16 jogadores, mantendo o jogo
100% jogável localmente. A conexão online real é **incremental** e ligada por você quando os Unity
Services estiverem configurados. Nada de rede roda por padrão — o gameplay local atual não muda.

## O que já está pronto no projeto

- **Netcode for GameObjects (NGO) 2.11.2** já instalado (Unity 6000.4.6f1), além do **Multiplayer Center**.
- Limite central de **16 jogadores**: `PartyRacers.Networking.RaceConstants.MaxPlayers`.
- **Registro de jogadores** (local / remoto / bot): `RacePlayerRegistry` + `RacePlayerInfo` (singleton,
  persistente entre cenas).
- **Garagem como lobby**: `GarageController` agora mostra painel de lobby — lista de jogadores, status
  de pronto, botões PRONTO e CONVIDAR, contador `x/16`, feedback de conexão e o botão CORRER/INICIAR.
- **Spawns para 16 karts**: `RaceSpawnManager` (usa pontos da cena ou gera uma grid procedural).
- **Largada multi-kart**: `RaceManager` agora trava/destrava TODOS os karts na contagem regressiva.
- **Separação de input**: `IKartInputSource` + `KartController.SetInputSource(...)` permitem alimentar
  o kart com input de rede/bot sem tocar na física (input nulo = controle local idêntico ao atual).
- **Marcador de papel** do kart: `KartNetworkIdentity` (MonoBehaviour puro, sem acoplar à rede ainda).
- **Ponte online** `NetworkBootstrap`: init dos Unity Services + auth, com Relay/Lobby como TODO guiado.
  Todo o código de rede fica atrás do define `PARTYRACERS_ONLINE` (desligado por padrão).

## Passo a passo para ligar o online

### 1. Instalar os Unity Services (Relay + Lobby + Authentication + Core)
Opção recomendada (Unity 6): **Window ▸ Multiplayer ▸ Multiplayer Center**, selecione o objetivo
"Hospedar via Relay + Lobby (Distributed Authority/Client-Server)" e deixe o assistente instalar os
pacotes. Alternativamente, via **Package Manager** adicione:
- `com.unity.services.multiplayer` (pacote unificado do Unity 6 — já traz Relay, Lobby, Auth e Core), ou
  individualmente `com.unity.services.core`, `com.unity.services.authentication`,
  `com.unity.services.relay`, `com.unity.services.lobby`.

### 2. Vincular o projeto aos Unity Services
- **Edit ▸ Project Settings ▸ Services**: faça login e vincule a um Project ID (crie um na
  Unity Cloud Dashboard se necessário).
- Em **Authentication**, deixe habilitado o login anônimo (o `NetworkBootstrap` usa
  `SignInAnonymouslyAsync`).
- Em **Relay** e **Lobby** (dashboard da Unity), confirme que os serviços estão ativos para o projeto.

### 3. Ativar a camada de rede no código
- **Edit ▸ Project Settings ▸ Player ▸ Scripting Define Symbols**: adicione `PARTYRACERS_ONLINE`.
  Isso compila a implementação online dentro de `NetworkBootstrap` (init + auth). Sem o define, tudo
  permanece local.

### 4. Objetos de cena necessários
- Na cena **Garage**: o `GarageController` cria automaticamente um objeto `NetworkSystems` com
  `RacePlayerRegistry` + `NetworkBootstrap` (persistente). Nada a fazer manualmente.
- Crie um GameObject **NetworkManager** (componente `NetworkManager` do NGO) com um
  **UnityTransport** anexado, presente na cena de corrida (ou persistente). Configure o
  `UnityTransport` para usar o Relay (o `NetworkBootstrap` preencherá os dados do Relay no host/cliente
  — ver TODOs no arquivo).
- Adicione um `RaceSpawnManager` na cena **DEMO** (posicione-o na largada; ele gera 16 vagas em grid
  ou use pontos filhos como spawns) e marque `placeOnSpawnPoints` no `RaceManager` se quiser que ele
  posicione os karts na grid.

### 5. Network Prefabs a registrar (na Default Network Prefabs List do NGO)
Quando for sincronizar de fato, adicione `NetworkObject` + um componente de sincronização e registre:
- `Prefabs/Cars/PlayerKart_Local.prefab` (o kart do jogador) — autoridade do dono controla input via
  `KartController.SetInputSource`; clientes remotos recebem transform/estado.
- `Prefabs/Powers/Rocket.prefab` (projétil do foguete) — spawnar via servidor.
- `Prefabs/Powers/Magic shield blue.prefab` (visual do escudo) — ou sincronize só o estado e instancie
  localmente (mais barato).
- VFX (`VFXExplosion`, `VFXBoing`, `VFXRocketTrail`) — preferir spawn local por evento (não precisam de
  NetworkObject).

### 6. Cenas no Build Settings
Já configurado: **Garage** (índice 0), **MainTrack** (1), **DEMO** (2). Mantenha a Garage como inicial.

### 7. Ordem recomendada de teste
1. **Local primeiro**: rode a Garage, confirme o lobby (1/16, PRONTO, CORRER carrega a DEMO) e a
   corrida local intacta.
2. Instale os Services (passo 1) e vincule o projeto (passo 2) — rode novamente; o status do lobby deve
   mostrar "Conectado aos Unity Services".
3. Adicione `PARTYRACERS_ONLINE` (passo 3) e implemente os TODOs de Relay/Lobby em `NetworkBootstrap`.
4. Teste com 2 instâncias (ParrelSync ou build + editor) antes de escalar para 16.

## Pendências técnicas (por design, incrementais)
- Conexão real de **Relay** (alocação + join code) e **Lobby** (criar/entrar, lista de jogadores em
  rede): TODOs marcados em `NetworkBootstrap.HostGameAsync`.
- Componente de **sincronização do kart** (NetworkTransform + estado de poderes/escudo/foguete) a ser
  criado e anexado ao prefab, lendo `KartNetworkIdentity` para autoridade.
- **Convite real** (compartilhar join code do Lobby) — o botão CONVIDAR já existe; falta plugar o código.
- Sincronização de **VFX/colisões de poderes** (hoje locais).
- **Bots** para completar a grid de 16: a base existe (`PlayerKind.Bot`, `SetInputSource`), falta uma IA
  de input.
