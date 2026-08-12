using UnityEngine;

namespace PartyRacers.UI
{
    /// <summary>
    /// Retrato 3D do kart e das pecas para a UI.
    ///
    /// Isto NAO viola "nada de sprite gerado por codigo": essa regra vale
    /// para MOLDURA (quadro, botao, chip, tracejado, fundo). O retrato e o
    /// modelo que o jogador montou agora — nenhum sprite pode existir para ele.
    ///
    /// Componente na UI: RawImage / background-image alimentado por
    /// RenderTexture. Nunca Sprite.Create.
    ///
    /// O RIG E UM PREFAB (camera + 3 luzes + turntable + layer dedicada),
    /// montado a mao e commitado. Este script so instancia o prefab,
    /// posiciona a camera e chama Render().
    ///
    /// Luzes (especificacao, nao inferencia):
    ///   Key   Directional  1.15  #FFF4E2  rot ( 35, -40, 0)
    ///   Fill  Directional  0.42  #9BB4FF  rot ( 12, 145, 0)
    ///   Rim   Directional  0.78  #35A7FF  rot ( -8, 195, 0)
    ///   Ambient  cor plana #1A1E44  intensidade 0.35
    /// Camera: Perspective, FOV 28, Clear Flags Solid Color com ALPHA 0,
    ///         Culling Mask apenas na layer KartPreview.
    /// Turntable parado por padrao; gira 12 graus/s so no card em foco.
    /// RT: 256x256, RGB32, depth 16, AA 2, pool de 16, 1 render por frame,
    ///     liberada ao sair do viewport.
    /// </summary>
    public sealed class PreviewStudio : MonoBehaviour
    {
        [SerializeField] GameObject rigPrefab;
        [SerializeField] int poolSize = 16;
        [SerializeField] Vector2Int rtSize = new Vector2Int(256, 256);

        public RenderTexture Acquire(string itemId) => null;
        public void Release(RenderTexture rt) { }
    }
}
