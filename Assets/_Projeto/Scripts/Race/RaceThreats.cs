using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro das ameaças em voo na pista (foguete, disco voador, armadilha armada). Existe porque o
/// ÚNICO aviso de ataque do HUD é o arco vermelho na borda — sem texto, sem seta, sem ícone — e
/// alguém precisa saber que há algo vindo para acendê-lo.
///
/// Cada projétil se inscreve ao nascer e se apaga ao morrer. É mais barato e muito mais confiável
/// que a HUD varrer a cena atrás de projéteis por tipo, e não obriga o HUD a conhecer nenhum poder
/// em particular: ameaça nova entra no jogo mexendo só no próprio script dela.
/// </summary>
public static class RaceThreats
{
    private static readonly List<Transform> ativos = new List<Transform>();

    public static IReadOnlyList<Transform> Ativos => ativos;

    public static void Registrar(Transform ameaca)
    {
        if (ameaca != null && !ativos.Contains(ameaca))
            ativos.Add(ameaca);
    }

    public static void Remover(Transform ameaca)
    {
        if (ameaca != null)
            ativos.Remove(ameaca);
    }

    /// <summary>Descarta entradas destruídas. Chamado por quem varre a lista.</summary>
    public static void Limpar()
    {
        for (int i = ativos.Count - 1; i >= 0; i--)
        {
            if (ativos[i] == null)
                ativos.RemoveAt(i);
        }
    }
}
