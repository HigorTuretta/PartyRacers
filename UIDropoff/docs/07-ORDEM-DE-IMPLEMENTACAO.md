# 07 · Ordem de implementação

Não construa tudo de uma vez. Um componente compartilhado validado é **estável**: nenhuma tela posterior cria versão alternativa dele.

| # | Etapa | Pronto quando |
|---|---|---|
| 1 | **Fundação** — copiar `UI/` e `Art/`, gerar os 7 font assets, criar as 3 PanelSettings | `theme.uss` e `base.uss` compilam sem aviso, fontes resolvem |
| 2 | **Shell** — `Shell.uxml` num UIDocument, cena vazia | barra superior idêntica à referência: marca inclinada −2°, 4 abas, carteira, perfil |
| 3 | **Compartilhados** — abrir cada template no UI Builder | cada um renderiza sozinho: botão com sombra dura e pressionar, tracejado fechado nos 4 lados, chapinhas, avatar, card |
| 4 | **Lobby** | validação completa (4 estados) |
| 5 | **Busca de partida** | validação (5 etapas) |
| 6 | **Sala privada** | validação |
| 7 | **Garagem** | validação (7 categorias) |
| 8 | **HUD PC** | validação (5 estados) |
| 9 | **HUD celular** | validação |
| 10 | **Cena 3D** — karts, plataforma, poses, PreviewStudio | placas seguem os karts; garagem com kart e chão em 1341 |

A etapa 3 é a que economiza tempo depois. Um tracejado que fecha errado, descoberto na etapa 3, custa uma correção; descoberto na etapa 9, custa seis.

## Regra de corte

Se uma tela antiga em uGUI ainda existir no projeto, **ela morre no mesmo commit** em que a versão UI Toolkit entra. Não fica ao lado, não fica atrás de flag, não fica comentada.

Duas fontes de verdade para a mesma tela foi o que travou o porte anterior: cada rodada recomeçava e nada acumulava.

## Ao terminar

1. as capturas de todas as telas e estados, em resolução fixa
2. `UiPortGuardTest` verde
3. confirmação de que nenhuma tela de UI sobrou em uGUI
4. `docs/08` respondido — o que faltou, o que você decidiu, o que precisa de decisão do dono
