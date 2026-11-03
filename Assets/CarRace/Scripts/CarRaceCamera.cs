using UnityEngine;

namespace CarRace
{
    public sealed class CarRaceCamera : MonoBehaviour
    {
        private Transform target;
        private Vector3 velocity;

        private void Awake()
        {
            if (GetComponent<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }
        }

        public void Initialize(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            velocity = Vector3.zero;
            Vector3 targetHead = target.position + Vector3.up * 1.3f;
            Vector3 desired = target.position - target.forward * 5.6f + Vector3.up * 2.2f;
            Vector3 dir = (desired - targetHead).normalized;
            float dist = Vector3.Distance(targetHead, desired);
            if (Physics.SphereCast(targetHead, 0.35f, dir, out RaycastHit hit, dist))
            {
                if (hit.collider.transform.root != target.root)
                {
                    desired = targetHead + dir * Mathf.Max(hit.distance - 0.3f, 1.8f);
                }
            }
            transform.position = desired;
            Vector3 lookPoint = target.position + target.forward * 3.5f + Vector3.up * 0.8f;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 targetHead = target.position + Vector3.up * 1.3f;
            Vector3 desired = target.position - target.forward * 5.6f + Vector3.up * 2.2f;

            // Camera collision check: prevent camera from clipping inside rocks
            Vector3 dir = (desired - targetHead).normalized;
            float dist = Vector3.Distance(targetHead, desired);
            if (Physics.SphereCast(targetHead, 0.35f, dir, out RaycastHit hit, dist))
            {
                if (hit.collider.transform.root != target.root)
                {
                    desired = targetHead + dir * Mathf.Max(hit.distance - 0.3f, 1.8f);
                }
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 lookPoint = target.position + target.forward * 3.5f + Vector3.up * 0.8f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookPoint - transform.position), 10f * Time.unscaledDeltaTime);
        }
    }
}
