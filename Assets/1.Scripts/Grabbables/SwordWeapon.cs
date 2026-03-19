using _1.Scripts.Audio;
using _1.Scripts.Character;
using Fusion;
using UnityEngine;

namespace _1.Scripts.Grabbables
{
    public class SwordWeapon : NetworkWeapon
    {
        public float swingSpeed = 2.5f;
        public float cleaveRadius = 1.5f;
        public int damage = 40;
        public float swingCooldown = 0.35f;

        [Networked]
        private TickTimer SwingCooldown { get; set; }

        private Vector3 lastPos;

        private void Update()
        {
            if (!grabber)
            {
                return;
            }

            float velocity = (transform.position - lastPos).magnitude / Time.deltaTime;
            lastPos = transform.position;

            if (velocity > swingSpeed)
            {
                TryCleave();
            }
        }

        private void TryCleave()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (!SwingCooldown.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            SwingCooldown = TickTimer.CreateFromSeconds(Runner, swingCooldown);

            Collider[] hits = Physics.OverlapSphere(transform.position, cleaveRadius,~0, QueryTriggerInteraction.Collide);

            foreach (Collider c in hits)
            {
                PlayerObject player = c.GetComponentInParent<PlayerObject>();

                if (player && player.Ref != GetOwner())
                {
                    AudioManager.Play("SFX_Hit",transform.position);
                    player.RPC_RequestDamage(GetOwner(), damage);
                }
            }
        }
    }
}