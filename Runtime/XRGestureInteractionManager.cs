using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

        private float lastDebugTime = 0f;

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
            if (!IsXRReady)
                return;

            // Debug hand tracking status every few seconds
            if (showDebugInfo && Time.time - lastDebugTime > 3.0f)
            {
                LogHandTrackingStatus();
                lastDebugTime = Time.time;
            }

            // Monitor gesture detection
            UpdateGestureDetection();
        }

        private void InitializeXRComponents()
        {
            Debug.Log("[XRGestureManager] Starting XR component initialization...");
            
            // Check if Meta XR SDK is available
            isMetaXRAvailable = CheckMetaXRAvailability();
            
            Debug.Log($"[XRGestureManager] Meta XR Available: {isMetaXRAvailable}");
            
            if (isMetaXRAvailable && autoFindOVRHands)
            {
                FindOVRComponents();
            }

            if (!IsXRReady)
            {
                Debug.LogWarning("[XRGestureManager] No OVR Hands found. XR gesture interactions will not work.");
                
                // Try one more time with comprehensive search
                TryComprehensiveHandSearch();
            }
            
            Debug.Log($"[XRGestureManager] XR initialization complete - Ready: {IsXRReady}");
        }

        private bool CheckMetaXRAvailability()
        {
            try
            {
                // Use more robust type searching across all loaded assemblies
                bool hasTraditionalSetup = FindTypeInAssemblies("OVRHand") != null && FindTypeInAssemblies("OVRCameraRig") != null;
                bool hasModernSetup = FindTypeInAssemblies("Hand") != null || FindTypeInAssemblies("XROrigin") != null;
                
                // Additional checks for Interaction SDK
                bool hasInteractionSDK = FindTypeInAssemblies("Oculus.Interaction.Input.Hand") != null;
                
                Debug.Log($"[XRGestureManager] Meta XR availability - Traditional: {hasTraditionalSetup}, Modern: {hasModernSetup}, InteractionSDK: {hasInteractionSDK}");
                
                return hasTraditionalSetup || hasModernSetup || hasInteractionSDK;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error checking Meta XR availability: {e.Message}");
                return false;
            }
        }

        private System.Type FindTypeInAssemblies(string typeName)
        {
            try
            {
                // First try simple type name
                var type = System.Type.GetType(typeName);
                if (type != null) return type;

                // Search through all loaded assemblies
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // Try exact match first
                        type = assembly.GetType(typeName);
                        if (type != null) return type;

                        // Try partial match for nested types and different namespaces
                        var types = assembly.GetTypes();
                        foreach (var t in types)
                        {
                            if (t.Name == typeName || t.FullName.EndsWith("." + typeName))
                            {
                                return t;
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
            catch (System.Exception)
            {
                // Ignore type search errors
            }

            return null;
        }

        private void FindOVRComponents()
        {
            if (!isMetaXRAvailable) return;

            try
            {
                // Find OVR Camera Rig using reflection
                var ovrCameraRigType = FindTypeInAssemblies("OVRCameraRig");
                if (ovrCameraRigType != null)
                {
                    var findMethod = typeof(Object).GetMethod("FindFirstObjectByType", new System.Type[] { });
                    var genericMethod = findMethod.MakeGenericMethod(ovrCameraRigType);
                    cameraRig = genericMethod.Invoke(null, null);
                    
                    if (cameraRig == null)
                    {
                        Debug.LogWarning("[XRGestureManager] OVRCameraRig not found in scene. Please add Meta XR setup.");
                    }
                }

                // Try to find OVR Hands using multiple approaches
                bool foundOVRHands = TryFindOVRHands();
                
                // Fallback: Try to find custom hand setups
                if (!foundOVRHands)
                {
                    TryFindCustomHandSetup();
                }

                // Additional direct search by GameObject names (for Building Blocks)
                if (leftHand == null || rightHand == null)
                {
                    TryFindHandsByGameObjectNames();
                }

                Debug.Log($"[XRGestureManager] Hand setup complete - Left: {leftHand != null}, Right: {rightHand != null}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding OVR components: {e.Message}");
            }
        }

        private bool TryFindOVRHands()
        {
            try
            {
                // Find OVR Hands using improved type search
                var ovrHandType = FindTypeInAssemblies("OVRHand");
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
                            var handEnumType = FindTypeInAssemblies("OVRHand+Hand");
                            
                            if (handEnumType != null)
                            {
                                try
                                {
                                    var leftHandValue = System.Enum.Parse(handEnumType, "HandLeft");
                                    var rightHandValue = System.Enum.Parse(handEnumType, "HandRight");
                                    
                                    if (handTypeEnum.Equals(leftHandValue))
                                        leftHand = hand;
                                    else if (handTypeEnum.Equals(rightHandValue))
                                        rightHand = hand;
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogWarning($"[XRGestureManager] Error parsing hand enum: {e.Message}");
                                }
                            }
                        }
                    }

                    if (hands.Length > 0)
                    {
                        Debug.Log($"[XRGestureManager] Found {hands.Length} OVR Hands");
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding standard OVRHands: {e.Message}");
            }
            
            return false;
        }

        private void TryFindCustomHandSetup()
        {
            Debug.Log("[XRGestureManager] Searching for custom hand setup...");
            
            // Try to find Building Blocks / OpenXR hands first
            if (TryFindBuildingBlocksHands())
            {
                Debug.Log("[XRGestureManager] Found Building Blocks hands");
                return;
            }
            
            // Look for GameObjects with "Hand" in their name and try to find OVRHand components
            var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            
            foreach (var go in allGameObjects)
            {
                if (go.name.ToLower().Contains("hand"))
                {
                    // Try to find OVRHand component in this GameObject or its children
                    var ovrHandComponent = go.GetComponentInChildren<MonoBehaviour>()
                        ?.GetType().Name == "OVRHand" ? go.GetComponentInChildren<MonoBehaviour>() : null;
                    
                    if (ovrHandComponent != null)
                    {
                        try
                        {
                            var handTypeProperty = ovrHandComponent.GetType().GetProperty("HandType");
                            if (handTypeProperty != null)
                            {
                                var handTypeEnum = handTypeProperty.GetValue(ovrHandComponent);
                                var handEnumType = System.Type.GetType("OVRHand+Hand");
                                
                                if (handEnumType != null)
                                {
                                    var leftHandValue = System.Enum.Parse(handEnumType, "HandLeft");
                                    var rightHandValue = System.Enum.Parse(handEnumType, "HandRight");
                                    
                                    if (handTypeEnum.Equals(leftHandValue))
                                    {
                                        leftHand = ovrHandComponent;
                                        Debug.Log($"[XRGestureManager] Found custom left hand: {go.name}");
                                    }
                                    else if (handTypeEnum.Equals(rightHandValue))
                                    {
                                        rightHand = ovrHandComponent;
                                        Debug.Log($"[XRGestureManager] Found custom right hand: {go.name}");
                                    }
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[XRGestureManager] Error processing custom hand {go.name}: {e.Message}");
                        }
                    }
                }
            }
            
            // Additional fallback: Search by naming convention
            if (leftHand == null || rightHand == null)
            {
                SearchByNamingConvention();
            }
        }

        private bool TryFindBuildingBlocksHands()
        {
            try
            {
                // Look for Interaction SDK Hand components with improved type search
                var handType = FindTypeInAssemblies("Hand");
                var interactionHandType = FindTypeInAssemblies("Oculus.Interaction.Input.Hand");
                
                // Try both possible hand types
                var handTypes = new System.Type[] { handType, interactionHandType }.Where(t => t != null).ToArray();
                
                foreach (var currentHandType in handTypes)
                {
                    if (TryFindHandsOfType(currentHandType))
                    {
                        return true;
                    }
                }
                
                // Also try to find OVRHand components that might be part of Building Blocks setup
                return TryFindOVRHandsInBuildingBlocks();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding Building Blocks hands: {e.Message}");
                return false;
            }
        }

        private bool TryFindHandsOfType(System.Type handType)
        {
            try
            {
                var findMethod = typeof(Object).GetMethod("FindObjectsByType", new System.Type[] { typeof(FindObjectsSortMode) });
                var genericMethod = findMethod.MakeGenericMethod(handType);
                var hands = (Object[])genericMethod.Invoke(null, new object[] { FindObjectsSortMode.None });

                foreach (var hand in hands)
                {
                    try
                    {
                        // Try to get handedness information
                        var handednessProperty = hand.GetType().GetProperty("Handedness");
                        if (handednessProperty != null)
                        {
                            var handedness = handednessProperty.GetValue(hand);
                            var handednessType = FindTypeInAssemblies("Handedness") ?? FindTypeInAssemblies("Oculus.Interaction.Input.Handedness");
                            
                            if (handednessType != null)
                            {
                                try
                                {
                                    var leftValue = System.Enum.Parse(handednessType, "Left");
                                    var rightValue = System.Enum.Parse(handednessType, "Right");
                                    
                                    if (handedness.Equals(leftValue))
                                    {
                                        leftHand = hand;
                                        Debug.Log($"[XRGestureManager] Found Building Blocks left hand: {((MonoBehaviour)hand).name}");
                                    }
                                    else if (handedness.Equals(rightValue))
                                    {
                                        rightHand = hand;
                                        Debug.Log($"[XRGestureManager] Found Building Blocks right hand: {((MonoBehaviour)hand).name}");
                                    }
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogWarning($"[XRGestureManager] Error parsing handedness: {e.Message}");
                                }
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[XRGestureManager] Error processing Building Blocks hand: {e.Message}");
                    }
                }

                if (hands.Length > 0)
                {
                    Debug.Log($"[XRGestureManager] Found {hands.Length} Building Blocks hands of type {handType.Name}");
                    return leftHand != null || rightHand != null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding hands of type {handType?.Name}: {e.Message}");
            }
            
            return false;
        }

        private bool TryFindOVRHandsInBuildingBlocks()
        {
            try
            {
                // Look for OVRHand components that might be part of Building Blocks
                var ovrHandType = FindTypeInAssemblies("OVRHand");
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
                            var handEnumType = FindTypeInAssemblies("OVRHand+Hand");
                            
                            if (handEnumType != null)
                            {
                                try
                                {
                                    var leftHandValue = System.Enum.Parse(handEnumType, "HandLeft");
                                    var rightHandValue = System.Enum.Parse(handEnumType, "HandRight");
                                    
                                    if (handTypeEnum.Equals(leftHandValue) && leftHand == null)
                                    {
                                        leftHand = hand;
                                        Debug.Log($"[XRGestureManager] Found OVRHand left in Building Blocks: {((MonoBehaviour)hand).name}");
                                    }
                                    else if (handTypeEnum.Equals(rightHandValue) && rightHand == null)
                                    {
                                        rightHand = hand;
                                        Debug.Log($"[XRGestureManager] Found OVRHand right in Building Blocks: {((MonoBehaviour)hand).name}");
                                    }
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogWarning($"[XRGestureManager] Error parsing OVRHand enum: {e.Message}");
                                }
                            }
                        }
                    }

                    return hands.Length > 0 && (leftHand != null || rightHand != null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error finding OVRHands in Building Blocks: {e.Message}");
            }
            
            return false;
        }

        private void SearchByNamingConvention()
        {
            Debug.Log("[XRGestureManager] Searching by naming convention...");
            
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            
            foreach (var transform in allTransforms)
            {
                string name = transform.name.ToLower();
                
                // Look for objects with left/right hand naming
                if (name.Contains("left") && name.Contains("hand") && leftHand == null)
                {
                    var ovrHand = transform.GetComponentInChildren<MonoBehaviour>()
                        ?.GetType().Name == "OVRHand" ? transform.GetComponentInChildren<MonoBehaviour>() : null;
                    
                    if (ovrHand != null)
                    {
                        leftHand = ovrHand;
                        Debug.Log($"[XRGestureManager] Found left hand by naming: {transform.name}");
                    }
                }
                else if (name.Contains("right") && name.Contains("hand") && rightHand == null)
                {
                    var ovrHand = transform.GetComponentInChildren<MonoBehaviour>()
                        ?.GetType().Name == "OVRHand" ? transform.GetComponentInChildren<MonoBehaviour>() : null;
                    
                    if (ovrHand != null)
                    {
                        rightHand = ovrHand;
                        Debug.Log($"[XRGestureManager] Found right hand by naming: {transform.name}");
                    }
                }
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
                Debug.Log($"[XRGestureManager] Gesture detected: {gestureData.type} at {gestureData.position} (confidence: {gestureData.confidence})");
            }

            // Find nearby interactable objects
            Collider[] nearbyColliders = Physics.OverlapSphere(
                gestureData.position, 
                detectionRange, 
                interactableLayerMask
            );

            if (showDebugInfo)
            {
                Debug.Log($"[XRGestureManager] Found {nearbyColliders.Length} colliders within {detectionRange}m of gesture");
            }

            int respondersFound = 0;
            foreach (var collider in nearbyColliders)
            {
                var responder = collider.GetComponent<XRGestureResponder>();
                if (responder != null)
                {
                    respondersFound++;
                    if (responder.CanRespondToGesture(gestureData.type))
                    {
                        if (showDebugInfo)
                        {
                            Debug.Log($"[XRGestureManager] Triggering {gestureData.type} on {collider.name}");
                        }
                        responder.OnGestureTriggered(gestureData.type, gestureData, collider.gameObject);
                        TriggerEffect(collider.gameObject, gestureData, responder.GetConfigForGesture(gestureData.type));
                    }
                    else
                    {
                        if (showDebugInfo)
                        {
                            Debug.Log($"[XRGestureManager] {collider.name} cannot respond to {gestureData.type}");
                        }
                    }
                }
            }
            
            if (showDebugInfo && respondersFound == 0)
            {
                Debug.Log($"[XRGestureManager] No XRGestureResponder components found in nearby objects");
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
                        // For infinite effects like InfiniteRotation, always update
                        if (effect.Type == InteractionType.InfiniteRotation || 
                            effect.Type == InteractionType.Heartbeat)
                        {
                            // Create a dummy gesture data for continuous effects
                            var continuousData = new GestureData(
                                gestureEffect.Key,
                                target.transform.position,
                                Vector3.forward,
                                1.0f,
                                1.0f,
                                true
                            );
                            effect.Update(target, continuousData);
                        }
                        else
                        {
                            // For other effects, check if gesture is still detected
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

        private void TryComprehensiveHandSearch()
        {
            Debug.Log("[XRGestureManager] Starting comprehensive hand search...");
            
            // List all MonoBehaviour components in the scene for debugging
            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var handRelatedComponents = new List<string>();
            
            foreach (var comp in allComponents)
            {
                if (comp != null)
                {
                    string typeName = comp.GetType().Name;
                    string fullTypeName = comp.GetType().FullName;
                    
                    // Check if component name contains "Hand" or "OVR"
                    if (typeName.ToLower().Contains("hand") || 
                        typeName.ToLower().Contains("ovr") || 
                        fullTypeName.ToLower().Contains("hand") ||
                        fullTypeName.ToLower().Contains("interaction"))
                    {
                        handRelatedComponents.Add($"{comp.name}: {typeName} ({fullTypeName})");
                        
                        // Try to use this component as a hand
                        TryUseAsHand(comp);
                    }
                }
            }
            
            Debug.Log($"[XRGestureManager] Found {handRelatedComponents.Count} hand-related components:");
            foreach (var comp in handRelatedComponents)
            {
                Debug.Log($"  - {comp}");
            }
        }

        private void TryUseAsHand(MonoBehaviour component)
        {
            try
            {
                var type = component.GetType();
                
                // Check for OVRHand properties
                var handTypeProperty = type.GetProperty("HandType");
                if (handTypeProperty != null)
                {
                    var handTypeValue = handTypeProperty.GetValue(component);
                    Debug.Log($"[XRGestureManager] Found HandType property on {component.name}: {handTypeValue}");
                    
                    // Try to determine if it's left or right
                    string handTypeStr = handTypeValue.ToString().ToLower();
                    if (handTypeStr.Contains("left"))
                    {
                        leftHand = component;
                        Debug.Log($"[XRGestureManager] Assigned left hand: {component.name}");
                    }
                    else if (handTypeStr.Contains("right"))
                    {
                        rightHand = component;
                        Debug.Log($"[XRGestureManager] Assigned right hand: {component.name}");
                    }
                    return;
                }
                
                // Check for Interaction SDK Handedness property
                var handednessProperty = type.GetProperty("Handedness");
                if (handednessProperty != null)
                {
                    var handednessValue = handednessProperty.GetValue(component);
                    Debug.Log($"[XRGestureManager] Found Handedness property on {component.name}: {handednessValue}");
                    
                    string handednessStr = handednessValue.ToString().ToLower();
                    if (handednessStr.Contains("left"))
                    {
                        leftHand = component;
                        Debug.Log($"[XRGestureManager] Assigned left hand: {component.name}");
                    }
                    else if (handednessStr.Contains("right"))
                    {
                        rightHand = component;
                        Debug.Log($"[XRGestureManager] Assigned right hand: {component.name}");
                    }
                    return;
                }
                
                // Check by object name as fallback
                string objName = component.name.ToLower();
                if (objName.Contains("left") && objName.Contains("hand") && leftHand == null)
                {
                    leftHand = component;
                    Debug.Log($"[XRGestureManager] Assigned left hand by name: {component.name}");
                }
                else if (objName.Contains("right") && objName.Contains("hand") && rightHand == null)
                {
                    rightHand = component;
                    Debug.Log($"[XRGestureManager] Assigned right hand by name: {component.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRGestureManager] Error analyzing component {component.name}: {e.Message}");
            }
        }

        private void TryFindHandsByGameObjectNames()
        {
            Debug.Log("[XRGestureManager] Searching for hands by GameObject names...");
            
            // Common hand object names used in Building Blocks
            string[] leftHandNames = { "LeftHand", "Left Hand", "HandLeft", "Hand_Left", "OVRHandLeft", "LeftHandAnchor" };
            string[] rightHandNames = { "RightHand", "Right Hand", "HandRight", "Hand_Right", "OVRHandRight", "RightHandAnchor" };
            
            // Try to find left hand
            if (leftHand == null)
            {
                foreach (string name in leftHandNames)
                {
                    GameObject leftHandGO = GameObject.Find(name);
                    if (leftHandGO != null)
                    {
                        var handComponent = leftHandGO.GetComponent<MonoBehaviour>();
                        if (handComponent != null)
                        {
                            leftHand = handComponent;
                            Debug.Log($"[XRGestureManager] Found left hand by name: {name} -> {handComponent.GetType().Name}");
                            break;
                        }
                        
                        // Try child components
                        var childHandComponent = leftHandGO.GetComponentInChildren<MonoBehaviour>();
                        if (childHandComponent != null && (
                            childHandComponent.GetType().Name.Contains("Hand") || 
                            childHandComponent.GetType().Name.Contains("OVR")))
                        {
                            leftHand = childHandComponent;
                            Debug.Log($"[XRGestureManager] Found left hand in children: {name} -> {childHandComponent.GetType().Name}");
                            break;
                        }
                    }
                }
            }
            
            // Try to find right hand
            if (rightHand == null)
            {
                foreach (string name in rightHandNames)
                {
                    GameObject rightHandGO = GameObject.Find(name);
                    if (rightHandGO != null)
                    {
                        var handComponent = rightHandGO.GetComponent<MonoBehaviour>();
                        if (handComponent != null)
                        {
                            rightHand = handComponent;
                            Debug.Log($"[XRGestureManager] Found right hand by name: {name} -> {handComponent.GetType().Name}");
                            break;
                        }
                        
                        // Try child components
                        var childHandComponent = rightHandGO.GetComponentInChildren<MonoBehaviour>();
                        if (childHandComponent != null && (
                            childHandComponent.GetType().Name.Contains("Hand") || 
                            childHandComponent.GetType().Name.Contains("OVR")))
                        {
                            rightHand = childHandComponent;
                            Debug.Log($"[XRGestureManager] Found right hand in children: {name} -> {childHandComponent.GetType().Name}");
                            break;
                        }
                    }
                }
            }
            
            // If still not found, try searching all GameObjects with "Hand" in name
            if (leftHand == null || rightHand == null)
            {
                var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in allGameObjects)
                {
                    if (go.name.ToLower().Contains("hand"))
                    {
                        Debug.Log($"[XRGestureManager] Found GameObject with 'Hand' in name: {go.name}");
                        
                        if (leftHand == null && go.name.ToLower().Contains("left"))
                        {
                            var component = go.GetComponentInChildren<MonoBehaviour>();
                            if (component != null)
                            {
                                leftHand = component;
                                Debug.Log($"[XRGestureManager] Assigned left hand from search: {go.name} -> {component.GetType().Name}");
                            }
                        }
                        
                        if (rightHand == null && go.name.ToLower().Contains("right"))
                        {
                            var component = go.GetComponentInChildren<MonoBehaviour>();
                            if (component != null)
                            {
                                rightHand = component;
                                Debug.Log($"[XRGestureManager] Assigned right hand from search: {go.name} -> {component.GetType().Name}");
                            }
                        }
                    }
                }
            }
        }

        private void LogHandTrackingStatus()
        {
            Debug.Log($"[XRGestureManager] Hand Tracking Status - Left Hand: {leftHand != null}, Right Hand: {rightHand != null}");
            
            if (leftHand != null)
            {
                try
                {
                    // Safely cast to MonoBehaviour and get Transform
                    var leftHandComponent = leftHand as MonoBehaviour;
                    if (leftHandComponent != null)
                    {
                        Debug.Log($"[XRGestureManager] Left hand position: {leftHandComponent.transform.position}");
                    }
                }
                catch (System.Exception) { }
            }
            
            if (rightHand != null)
            {
                try
                {
                    // Safely cast to MonoBehaviour and get Transform
                    var rightHandComponent = rightHand as MonoBehaviour;
                    if (rightHandComponent != null)
                    {
                        Debug.Log($"[XRGestureManager] Right hand position: {rightHandComponent.transform.position}");
                    }
                }
                catch (System.Exception) { }
            }

            Debug.Log($"[XRGestureManager] Registered responders: {registeredResponders.Count}, Gesture detectors: {gestureDetectors.Count}");
        }

        private void UpdateGestureDetection()
        {
            // Update all gesture detectors
            foreach (var detector in gestureDetectors)
            {
                detector.UpdateDetection();
            }

            // Update active effects
            UpdateActiveEffects();
            
            // Editor simulation for testing
            #if UNITY_EDITOR
            HandleEditorSimulation();
            #endif
        }

        #if UNITY_EDITOR
        private void HandleEditorSimulation()
        {
            // Debug every frame to see if this method is being called
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space))
            {
                Debug.Log("[XRGestureManager] Space key detected - simulation system is working!");
            }

            // Check conditions
            if (!UnityEngine.Application.isEditor)
            {
                Debug.Log("[XRGestureManager] Not in editor mode");
                return;
            }

            if (!IsXRReady)
            {
                Debug.Log("[XRGestureManager] XR not ready for simulation");
                return;
            }

            // Keyboard shortcuts for gesture simulation with immediate feedback
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha1))
            {
                Debug.Log("[XRGestureManager] Key 1 pressed - triggering Point gesture");
                SimulateGesture(GestureType.PointToSelect, "Simulated Point gesture");
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha2))
            {
                Debug.Log("[XRGestureManager] Key 2 pressed - triggering Pinch gesture");
                SimulateGesture(GestureType.Pinch, "Simulated Pinch gesture");
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha3))
            {
                Debug.Log("[XRGestureManager] Key 3 pressed - triggering Tap gesture");
                SimulateGesture(GestureType.Tap, "Simulated Tap gesture");
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha4))
            {
                Debug.Log("[XRGestureManager] Key 4 pressed - triggering OpenPalm gesture");
                SimulateGesture(GestureType.OpenPalm, "Simulated OpenPalm gesture");
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha5))
            {
                Debug.Log("[XRGestureManager] Key 5 pressed - triggering Wave gesture");
                SimulateGesture(GestureType.Wave, "Simulated Wave gesture");
            }

            // Show instructions and system status
            if (showDebugInfo && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.H))
            {
                UnityEngine.Debug.Log("[XRGestureManager] Editor Simulation Keys:\n" +
                    "1 = Point To Select\n" +
                    "2 = Pinch\n" +
                    "3 = Tap\n" +
                    "4 = Open Palm\n" +
                    "5 = Wave\n" +
                    "Space = Test input detection\n" +
                    "H = Show this help\n" +
                    $"IsXRReady: {IsXRReady}\n" +
                    $"Registered Responders: {registeredResponders.Count}");
            }
        }

        private void SimulateGesture(GestureType gestureType, string debugMessage)
        {
            // Try to find the blue dragon object for accurate positioning
            GameObject targetObject = GameObject.Find("blue dragon");
            UnityEngine.Vector3 simulatedPosition;
            
            if (targetObject != null)
            {
                // Position the gesture near the target object
                simulatedPosition = targetObject.transform.position + UnityEngine.Vector3.up * 0.5f;
                Debug.Log($"[XRGestureManager] Targeting {targetObject.name} at {simulatedPosition}");
            }
            else
            {
                // Fallback to camera-based positioning
                simulatedPosition = UnityEngine.Camera.main != null 
                    ? UnityEngine.Camera.main.transform.position + UnityEngine.Camera.main.transform.forward * 2f
                    : new UnityEngine.Vector3(0, 1, 2);
            }

            var gestureData = new GestureData
            {
                type = gestureType,
                position = simulatedPosition,
                direction = UnityEngine.Vector3.forward,
                confidence = 1.0f,
                intensity = 1.0f,
                isLeftHand = true
            };

            if (showDebugInfo)
            {
                UnityEngine.Debug.Log($"[XRGestureManager] {debugMessage} at {simulatedPosition}");
            }

            OnGestureDetected(gestureData);
        }
        #endif
    }
} 