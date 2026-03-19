using System;
using System.Collections.Generic;
using System.Linq;
using _1.Scripts.Audio;
using _1.Scripts.Character;
using Fusion;
using Fusion.Menu;
using Fusion.Sockets;
using FusionDemo;
using UnityEngine;

namespace _1.Scripts.Managers
{
    public enum MatchState
    {
        WaitingForPlayers,
        PreMatch,
        MidMatch,
        EndMatch,
        GameOver
    }

    public class GameManager : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public static GameManager Instance { get; private set; }

        public FusionBootstrap starter;
        private FusionMenuConnectionBehaviour connection;

        [Header("Base avatar used if the selected skin avatar from the menu was not found.")]
        [SerializeField] private NetworkPrefabRef _baseAvatar;

        [SerializeField] private Transform[] player1SpawnPoints;
        [SerializeField] private Transform[] player2SpawnPoints;

        [SerializeField] private GameObject[] gameCanvasList;

        private const int WINS_TO_END_GAME = 5;
        private const float MATCH_START_DELAY = 0f;

        // MATCH STATE
        [Networked]
        private MatchState CurrentState { get; set; }

        [Networked]
        private TickTimer LobbyStartTimer { get; set; }

        [Networked]
        private TickTimer MatchTimer { get; set; }

        [Networked]
        public PlayerRef RoundWinner { get; set; }

        // PLAYER DATA
        [Networked, Capacity(8)]
        private NetworkDictionary<PlayerRef, int> PlayerWins => default;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public override void Spawned()
        {
            Runner.AddCallbacks(this);
            starter = FindObjectOfType<FusionBootstrap>();

            if (Object.HasStateAuthority)
            {
                CurrentState = MatchState.WaitingForPlayers;
            }

            IntroSampleCharacterSelectionUI playerSettingsView = FindFirstObjectByType<IntroSampleCharacterSelectionUI>(FindObjectsInactive.Include);
            if (playerSettingsView)
            {
                SpawnPlayerPrefab(playerSettingsView.GetSelectedSkin(Runner.Topology), playerSettingsView.ConnectionArgs.Username);
            }
            else
            {
                SpawnPlayerPrefab(default, Runner.LocalPlayer.ToString());
            }
        }

        private void SpawnPlayerPrefab(NetworkPrefabId avatarSkin, string username)
        {
            NetworkObject avatar;

            if (avatarSkin != default)
            {
                avatar = Runner.Spawn(avatarSkin, Vector3.zero, inputAuthority: Runner.LocalPlayer);
            }
            else
            {
                avatar = Runner.Spawn(_baseAvatar, Vector3.zero, inputAuthority: Runner.LocalPlayer);
            }

            Runner.SetPlayerObject(Runner.LocalPlayer, avatar);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            runner.RemoveCallbacks(this);
            starter.Shutdown();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            switch (CurrentState)
            {
                case MatchState.WaitingForPlayers:

                    if (Runner.ActivePlayers.Count() >= 2)
                    {
                        if (!LobbyStartTimer.IsRunning)
                        {
                            LobbyStartTimer = TickTimer.CreateFromSeconds(Runner, MATCH_START_DELAY);
                        }

                        if (LobbyStartTimer.Expired(Runner))
                        {
                            LobbyStartTimer = TickTimer.None;
                            StartPreMatch();
                        }
                    }
                    else
                    {
                        // Not enough players anymore → cancel countdown
                        LobbyStartTimer = TickTimer.None;
                    }

                    break;

                case MatchState.PreMatch:

                    if (MatchTimer.Expired(Runner))
                        StartMidMatch();

                    break;

                case MatchState.EndMatch:

                    if (MatchTimer.Expired(Runner))
                    {
                        if (CheckGameOver())
                            StartGameOver();
                        else
                            StartPreMatch();
                    }

                    break;

                case MatchState.GameOver:

                    if (MatchTimer.Expired(Runner))
                        RPC_DisconnectEveryone();

                    break;
            }
        }

        private void StartPreMatch()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            CurrentState = MatchState.PreMatch;
            MatchTimer = TickTimer.CreateFromSeconds(Runner, 3f);

            PlayerRegistry.ForEach(obj =>
            {
                obj.RPC_DisableMovement(true);
                RespawnPlayer(obj);
            });
            
            AudioManager.Play("VO_GetReady");
            AudioManager.PlayMusic("BGM_Duel");
            //RPC_ShowGetReadyUI(0);
        }

        public void RespawnPlayer(PlayerObject obj)
        {
            obj.RPC_ResetPlayer();

            Vector3 pos = GetSpawnPosition(obj.Ref, out Quaternion rot);

            obj.RPC_SetSpawnPosition(pos, rot);
        }

        private void StartMidMatch()
        {
            if (!Object.HasStateAuthority) return;

            CurrentState = MatchState.MidMatch;
            AudioManager.Play("VO_LetsRock");

            RPC_ShowGetReadyUI(1);

            PlayerRegistry.ForEach(obj => obj.RPC_DisableMovement(false));
        }

        private void StartEndMatch(PlayerRef winner)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            CurrentState = MatchState.EndMatch;
            RoundWinner = winner;

            RegisterWin(winner);

            MatchTimer = TickTimer.CreateFromSeconds(Runner, 3f);

            RPC_PlayWinLoseAudio(winner);
            RPC_DisplayGameUI(winner);

            PlayerRegistry.ForEach(obj => obj.RPC_DisableMovement(true));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayWinLoseAudio(PlayerRef winner)
        {
            AudioManager.Instance.PlayLocal(PlayerObject.Local.Ref == winner ? "VO_Win" : "VO_Lose");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DisplayGameUI(PlayerRef winner)
        {
            ShowGetReadyUI(PlayerObject.Local.Ref == winner ? 2 : 3);
        }

        private void StartGameOver()
        {
            CurrentState = MatchState.GameOver;
            MatchTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DisconnectEveryone()
        {
            AudioManager.PlayMusic("BGM_Lobby");
            Runner.Shutdown();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReportKill(PlayerRef deadPlayer)
        {
            OnPlayerKilled(deadPlayer);
        }

        private void OnPlayerKilled(PlayerRef deadPlayer)
        {
            if (!Object.HasStateAuthority)
                return;

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (player == deadPlayer)
                {
                    continue;
                }

                StartEndMatch(player);
                break;
            }
        }

        private void RegisterWin(PlayerRef player)
        {
            if (!PlayerWins.ContainsKey(player))
                PlayerWins.Set(player, 0);

            PlayerWins.Set(player, PlayerWins[player] + 1);
        }

        private bool CheckGameOver()
        {
            foreach (KeyValuePair<PlayerRef, int> pair in PlayerWins)
            {
                if (pair.Value >= WINS_TO_END_GAME)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetSpawnPosition(PlayerRef player, out Quaternion rotation)
        {
            // Determine which spawn list to use
            int index = player.AsIndex;

            Transform[] spawnList = index % 2 == 0 ? player1SpawnPoints : player2SpawnPoints;

            if (spawnList == null || spawnList.Length == 0)
            {
                rotation = Quaternion.identity;
                return Vector3.zero;
            }

            int spawnIndex = UnityEngine.Random.Range(0, spawnList.Length);

            rotation = spawnList[spawnIndex].rotation;
            return spawnList[spawnIndex].position;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowGetReadyUI(int uiIndex)
        {
            ShowGetReadyUI(uiIndex);
        }

        private void ShowGetReadyUI(int uiIndex)
        {
            if (Camera.main == null)
            {
                return;
            }

            Transform cam = Camera.main.transform;

            // Flatten forward (ignore vertical tilt)
            Vector3 forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();

            float distance = 2f;

            Vector3 pos = cam.position + forward * distance;

            pos.y = cam.position.y;

            Quaternion rot = Quaternion.LookRotation(pos - cam.position);

            GameObject ui = Instantiate(gameCanvasList[uiIndex], pos, rot);
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            if (!PlayerWins.ContainsKey(player))
                PlayerWins.Set(player, 0);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            if (PlayerWins.ContainsKey(player))
                PlayerWins.Remove(player);
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
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

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
        }

        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}