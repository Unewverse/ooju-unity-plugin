using System.Collections.Generic;
using UnityEngine;

namespace OOJUPlugin
{
    public class XRGestureInteractionManager : MonoBehaviour
    {
        [Header("XR Setup")]
        [SerializeField] private bool autoFindOVRHands = true;
        [SerializeField] private float detectionRange = 2f;
        [SerializeField] private LayerMask interactableLayerMask = -1;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool drawGestureGizmos = true;

        // Singleton
        public static XRGestureInteractionManager Instance { get; private set; }

        // Gesture Detection
        private List<IGestureDetector> gestureDetectors = new List<IGestureDetector>();
        private Dictionary<GestureType, IGestureDetector> detectorMap = new Dictionary<GestureType, IGestureDetector>();

        // Interaction Management
        private List<XRGestureResponder> registeredResponders = new List<XRGestureResponder>();
        private Dictionary<GameObject, Dictionary<GestureType, IInteractionEffect>> activeEffects = 
            new Dictionary<GameObject, Dictionary<GestureType, IInteractionEffect>>();

        // OVR Components (will be found automatically)
        private object leftHand;
        private object rightHand;
        private object cameraRig;
        private bool isMetaXRAvailable = false;
        
        public bool IsXRReady => leftHand != null || rightHand != null;
        public object LeftHand => leftHand;
        public object RightHand => rightHand;

        void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            InitializeXRComponents();
            InitializeGestureDetectors();
        }

        void Update()
        {
            if (!IsXRReady) return;

            // Update all gesture detectors
            foreach (var detector in gestureDetectors)
            {
                detector.UpdateDetection();
            }

            // Update active effects
            UpdateActiveEffects();
        }

        private void InitializeXRComponents()
        {
            // Check if Meta XR SDK is available
            isMetaXRAvailable = CheckMetaXRAvailability();
            
            if (isMetaXRAvailable && autoFindOVRHands)
            {
                FindOVRComponents();
            }

            if (!IsXRReady)
            {
                Debug.LogWarning("[XRGestureManager] No OVR Hands found. XR gesture interactions will not work.");
            }
        }

        private bool CheckMetaXRAvailability()
        {
            try
            {
                var ovrHandType = System.Type.GetType("OVRHand");
                var ovrCameraRigType = System.Type.GetType("OVRCameraRig");
                return ovrHandType != null && ovrCameraRigType != null;
            }
            catch
            {
                return false;
            }
        }

        private void FindOVRComponents()
        {
            if (!isMetaXRAvailable) return;

            try
            {
                // Find OVR Camera Rig using reflection
                var ovrCameraRigType = System.Type.GetType("OVRCameraRig");
                if (ovrCameraRigType != null)
                {
                    var findMethod = typeof(Object).GetMethod("FindFirstObjectByType", new System.Type[] { });
                    var genericMethod = findMethod.MakeGenericMethod(ovrCameraRigType);
                    cameraRig = genericMethod.Invoke(null, null);
                    
                    if (cameraRig == null)
                    {
                        Debug.LogWarning("[XRGestureManager] OVRCameraRig not found in scene. Please add Meta XR setup.");
                        return;
                    }
                }

                // Find OVR Hands using reflection
                var ovrHandType = System.Type.GetType("OVRHand");
                if (ovrHandType != null)
                {
                    var findMethod = typeof(Object).GetMethod("FindObjectsByType", new System.Type[] { typeof(FindObjectsSortMode) });
                    var genericMethod = findMethod.MakeGenericMethod(ovrHandType);
                    var hands = (Object[])genericMethod.Invoke(null, new object[] { FindObjectsSortMode.None });

                    foreach (var hand in hands)
                    {
                        var handType = hand.GetType();
                        var handTypeProperty = handType.GetProperty("HandType");
                        if (handTypeProperty != null)
                        {
                            var handTypeEnum = handTypeProperty.GetValue(hand);
                            var handEnumType = System.Type.GetType("OVRHand+Hand");
                            
                            if (handEnumType != null)
                            {
                                var leftHandValue = System.Enum.Parse(handEnumType, "HandLeft");
                                var rightHandValue = System.Enum.Parse(handEnumType, "HandRight");
                                
                                if (handTypeEnum.Equals(leftHandValue))
                                    leftHand = hand;
                                else if (handTypeEnum.Equals(rightHandValue))
                                    rightHand = hand;
                            }
                        }
                    }

                    Debug.Log($"[XRGestureManager] Found {hands.Length} OVR Hands");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding OVR components: {e.Message}");
            }
        }

        private void InitializeGestureDetectors()
        {
            // Auto-create and register all gesture detectors
            CreateAndRegisterDetector<PinchGestureDetector>();
            CreateAndRegisterDetector<TapGestureDetector>();
            CreateAndRegisterDetector<PointGestureDetector>();
            CreateAndRegisterDetector<OpenPalmGestureDetector>();
            CreateAndRegisterDetector<WaveGestureDetector>();

            Debug.Log($"[XRGestureManager] Initialized {gestureDetectors.Count} gesture detectors");
        }

        private void CreateAndRegisterDetector<T>() where T : MonoBehaviour, IGestureDetector
        {
            var detectorGO = new GameObject($"{typeof(T).Name}");
            detectorGO.transform.SetParent(transform);
            
            var detector = detectorGO.AddComponent<T>();
            RegisterGestureDetector(detector);
        }

        public void RegisterResponder(XRGestureResponder responder)
        {
            if (responder == null)
            {
                Debug.LogError("[XRGestureManager] Cannot register null responder");
                return;
            }

            if (!registeredResponders.Contains(responder))
            {
                registeredResponders.Add(responder);
                Debug.Log($"[XRGestureManager] Registered responder: {responder.name}");
            }
        }

        public void UnregisterResponder(XRGestureResponder responder)
        {
            registeredResponders.Remove(responder);
            
            // Clean up any active effects for this responder
            if (activeEffects.ContainsKey(responder.gameObject))
            {
                var effects = activeEffects[responder.gameObject];
                foreach (var effect in effects.Values)
                {
                    effect.Stop(responder.gameObject);
                }
                activeEffects.Remove(responder.gameObject);
            }
        }

        public void RegisterGestureDetector(IGestureDetector detector)
        {
            if (!gestureDetectors.Contains(detector))
            {
                gestureDetectors.Add(detector);
                detectorMap[detector.Type] = detector;
                
                // Subscribe to gesture events
                detector.OnGestureDetected += OnGestureDetected;
                detector.OnGestureReleased += OnGestureReleased;
                
                detector.Initialize();
                Debug.Log($"[XRGestureManager] Registered gesture detector: {detector.Type}");
            }
        }

        private void OnGestureDetected(GestureData gestureData)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XRGestureManager] Gesture detected: {gestureData.type} at {gestureData.position}");
            }

            // Find nearby interactable objects
            Collider[] nearbyColliders = Physics.OverlapSphere(
                gestureData.position, 
                detectionRange, 
                interactableLayerMask
            );

            foreach (var collider in nearbyColliders)
            {
                var responder = collider.GetComponent<XRGestureResponder>();
                if (responder != null && responder.CanRespondToGesture(gestureData.type))
                {
                    responder.OnGestureTriggered(gestureData.type, gestureData, collider.gameObject);
                    TriggerEffect(collider.gameObject, gestureData, responder.GetConfigForGesture(gestureData.type));
                }
            }
        }

        private void OnGestureReleased(GestureData gestureData)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XRGestureManager] Gesture released: {gestureData.type}");
            }

            // Notify responders and stop temporary effects
            foreach (var responder in registeredResponders)
            {
                if (responder.CanRespondToGesture(gestureData.type))
                {
                    responder.OnGestureReleased(gestureData.type, gestureData, responder.gameObject);
                }
            }
        }

        private void TriggerEffect(GameObject target, GestureData gestureData, GestureInteractionConfig config)
        {
            if (config == null) return;

            // Create effect instance
            IInteractionEffect effect = CreateEffect(config.effect);
            if (effect == null) return;

            // Store active effect
            if (!activeEffects.ContainsKey(target))
            {
                activeEffects[target] = new Dictionary<GestureType, IInteractionEffect>();
            }

            // Stop previous effect of same type
            if (activeEffects[target].ContainsKey(gestureData.type))
            {
                activeEffects[target][gestureData.type].Stop(target);
            }

            // Start new effect
            activeEffects[target][gestureData.type] = effect;
            effect.Execute(target, gestureData);
        }

        private void UpdateActiveEffects()
        {
            foreach (var targetEffects in activeEffects)
            {
                var target = targetEffects.Key;
                if (target == null) continue;

                foreach (var gestureEffect in targetEffects.Value)
                {
                    var effect = gestureEffect.Value;
                    if (effect.IsActive)
                    {
                        // Get current gesture data for continuous effects
                        var detector = detectorMap.ContainsKey(gestureEffect.Key) ? 
                                     detectorMap[gestureEffect.Key] : null;
                        
                        if (detector != null && detector.IsDetected)
                        {
                            var currentData = new GestureData(
                                gestureEffect.Key,
                                detector.Position,
                                detector.Direction,
                                detector.Confidence,
                                1f,
                                detector.IsLeftHand
                            );
                            effect.Update(target, currentData);
                        }
                    }
                }
            }
        }

        private IInteractionEffect CreateEffect(InteractionType effectType)
        {
            switch (effectType)
            {
                case InteractionType.FollowHand:
                    return new FollowHandEffect();
                case InteractionType.InfiniteRotation:
                    return new InfiniteRotationEffect();
                case InteractionType.Highlight:
                    return new HighlightEffect();
                case InteractionType.PushAway:
                    return new PushAwayEffect();
                case InteractionType.Heartbeat:
                    return new HeartbeatEffect();
                default:
                    Debug.LogWarning($"[XRGestureManager] Unknown effect type: {effectType}");
                    return null;
            }
        }

        private Vector3 GetHandPosition(object hand)
        {
            if (!isMetaXRAvailable || hand == null) return Vector3.zero;

            try
            {
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

        void OnDrawGizmos()
        {
            if (!drawGestureGizmos || !IsXRReady) return;

            // Draw detection range around hands
            if (leftHand != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(GetHandPosition(leftHand), detectionRange);
            }

            if (rightHand != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(GetHandPosition(rightHand), detectionRange);
            }
        }
    }
} 