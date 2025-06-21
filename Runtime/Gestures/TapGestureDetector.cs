using UnityEngine;

namespace OOJUPlugin
{
    public class TapGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Tap Detection Settings")]
        [SerializeField] private float tapVelocityThreshold = 1.5f;
        [SerializeField] private float tapDistanceThreshold = 0.05f;
        [SerializeField] private float tapDuration = 0.2f;
        [SerializeField] private float cooldownTime = 0.3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private object leftHand;
        private object rightHand;
        private Vector3 lastLeftHandPos;
        private Vector3 lastRightHandPos;
        private float lastLeftTapTime;
        private float lastRightTapTime;
        private bool currentlyDetected = false;
        private float currentConfidence = 0f;
        private Vector3 currentPosition = Vector3.zero;
        private bool isLeftHandActive = false;
        private bool isMetaXRAvailable = false;

        public GestureType Type => GestureType.Tap;
        public bool IsDetected => currentlyDetected;
        public float Confidence => currentConfidence;
        public Vector3 Position => currentPosition;
        public Vector3 Direction => isLeftHandActive ? Vector3.left : Vector3.right;
        public bool IsLeftHand => isLeftHandActive;

        public event System.Action<GestureData> OnGestureDetected;
        public event System.Action<GestureData> OnGestureReleased;

        public void Initialize()
        {
            // Check if Meta XR SDK is available at runtime
            isMetaXRAvailable = CheckMetaXRAvailability();
            
            if (!isMetaXRAvailable)
            {
                Debug.LogWarning("[TapGestureDetector] Meta XR SDK not available. Tap detection disabled.");
                return;
            }

            // Get reference to hands from the manager
            if (XRGestureInteractionManager.Instance != null)
            {
                leftHand = XRGestureInteractionManager.Instance.LeftHand;
                rightHand = XRGestureInteractionManager.Instance.RightHand;
            }

            if (leftHand == null && rightHand == null)
            {
                Debug.LogWarning("[TapGestureDetector] No OVR Hands found for tap detection");
            }

            // Initialize positions
            if (leftHand != null) lastLeftHandPos = GetTapPosition(leftHand);
            if (rightHand != null) lastRightHandPos = GetTapPosition(rightHand);
        }

        private bool CheckMetaXRAvailability()
        {
            // Try to find OVRHand type using reflection
            try
            {
                var ovrHandType = System.Type.GetType("OVRHand");
                return ovrHandType != null;
            }
            catch
            {
                return false;
            }
        }

        public void UpdateDetection()
        {
            if (!enabled || !isMetaXRAvailable) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            // Check left hand
            if (leftHand != null && IsHandTracked(leftHand))
            {
                CheckHandForTap(leftHand, ref lastLeftHandPos, ref lastLeftTapTime, true);
            }

            // Check right hand
            if (rightHand != null && IsHandTracked(rightHand))
            {
                CheckHandForTap(rightHand, ref lastRightHandPos, ref lastRightTapTime, false);
            }

            // Reset detection state after cooldown
            if (currentlyDetected && Time.time > (isLeftHandActive ? lastLeftTapTime : lastRightTapTime) + tapDuration)
            {
                currentlyDetected = false;
                currentConfidence = 0f;
            }
        }

        private bool IsHandTracked(object hand)
        {
            if (!isMetaXRAvailable || hand == null) return false;
            
            try
            {
                // Use reflection to check IsTracked property
                var handType = hand.GetType();
                var isTrackedProperty = handType.GetProperty("IsTracked");
                if (isTrackedProperty != null)
                {
                    return (bool)isTrackedProperty.GetValue(hand);
                }
            }
            catch
            {
                // Ignore reflection errors
            }
            
            return false;
        }

        private void CheckHandForTap(object hand, ref Vector3 lastPos, ref float lastTapTime, bool isLeft)
        {
            Vector3 currentPos = GetTapPosition(hand);
            Vector3 velocity = (currentPos - lastPos) / Time.deltaTime;
            float velocityMagnitude = velocity.magnitude;

            // Check if in cooldown
            if (Time.time < lastTapTime + cooldownTime)
            {
                lastPos = currentPos;
                return;
            }

            // Detect tap: high velocity followed by near-stop
            bool isFastMovement = velocityMagnitude > tapVelocityThreshold;
            bool isWithinTapDistance = Vector3.Distance(currentPos, lastPos) < tapDistanceThreshold;

            if (isFastMovement && !currentlyDetected)
            {
                // Start monitoring for tap completion
                if (showDebugInfo)
                {
                    Debug.Log($"[TapDetector] Fast movement detected - {(isLeft ? "Left" : "Right")} hand, velocity: {velocityMagnitude:F2}");
                }
            }

            // Check for tap completion (quick movement then stop)
            if (velocityMagnitude < tapVelocityThreshold * 0.3f && !currentlyDetected)
            {
                // Check if this follows a fast movement within the tap duration
                Vector3 handDirection = GetHandForwardDirection(hand);
                TriggerTapDetected(currentPos, handDirection, velocityMagnitude, isLeft);
                lastTapTime = Time.time;
            }

            lastPos = currentPos;
        }

        private Vector3 GetTapPosition(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return Vector3.zero;

            try
            {
                // Try to get PointerPose position
                var handType = hand.GetType();
                var pointerPoseProperty = handType.GetProperty("PointerPose");
                if (pointerPoseProperty != null)
                {
                    var pointerPose = pointerPoseProperty.GetValue(hand);
                    if (pointerPose != null)
                    {
                        var poseType = pointerPose.GetType();
                        var positionProperty = poseType.GetProperty("position");
                        if (positionProperty != null)
                        {
                            return (Vector3)positionProperty.GetValue(pointerPose);
                        }
                    }
                }

                // Fallback to transform position
                var transformProperty = handType.GetProperty("transform");
                if (transformProperty != null)
                {
                    var transform = (Transform)transformProperty.GetValue(hand);
                    return transform.position;
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return Vector3.zero;
        }

        private Vector3 GetHandForwardDirection(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return Vector3.forward;

            try
            {
                // Use hand forward direction
                var handType = hand.GetType();
                var transformProperty = handType.GetProperty("transform");
                if (transformProperty != null)
                {
                    var transform = (Transform)transformProperty.GetValue(hand);
                    return transform.forward;
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return Vector3.forward;
        }

        private void TriggerTapDetected(Vector3 position, Vector3 direction, float intensity, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.Tap,
                position,
                direction,
                1f, // Confidence is always high for tap
                intensity,
                isLeft
            );

            currentlyDetected = true;
            currentConfidence = 1f;
            currentPosition = position;
            isLeftHandActive = isLeft;

            if (showDebugInfo)
            {
                Debug.Log($"[TapDetector] Tap detected - {(isLeft ? "Left" : "Right")} hand at {position}");
            }

            OnGestureDetected?.Invoke(gestureData);

            // Auto-release after tap duration
            Invoke(nameof(ReleaseTap), tapDuration);
        }

        private void ReleaseTap()
        {
            if (currentlyDetected)
            {
                var gestureData = new GestureData(
                    GestureType.Tap,
                    currentPosition,
                    isLeftHandActive ? Vector3.left : Vector3.right,
                    0f,
                    0f,
                    isLeftHandActive
                );

                OnGestureReleased?.Invoke(gestureData);
                currentlyDetected = false;
                currentConfidence = 0f;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled || !isMetaXRAvailable) return;

            // Draw tap positions and velocity
            if (leftHand != null && IsHandTracked(leftHand))
            {
                Vector3 pos = GetTapPosition(leftHand);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(pos, 0.02f);

                // Draw velocity vector
                Vector3 velocity = (pos - lastLeftHandPos) / Time.deltaTime;
                if (velocity.magnitude > tapVelocityThreshold)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(pos, velocity.normalized * 0.1f);
                }
            }

            if (rightHand != null && IsHandTracked(rightHand))
            {
                Vector3 pos = GetTapPosition(rightHand);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, 0.02f);

                // Draw velocity vector
                Vector3 velocity = (pos - lastRightHandPos) / Time.deltaTime;
                if (velocity.magnitude > tapVelocityThreshold)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(pos, velocity.normalized * 0.1f);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Tap Detection")]
        private void TestTapDetection()
        {
            var testData = new GestureData(GestureType.Tap, transform.position, Vector3.forward);
            OnGestureDetected?.Invoke(testData);
        }
#endif
    }
} 