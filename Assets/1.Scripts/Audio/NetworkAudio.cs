using Fusion;
using UnityEngine;

namespace _1.Scripts.Audio
{
    public class NetworkAudio : NetworkBehaviour
    {
        public static NetworkAudio Instance;

        private void Awake()
        {
            Instance = this;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_PlayGlobal(string clip, float pitch = 1f)
        {
            AudioManager.Instance.PlayLocal(clip, null, pitch);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_PlayGlobal(string clip, Vector3 pos, float pitch = 1f)
        {
            AudioManager.Instance.PlayLocal(clip, pos, pitch);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_PlayMusicGlobal(string clip)
        {
            AudioManager.Instance.PlayMusicLocal(clip);
        }
    }
}