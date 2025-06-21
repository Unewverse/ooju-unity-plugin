using UnityEngine;

namespace OOJUPlugin
{
    public class HeartbeatEffect : IInteractionEffect
    {
        public InteractionType Type => InteractionType.Heartbeat;
        public bool IsActive { get; private set; }

        [Header("Heartbeat Settings")]
        private float heartbeatRate = 80f; // BPM (beats per minute)
        private float pulseIntensity = 0.2f;
        private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private bool syncWithGestureIntensity = true;
        private bool useDoublebeat = true; // Like real heartbeat (lub-dub)

        [Header("Visual Effects")]
        private bool addColorPulse = true;
        private Color heartbeatColor = Color.red;
        private bool addEmissionPulse = true;
        private float emissionIntensity = 0.3f;

        [Header("Physics Effects")]
        private bool addPhysicalPulse = false;
        private float physicalPulseForce = 2f;

        // Internal state
        private Vector3 originalScale;
        private Color originalColor;
        private Color originalEmissionColor;
        private bool hasEmission;
        private MeshRenderer targetRenderer;
        private Rigidbody targetRigidbody;
        private float heartbeatStartTime;
        private float lastBeatTime;
        private bool isFirstBeat = true;
        private GameObject currentTarget;

        // Timer-based restoration
        private float colorRestoreTime = -1f;
        private float emissionRestoreTime = -1f;
        private float scaleRestoreTime = -1f;
        private float secondBeatTime = -1f;

        public void Execute(GameObject target, GestureData gestureData)
        {
            if (target == null) return;

            IsActive = true;
            currentTarget = target;
            heartbeatStartTime = Time.time;
            lastBeatTime = Time.time;
            originalScale = target.transform.localScale;
            isFirstBeat = true;

            // Setup components
            SetupRenderer(target);
            SetupPhysics(target);

            // Adjust heartbeat rate based on gesture intensity
            if (syncWithGestureIntensity)
            {
                float intensityMultiplier = Mathf.Clamp(gestureData.intensity, 0.5f, 2f);
                heartbeatRate *= intensityMultiplier;
            }

            Debug.Log($"[HeartbeatEffect] Started heartbeat for {target.name} at {heartbeatRate:F0} BPM");
        }

        public void Update(GameObject target, GestureData gestureData)
        {
            if (!IsActive || target == null) return;

            float currentTime = Time.time;
            float timeSinceStart = currentTime - heartbeatStartTime;

            // Handle timer-based restorations
            HandleTimerRestorations(currentTime);

            // Calculate heartbeat timing
            float beatsPerSecond = heartbeatRate / 60f;
            float beatInterval = 1f / beatsPerSecond;

            // Check if it's time for a beat
            if (currentTime - lastBeatTime >= beatInterval)
            {
                TriggerHeartbeat(target, gestureData);
                lastBeatTime = currentTime;
            }

            // Apply continuous effects
            ApplyContinuousEffects(target, gestureData, timeSinceStart);
        }

        private void HandleTimerRestorations(float currentTime)
        {
            // Handle color restoration
            if (colorRestoreTime > 0 && currentTime >= colorRestoreTime)
            {
                RestoreColor();
                colorRestoreTime = -1f;
            }

            // Handle emission restoration
            if (emissionRestoreTime > 0 && currentTime >= emissionRestoreTime)
            {
                RestoreEmission();
                emissionRestoreTime = -1f;
            }

            // Handle scale restoration
            if (scaleRestoreTime > 0 && currentTime >= scaleRestoreTime)
            {
                RestoreScale();
                scaleRestoreTime = -1f;
            }

            // Handle second beat
            if (secondBeatTime > 0 && currentTime >= secondBeatTime)
            {
                PerformSecondBeat();
                secondBeatTime = -1f;
            }
        }

        public void Stop(GameObject target)
        {
            if (!IsActive || target == null) return;

            IsActive = false;

            // Restore original appearance
            RestoreOriginalAppearance(target);

            // Clear timers
            colorRestoreTime = -1f;
            emissionRestoreTime = -1f;
            scaleRestoreTime = -1f;
            secondBeatTime = -1f;

            Debug.Log($"[HeartbeatEffect] Stopped heartbeat for {target.name}");
        }

        private void SetupRenderer(GameObject target)
        {
            targetRenderer = target.GetComponent<MeshRenderer>();
            if (targetRenderer == null) return;

            // Store original properties
            if (targetRenderer.material.HasProperty("_Color"))
            {
                originalColor = targetRenderer.material.color;
            }

            if (targetRenderer.material.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = targetRenderer.material.GetColor("_EmissionColor");
                hasEmission = true;
                targetRenderer.material.EnableKeyword("_EMISSION");
            }
        }

        private void SetupPhysics(GameObject target)
        {
            if (addPhysicalPulse)
            {
                targetRigidbody = target.GetComponent<Rigidbody>();
            }
        }

        private void TriggerHeartbeat(GameObject target, GestureData gestureData)
        {
            if (useDoublebeat)
            {
                // Create double-beat pattern (lub-dub)
                if (isFirstBeat)
                {
                    PerformBeat(target, gestureData, 1f); // Strong beat (lub)
                    
                    // Schedule second beat shortly after
                    secondBeatTime = Time.time + 0.15f;
                }
                isFirstBeat = !isFirstBeat;
            }
            else
            {
                PerformBeat(target, gestureData, 1f);
            }
        }

        private void PerformSecondBeat()
        {
            if (currentTarget != null)
            {
                var weakGestureData = new GestureData(GestureType.Wave, Vector3.zero, Vector3.zero, 0.5f, 0.5f, false);
                PerformBeat(currentTarget, weakGestureData, 0.6f); // Weaker second beat (dub)
            }
        }

        private void PerformBeat(GameObject target, GestureData gestureData, float beatIntensity)
        {
            // Scale pulse
            StartScalePulse(target, beatIntensity);

            // Color pulse
            if (addColorPulse)
            {
                StartColorPulse(target, beatIntensity);
            }

            // Emission pulse
            if (addEmissionPulse && hasEmission)
            {
                StartEmissionPulse(target, beatIntensity);
            }

            // Physical pulse
            if (addPhysicalPulse && targetRigidbody != null)
            {
                ApplyPhysicalPulse(target, gestureData, beatIntensity);
            }
        }

        private void StartScalePulse(GameObject target, float beatIntensity)
        {
            float pulseAmount = pulseIntensity * beatIntensity;
            Vector3 targetScale = originalScale * (1f + pulseAmount);

            // Apply scale immediately
            target.transform.localScale = targetScale;
            
            // Schedule scale restore
            scaleRestoreTime = Time.time + 0.3f;
        }

        private void StartColorPulse(GameObject target, float beatIntensity)
        {
            if (targetRenderer == null) return;

            Color pulseColor = Color.Lerp(originalColor, heartbeatColor, pulseIntensity * beatIntensity);
            
            // Apply color immediately
            targetRenderer.material.color = pulseColor;
            
            // Schedule color restore
            colorRestoreTime = Time.time + 0.3f;
        }

        private void StartEmissionPulse(GameObject target, float beatIntensity)
        {
            if (targetRenderer == null) return;

            Color emissionColor = heartbeatColor * emissionIntensity * beatIntensity;
            
            // Apply emission immediately
            if (targetRenderer.material.HasProperty("_EmissionColor"))
            {
                targetRenderer.material.SetColor("_EmissionColor", emissionColor);
                
                // Schedule emission restore
                emissionRestoreTime = Time.time + 0.3f;
            }
        }

        private void RestoreColor()
        {
            if (targetRenderer != null)
                targetRenderer.material.color = originalColor;
        }

        private void RestoreEmission()
        {
            if (targetRenderer != null && targetRenderer.material.HasProperty("_EmissionColor"))
                targetRenderer.material.SetColor("_EmissionColor", originalEmissionColor);
        }

        private void RestoreScale()
        {
            if (currentTarget != null)
                currentTarget.transform.localScale = originalScale;
        }

        private void ApplyPhysicalPulse(GameObject target, GestureData gestureData, float beatIntensity)
        {
            if (targetRigidbody == null) return;

            // Apply outward force in all directions (like a heartbeat)
            Vector3 pulseForce = Vector3.up * physicalPulseForce * beatIntensity;
            targetRigidbody.AddForce(pulseForce, ForceMode.Impulse);

            // Add some randomness for more organic feel
            Vector3 randomForce = Random.insideUnitSphere * physicalPulseForce * 0.3f * beatIntensity;
            targetRigidbody.AddForce(randomForce, ForceMode.Impulse);
        }

        private void ApplyContinuousEffects(GameObject target, GestureData gestureData, float timeSinceStart)
        {
            // Subtle continuous effects between beats
            if (targetRenderer != null && addColorPulse)
            {
                // Very subtle color breathing
                float breathingIntensity = Mathf.Sin(timeSinceStart * 2f) * 0.05f;
                Color breathingColor = Color.Lerp(originalColor, heartbeatColor, breathingIntensity);
                // Apply only if not currently pulsing
            }
        }

        private void RestoreOriginalAppearance(GameObject target)
        {
            // Restore scale
            target.transform.localScale = originalScale;

            // Restore colors
            if (targetRenderer != null)
            {
                targetRenderer.material.color = originalColor;
                
                if (hasEmission)
                {
                    targetRenderer.material.SetColor("_EmissionColor", originalEmissionColor);
                }
            }
        }

        // Configuration methods for script generation
        public void SetHeartbeatRate(float bpm)
        {
            heartbeatRate = Mathf.Clamp(bpm, 30f, 200f);
        }

        public void SetPulseIntensity(float intensity)
        {
            pulseIntensity = Mathf.Clamp01(intensity);
        }

        public void SetHeartbeatColor(Color color)
        {
            heartbeatColor = color;
        }

        public void SetSyncWithGesture(bool sync)
        {
            syncWithGestureIntensity = sync;
        }

        public void SetDoublebeat(bool enable)
        {
            useDoublebeat = enable;
        }

        public void SetVisualEffects(bool colorPulse, bool emissionPulse, float emission = 0.3f)
        {
            addColorPulse = colorPulse;
            addEmissionPulse = emissionPulse;
            emissionIntensity = emission;
        }

        public void SetPhysicalEffects(bool enable, float force = 2f)
        {
            addPhysicalPulse = enable;
            physicalPulseForce = force;
        }
    }
} 