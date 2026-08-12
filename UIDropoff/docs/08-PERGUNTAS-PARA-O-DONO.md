# 08 · Perguntas para o dono

O HTML resolveu o design das telas. Ele **não** resolve tudo o que a Unity precisa. Abaixo, o que ficou em aberto.

**Não decida nenhuma destas sozinho.** Pergunte, anote a resposta aqui, e só então implemente.

## Já decidido — não reabra

| Questão | Decisão |
|---|---|
| blur dos painéis | **só alpha.** Sem backdrop-filter. Um Render Feature de blur é decisão de pipeline, fora deste porte |
| retrato do kart | **RenderTexture**, não sprite. A regra "nada de sprite por código" vale para moldura |
| sprites 9-slice antigos | **aposentados.** Painel, botão, card e chapinha são nativos no USS |
| SVG dos ícones | **usar o PNG** |
| paginação na garagem | **proibida.** A lista rola |
| limite de 40s da busca | **regra interna**, nunca aparece na tela |
| centro do palco na garagem | **1341**, com o chão junto |

## Em aberto

### 1. Conteúdo real
O protótipo usa nomes, níveis, pings e cosméticos inventados. De onde vem o dado real — ScriptableObject, backend, Photon? Enquanto não houver resposta, o controller preenche com os mesmos valores do HTML.

### 2. Quantidade de cosméticos por categoria
O HTML mostra 12 em MODELO. As outras 6 categorias têm quantas? A grade é de 4 colunas e rola, então qualquer número funciona — mas a captura de validação precisa bater.

### 3. Poses de câmera da garagem
São 7 categorias e o HTML nomeia as poses só por rótulo ("ORBITAL 3/4"). Os `Transform` de cada pose precisam ser posicionados na cena por alguém que veja o kart. Quem faz isso?

### 4. Sombra dos karts no lobby
`Shadow_Ellipse.png` existe como fallback de UI, mas o certo é blob shadow 3D no chão. Confirma que a sombra é 3D?

### 5. HUD celular — retrato
O HTML só tem paisagem (2340×1080). Existe modo retrato? Se sim, é outro UXML.

### 6. Estados sem captura
Escudo em recarga, escudo vazio, amigo offline, vaga expulsa, sala privada cheia. O HTML cobre todos — navegue nele. Se algum estado **não** existir no HTML, é aqui que ele entra.

### 7. Localização
Todo texto está em português, dentro dos UXML. Vai haver outro idioma? Se sim, o texto sai do UXML e vira chave — decisão que muda todos os arquivos, melhor saber antes.

### 8. Som
O protótipo não tem áudio. Clique, hover, confirmação, entrada de piloto na busca: existe biblioteca de som? Sem resposta, não invente.

---

## Como usar este arquivo

Ao terminar cada tela, acrescente aqui o que você **teve** que decidir sem resposta, marcando claramente:

```
### [tela] — decisão tomada sem confirmação
o que:      ...
por quê:    ...
como reverter: ...
```

Uma decisão registrada é reversível. Uma decisão silenciosa vira defeito seis semanas depois.
