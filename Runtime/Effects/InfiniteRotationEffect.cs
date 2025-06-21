using UnityEngine;

namespace OOJUPlugin
{
    public class InfiniteRotationEffect : IInteractionEffect
    {
        public InteractionType Type => InteractionType.InfiniteRotation;
        public bool IsActive { get; private set; }

        [Header("Rotation Settings")]
        private float rotationSpeed = 90f; // degrees per second
        private Vector3 rotationAxis = Vector3.up;
        private bool randomizeAxis = false;
        private bool accelerateOverTime = false;
        private float maxSpeed = 360f;
        private float acceleration = 30f;

        [Header("Visual Effects")]
        private bool addParticleEffect = false;
        private bool addTrailEffect = false;
        private bool scaleWhileRotating = false;
        private float scaleMultiplier = 1.2f;

        // Internal state
        private float currentSpeed;
        private Vector3 originalScale;
        private float rotationStartTime;
        private bool isFirstFrame = true;

        public void Execute(GameObject target, GestureData gestureData)
        {
            if (target == null) return;

            IsActive = true;
            currentSpeed = rotationSpeed;
            rotationStartTime = Time.time;
            originalScale = target.transform.localScale;
            isFirstFrame = true;

            // Randomize rotation axis if enabled
            if (randomizeAxis)
            {
                rotationAxis = Random.insideUnitSphere.normalized;
            }
            else
            {
                // Use gesture direction to determine rotation axis
                Vector3 gestureDirection = gestureData.direction.normalized;
                rotationAxis = Vector3.Cross(gestureDirection, Vector3.up);
                if (rotationAxis.magnitude < 0.1f)
                {
                    rotationAxis = Vector3.Cross(gestureDirection, Vector3.forward);
                }
                rotationAxis = rotationAxis.normalized;
            }

            // Add visual effects
            AddVisualEffects(target);

            Debug.Log($"[InfiniteRotationEffect] Started infinite rotation for {target.name} on axis {rotationAxis}");
        }

        public void Update(GameObject target, GestureData gestureData)
        {
            if (!IsActive || target == null) return;

            // Handle acceleration
            if (accelerateOverTime)
            {
                float timeSinceStart = Time.time - rotationStartTime;
                currentSpeed = Mathf.Min(rotationSpeed + (acceleration * timeSinceStart), maxSpeed);
            }

            // Apply rotation
            float rotationAmount = currentSpeed * Time.deltaTime;
            target.transform.Rotate(rotationAxis, rotationAmount, Space.World);

            // Apply scaling effect
            if (scaleWhileRotating)
            {
                float scaleOscillation = 1f + Mathf.Sin(Time.time * 5f) * (scaleMultiplier - 1f) * 0.5f;
                target.transform.localScale = originalScale * scaleOscillation;
            }

            // Visual feedback - glow effect simulation
            ApplyVisualFeedback(target);
        }

        public void Stop(GameObject target)
        {
            if (!IsActive || target == null) return;

            IsActive = false;

            // Restore original scale
            target.transform.localScale = originalScale;

            // Remove visual effects
            RemoveVisualEffects(target);

            Debug.Log($"[InfiniteRotationEffect] Stopped infinite rotation for {target.name}");
        }

        private void AddVisualEffects(GameObject target)
        {
            // Add a simple color change for visual feedback
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Create a new material instance to avoid modifying the original
                var material = renderer.material;
                if (material.HasProperty("_Color"))
                {
                    Color originalColor = material.color;
                    Color rotatingColor = Color.Lerp(originalColor, Color.cyan, 0.3f);
                    material.color = rotatingColor;
                }
            }

            // Add a subtle emission effect
            if (renderer != null && renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.SetColor("_EmissionColor", Color.cyan * 0.2f);
                renderer.material.EnableKeyword("_EMISSION");
            }
        }

        private void RemoveVisualEffects(GameObject target)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Reset material properties
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", Color.black);
                    renderer.material.DisableKeyword("_EMISSION");
                }
            }
        }

        private void ApplyVisualFeedback(GameObject target)
        {
            // Create a pulsing glow effect based on rotation speed
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material.HasProperty("_EmissionColor"))
            {
                float speedRatio = currentSpeed / maxSpeed;
                float glowIntensity = Mathf.Sin(Time.time * 10f) * 0.1f + speedRatio * 0.3f;
                Color glowColor = Color.cyan * glowIntensity;
                renderer.material.SetColor("_EmissionColor", glowColor);
            }
        }

        // Configuration methods for script generation
        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = Mathf.Clamp(speed, 10f, 720f);
        }

        public void SetRotationAxis(Vector3 axis)
        {
            rotationAxis = axis.normalized;
        }

        public void SetRandomizeAxis(bool randomize)
        {
            randomizeAxis = randomize;
        }

        public void SetAcceleration(bool accelerate, float accel = 30f, float maxSpd = 360f)
        {
            accelerateOverTime = accelerate;
            acceleration = accel;
            maxSpeed = maxSpd;
        }

        public void SetScaling(bool enableScaling, float multiplier = 1.2f)
        {
            scaleWhileRotating = enableScaling;
            scaleMultiplier = multiplier;
        }

        public void SetVisualEffects(bool particles, bool trails)
        {
            addParticleEffect = particles;
            addTrailEffect = trails;
        }
    }
} 