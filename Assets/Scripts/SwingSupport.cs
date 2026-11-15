using UnityEngine;

namespace NailBalance
{
    [RequireComponent(typeof(HingeJoint2D))]
    public class SwingSupport : MonoBehaviour
    {
        [Range(1, 10)] public float speed = 4f;
        [Min(1)] public float period = 5f;
        HingeJoint2D joint;
        float startTime;

        void Start()
        {
            joint = GetComponent<HingeJoint2D>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = transform.TransformPoint(joint.anchor);
            startTime = Time.time;
        }

        void FixedUpdate() => Drive(Time.time - startTime);

        public void Drive(float elapsed)
        {
            if (!joint) joint = GetComponent<HingeJoint2D>();
            JointMotor2D motor = joint.motor;
            motor.motorSpeed = Mathf.Cos(elapsed * Mathf.PI * 2f / period) * speed;
            joint.motor = motor;
        }
    }
}
