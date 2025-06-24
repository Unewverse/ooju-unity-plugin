using UnityEngine;

namespace OOJUPlugin
{
    public class PointGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Point Detection Settings")]
        [SerializeField] private float pointingAngleThreshold = 30f; // degrees
        [SerializeField] private float fingerExtensionThreshold = 0.8f;
        [SerializeField] private float otherFingersClosedThreshold = 0.3f;
        [SerializeField] private float detectionStabilityTime = 0.1f;
        [SerializeField] private float maxPointingDistance = 3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool drawPointingRay = true;

        private object leftHand;
        private object rightHand;
        private bool isMetaXRAvailable = false;
        private float leftPointingStartTime;
        private float rightPointingStartTime;
        private bool leftWasPointing = false;
        private bool rightWasPointing = false;
        private bool currentlyDetected = false;
        private float currentConfidence = 0f;
        private Vector3 currentPosition = Vector3.zero;
        private Vector3 currentDirection = Vector3.forward;
        private bool isLeftHandActive = false;

        public GestureType Type => GestureType.PointToSelect;
        public bool IsDetected => currentlyDetected;
        public float Confidence => currentConfidence;
        public Vector3 Position => currentPosition;
        public Vector3 Direction => currentDirection;
        public bool IsLeftHand => isLeftHandActive;

        public event System.Action<GestureData> OnGestureDetected;
        public event System.Action<GestureData> OnGestureReleased;

        public void Initialize()
        {
            // Check if Meta XR SDK is available at runtime
            isMetaXRAvailable = CheckMetaXRAvailability();
            
            if (!isMetaXRAvailable)
            {
                Debug.LogWarning("[PointGestureDetector] Meta XR SDK not available. Point detection disabled.");
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
                Debug.LogWarning("[PointGestureDetector] No OVR Hands found for point detection");
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

            // Check left hand
            if (leftHand != null && IsHandTracked(leftHand))
            {
                CheckHandForPointing(leftHand, ref leftPointingStartTime, ref leftWasPointing, true);
            }

            // Check right hand
            if (rightHand != null && IsHandTracked(rightHand))
            {
                CheckHandForPointing(rightHand, ref rightPointingStartTime, ref rightWasPointing, false);
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

        private void CheckHandForPointing(object hand, ref float pointingStartTime, ref bool wasPointing, bool isLeft)
        {
            bool isPointingNow = IsHandPointing(hand, out float confidence, out Vector3 pointDirection);

            if (isPointingNow && !wasPointing)
            {
                // Started pointing
                pointingStartTime = Time.time;
                wasPointing = true;

                if (showDebugInfo)
                {
                    Debug.Log($"[PointDetector] Started pointing - {(isLeft ? "Left" : "Right")} hand");
                }
            }
            else if (!isPointingNow && wasPointing)
            {
                // Stopped pointing
                wasPointing = false;
                
                if (currentlyDetected && isLeftHandActive == isLeft)
                {
                    TriggerPointReleased(isLeft);
                }

                if (showDebugInfo)
                {
                    Debug.Log($"[PointDetector] Stopped pointing - {(isLeft ? "Left" : "Right")} hand");
                }
            }
            else if (isPointingNow && wasPointing)
            {
                // Continue pointing - check stability
                float pointingDuration = Time.time - pointingStartTime;
                
                if (pointingDuration >= detectionStabilityTime)
                {
                    Vector3 pointingPosition = GetPointingPosition(hand);
                    
                    if (!currentlyDetected || isLeftHandActive != isLeft)
                    {
                        TriggerPointDetected(pointingPosition, pointDirection, confidence, isLeft);
                    }
                    else
                    {
                        // Update current pointing
                        UpdateCurrentPointing(pointingPosition, pointDirection, confidence, isLeft);
                    }
                }
            }
        }

        private bool IsHandPointing(object hand, out float confidence, out Vector3 pointDirection)
        {
            confidence = 0f;
            pointDirection = Vector3.forward;

            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return false;

            try
            {
                // Get finger curl values using reflection
                float indexCurl = 1f - GetFingerPinchStrength(hand, "Index");
                float middleCurl = GetFingerPinchStrength(hand, "Middle");
                float ringCurl = GetFingerPinchStrength(hand, "Ring");
                float pinkyCurl = GetFingerPinchStrength(hand, "Pinky");

                // Index finger should be extended
                bool indexExtended = indexCurl > fingerExtensionThreshold;

                // Other fingers should be closed
                bool otherFingersClosed = (middleCurl + ringCurl + pinkyCurl) / 3f > otherFingersClosedThreshold;

                // Get pointing direction
                pointDirection = GetHandForwardDirection(hand);

                // Calculate confidence based on finger positions
                float fingerConfidence = 0f;
                if (indexExtended && otherFingersClosed)
                {
                    fingerConfidence = Mathf.Min(indexCurl, (middleCurl + ringCurl + pinkyCurl) / 3f);
                }

                // Check pointing angle (hand should be relatively straight)
                Vector3 handForward = GetHandForwardDirection(hand);
                float pointingAngle = Vector3.Angle(handForward, pointDirection);
                bool goodPointingAngle = pointingAngle < pointingAngleThreshold;

                if (indexExtended && otherFingersClosed && goodPointingAngle)
                {
                    confidence = fingerConfidence * (1f - pointingAngle / pointingAngleThreshold);
                    return true;
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return false;
        }

        private float GetFingerPinchStrength(object hand, string fingerName)
        {
            if (!isMetaXRAvailable || hand == null) return 0f;

            try
            {
                // Use reflection to call GetFingerPinchStrength method
                var handType = hand.GetType();
                var getPinchMethod = handType.GetMethod("GetFingerPinchStrength");
                if (getPinchMethod != null)
                {
                    // Get HandFinger enum value
                    var handFingerType = System.Type.GetType("OVRHand+HandFinger");
                    if (handFingerType != null)
                    {
                        var fingerValue = System.Enum.Parse(handFingerType, fingerName);
                        return (float)getPinchMethod.Invoke(hand, new object[] { fingerValue });
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return 0f;
        }

        private Vector3 GetHandForwardDirection(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return Vector3.forward;

            try
            {
                // Try to get PointerPose forward
                var handType = hand.GetType();
                var pointerPoseProperty = handType.GetProperty("PointerPose");
                if (pointerPoseProperty != null)
                {
                    var pointerPose = pointerPoseProperty.GetValue(hand);
                    if (pointerPose != null)
                    {
                        var poseType = pointerPose.GetType();
                        var forwardProperty = poseType.GetProperty("forward");
                        if (forwardProperty != null)
                        {
                            return (Vector3)forwardProperty.GetValue(pointerPose);
                        }
                    }
                }

                // Fallback to transform forward
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

        private Vector3 GetPointingPosition(object hand)
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

        private void TriggerPointDetected(Vector3 position, Vector3 direction, float confidence, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.PointToSelect,
                position,
                direction,
                confidence,
                1f,
                isLeft
            );

            currentlyDetected = true;
            currentConfidence = confidence;
            currentPosition = position;
            currentDirection = direction;
            isLeftHandActive = isLeft;

            if (showDebugInfo)
            {
                Debug.Log($"[PointDetector] Point detected - {(isLeft ? "Left" : "Right")} hand at {position}");
            }

            OnGestureDetected?.Invoke(gestureData);
        }

        private void UpdateCurrentPointing(Vector3 position, Vector3 direction, float confidence, bool isLeft)
        {
            currentConfidence = confidence;
            currentPosition = position;
            currentDirection = direction;
            isLeftHandActive = isLeft;
        }

        private void TriggerPointReleased(bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.PointToSelect,
                currentPosition,
                currentDirection,
                0f,
                0f,
                isLeft
            );

            if (showDebugInfo)
            {
                Debug.Log($"[PointDetector] Point released - {(isLeft ? "Left" : "Right")} hand");
            }

            OnGestureReleased?.Invoke(gestureData);
            currentlyDetected = false;
            currentConfidence = 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled || !drawPointingRay || !isMetaXRAvailable) return;

            // Draw pointing rays
            if (leftHand != null && IsHandTracked(leftHand) && leftWasPointing)
            {
                Vector3 startPos = GetPointingPosition(leftHand);
                Vector3 direction = GetHandForwardDirection(leftHand);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(startPos, direction * maxPointingDistance);
                Gizmos.DrawWireSphere(startPos, 0.02f);
            }

            if (rightHand != null && IsHandTracked(rightHand) && rightWasPointing)
            {
                Vector3 startPos = GetPointingPosition(rightHand);
                Vector3 direction = GetHandForwardDirection(rightHand);
                
                Gizmos.color = Color.red;
                Gizmos.DrawRay(startPos, direction * maxPointingDistance);
                Gizmos.DrawWireSphere(startPos, 0.02f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Point Detection")]
        private void TestPointDetection()
        {
            var testData = new GestureData(GestureType.PointToSelect, transform.position, transform.forward);
            OnGestureDetected?.Invoke(testData);
        }
#endif
    }
} 