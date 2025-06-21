using UnityEngine;

namespace OOJUPlugin
{
    public class HighlightEffect : IInteractionEffect
    {
        public InteractionType Type => InteractionType.Highlight;
        public bool IsActive { get; private set; }

        [Header("Highlight Settings")]
        private Color highlightColor = Color.yellow;
        private float highlightIntensity = 0.5f;
        private float pulseSpeed = 2f;
        private bool useDistanceBasedIntensity = true;
        private float maxHighlightDistance = 2f;

        [Header("Outline Effect")]
        private bool addOutlineEffect = true;
        private float outlineWidth = 0.02f;
        private Color outlineColor = Color.cyan;

        [Header("Scale Effect")]
        private bool scaleOnHighlight = true;
        private float scaleMultiplier = 1.1f;
        private float scaleSpeed = 3f;

        // Internal state
        private Material originalMaterial;
        private Material highlightMaterial;
        private Color originalColor;
        private Color originalEmissionColor;
        private Vector3 originalScale;
        private bool hasEmission;
        private MeshRenderer targetRenderer;
        private float highlightStartTime;

        public void Execute(GameObject target, GestureData gestureData)
        {
            if (target == null) return;

            IsActive = true;
            highlightStartTime = Time.time;
            originalScale = target.transform.localScale;

            // Setup materials and colors
            SetupHighlight(target);

            // Calculate distance-based intensity
            float distance = Vector3.Distance(gestureData.position, target.transform.position);
            float distanceIntensity = useDistanceBasedIntensity ? 
                Mathf.Clamp01(1f - (distance / maxHighlightDistance)) : 1f;

            Debug.Log($"[HighlightEffect] Started highlighting {target.name} (distance: {distance:F2}m, intensity: {distanceIntensity:F2})");
        }

        public void Update(GameObject target, GestureData gestureData)
        {
            if (!IsActive || target == null || targetRenderer == null) return;

            // Calculate distance-based intensity
            float distance = Vector3.Distance(gestureData.position, target.transform.position);
            float distanceIntensity = useDistanceBasedIntensity ? 
                Mathf.Clamp01(1f - (distance / maxHighlightDistance)) : 1f;

            // Create pulsing effect
            float timeOffset = Time.time - highlightStartTime;
            float pulseMultiplier = 0.5f + 0.5f * Mathf.Sin(timeOffset * pulseSpeed);
            float finalIntensity = highlightIntensity * distanceIntensity * pulseMultiplier;

            // Apply highlight color
            Color currentHighlight = Color.Lerp(originalColor, highlightColor, finalIntensity);
            targetRenderer.material.color = currentHighlight;

            // Apply emission if supported
            if (hasEmission)
            {
                Color emissionColor = highlightColor * finalIntensity * 0.3f;
                targetRenderer.material.SetColor("_EmissionColor", emissionColor);
            }

            // Apply scaling effect
            if (scaleOnHighlight)
            {
                float scaleValue = Mathf.Lerp(1f, scaleMultiplier, finalIntensity);
                Vector3 targetScale = originalScale * scaleValue;
                target.transform.localScale = Vector3.Lerp(target.transform.localScale, targetScale, scaleSpeed * Time.deltaTime);
            }

            // Show distance-based information (for debugging)
            if (distance <= maxHighlightDistance * 0.5f)
            {
                // Very close - add extra visual feedback
                ApplyCloseProximityEffect(target, finalIntensity);
            }
        }

        public void Stop(GameObject target)
        {
            if (!IsActive || target == null) return;

            IsActive = false;

            // Restore original appearance
            RestoreOriginalAppearance(target);

            Debug.Log($"[HighlightEffect] Stopped highlighting {target.name}");
        }

        private void SetupHighlight(GameObject target)
        {
            targetRenderer = target.GetComponent<MeshRenderer>();
            if (targetRenderer == null) return;

            // Store original material properties
            originalMaterial = targetRenderer.material;
            
            // Create a new material instance to avoid modifying the original
            highlightMaterial = new Material(originalMaterial);
            targetRenderer.material = highlightMaterial;

            // Store original colors
            if (highlightMaterial.HasProperty("_Color"))
            {
                originalColor = highlightMaterial.color;
            }

            if (highlightMaterial.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = highlightMaterial.GetColor("_EmissionColor");
                hasEmission = true;
                
                // Enable emission keyword
                highlightMaterial.EnableKeyword("_EMISSION");
            }

            // Add outline effect if requested
            if (addOutlineEffect)
            {
                AddOutlineEffect(target);
            }
        }

        private void AddOutlineEffect(GameObject target)
        {
            // Simple outline effect using a slightly larger duplicate
            GameObject outlineObject = new GameObject($"{target.name}_Outline");
            outlineObject.transform.SetParent(target.transform);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one * (1f + outlineWidth);

            // Copy mesh
            var targetMeshFilter = target.GetComponent<MeshFilter>();
            if (targetMeshFilter != null)
            {
                var outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
                outlineMeshFilter.mesh = targetMeshFilter.mesh;

                var outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
                
                // Create outline material
                var outlineMaterial = new Material(Shader.Find("Unlit/Color"));
                outlineMaterial.color = outlineColor;
                outlineRenderer.material = outlineMaterial;

                // Render behind the main object
                outlineRenderer.sortingOrder = -1;
            }
        }

        private void ApplyCloseProximityEffect(GameObject target, float intensity)
        {
            // Add a special close-proximity effect
            if (targetRenderer != null && hasEmission)
            {
                // Extra bright emission when very close
                Color closeEmission = highlightColor * intensity * 0.6f;
                targetRenderer.material.SetColor("_EmissionColor", closeEmission);
            }

            // Slight rotation for attention
            float rotationAmount = Mathf.Sin(Time.time * 8f) * intensity * 2f;
            target.transform.Rotate(Vector3.up, rotationAmount * Time.deltaTime);
        }

        private void RestoreOriginalAppearance(GameObject target)
        {
            if (targetRenderer != null)
            {
                // Restore original material
                targetRenderer.material = originalMaterial;
            }

            // Restore original scale
            target.transform.localScale = originalScale;

            // Remove outline objects
            RemoveOutlineEffects(target);

            // Destroy highlight material
            if (highlightMaterial != null)
            {
                Object.DestroyImmediate(highlightMaterial);
                highlightMaterial = null;
            }
        }

        private void RemoveOutlineEffects(GameObject target)
        {
            // Find and destroy outline objects
            for (int i = target.transform.childCount - 1; i >= 0; i--)
            {
                var child = target.transform.GetChild(i);
                if (child.name.EndsWith("_Outline"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        // Configuration methods for script generation
        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
        }

        public void SetHighlightIntensity(float intensity)
        {
            highlightIntensity = Mathf.Clamp01(intensity);
        }

        public void SetPulseSpeed(float speed)
        {
            pulseSpeed = Mathf.Max(0f, speed);
        }

        public void SetDistanceBasedIntensity(bool useDistance, float maxDistance = 2f)
        {
            useDistanceBasedIntensity = useDistance;
            maxHighlightDistance = maxDistance;
        }

        public void SetOutlineEffect(bool enable, float width = 0.02f, Color color = default)
        {
            addOutlineEffect = enable;
            outlineWidth = width;
            if (color != default) outlineColor = color;
        }

        public void SetScaleEffect(bool enable, float multiplier = 1.1f, float speed = 3f)
        {
            scaleOnHighlight = enable;
            scaleMultiplier = multiplier;
            scaleSpeed = speed;
        }
    }
} 