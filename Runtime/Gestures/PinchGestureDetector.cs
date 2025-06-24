using UnityEngine;

namespace OOJUPlugin
{
    public class PinchGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Pinch Detection Settings")]
        [SerializeField] private float pinchThreshold = 0.7f;
        [SerializeField] private float releaseThreshold = 0.3f;
        [SerializeField] private bool detectBothHands = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private object leftHand;
        private object rightHand;
        private bool wasLeftPinching = false;
        private bool wasRightPinching = false;
        private bool currentlyDetected = false;
        private float currentConfidence = 0f;
        private Vector3 currentPosition = Vector3.zero;
        private bool isLeftHandActive = false;
        private bool isMetaXRAvailable = false;

        public GestureType Type => GestureType.Pinch;
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
                Debug.LogWarning("[PinchGestureDetector] Meta XR SDK not available. Pinch detection disabled.");
                return;
            }

            // Get reference to hands from the manager
            if (XRGestureInteractionManager.Instance != null)
            {
                leftHand = XRGestureInteractionManager.Instance.LeftHand;
                rightHand = XRGestureInteractionManager.Instance.RightHand;
                
                Debug.Log($"[PinchGestureDetector] Hands from manager - Left: {leftHand != null}, Right: {rightHand != null}");
            }

            if (leftHand == null && rightHand == null)
            {
                Debug.LogWarning("[PinchGestureDetector] No OVR Hands found for pinch detection");
            }
            else
            {
                Debug.Log($"[PinchGestureDetector] Pinch detection initialized successfully");
            }
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

            bool leftPinching = false;
            bool rightPinching = false;
            float leftStrength = 0f;
            float rightStrength = 0f;
            Vector3 leftPos = Vector3.zero;
            Vector3 rightPos = Vector3.zero;

            // Check left hand
            if (leftHand != null && IsHandTracked(leftHand))
            {
                leftStrength = GetPinchStrength(leftHand);
                leftPinching = leftStrength > pinchThreshold;
                leftPos = GetPinchPosition(leftHand);

                if (showDebugInfo)
                {
                    Debug.Log($"[PinchDetector] Left hand pinch strength: {leftStrength:F2}");
                }
            }

            // Check right hand
            if (rightHand != null && IsHandTracked(rightHand))
            {
                rightStrength = GetPinchStrength(rightHand);
                rightPinching = rightStrength > pinchThreshold;
                rightPos = GetPinchPosition(rightHand);

                if (showDebugInfo)
                {
                    Debug.Log($"[PinchDetector] Right hand pinch strength: {rightStrength:F2}");
                }
            }

            // Handle left hand pinch events
            if (leftPinching && !wasLeftPinching)
            {
                TriggerPinchDetected(leftPos, leftStrength, true);
            }
            else if (!leftPinching && wasLeftPinching && leftStrength < releaseThreshold)
            {
                TriggerPinchReleased(leftPos, leftStrength, true);
            }

            // Handle right hand pinch events
            if (rightPinching && !wasRightPinching)
            {
                TriggerPinchDetected(rightPos, rightStrength, false);
            }
            else if (!rightPinching && wasRightPinching && rightStrength < releaseThreshold)
            {
                TriggerPinchReleased(rightPos, rightStrength, false);
            }

            // Update current state (prioritize the stronger pinch)
            if (leftPinching || rightPinching)
            {
                if (leftStrength > rightStrength)
                {
                    currentPosition = leftPos;
                    currentConfidence = leftStrength;
                    isLeftHandActive = true;
                }
                else
                {
                    currentPosition = rightPos;
                    currentConfidence = rightStrength;
                    isLeftHandActive = false;
                }
                currentlyDetected = true;
            }
            else
            {
                currentlyDetected = false;
                currentConfidence = 0f;
            }

            wasLeftPinching = leftPinching;
            wasRightPinching = rightPinching;
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

        private float GetPinchStrength(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return 0f;

            try
            {
                // Use reflection to call GetFingerPinchStrength method
                var handType = hand.GetType();
                var getPinchMethod = handType.GetMethod("GetFingerPinchStrength");
                if (getPinchMethod != null)
                {
                    // Get HandFinger.Index enum value
                    var handFingerType = System.Type.GetType("OVRHand+HandFinger");
                    if (handFingerType != null)
                    {
                        var indexValue = System.Enum.Parse(handFingerType, "Index");
                        return (float)getPinchMethod.Invoke(hand, new object[] { indexValue });
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return 0f;
        }

        private Vector3 GetPinchPosition(object hand)
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

        private void TriggerPinchDetected(Vector3 position, float strength, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.Pinch,
                position,
                isLeft ? Vector3.left : Vector3.right,
                strength,
                strength,
                isLeft
            );

            if (showDebugInfo)
            {
                Debug.Log($"[PinchDetector] Pinch detected - {(isLeft ? "Left" : "Right")} hand at {position}");
            }

            OnGestureDetected?.Invoke(gestureData);
        }

        private void TriggerPinchReleased(Vector3 position, float strength, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.Pinch,
                position,
                isLeft ? Vector3.left : Vector3.right,
                strength,
                strength,
                isLeft
            );

            if (showDebugInfo)
            {
                Debug.Log($"[PinchDetector] Pinch released - {(isLeft ? "Left" : "Right")} hand");
            }

            OnGestureReleased?.Invoke(gestureData);
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled || !isMetaXRAvailable) return;

            // Draw pinch positions
            if (leftHand != null && IsHandTracked(leftHand))
            {
                Gizmos.color = wasLeftPinching ? Color.green : Color.blue;
                Gizmos.DrawWireSphere(GetPinchPosition(leftHand), 0.02f);
            }

            if (rightHand != null && IsHandTracked(rightHand))
            {
                Gizmos.color = wasRightPinching ? Color.green : Color.red;
                Gizmos.DrawWireSphere(GetPinchPosition(rightHand), 0.02f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Pinch Detection")]
        private void TestPinchDetection()
        {
            var testData = new GestureData(GestureType.Pinch, transform.position, Vector3.forward);
            OnGestureDetected?.Invoke(testData);
        }
#endif
    }
} 