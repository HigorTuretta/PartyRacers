using ithappy;
using UnityEngine;

namespace PartyRacers.UI.Garage
{
    /// <summary>
    /// Como cada categoria da garagem é olhada — a mesma resposta para o card e para o carro grande.
    ///
    /// Se a foto do card mostra o escapamento por trás e a câmera do palco vai para a frente do
    /// carro, clicar num card desorienta: o jogador vê a peça na miniatura e não a encontra no
    /// modelo. Uma fonte só evita a divergência.
    ///
    /// O ÂNGULO é autoral — é ele que faz a peça "ler". O RECORTE é medido na peça montada
    /// (<see cref="KartVisualCustomizer.TryGetElementBounds"/>), porque os 15 carros do pack têm
    /// proporções diferentes e nenhuma tabela de coordenadas serve para todos.
    /// </summary>
    public static class GarageFraming
    {
        /// <summary>Elemento do carro que a categoria edita. <c>None</c> = carro inteiro.</summary>
        public static CarElementName Elemento(string categoria) => categoria?.ToUpperInvariant() switch
        {
            "RODAS" => CarElementName.Wheel,
            "PILOTO" => CarElementName.Racer,
            "FRENTE" => CarElementName.FrontBumper,
            "TRASEIRA" => CarElementName.RearBumper,
            "TETO" => CarElementName.Spoiler,
            "ADESIVOS" => CarElementName.Decals,
            "FARÓIS" => CarElementName.Headlight,
            "NEBLINA" => CarElementName.FogLight,
            "ESCAPE" => CarElementName.Pipe,
            "MOTOR" => CarElementName.Engine,
            _ => CarElementName.None,
        };

        /// <summary>
        /// Peça que só se vê com o resto do carro fora do caminho.
        ///
        /// O piloto do pack é um modelo completo — jaqueta, cabeça, capacete —, mas mora dentro de
        /// uma cabine FECHADA de vidro opaco. De qualquer ângulo, o card sai igual para as seis
        /// variantes. Fotografado sozinho ele vira um retrato, que é como uma seleção de
        /// personagem deve mesmo se parecer.
        ///
        /// Vale só para o CARD. No carro grande a peça continua no lugar dela: mentir sobre onde a
        /// peça fica seria pior que não mostrá-la.
        /// </summary>
        public static bool Isolar(string categoria)
            => string.Equals(categoria, "PILOTO", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// De que lado olhar. Categoria sem ângulo próprio deduz um pelo lado do carro em que a
        /// peça está — assim uma categoria nova nunca nasce apontando para o lugar errado.
        /// </summary>
        public static Vector3 Direcao(string categoria, Bounds carro, Vector3 peca)
        {
            switch (categoria?.ToUpperInvariant())
            {
                case "MODELO":
                case "COR": return new Vector3(1f, 0.45f, 1f).normalized;
                // Uma roda só, de frente e um pouco acima: é o desenho do aro que muda entre as
                // quinze do catálogo, e ele só se lê de lado.
                case "RODAS": return new Vector3(1f, 0.22f, 0.3f).normalized;
                case "FRENTE": return new Vector3(0.55f, 0.32f, 1f).normalized;
                case "FARÓIS": return new Vector3(0.45f, 0.34f, 1f).normalized;
                case "NEBLINA": return new Vector3(0.45f, 0.2f, 1f).normalized;
                case "TRASEIRA": return new Vector3(0.55f, 0.34f, -1f).normalized;
                case "ESCAPE": return new Vector3(0.75f, 0.3f, -1f).normalized;
                case "TETO": return new Vector3(0.65f, 0.62f, -0.8f).normalized;
                // O motor sai pelo capô (medido em +0,65 da meia-altura): vista de cima e da frente.
                case "MOTOR": return new Vector3(0.5f, 0.8f, 0.4f).normalized;
                // O adesivo mora na lateral: qualquer vista 3/4 o achata até sumir.
                case "ADESIVOS": return new Vector3(1f, 0.16f, 0.12f).normalized;
                // O piloto fica dentro de uma cabine FECHADA e do lado ESQUERDO (medido em -0,28 da
                // meia-largura). De cima aparece o teto; do lado direito, a travessia da cabine
                // inteira. Três-quartos dianteiro pelo lado dele pega para-brisa e janela ao mesmo
                // tempo, que é onde o capacete e o tronco aparecem.
                case "PILOTO": return new Vector3(-0.8f, 0.45f, 0.75f).normalized;
            }

            Vector3 e = carro.extents;
            Vector3 o = new Vector3(e.x > 0.001f ? (peca.x - carro.center.x) / e.x : 0f,
                                    e.y > 0.001f ? (peca.y - carro.center.y) / e.y : 0f,
                                    e.z > 0.001f ? (peca.z - carro.center.z) / e.z : 0f);

            float z = Mathf.Abs(o.z) < 0.3f ? 0.9f : Mathf.Sign(o.z);
            return new Vector3(0.85f, 0.3f + Mathf.Clamp01(o.y) * 0.5f, z).normalized;
        }

        /// <summary>
        /// Raio a enquadrar para uma peça — a peça com folga, presa entre um piso e um teto.
        ///
        /// Sem o PISO, um farol de poucos centímetros põe a câmera dentro da lataria. Sem o TETO,
        /// uma peça grande (o piloto, as quatro rodas juntas) devolve o carro inteiro e a categoria
        /// deixa de mostrar o que está sendo editado. A folga de 1,7 existe porque peça encostada
        /// na borda do quadro não se lê: é preciso ver onde ela se encaixa.
        /// </summary>
        public static float RaioDaPeca(Bounds peca, Bounds carro)
        {
            float inteiro = carro.extents.magnitude;
            return Mathf.Clamp(peca.extents.magnitude * 1.7f, inteiro * 0.3f, inteiro * 0.8f);
        }
    }
}
