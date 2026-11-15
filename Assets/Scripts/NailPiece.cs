using System.Collections.Generic;
using UnityEngine;

namespace NailBalance
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class NailPiece : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }
        public bool Released { get; private set; }
        readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

        void Awake() => Body = GetComponent<Rigidbody2D>();

        public void Prepare(PuzzleDefinition.NailSettings settings)
        {
            Body = GetComponent<Rigidbody2D>();
            Body.simulated = false;
            Body.mass = settings.mass;
            transform.localScale = new Vector3(settings.length, 1, 1);
            transform.rotation = Quaternion.Euler(0, 0, settings.initialAngle);
            Released = false;
        }

        public void Release()
        {
            Released = true;
            Body.simulated = true;
            Body.WakeUp();
        }

        // A supported nail must touch the platform, or another supported nail.
        public bool TouchesSupport(Transform support, HashSet<Rigidbody2D> supported)
        {
            int count = Body.GetContacts(contacts);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = contacts[i].collider;
                if (other.attachedRigidbody == Body) other = contacts[i].otherCollider;
                if (other.transform.IsChildOf(support) || supported.Contains(other.attachedRigidbody))
                    return true;
            }
            return false;
        }

        public bool IsQuiet => Body.linearVelocity.sqrMagnitude < 0.16f && Mathf.Abs(Body.angularVelocity) < 25f;
    }
}
