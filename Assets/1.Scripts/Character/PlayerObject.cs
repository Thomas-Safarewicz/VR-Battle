using _1.Scripts.Managers;
using Fusion;
using UnityEngine;

namespace _1.Scripts.Character
{
    /// <summary>
    /// Network Behaviour that defines information for the players other than movement.
    /// </summary>
    public class PlayerObject : NetworkBehaviour
    {
        /// <summary>
        /// A static reference to the local player
        /// </summary>
        public static PlayerObject Local { get; private set; }

        [Networked]
        public PlayerRef Ref { get; set; }

        [Networked]
        public byte Index { get; set; }

        [field: Header("References"), SerializeField]
        public PlayerMovement Controller { get; private set; }

        [Networked, Tooltip("Which PlayerRef killed this player.")]
        public PlayerRef Killer { get; set; } = PlayerRef.None;

        [Networked]
        private bool IsDead { get; set; }

        [Tooltip("The length of time the player will be in an invulernable \"damaged\" state after being hit."), SerializeField]
        float damageRecoveryTime;

        [Networked, Tooltip("How much health the player has.  Player will die if it reaches 0.")]
        private int Health { get; set; }

        [Networked, Tooltip("The maximum amount of health the player has.  Will increase when the player levels up.")]
        public int MaxHealth { get; set; }

        [Networked]
        private Vector3 TargetPosition { get; set; }

        [Networked]
        private Quaternion TargetRotation { get; set; }

        [Networked]
        private NetworkBool TeleportPending { get; set; }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        public void RPC_Init(PlayerRef pRef, byte index)
        {
            Ref = pRef;
            Index = index;
        }

        public override void Spawned()
        {
            base.Spawned();

            if (Object.HasInputAuthority)
            {
                Local = this;
                Controller = FindObjectOfType<PlayerMovement>();
            }

            if (Runner.IsSceneAuthority)
            {
                PlayerRegistry.Server_Add(Runner, Object.InputAuthority, this);
                GameManager.Instance.RespawnPlayer(this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority)
                return;

            if (TeleportPending)
            {
                Controller.Teleport(TargetPosition, TargetRotation);

                TeleportPending = false;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetSpawnPosition(Vector3 position, Quaternion rotation)
        {
            TargetPosition = position;
            TargetRotation = rotation;
            TeleportPending = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DisableMovement(bool disable)
        {
            Controller.DisableMovement(disable);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ResetPlayer()
        {
            Health = MaxHealth;
            Killer = PlayerRef.None;

            IsDead = false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestDamage(PlayerRef srcRef, int damage)
        {
            AttemptDamage(srcRef, damage);
        }

        private void AttemptDamage(PlayerRef srcRef, int damage)
        {
            if (IsDead)
            {
                return;
            }

            Health -= damage;
            if (Health <= 0)
            {
                IsDead = true;
                Controller.disableMovement = true;

                Killer = srcRef;

                GameManager.Instance.RPC_ReportKill(Ref);
            }
        }
    }
}