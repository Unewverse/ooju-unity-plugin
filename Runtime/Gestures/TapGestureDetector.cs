using UnityEngine;

namespace OOJUPlugin
{
    public class TapGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Tap Detection Settings")]
        [SerializeField] private float tapVelocityThreshold = 0.1f;
        [SerializeField] private float tapDistanceThreshold = 0.01f;
        [SerializeField] private float tapDuration = 0.2f;
        [SerializeField] private float cooldownTime = 0.3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showRealtimeHandData = true;
        [SerializeField] private bool showMovementDebug = true;

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
        
        // Debug timing
        private float lastDebugTime = 0f;
        private float debugInterval = 1f; // Debug output every 1 second

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
                
                Debug.Log($"[TapGestureDetector] Hands from manager - Left: {leftHand != null}, Right: {rightHand != null}");
            }

            if (leftHand == null && rightHand == null)
            {
                Debug.LogWarning("[TapGestureDetector] No OVR Hands found for tap detection");
            }
            else
            {
                Debug.Log($"[TapGestureDetector] Tap detection initialized successfully");
            }

            // Initialize positions
            if (leftHand != null) lastLeftHandPos = GetTapPosition(leftHand);
            if (rightHand != null) lastRightHandPos = GetTapPosition(rightHand);
        }

        private bool CheckMetaXRAvailability()
        {
            // Try to find OVRHand type using more robust search
            try
            {
                // Search through all loaded assemblies
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            if (type.Name == "OVRHand" || type.FullName.EndsWith(".OVRHand"))
                            {
                                return true;
                            }
                        }
                    }
                    catch (System.Exception)
                    {
                        // Skip assemblies that can't be loaded
                        continue;
                    }
                }
            }
            catch
            {
                return false;
            }
            
            return false;
        }

        public void UpdateDetection()
        {
            if (!enabled || !isMetaXRAvailable) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            // Real-time hand data debugging for VR
            if (showRealtimeHandData && Time.time > lastDebugTime + debugInterval)
            {
                if (leftHand != null)
                {
                    Vector3 leftPos = GetTapPosition(leftHand);
                    bool leftTracked = IsHandTracked(leftHand);
                    Debug.Log($"[TapDetector] Left Hand - Pos: {leftPos}, Tracked: {leftTracked}");
                }
                
                if (rightHand != null)
                {
                    Vector3 rightPos = GetTapPosition(rightHand);
                    bool rightTracked = IsHandTracked(rightHand);
                    Debug.Log($"[TapDetector] Right Hand - Pos: {rightPos}, Tracked: {rightTracked}");
                }
                
                lastDebugTime = Time.time;
            }

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
            if (hand == null) return false;
            
            try
            {
                // First try standard IsTracked property
                var handType = hand.GetType();
                var isTrackedProperty = handType.GetProperty("IsTracked");
                if (isTrackedProperty != null)
                {
                    return (bool)isTrackedProperty.GetValue(hand);
                }
                
                // For Building Blocks, check if transform is moving (indicating tracking)
                var handComponent = hand as MonoBehaviour;
                if (handComponent != null)
                {
                    // If we have a valid transform position, consider it tracked
                    return handComponent.transform.position != Vector3.zero;
                }
            }
            catch
            {
                // If reflection fails, assume tracked if hand exists
                return true;
            }
            
            return true; // Default to true if hand exists
        }

        private void CheckHandForTap(object hand, ref Vector3 lastPos, ref float lastTapTime, bool isLeft)
        {
            Vector3 currentPos = GetTapPosition(hand);
            
            // Skip if position is zero (not tracked)
            if (currentPos == Vector3.zero)
            {
                lastPos = currentPos;
                return;
            }
            
            Vector3 movement = currentPos - lastPos;
            float distance = movement.magnitude;
            float velocityMagnitude = distance / Time.deltaTime;

            // Movement debugging
            if (showMovementDebug && distance > 0.001f) // Show any movement above 1mm
            {
                Debug.Log($"[TapDetector] {(isLeft ? "Left" : "Right")} hand movement - Distance: {distance:F4}m, Velocity: {velocityMagnitude:F3}m/s, Threshold: {tapVelocityThreshold}m/s");
            }

            // Check if in cooldown
            if (Time.time < lastTapTime + cooldownTime)
            {
                lastPos = currentPos;
                return;
            }

            // Very simple tap detection: any movement above very low threshold
            if (distance > tapDistanceThreshold || velocityMagnitude > tapVelocityThreshold)
            {
                Vector3 handDirection = GetHandForwardDirection(hand);
                TriggerTapDetected(currentPos, handDirection, velocityMagnitude, isLeft);
                lastTapTime = Time.time;
                
                Debug.Log($"[TapDetector] TAP DETECTED! {(isLeft ? "Left" : "Right")} hand - Distance: {distance:F4}m, Velocity: {velocityMagnitude:F3}m/s");
            }

            lastPos = currentPos;
        }

        private Vector3 GetTapPosition(object hand)
        {
            if (hand == null) return Vector3.zero;

            try
            {
                // Try Building Blocks Hand approach first
                var handComponent = hand as MonoBehaviour;
                if (handComponent != null)
                {
                    // Use the hand's transform position directly
                    return handComponent.transform.position;
                }
                
                // Try to get PointerPose position for traditional OVRHand
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

                // Final fallback to transform
                var transformProperty = handType.GetProperty("transform");
                if (transformProperty != null)
                {
                    var transform = (Transform)transformProperty.GetValue(hand);
                    return transform.position;
                }
            }
            catch (System.Exception e)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[TapDetector] Error getting hand position: {e.Message}");
                }
            }

            return Vector3.zero;
        }

        private Vector3 GetHandForwardDirection(object hand)
        {
            if (hand == null) return Vector3.forward;

            try
            {
                // Use Building Blocks Hand approach first
                var handComponent = hand as MonoBehaviour;
                if (handComponent != null)
                {
                    return handComponent.transform.forward;
                }
                
                // Fallback to reflection
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
                // Ignore errors
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