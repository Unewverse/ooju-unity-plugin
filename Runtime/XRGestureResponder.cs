using System.Collections.Generic;
using UnityEngine;

namespace OOJUPlugin
{
    [RequireComponent(typeof(Collider))]
    public class XRGestureResponder : MonoBehaviour, IXRGestureResponder
    {
        [Header("Gesture Interactions")]
        [SerializeField] private GestureInteractionConfig[] interactions = new GestureInteractionConfig[0];
        
        [Header("Visual Feedback")]
        [SerializeField] private bool showDebugFeedback = true;
        [SerializeField] private Color debugColor = Color.yellow;
        
        [Header("Auto Setup")]
        [SerializeField] private bool ensureColliderSetup = true;

        private Dictionary<GestureType, GestureInteractionConfig> configMap;
        private Collider objectCollider;
        private bool isInitialized = false;

        void Start()
        {
            Initialize();
        }

        void OnDestroy()
        {
            if (XRGestureInteractionManager.Instance != null)
            {
                XRGestureInteractionManager.Instance.UnregisterResponder(this);
            }
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // Setup collider
            SetupCollider();

            // Build config map
            BuildConfigMap();

            // Register with manager
            if (XRGestureInteractionManager.Instance != null)
            {
                XRGestureInteractionManager.Instance.RegisterResponder(this);
            }
            else
            {
                Debug.LogWarning($"[XRGestureResponder] No XRGestureInteractionManager found. Creating one automatically.");
                CreateGestureManager();
            }

            isInitialized = true;
        }

        private void SetupCollider()
        {
            objectCollider = GetComponent<Collider>();
            if (objectCollider == null && ensureColliderSetup)
            {
                // Add a collider if none exists
                var meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    var meshCollider = gameObject.AddComponent<MeshCollider>();
                    meshCollider.convex = true; // Required for trigger detection
                    objectCollider = meshCollider;
                }
                else
                {
                    objectCollider = gameObject.AddComponent<BoxCollider>();
                }
                
                Debug.Log($"[XRGestureResponder] Auto-added collider to {gameObject.name}");
            }

            if (objectCollider != null)
            {
                // Ensure it can be detected
                if (!objectCollider.isTrigger)
                {
                    // We need the collider to be detectable but can still be solid
                    // The gesture detection uses Physics.OverlapSphere which works with both
                }
            }
        }

        private void BuildConfigMap()
        {
            configMap = new Dictionary<GestureType, GestureInteractionConfig>();
            
            foreach (var config in interactions)
            {
                if (config != null)
                {
                    configMap[config.gesture] = config;
                }
            }
        }

        private void CreateGestureManager()
        {
            var managerGO = new GameObject("XRGestureInteractionManager");
            managerGO.AddComponent<XRGestureInteractionManager>();
        }

        public void OnGestureTriggered(GestureType gesture, GestureData data, GameObject target)
        {
            if (showDebugFeedback)
            {
                Debug.Log($"[XRGestureResponder] {gameObject.name} triggered by {gesture} gesture");
            }

            // Visual feedback
            if (showDebugFeedback)
            {
                StartCoroutine(ShowVisualFeedback());
            }
        }

        public void OnGestureReleased(GestureType gesture, GestureData data, GameObject target)
        {
            if (showDebugFeedback)
            {
                Debug.Log($"[XRGestureResponder] {gameObject.name} released from {gesture} gesture");
            }
        }

        public bool CanRespondToGesture(GestureType gesture)
        {
            return configMap != null && configMap.ContainsKey(gesture);
        }

        public GestureInteractionConfig GetConfigForGesture(GestureType gesture)
        {
            return configMap != null && configMap.ContainsKey(gesture) ? configMap[gesture] : null;
        }

        // Public methods for script generation
        public void AddGestureInteraction(GestureType gesture, InteractionType effect, float intensity = 1f)
        {
            var newConfig = new GestureInteractionConfig
            {
                gesture = gesture,
                effect = effect,
                intensity = intensity,
                duration = -1f,
                requiresContact = true,
                triggerDistance = 0.1f
            };

            // Add to array
            var newInteractions = new GestureInteractionConfig[interactions.Length + 1];
            for (int i = 0; i < interactions.Length; i++)
            {
                newInteractions[i] = interactions[i];
            }
            newInteractions[interactions.Length] = newConfig;
            interactions = newInteractions;

            // Rebuild config map
            BuildConfigMap();

            Debug.Log($"[XRGestureResponder] Added {gesture} -> {effect} interaction to {gameObject.name}");
        }

        public void RemoveGestureInteraction(GestureType gesture)
        {
            var newInteractions = new List<GestureInteractionConfig>();
            
            foreach (var interaction in interactions)
            {
                if (interaction.gesture != gesture)
                {
                    newInteractions.Add(interaction);
                }
            }

            interactions = newInteractions.ToArray();
            BuildConfigMap();

            Debug.Log($"[XRGestureResponder] Removed {gesture} interaction from {gameObject.name}");
        }

        private System.Collections.IEnumerator ShowVisualFeedback()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var originalColor = renderer.material.color;
                renderer.material.color = debugColor;
                yield return new WaitForSeconds(0.2f);
                renderer.material.color = originalColor;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (objectCollider != null)
            {
                Gizmos.color = debugColor;
                Gizmos.DrawWireCube(objectCollider.bounds.center, objectCollider.bounds.size);
            }
        }

        // Editor helper methods
        [System.Obsolete("Use AddGestureInteraction instead")]
        public void SetupGestureInteraction(GestureType gesture, InteractionType effect, string description = "")
        {
            AddGestureInteraction(gesture, effect);
        }

#if UNITY_EDITOR
        [ContextMenu("Test Pinch Gesture")]
        private void TestPinchGesture()
        {
            var testData = new GestureData(GestureType.Pinch, transform.position, Vector3.forward);
            OnGestureTriggered(GestureType.Pinch, testData, gameObject);
        }

        [ContextMenu("Setup Basic Interactions")]
        private void SetupBasicInteractions()
        {
            interactions = new GestureInteractionConfig[]
            {
                new GestureInteractionConfig { gesture = GestureType.Pinch, effect = InteractionType.FollowHand },
                new GestureInteractionConfig { gesture = GestureType.Tap, effect = InteractionType.InfiniteRotation },
                new GestureInteractionConfig { gesture = GestureType.PointToSelect, effect = InteractionType.Highlight }
            };
            BuildConfigMap();
            Debug.Log($"[XRGestureResponder] Setup basic interactions for {gameObject.name}");
        }
#endif
    }
} 