using UnityEngine;

namespace CarRace
{
    public sealed class CarRaceCamera : MonoBehaviour
    {
        private Transform target;
        private Vector3 velocity;

        public void Initialize(Transform followTarget)
        {
            target = followTarget;
            transform.position = target.position - target.forward * 8f + Vector3.up * 4.2f;
            transform.LookAt(target.position + Vector3.up * 1f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position - target.forward * 8.5f + Vector3.up * 4.3f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.18f);
            Vector3 lookPoint = target.position + target.forward * 4f + Vector3.up * 0.9f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookPoint - transform.position), 8f * Time.unscaledDeltaTime);
        }
    }
}
