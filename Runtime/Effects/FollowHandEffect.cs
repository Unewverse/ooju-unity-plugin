using UnityEngine;

namespace OOJUPlugin
{
    public class FollowHandEffect : IInteractionEffect
    {
        public InteractionType Type => InteractionType.FollowHand;
        public bool IsActive { get; private set; }

        [Header("Follow Settings")]
        private float followSpeed = 8f;
        private float rotationSpeed = 5f;
        private Vector3 handOffset = Vector3.zero;
        private bool maintainOriginalRotation = false;
        private bool smoothFollowing = true;

        [Header("Physics")]
        private bool disablePhysics = true;
        private bool restorePhysicsOnRelease = true;

        // Internal state
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Rigidbody targetRigidbody;
        private bool originalKinematic;
        private bool originalUseGravity;
        private bool wasPhysicsDisabled = false;

        public void Execute(GameObject target, GestureData gestureData)
        {
            if (target == null) return;

            IsActive = true;

            // Store original state
            originalPosition = target.transform.position;
            originalRotation = target.transform.rotation;

            // Handle physics
            HandlePhysicsSetup(target);

            // Calculate offset based on gesture position
            CalculateHandOffset(target, gestureData);

            Debug.Log($"[FollowHandEffect] Started following for {target.name}");
        }

        public void Update(GameObject target, GestureData gestureData)
        {
            if (!IsActive || target == null) return;

            // Calculate target position with offset
            targetPosition = gestureData.position + handOffset;

            // Handle rotation
            if (!maintainOriginalRotation)
            {
                // Face the hand direction
                Vector3 directionToHand = (gestureData.position - target.transform.position).normalized;
                if (directionToHand != Vector3.zero)
                {
                    targetRotation = Quaternion.LookRotation(directionToHand);
                }
            }
            else
            {
                targetRotation = originalRotation;
            }

            // Apply movement
            if (smoothFollowing)
            {
                // Smooth interpolation
                target.transform.position = Vector3.Lerp(
                    target.transform.position, 
                    targetPosition, 
                    followSpeed * Time.deltaTime
                );

                target.transform.rotation = Quaternion.Slerp(
                    target.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
            else
            {
                // Direct assignment
                target.transform.position = targetPosition;
                target.transform.rotation = targetRotation;
            }

            // Visual feedback - slight scale pulsing
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.05f;
            Vector3 baseScale = Vector3.one;
            target.transform.localScale = baseScale * pulse;
        }

        public void Stop(GameObject target)
        {
            if (!IsActive || target == null) return;

            IsActive = false;

            // Restore physics
            if (restorePhysicsOnRelease)
            {
                RestorePhysics(target);
            }

            // Reset scale
            target.transform.localScale = Vector3.one;

            Debug.Log($"[FollowHandEffect] Stopped following for {target.name}");
        }

        private void HandlePhysicsSetup(GameObject target)
        {
            if (!disablePhysics) return;

            targetRigidbody = target.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                originalKinematic = targetRigidbody.isKinematic;
                originalUseGravity = targetRigidbody.useGravity;

                targetRigidbody.isKinematic = true;
                targetRigidbody.useGravity = false;
                wasPhysicsDisabled = true;
            }
        }

        private void RestorePhysics(GameObject target)
        {
            if (!wasPhysicsDisabled || targetRigidbody == null) return;

            targetRigidbody.isKinematic = originalKinematic;
            targetRigidbody.useGravity = originalUseGravity;
            wasPhysicsDisabled = false;
        }

        private void CalculateHandOffset(GameObject target, GestureData gestureData)
        {
            // Calculate offset to maintain relative position
            Vector3 objectToHand = target.transform.position - gestureData.position;
            
            // Limit the offset to a reasonable distance (prevent objects from being too far)
            float maxOffset = 0.3f;
            if (objectToHand.magnitude > maxOffset)
            {
                objectToHand = objectToHand.normalized * maxOffset;
            }

            handOffset = objectToHand;
        }

        // Configuration methods for script generation
        public void SetFollowSpeed(float speed)
        {
            followSpeed = Mathf.Clamp(speed, 0.1f, 20f);
        }

        public void SetSmoothFollowing(bool smooth)
        {
            smoothFollowing = smooth;
        }

        public void SetMaintainRotation(bool maintain)
        {
            maintainOriginalRotation = maintain;
        }

        public void SetPhysicsHandling(bool disable, bool restore = true)
        {
            disablePhysics = disable;
            restorePhysicsOnRelease = restore;
        }

        public void SetHandOffset(Vector3 offset)
        {
            handOffset = offset;
        }
    }
} 