using UnityEngine;

namespace OOJUPlugin
{
    public class PushAwayEffect : IInteractionEffect
    {
        public InteractionType Type => InteractionType.PushAway;
        public bool IsActive { get; private set; }

        [Header("Push Settings")]
        private float pushForce = 10f;
        private float maxPushDistance = 2f;
        private bool useDistanceBasedForce = true;
        private bool addTorque = true;
        private float torqueMultiplier = 5f;

        [Header("Physics")]
        private bool requireRigidbody = true;
        private bool addRigidbodyIfMissing = true;
        private float pushDuration = 0.5f;
        private bool continuousPush = false;

        [Header("Visual Effects")]
        private bool addShockwave = true;
        private bool addParticles = false;
        private Color pushEffectColor = Color.cyan;

        // Internal state
        private Vector3 originalPosition;
        private Rigidbody targetRigidbody;
        private bool addedRigidbody = false;
        private float pushStartTime;
        private Vector3 pushDirection;
        private float totalPushForce;
        private GameObject shockwaveEffect;
        private float shockwaveStartTime = -1f;
        private float shockwaveMaxScale = 2f;
        private float shockwaveExpansionTime = 0.5f;

        public void Execute(GameObject target, GestureData gestureData)
        {
            if (target == null) return;

            IsActive = true;
            pushStartTime = Time.time;
            originalPosition = target.transform.position;

            // Setup physics
            SetupPhysics(target);

            // Calculate push parameters
            CalculatePushParameters(target, gestureData);

            // Apply initial push
            ApplyPushForce(target, gestureData);

            // Add visual effects
            if (addShockwave)
            {
                CreateShockwaveEffect(target, gestureData);
            }

            Debug.Log($"[PushAwayEffect] Started push away for {target.name} with force {totalPushForce:F1}");
        }

        public void Update(GameObject target, GestureData gestureData)
        {
            if (!IsActive || target == null) return;

            // Handle continuous push
            if (continuousPush)
            {
                ApplyPushForce(target, gestureData);
            }

            // Update visual effects
            UpdateVisualEffects(target, gestureData);

            // Update shockwave animation
            UpdateShockwaveAnimation();

            // Check if push should end
            if (!continuousPush && Time.time > pushStartTime + pushDuration)
            {
                Stop(target);
            }
        }

        public void Stop(GameObject target)
        {
            if (!IsActive || target == null) return;

            IsActive = false;

            // Clean up visual effects
            CleanupVisualEffects();

            // Optionally remove added rigidbody
            if (addedRigidbody && targetRigidbody != null)
            {
                // Let it settle for a moment before removing
                Object.Destroy(targetRigidbody, 2f);
            }

            Debug.Log($"[PushAwayEffect] Stopped push away for {target.name}");
        }

        private void SetupPhysics(GameObject target)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();

            if (targetRigidbody == null)
            {
                if (addRigidbodyIfMissing)
                {
                    targetRigidbody = target.AddComponent<Rigidbody>();
                    addedRigidbody = true;
                    
                    // Set reasonable physics properties
                    targetRigidbody.mass = 1f;
                    targetRigidbody.linearDamping = 0.5f;
                    targetRigidbody.angularDamping = 0.5f;
                }
                else if (requireRigidbody)
                {
                    Debug.LogWarning($"[PushAwayEffect] {target.name} needs a Rigidbody for push effect");
                    return;
                }
            }

            // Ensure rigidbody is not kinematic for physics
            if (targetRigidbody != null)
            {
                targetRigidbody.isKinematic = false;
            }
        }

        private void CalculatePushParameters(GameObject target, GestureData gestureData)
        {
            // Calculate push direction (from hand to object)
            Vector3 handToObject = (target.transform.position - gestureData.position).normalized;
            
            // Use palm direction as additional influence
            Vector3 palmDirection = gestureData.direction.normalized;
            
            // Combine directions (weighted towards palm direction)
            pushDirection = Vector3.Lerp(handToObject, palmDirection, 0.7f).normalized;

            // Calculate distance-based force
            float distance = Vector3.Distance(gestureData.position, target.transform.position);
            float distanceMultiplier = useDistanceBasedForce ? 
                Mathf.Clamp01(1f - (distance / maxPushDistance)) : 1f;

            // Use gesture intensity (hand movement speed)
            float intensityMultiplier = Mathf.Clamp01(gestureData.intensity);

            // Calculate total force
            totalPushForce = pushForce * distanceMultiplier * intensityMultiplier;
        }

        private void ApplyPushForce(GameObject target, GestureData gestureData)
        {
            if (targetRigidbody == null) return;

            // Apply main push force
            Vector3 forceVector = pushDirection * totalPushForce;
            targetRigidbody.AddForce(forceVector, ForceMode.Impulse);

            // Add torque for more dynamic movement
            if (addTorque)
            {
                Vector3 torqueVector = Vector3.Cross(pushDirection, Vector3.up) * torqueMultiplier;
                targetRigidbody.AddTorque(torqueVector, ForceMode.Impulse);
            }

            // Apply force at a point for more realistic physics
            Vector3 forcePoint = target.transform.position + Vector3.up * 0.1f; // Slightly above center
            targetRigidbody.AddForceAtPosition(forceVector * 0.5f, forcePoint, ForceMode.Impulse);
        }

        private void CreateShockwaveEffect(GameObject target, GestureData gestureData)
        {
            // Create a simple shockwave effect
            shockwaveEffect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shockwaveEffect.name = $"{target.name}_Shockwave";
            
            // Position at hand location
            shockwaveEffect.transform.position = gestureData.position;
            shockwaveEffect.transform.localScale = Vector3.zero;

            // Setup material
            var renderer = shockwaveEffect.GetComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Transparent"));
            material.color = new Color(pushEffectColor.r, pushEffectColor.g, pushEffectColor.b, 0.3f);
            renderer.material = material;

            // Remove collider
            Object.Destroy(shockwaveEffect.GetComponent<Collider>());

            // Start expansion animation
            shockwaveStartTime = Time.time;
        }

        private void UpdateShockwaveAnimation()
        {
            if (shockwaveEffect == null || shockwaveStartTime < 0) return;

            float elapsedTime = Time.time - shockwaveStartTime;
            float progress = elapsedTime / shockwaveExpansionTime;

            if (progress <= 1f)
            {
                // Animate expansion
                float currentScale = Mathf.Lerp(0f, shockwaveMaxScale, progress);
                shockwaveEffect.transform.localScale = Vector3.one * currentScale;

                // Fade out
                var renderer = shockwaveEffect.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    float alpha = Mathf.Lerp(0.3f, 0f, progress);
                    Color color = renderer.material.color;
                    color.a = alpha;
                    renderer.material.color = color;
                }
            }
            else
            {
                // Animation complete, destroy shockwave
                Object.Destroy(shockwaveEffect);
                shockwaveEffect = null;
                shockwaveStartTime = -1f;
            }
        }

        private void UpdateVisualEffects(GameObject target, GestureData gestureData)
        {
            if (targetRigidbody == null) return;

            // Create trailing effect based on velocity
            float velocity = targetRigidbody.linearVelocity.magnitude;
            if (velocity > 1f)
            {
                // Add velocity-based visual feedback
                var renderer = target.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // Create a temporary glow effect
                    Color glowColor = pushEffectColor * Mathf.Clamp01(velocity / 5f);
                    if (renderer.material.HasProperty("_EmissionColor"))
                    {
                        renderer.material.SetColor("_EmissionColor", glowColor * 0.2f);
                        renderer.material.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private void CleanupVisualEffects()
        {
            // Clean up shockwave
            if (shockwaveEffect != null)
            {
                Object.Destroy(shockwaveEffect);
                shockwaveEffect = null;
            }
            shockwaveStartTime = -1f;
        }

        // Configuration methods for script generation
        public void SetPushForce(float force)
        {
            pushForce = Mathf.Max(0f, force);
        }

        public void SetMaxPushDistance(float distance)
        {
            maxPushDistance = Mathf.Max(0.1f, distance);
        }

        public void SetDistanceBasedForce(bool useDistance)
        {
            useDistanceBasedForce = useDistance;
        }

        public void SetTorqueEffect(bool enable, float multiplier = 5f)
        {
            addTorque = enable;
            torqueMultiplier = multiplier;
        }

        public void SetPhysicsHandling(bool requireRb, bool addIfMissing = true)
        {
            requireRigidbody = requireRb;
            addRigidbodyIfMissing = addIfMissing;
        }

        public void SetPushDuration(float duration, bool continuous = false)
        {
            pushDuration = duration;
            continuousPush = continuous;
        }

        public void SetVisualEffects(bool shockwave, bool particles, Color effectColor = default)
        {
            addShockwave = shockwave;
            addParticles = particles;
            if (effectColor != default) pushEffectColor = effectColor;
        }
    }
} 