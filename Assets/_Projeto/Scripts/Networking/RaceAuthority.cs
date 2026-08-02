using Unity.Netcode;

namespace PartyRacers.Networking
{
    /// <summary>
    /// Responde "quem manda aqui?" sem obrigar cada sistema de gameplay a conhecer o Netcode.
    ///
    /// Offline a resposta é sempre "eu": o jogo roda exatamente como antes. Online, só o servidor
    /// decide o que é canônico (poder sorteado, caixa consumida, quem terminou a corrida) — os
    /// clientes apenas reproduzem. Sem isto cada máquina sorteava o próprio resultado e as
    /// partidas divergiam.
    ///
    /// Não depende do define PARTYRACERS_ONLINE: o pacote Netcode está sempre presente e, sem
    /// sessão ativa, <see cref="IsNetworked"/> é falso — o caminho local continua idêntico.
    /// </summary>
    public static class RaceAuthority
    {
        /// <summary>Existe uma sessão de rede ativa (host, servidor ou cliente).</summary>
        public static bool IsNetworked
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening;
            }
        }

        /// <summary>Esta máquina é o servidor da sessão.</summary>
        public static bool IsServer
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening && manager.IsServer;
            }
        }

        /// <summary>Esta máquina é um cliente puro (não hospeda a partida).</summary>
        public static bool IsPureClient
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening && !manager.IsServer;
            }
        }

        /// <summary>
        /// Esta máquina pode decidir o resultado de uma regra de corrida: offline sempre, online
        /// apenas no servidor.
        /// </summary>
        public static bool HasSimulationAuthority => !IsNetworked || IsServer;

        /// <summary>Resolve um objeto de rede pelo id, ou null se ele não existe aqui.</summary>
        public static NetworkObject FindSpawned(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.SpawnManager == null)
                return null;

            return manager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject found)
                ? found
                : null;
        }
    }
}
