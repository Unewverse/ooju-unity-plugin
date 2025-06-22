using UnityEngine;

namespace OOJUPlugin
{
    public class OpenPalmGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Open Palm Detection Settings")]
        [SerializeField] private float fingerExtensionThreshold = 0.7f;
        [SerializeField] private int requiredExtendedFingers = 4; // All except thumb
        [SerializeField] private float palmFacingThreshold = 0.6f; // Dot product threshold
        [SerializeField] private float detectionStabilityTime = 0.15f;
        [SerializeField] private float handMovementThreshold = 0.5f; // For push detection
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool drawPalmDirection = true;

        private object leftHand;
        private object rightHand;
        private bool isMetaXRAvailable = false;
        private float leftPalmStartTime;
        private float rightPalmStartTime;
        private bool leftWasPalmOpen = false;
        private bool rightWasPalmOpen = false;
        private Vector3 lastLeftHandPos;
        private Vector3 lastRightHandPos;
        private bool currentlyDetected = false;
        private float currentConfidence = 0f;
        private Vector3 currentPosition = Vector3.zero;
        private Vector3 currentDirection = Vector3.forward;
        private bool isLeftHandActive = false;

        public GestureType Type => GestureType.OpenPalm;
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
                Debug.LogWarning("[OpenPalmGestureDetector] Meta XR SDK not available. Open palm detection disabled.");
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
                Debug.LogWarning("[OpenPalmGestureDetector] No OVR Hands found for open palm detection");
            }

            // Initialize positions
            if (leftHand != null) lastLeftHandPos = GetHandPosition(leftHand);
            if (rightHand != null) lastRightHandPos = GetHandPosition(rightHand);
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

            // Check left hand
            if (leftHand != null && IsHandTracked(leftHand))
            {
                CheckHandForOpenPalm(leftHand, ref leftPalmStartTime, ref leftWasPalmOpen, ref lastLeftHandPos, true);
            }

            // Check right hand
            if (rightHand != null && IsHandTracked(rightHand))
            {
                CheckHandForOpenPalm(rightHand, ref rightPalmStartTime, ref rightWasPalmOpen, ref lastRightHandPos, false);
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

        private Vector3 GetHandPosition(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return Vector3.zero;

            try
            {
                // Use hand transform position
                var handType = hand.GetType();
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

        private void CheckHandForOpenPalm(object hand, ref float palmStartTime, ref bool wasPalmOpen, ref Vector3 lastPos, bool isLeft)
        {
            bool isPalmOpenNow = IsHandOpenPalm(hand, out float confidence, out Vector3 palmDirection, out float pushIntensity);
            Vector3 currentPos = GetHandPosition(hand);

            if (isPalmOpenNow && !wasPalmOpen)
            {
                // Started open palm
                palmStartTime = Time.time;
                wasPalmOpen = true;

                if (showDebugInfo)
                {
                    Debug.Log($"[OpenPalmDetector] Started open palm - {(isLeft ? "Left" : "Right")} hand");
                }
            }
            else if (!isPalmOpenNow && wasPalmOpen)
            {
                // Stopped open palm
                wasPalmOpen = false;
                
                if (currentlyDetected && isLeftHandActive == isLeft)
                {
                    TriggerOpenPalmReleased(isLeft);
                }

                if (showDebugInfo)
                {
                    Debug.Log($"[OpenPalmDetector] Stopped open palm - {(isLeft ? "Left" : "Right")} hand");
                }
            }
            else if (isPalmOpenNow && wasPalmOpen)
            {
                // Continue open palm - check stability and movement
                float palmDuration = Time.time - palmStartTime;
                
                if (palmDuration >= detectionStabilityTime)
                {
                    Vector3 palmPosition = GetHandPosition(hand);
                    
                    if (!currentlyDetected || isLeftHandActive != isLeft)
                    {
                        TriggerOpenPalmDetected(palmPosition, palmDirection, confidence, pushIntensity, isLeft);
                    }
                    else
                    {
                        // Update current open palm
                        UpdateCurrentOpenPalm(palmPosition, palmDirection, confidence, pushIntensity, isLeft);
                    }
                }
            }

            lastPos = currentPos;
        }

        private bool IsHandOpenPalm(object hand, out float confidence, out Vector3 palmDirection, out float pushIntensity)
        {
            confidence = 0f;
            palmDirection = Vector3.forward;
            pushIntensity = 0f;

            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return false;

            try
            {
                // Get finger extension states
                float[] fingerExtensions = new float[4]; // Index, Middle, Ring, Pinky
                fingerExtensions[0] = 1f - GetFingerPinchStrength(hand, "Index");
                fingerExtensions[1] = 1f - GetFingerPinchStrength(hand, "Middle");
                fingerExtensions[2] = 1f - GetFingerPinchStrength(hand, "Ring");
                fingerExtensions[3] = 1f - GetFingerPinchStrength(hand, "Pinky");

                // Count extended fingers
                int extendedFingerCount = 0;
                float averageExtension = 0f;

                for (int i = 0; i < fingerExtensions.Length; i++)
                {
                    averageExtension += fingerExtensions[i];
                    if (fingerExtensions[i] > fingerExtensionThreshold)
                    {
                        extendedFingerCount++;
                    }
                }
                averageExtension /= fingerExtensions.Length;

                // Check if enough fingers are extended
                bool hasExtendedFingers = extendedFingerCount >= requiredExtendedFingers;

                // Get palm direction (should face outward)
                palmDirection = GetPalmDirection(hand);
                
                // Check if palm is facing forward/outward
                Vector3 handForward = GetHandForwardDirection(hand);
                float palmFacingFactor = Vector3.Dot(palmDirection, handForward);
                bool isPalmFacingOutward = palmFacingFactor > palmFacingThreshold;

                // Calculate push intensity based on hand movement
                Vector3 handVelocity = (GetHandPosition(hand) - (isLeftHandActive ? lastLeftHandPos : lastRightHandPos)) / Time.deltaTime;
                pushIntensity = Mathf.Clamp01(handVelocity.magnitude / handMovementThreshold);

                if (hasExtendedFingers && isPalmFacingOutward)
                {
                    confidence = averageExtension * palmFacingFactor;
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
                // Use hand transform forward
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

        private Vector3 GetPalmDirection(object hand)
        {
            if (!isMetaXRAvailable || hand == null || !IsHandTracked(hand)) return Vector3.forward;

            try
            {
                // Use hand's up direction as palm normal (palm faces in the direction of hand.up)
                var handType = hand.GetType();
                var transformProperty = handType.GetProperty("transform");
                if (transformProperty != null)
                {
                    var transform = (Transform)transformProperty.GetValue(hand);
                    return transform.up;
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return Vector3.forward;
        }

        private void TriggerOpenPalmDetected(Vector3 position, Vector3 direction, float confidence, float pushIntensity, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.OpenPalm,
                position,
                direction,
                confidence,
                pushIntensity,
                isLeft
            );

            currentlyDetected = true;
            currentConfidence = confidence;
            currentPosition = position;
            currentDirection = direction;
            isLeftHandActive = isLeft;

            if (showDebugInfo)
            {
                Debug.Log($"[OpenPalmDetector] Open palm detected - {(isLeft ? "Left" : "Right")} hand at {position} (push: {pushIntensity:F2})");
            }

            OnGestureDetected?.Invoke(gestureData);
        }

        private void UpdateCurrentOpenPalm(Vector3 position, Vector3 direction, float confidence, float pushIntensity, bool isLeft)
        {
            currentConfidence = confidence;
            currentPosition = position;
            currentDirection = direction;
            isLeftHandActive = isLeft;

            // Create updated gesture data for continuous effects
            var gestureData = new GestureData(
                GestureType.OpenPalm,
                position,
                direction,
                confidence,
                pushIntensity,
                isLeft
            );
        }

        private void TriggerOpenPalmReleased(bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.OpenPalm,
                currentPosition,
                currentDirection,
                0f,
                0f,
                isLeft
            );

            if (showDebugInfo)
            {
                Debug.Log($"[OpenPalmDetector] Open palm released - {(isLeft ? "Left" : "Right")} hand");
            }

            OnGestureReleased?.Invoke(gestureData);
            currentlyDetected = false;
            currentConfidence = 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled || !drawPalmDirection || !isMetaXRAvailable) return;

            // Draw palm directions
            if (leftHand != null && IsHandTracked(leftHand) && leftWasPalmOpen)
            {
                Vector3 palmPos = GetHandPosition(leftHand);
                Vector3 palmDir = GetPalmDirection(leftHand);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(palmPos, palmDir * 0.2f);
                Gizmos.DrawWireSphere(palmPos, 0.03f);
            }

            if (rightHand != null && IsHandTracked(rightHand) && rightWasPalmOpen)
            {
                Vector3 palmPos = GetHandPosition(rightHand);
                Vector3 palmDir = GetPalmDirection(rightHand);
                
                Gizmos.color = Color.red;
                Gizmos.DrawRay(palmPos, palmDir * 0.2f);
                Gizmos.DrawWireSphere(palmPos, 0.03f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Open Palm Detection")]
        private void TestOpenPalmDetection()
        {
            var testData = new GestureData(GestureType.OpenPalm, transform.position, transform.up);
            OnGestureDetected?.Invoke(testData);
        }
#endif
    }
} 