using _1.Scripts.Audio;
using _1.Scripts.Character;
using _1.Scripts.Managers;
using Fusion;
using Fusion.XR.Shared.Rig;
using UnityEngine;
using UnityEngine.XR;

namespace _1.Scripts.Grabbables
{
    public class GunWeapon : NetworkWeapon
    {
        public float recoilForce = 3f;
        public float range = 30f;
        public int damage = 50;
        public float fireCooldown = 0.5f;

        public int pellets = 8;
        public float pelletRadius = 0.08f;
        public float spreadAngle = 6f;

        public Transform nozzle;
        public ParticleSystem particle;

        [Networked]
        private TickTimer FireCooldown { get; set; }

        protected override void OnGrabPressed()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (!FireCooldown.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            AudioManager.Play("SFX_ShotGun", transform.position);
            particle.Play(true);
            FireCooldown = TickTimer.CreateFromSeconds(Runner, fireCooldown);


            Haptics.Pulse(grabber.hand.side == RigPart.RightController ? XRNode.RightHand : XRNode.LeftHand, 1f, 0.08f);


            PlayerObject owner = PlayerRegistry.GetPlayer(GetOwner());

            if (owner != null)
            {
                PlayerMovement movement = owner.Controller;

                if (movement)
                {
                    Vector3 recoilDir = (-nozzle.forward + Vector3.up * 0.35f).normalized;
                    movement.ApplyRecoil(recoilDir, recoilForce);
                }
            }

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = nozzle.forward;

                // Random spread
                dir = Quaternion.Euler(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0
                ) * dir;

                Ray ray = new(nozzle.position, dir);

                if (Physics.SphereCast(ray, pelletRadius, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide))
                {
                    PlayerObject player = hit.collider.GetComponentInParent<PlayerObject>();

                    if (!player || player.Ref == GetOwner())
                    {
                        continue;
                    }

                    AudioManager.Play("SFX_Hit", transform.position);
                    player.RPC_RequestDamage(GetOwner(), damage);
                }
            }
        }
    }
}