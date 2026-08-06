using System.Collections;
using System.Collections.Generic;
using ithappy;
using UnityEngine;

namespace PartyRacers.UI.Garage
{
    /// <summary>
    /// Fotografa as variantes do carro EQUIPADO para os cards da garagem.
    ///
    /// Por que em runtime, e não como PNG gerado no Editor: a lista de peças depende do carro.
    /// `GetVariantCount` pergunta ao rig montado, então as rodas do kart 03 não são as do kart 01 —
    /// uma foto tirada uma vez, com o carro padrão, mostra a peça errada em 14 dos 15 modelos. Era
    /// exatamente o que estava acontecendo.
    ///
    /// O estúdio é um carro clone escondido numa layer própria, com câmera e luz que só enxergam
    /// essa layer. Nada disso aparece no palco: a câmera do estúdio renderiza para RenderTexture e
    /// fica desligada entre uma foto e outra.
    ///
    /// As fotos saem UMA POR FRAME. Montar 15 variantes no mesmo frame trava a tela por meio
    /// segundo bem no clique que deveria ser instantâneo; espalhadas, os cards vão aparecendo e a
    /// grade parece carregar, que é o comportamento honesto.
    /// </summary>
    [DisallowMultipleComponent]
    public class PreviewStudio : MonoBehaviour
    {
        [Tooltip("Prefabs de carro na mesma ordem do KartVisualCustomizer.")]
        [SerializeField] private KartVisualCustomizer referencia;

        [Tooltip("Layer exclusiva do estúdio. Precisa estar FORA do culling mask da câmera do jogo.")]
        [SerializeField] private int layerDoEstudio = 31;

        [SerializeField] private int lado = 256;
        [SerializeField] private Vector3 posicaoDoEstudio = new Vector3(0f, -500f, 0f);

        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private readonly Queue<Pedido> fila = new Queue<Pedido>();

        private KartVisualCustomizer clone;
        private Camera camera3D;
        private Light luz;
        private Coroutine trabalhando;
        private int carroDoClone = -1;

        private sealed class Pedido
        {
            public string Chave;
            public int Carro;
            public string Categoria;
            public CarElementName Elemento;
            public bool EhModelo;
            public int Indice;
            public System.Action<Texture2D> Entregar;
        }

        /// <summary>
        /// Pede a foto de uma variante. Entrega na hora se já estiver em cache, senão entra na fila.
        /// </summary>
        public void Pedir(int carro, string categoria, CarElementName elemento, bool ehModelo,
                          int indice, System.Action<Texture2D> aoFicarPronta)
        {
            if (referencia == null || aoFicarPronta == null)
                return;

            // O carro entra na chave porque a MESMA peça de índice 3 é outra coisa em outro modelo.
            // A COR entra na chave do modelo porque a foto sai na tinta equipada: sem isso, repintar
            // o kart deixava a miniatura da sala na cor antiga até fechar o jogo.
            int cor = referencia.ColorIndex;
            string chave = ehModelo
                ? $"{categoria}_{indice}_{cor}"
                : $"{carro}_{cor}_{categoria}_{indice}";

            if (cache.TryGetValue(chave, out Texture2D pronta) && pronta != null)
            {
                aoFicarPronta(pronta);
                return;
            }

            fila.Enqueue(new Pedido
            {
                Chave = chave,
                Carro = carro,
                Categoria = categoria,
                Elemento = elemento,
                EhModelo = ehModelo,
                Indice = indice,
                Entregar = aoFicarPronta,
            });

            trabalhando ??= StartCoroutine(Trabalhar());
        }

        /// <summary>
        /// Foto do carro do jogador como ele está agora — modelo, peças e tinta.
        ///
        /// Usada fora da garagem (a miniatura na faixa de nome da sala privada), então não depende
        /// de nenhuma categoria: é o kart inteiro, do mesmo ângulo do card de MODELO.
        /// </summary>
        public void PedirCarroDoJogador(System.Action<Texture2D> aoFicarPronta)
        {
            if (referencia == null)
                return;

            Pedir(referencia.CarIndex, "MINIATURA", CarElementName.None, true, referencia.CarIndex,
                  aoFicarPronta);
        }

        /// <summary>Descarta o que dependia do carro que saiu de cena.</summary>
        public void EsquecerPecas()
        {
            var manter = new Dictionary<string, Texture2D>();

            foreach (KeyValuePair<string, Texture2D> par in cache)
            {
                // Fotos do carro INTEIRO valem sempre; as de peça morrem com o modelo que as gerou.
                if (par.Key.StartsWith("MODELO_") || par.Key.StartsWith("MINIATURA_"))
                    manter[par.Key] = par.Value;
                else if (par.Value != null)
                    Destroy(par.Value);
            }

            cache.Clear();
            foreach (KeyValuePair<string, Texture2D> par in manter)
                cache[par.Key] = par.Value;

            // Onde cada peça mora é propriedade do MODELO: a caixa do escapamento do kart 03 não
            // serve para mirar o do kart 11.
            recorteDaPeca.Clear();
        }

        private IEnumerator Trabalhar()
        {
            while (fila.Count > 0)
            {
                Pedido p = fila.Dequeue();

                if (cache.TryGetValue(p.Chave, out Texture2D jaFeita) && jaFeita != null)
                {
                    p.Entregar?.Invoke(jaFeita);
                    continue;
                }

                Montar();
                if (clone == null)
                    break;

                // Modelo troca o carro inteiro; peça monta a variante SOBRE o carro equipado.
                if (p.EhModelo)
                {
                    if (carroDoClone != p.Indice)
                    {
                        clone.SetCar(p.Indice);
                        carroDoClone = p.Indice;
                    }
                }
                else
                {
                    if (carroDoClone != p.Carro)
                    {
                        clone.SetCar(p.Carro);
                        carroDoClone = p.Carro;
                    }

                    clone.SetElement(p.Elemento, p.Indice);
                }

                // A COR equipada tem que ir junto. Sem ela o clone sai na tinta de fábrica e o
                // card mostra um carro que o jogador não reconhece como sendo o dele — foi o que
                // fez os previews parecerem de outro veículo.
                if (referencia != null)
                    clone.SetColor(referencia.ColorIndex);

                // O rig é montado no mesmo frame: sem esperar, a caixa medida é a do carro anterior.
                yield return null;

                // Variante vazia sem referência de recorte: acha onde a peça mora antes de mirar.
                if (!p.EhModelo && !recorteDaPeca.ContainsKey(p.Elemento)
                    && !clone.TryGetElementBounds(p.Elemento, out _))
                    yield return Localizar(p);

                VestirLayer(clone.transform);

                Texture2D foto = Fotografar(p);
                if (foto != null)
                {
                    cache[p.Chave] = foto;
                    p.Entregar?.Invoke(foto);
                }

                yield return null;
            }

            trabalhando = null;
        }

        // ------------------------------------------------------------------ Estúdio

        private void Montar()
        {
            if (clone != null)
                return;

            if (referencia == null)
                return;

            var raiz = new GameObject("__PreviewStudio") { hideFlags = HideFlags.DontSave };
            raiz.transform.position = posicaoDoEstudio;

            // Uma cópia do customizador, não do carro montado: é ela que sabe trocar peça.
            GameObject copia = Instantiate(referencia.gameObject, raiz.transform);
            copia.name = "Carro";
            copia.transform.localPosition = Vector3.zero;
            copia.transform.localRotation = Quaternion.identity;

            // O palco some nas telas que não mostram carro, e a cópia de um objeto desligado nasce
            // desligada — renderer desligado não aparece na foto. A sala privada pede a miniatura
            // do kart justamente com o palco fora de cena.
            copia.SetActive(true);

            clone = copia.GetComponent<KartVisualCustomizer>();

            // O clone não pode escrever a seleção do jogador: ele passa por TODAS as variantes da
            // peça para fotografá-las, e cada troca gravava em PlayerPrefs. Ao abrir a aba RODAS, a
            // roda equipada virava a última fotografada — a moldura verde marcava um card que o
            // jogador nunca escolheu, e ao entrar na corrida o carro chegava com essa peça.
            // Desligar os outros MonoBehaviour não bastava: quem grava é o próprio customizador.
            clone.SetLoadSelectionOnStart(false);
            clone.SetPersistSelection(false);

            foreach (MonoBehaviour c in copia.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != clone && !(c is CarCustomizer))
                    c.enabled = false;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(raiz.transform, false);
            camera3D = camGo.AddComponent<Camera>();
            camera3D.clearFlags = CameraClearFlags.SolidColor;
            camera3D.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera3D.cullingMask = 1 << layerDoEstudio;
            camera3D.fieldOfView = 32f;
            camera3D.nearClipPlane = 0.05f;
            camera3D.farClipPlane = 200f;
            camera3D.enabled = false;

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(raiz.transform, false);
            luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.2f;
            luz.cullingMask = 1 << layerDoEstudio;
            luz.transform.rotation = Quaternion.Euler(38f, 140f, 0f);

            camGo.layer = layerDoEstudio;
            luzGo.layer = layerDoEstudio;
            carroDoClone = referencia.CarIndex;
            clone.SetCar(carroDoClone);
        }

        private void VestirLayer(Transform t)
        {
            t.gameObject.layer = layerDoEstudio;
            for (int i = 0; i < t.childCount; i++)
                VestirLayer(t.GetChild(i));
        }

        private Texture2D Fotografar(Pedido p)
        {
            if (clone == null || camera3D == null || !clone.TryGetCarBounds(out Bounds carro))
                return null;

            if (carro.extents.magnitude < 0.01f)
                return null;

            Bounds peca = default;
            bool temPeca = !p.EhModelo && MedirPeca(p, out peca);
            bool sozinha = temPeca && GarageFraming.Isolar(p.Categoria);

            Vector3 centro;
            float raio;

            if (!temPeca)
            {
                centro = carro.center;
                raio = carro.extents.magnitude;
            }
            else
            {
                // O recorte sai da PEÇA, não de uma tabela de coordenadas. Duas variantes de
                // para-choque enquadradas como o carro inteiro davam dois cards idênticos, e a
                // tabela que corrigia isso errava de carro para carro — os 15 modelos do pack têm
                // tamanhos e proporções diferentes.
                centro = peca.center;

                // Sem o carro em volta não há contexto a mostrar: o piso e o teto de
                // `RaioDaPeca` deixariam o retrato pequeno no meio de um quadro vazio.
                raio = sozinha ? peca.extents.magnitude : GarageFraming.RaioDaPeca(peca, carro);
            }

            // O card tem 150 px: a peça precisa ocupá-lo. O raio já é o da ESFERA que envolve a
            // peça, bem maior do que a silhueta dela vista de qualquer lado — com 1,55 sobrava
            // metade do card em fundo vazio.
            //
            // A MINIATURA da sala é ainda mais apertada: ela vive num selo de 42 px na faixa de
            // nome, e ali qualquer margem vira um carrinho ilegível.
            bool miniatura = p.Categoria == "MINIATURA";
            Vector3 direcao = GarageFraming.Direcao(p.Categoria, carro, centro);
            float distancia = raio / Mathf.Tan(camera3D.fieldOfView * 0.5f * Mathf.Deg2Rad)
                            * (miniatura ? 0.9f : p.EhModelo ? 1.2f : sozinha ? 1.15f : 1.3f);

            camera3D.transform.position = centro + direcao * distancia;
            camera3D.transform.LookAt(centro);

            if (sozinha)
                Esconder(p.Elemento);

            var rt = RenderTexture.GetTemporary(lado, lado, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            camera3D.targetTexture = rt;
            camera3D.Render();

            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            RenderTexture ativo = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
            tex.Apply();
            RenderTexture.active = ativo;

            camera3D.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            Revelar();
            return tex;
        }

        /// <summary>
        /// Apaga tudo do clone menos a peça, para o retrato dela.
        ///
        /// Mexe só nos renderers, e só entre <see cref="Camera.Render"/> e o
        /// <see cref="Revelar"/> logo abaixo: nada disso chega a aparecer num frame.
        /// </summary>
        private void Esconder(CarElementName elemento)
        {
            escondidos.Clear();

            var daPeca = new HashSet<Renderer>();
            IReadOnlyList<GameObject> spawned = clone.CurrentRig != null
                ? clone.CurrentRig.GetSpawnedElements(elemento)
                : null;

            if (spawned != null)
                foreach (GameObject go in spawned)
                    if (go != null)
                        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                            daPeca.Add(r);

            foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || daPeca.Contains(r))
                    continue;

                r.enabled = false;
                escondidos.Add(r);
            }
        }

        private void Revelar()
        {
            foreach (Renderer r in escondidos)
                if (r != null)
                    r.enabled = true;

            escondidos.Clear();
        }

        private readonly List<Renderer> escondidos = new List<Renderer>();

        /// <summary>
        /// Onde a peça equipada está, em espaço de mundo.
        ///
        /// Quatro elementos do pack (aerofólio, adesivo, motor e farol de milha) têm o
        /// <c>Empty.prefab</c> como segunda variante: não há renderer para medir. Aí o
        /// enquadramento vem da variante CHEIA do mesmo elemento — o card do "sem" precisa mostrar
        /// o mesmo recorte do card do "com", senão os dois não se comparam e a grade parece quebrada.
        /// </summary>
        private bool MedirPeca(Pedido p, out Bounds caixa)
        {
            if (clone.TryGetElementBounds(p.Elemento, out caixa))
            {
                recorteDaPeca[p.Elemento] = caixa;
                return true;
            }

            return recorteDaPeca.TryGetValue(p.Elemento, out caixa);
        }

        /// <summary>
        /// Descobre onde a peça mora quando a variante equipada é o <c>Empty</c>: monta as outras,
        /// mede, e devolve a equipada. Acontece no máximo uma vez por elemento.
        ///
        /// É coroutine porque cada troca de peça precisa de um frame: o <c>Destroy</c> da variante
        /// anterior só acontece no fim do frame, e fotografar antes disso pegaria a peça velha e a
        /// nova juntas na mesma imagem.
        /// </summary>
        private IEnumerator Localizar(Pedido p)
        {
            int total = clone.GetElementVariantCount(p.Elemento);

            for (int i = 0; i < total && !recorteDaPeca.ContainsKey(p.Elemento); i++)
            {
                if (i == p.Indice)
                    continue;

                clone.SetElement(p.Elemento, i);
                yield return null;

                if (clone.TryGetElementBounds(p.Elemento, out Bounds achada))
                    recorteDaPeca[p.Elemento] = achada;
            }

            clone.SetElement(p.Elemento, p.Indice);
            yield return null;
        }

        private readonly Dictionary<CarElementName, Bounds> recorteDaPeca =
            new Dictionary<CarElementName, Bounds>();

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, Texture2D> par in cache)
                if (par.Value != null)
                    Destroy(par.Value);

            cache.Clear();

            if (clone != null)
                Destroy(clone.transform.root.gameObject);
        }
    }
}
