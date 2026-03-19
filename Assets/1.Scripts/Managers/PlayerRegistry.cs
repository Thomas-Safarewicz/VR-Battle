using System.Collections.Generic;
using System.Linq;
using _1.Scripts.Character;
using _1.Scripts.Tools;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace _1.Scripts.Managers
{
    /// <summary>
    /// Class that handles and references for all of the players who have joined and/or left the game.
    /// </summary>
    public class PlayerRegistry : NetworkBehaviour, INetworkRunnerCallbacks
    {
        private const byte CAPACITY = 2;

        private static PlayerRegistry Instance { get; set; }

        private static int Count => Instance.ObjectByRef.Count;

        [Networked, Capacity(CAPACITY)]
        public NetworkDictionary<PlayerRef, PlayerObject> ObjectByRef { get; }

        public override void Spawned()
        {
            base.Spawned();
            
            Instance = this;
            Runner.AddCallbacks(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            
            Instance = null;
            runner.RemoveCallbacks(this);
        }

        private bool GetAvailable(out byte index)
        {
            switch (ObjectByRef.Count)
            {
                case 0:
                    index = 0;
                    return true;
                case CAPACITY:
                    index = 0;
                    return false;
            }

            byte[] indices = ObjectByRef.OrderBy(kvp => kvp.Value.Index).Select(kvp => kvp.Value.Index).ToArray();

            for (int i = 0; i < indices.Length - 1; i++)
            {
                if (indices[i + 1] <= indices[i] + 1)
                {
                    continue;
                }

                index = (byte)(indices[i] + 1);
                return true;
            }

            index = (byte)(indices[^1] + 1);
            return true;
        }
        

        public static void Server_Add(NetworkRunner runner, PlayerRef pRef, PlayerObject pObj)
        {
            Debug.Assert(runner.IsServer);

            if (Instance.GetAvailable(out byte index))
            {
                Instance.ObjectByRef.Add(pRef, pObj);
                pObj.RPC_Init(pRef, index);
                Debug.Log($"ADDED to register player {pRef}", pObj);
            }
            else
            {
                Debug.LogWarning($"Unable to register player {pRef}", pObj);
            }
        }

        private  static void Server_Remove(NetworkRunner runner, PlayerRef pRef)
        {
            Debug.Assert(runner.IsServer);
            Debug.Assert(pRef.IsRealPlayer);

            if (Instance.ObjectByRef.Remove(pRef) == false)
            {
                Debug.LogWarning("Could not remove player from registry");
            }
        }

        private  static bool HasPlayer(PlayerRef pRef)
        {
            return Instance.ObjectByRef.ContainsKey(pRef);
        }

        public static PlayerObject GetPlayer(PlayerRef pRef)
        {
            return HasPlayer(pRef) ? Instance.ObjectByRef.Get(pRef) : null;
        }

        public static IEnumerable<PlayerObject> Where(System.Predicate<PlayerObject> match)
        {
            return Instance.ObjectByRef.Where(kvp => match.Invoke(kvp.Value)).Select(kvp => kvp.Value);
        }

        public static void ForEach(System.Action<PlayerObject> action)
        {
            foreach (var kvp in Instance.ObjectByRef)
            {
                action.Invoke(kvp.Value);
            }
        }

        public static void ForEachWhere(System.Predicate<PlayerObject> match, System.Action<PlayerObject> action)
        {
            foreach (KeyValuePair<PlayerRef, PlayerObject> kvp in Instance.ObjectByRef)
            {
                if (match.Invoke(kvp.Value))
                    action.Invoke(kvp.Value);
            }
        }

        public static int CountWhere(System.Predicate<PlayerObject> match)
        {
            int count = 0;
            foreach (KeyValuePair<PlayerRef, PlayerObject> kvp in Instance.ObjectByRef)
            {
                if (match.Invoke(kvp.Value))
                    count++;
            }

            return count;
        }

        public static bool Any(System.Predicate<PlayerObject> match)
        {
            foreach (KeyValuePair<PlayerRef, PlayerObject> kvp in Instance.ObjectByRef)
            {
                if (match.Invoke(kvp.Value)) return true;
            }

            return false;
        }

        public static PlayerObject GetRandom()
        {
            byte index = (byte)Random.Range(0, Count);
            byte i = 0;
            foreach (KeyValuePair<PlayerRef, PlayerObject> kvp in Instance.ObjectByRef)
            {
                PlayerObject pObj = kvp.Value;
                if (pObj != null)
                {
                    if (i == index) return pObj;
                    i++;
                }
            }

            throw new System.Exception("Something went inexplicably wrong");
        }

        public static PlayerObject[] GetRandom(int count)
        {
            List<PlayerObject> players = new();
            foreach (KeyValuePair<PlayerRef, PlayerObject> kvp in Instance.ObjectByRef)
            {
                players.Add(kvp.Value);
            }

            return players.Grab(count).ToArray();
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                Server_Remove(runner, player);
            }
        }

        #region INetworkRunnerCallbacks

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
           /* if (!runner.IsServer) return;

            NetworkObject playerObj = runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                PlayerObject pObj = playerObj.GetComponent<PlayerObject>();
                Server_Add(runner, player, pObj);
            }*/
        }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress,
            Fusion.Sockets.NetConnectFailedReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        #endregion
    }
}