using UnityEngine;
using System.Collections.Generic;

namespace OOJUPlugin
{
    public class WaveGestureDetector : MonoBehaviour, IGestureDetector
    {
        [Header("Wave Detection Settings")]
        [SerializeField] private float waveAmplitudeThreshold = 0.1f;
        [SerializeField] private float waveFrequencyMin = 1f; // Hz
        [SerializeField] private float waveFrequencyMax = 4f; // Hz
        [SerializeField] private int requiredOscillations = 2;
        [SerializeField] private float detectionWindow = 2f; // seconds
        [SerializeField] private float continuousWaveTime = 0.5f; // Time to consider continuous waving
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool drawWavePattern = true;

        private object leftHand;
        private object rightHand;
        private bool isMetaXRAvailable = false;
        
        // Wave detection data
        private Queue<Vector3> leftHandPositions = new Queue<Vector3>();
        private Queue<Vector3> rightHandPositions = new Queue<Vector3>();
        private Queue<float> leftHandTimes = new Queue<float>();
        private Queue<float> rightHandTimes = new Queue<float>();
        
        private bool leftWaveDetected = false;
        private bool rightWaveDetected = false;
        private float leftWaveStartTime;
        private float rightWaveStartTime;
        
        private bool currentlyDetected = false;
        private float currentConfidence = 0f;
        private Vector3 currentPosition = Vector3.zero;
        private Vector3 currentDirection = Vector3.forward;
        private bool isLeftHandActive = false;

        public GestureType Type => GestureType.Wave;
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
                Debug.LogWarning("[WaveGestureDetector] Meta XR SDK not available. Wave detection disabled.");
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
                Debug.LogWarning("[WaveGestureDetector] No OVR Hands found for wave detection");
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

            float currentTime = Time.time;

            // Track left hand
            if (leftHand != null && IsHandTracked(leftHand))
            {
                TrackHandMovement(leftHand, leftHandPositions, leftHandTimes, currentTime);
                CheckForWave(leftHandPositions, leftHandTimes, ref leftWaveDetected, ref leftWaveStartTime, true);
            }

            // Track right hand
            if (rightHand != null && IsHandTracked(rightHand))
            {
                TrackHandMovement(rightHand, rightHandPositions, rightHandTimes, currentTime);
                CheckForWave(rightHandPositions, rightHandTimes, ref rightWaveDetected, ref rightWaveStartTime, false);
            }

            // Clean old data
            CleanOldData(leftHandPositions, leftHandTimes, currentTime);
            CleanOldData(rightHandPositions, rightHandTimes, currentTime);
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

        private void TrackHandMovement(object hand, Queue<Vector3> positions, Queue<float> times, float currentTime)
        {
            Vector3 handPos = GetHandPosition(hand);
            
            // Add current position
            positions.Enqueue(handPos);
            times.Enqueue(currentTime);

            // Keep only recent data
            while (times.Count > 0 && currentTime - times.Peek() > detectionWindow)
            {
                positions.Dequeue();
                times.Dequeue();
            }
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

        private void CheckForWave(Queue<Vector3> positions, Queue<float> times, ref bool waveDetected, ref float waveStartTime, bool isLeft)
        {
            if (positions.Count < 10) return; // Need enough data points

            // Convert to arrays for analysis
            Vector3[] posArray = positions.ToArray();
            float[] timeArray = times.ToArray();

            // Analyze wave pattern
            var waveData = AnalyzeWavePattern(posArray, timeArray);

            bool isWaving = waveData.isWaving;
            float confidence = waveData.confidence;
            Vector3 waveCenter = waveData.center;
            Vector3 waveDirection = waveData.direction;

            if (isWaving && !waveDetected)
            {
                // Started waving
                waveDetected = true;
                waveStartTime = Time.time;
                TriggerWaveDetected(waveCenter, waveDirection, confidence, isLeft);
            }
            else if (!isWaving && waveDetected)
            {
                // Stopped waving
                waveDetected = false;
                
                if (currentlyDetected && isLeftHandActive == isLeft)
                {
                    TriggerWaveReleased(isLeft);
                }
            }
            else if (isWaving && waveDetected)
            {
                // Continue waving
                UpdateCurrentWave(waveCenter, waveDirection, confidence, isLeft);
            }
        }

        private (bool isWaving, float confidence, Vector3 center, Vector3 direction) AnalyzeWavePattern(Vector3[] positions, float[] times)
        {
            if (positions.Length < 10) return (false, 0f, Vector3.zero, Vector3.forward);

            // Calculate center point
            Vector3 center = Vector3.zero;
            for (int i = 0; i < positions.Length; i++)
            {
                center += positions[i];
            }
            center /= positions.Length;

            // Analyze oscillation in horizontal plane (ignore Y for now)
            List<float> horizontalDistances = new List<float>();
            List<float> verticalDistances = new List<float>();
            
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 offset = positions[i] - center;
                horizontalDistances.Add(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude);
                verticalDistances.Add(Mathf.Abs(offset.y));
            }

            // Find peaks and valleys to determine oscillation
            var horizontalPeaks = FindPeaks(horizontalDistances.ToArray());
            var verticalPeaks = FindPeaks(verticalDistances.ToArray());

            // Determine dominant wave direction
            Vector3 waveDirection = Vector3.forward;
            bool isWaving = false;
            float confidence = 0f;

            // Check horizontal waving (more common)
            if (horizontalPeaks.Count >= requiredOscillations)
            {
                float averageAmplitude = 0f;
                foreach (int peakIndex in horizontalPeaks)
                {
                    averageAmplitude += horizontalDistances[peakIndex];
                }
                averageAmplitude /= horizontalPeaks.Count;

                if (averageAmplitude > waveAmplitudeThreshold)
                {
                    isWaving = true;
                    confidence = Mathf.Clamp01(averageAmplitude / (waveAmplitudeThreshold * 3f));
                    
                    // Calculate wave direction based on movement pattern
                    if (positions.Length >= 2)
                    {
                        Vector3 recentMovement = positions[positions.Length - 1] - positions[positions.Length - 2];
                        waveDirection = Vector3.ProjectOnPlane(recentMovement, Vector3.up).normalized;
                    }
                }
            }

            // Check vertical waving (less common but possible)
            if (!isWaving && verticalPeaks.Count >= requiredOscillations)
            {
                float averageAmplitude = 0f;
                foreach (int peakIndex in verticalPeaks)
                {
                    averageAmplitude += verticalDistances[peakIndex];
                }
                averageAmplitude /= verticalPeaks.Count;

                if (averageAmplitude > waveAmplitudeThreshold)
                {
                    isWaving = true;
                    confidence = Mathf.Clamp01(averageAmplitude / (waveAmplitudeThreshold * 3f));
                    waveDirection = Vector3.up;
                }
            }

            return (isWaving, confidence, center, waveDirection);
        }

        private List<int> FindPeaks(float[] data)
        {
            List<int> peaks = new List<int>();
            
            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] > data[i - 1] && data[i] > data[i + 1])
                {
                    peaks.Add(i);
                }
            }
            
            return peaks;
        }

        private void CleanOldData(Queue<Vector3> positions, Queue<float> times, float currentTime)
        {
            while (times.Count > 0 && currentTime - times.Peek() > detectionWindow)
            {
                positions.Dequeue();
                times.Dequeue();
            }
        }

        private void TriggerWaveDetected(Vector3 position, Vector3 direction, float confidence, bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.Wave,
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
                Debug.Log($"[WaveDetector] Wave detected - {(isLeft ? "Left" : "Right")} hand at {position}");
            }

            OnGestureDetected?.Invoke(gestureData);
        }

        private void UpdateCurrentWave(Vector3 position, Vector3 direction, float confidence, bool isLeft)
        {
            currentConfidence = confidence;
            currentPosition = position;
            currentDirection = direction;
            isLeftHandActive = isLeft;
        }

        private void TriggerWaveReleased(bool isLeft)
        {
            var gestureData = new GestureData(
                GestureType.Wave,
                currentPosition,
                currentDirection,
                0f,
                0f,
                isLeft
            );

            if (showDebugInfo)
            {
                Debug.Log($"[WaveDetector] Wave released - {(isLeft ? "Left" : "Right")} hand");
            }

            OnGestureReleased?.Invoke(gestureData);
            currentlyDetected = false;
            currentConfidence = 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled || !drawWavePattern || !isMetaXRAvailable) return;

            // Draw wave patterns
            DrawWavePattern(leftHandPositions, Color.blue);
            DrawWavePattern(rightHandPositions, Color.red);
        }

        private void DrawWavePattern(Queue<Vector3> positions, Color color)
        {
            if (positions.Count < 2) return;

            Vector3[] posArray = positions.ToArray();
            Gizmos.color = color;

            for (int i = 0; i < posArray.Length - 1; i++)
            {
                Gizmos.DrawLine(posArray[i], posArray[i + 1]);
            }

            // Draw current position
            if (posArray.Length > 0)
            {
                Gizmos.DrawWireSphere(posArray[posArray.Length - 1], 0.02f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Wave Detection")]
        private void TestWaveDetection()
        {
            var testData = new GestureData(GestureType.Wave, transform.position, transform.right);
            OnGestureDetected?.Invoke(testData);
        }
#endif
    }
} 