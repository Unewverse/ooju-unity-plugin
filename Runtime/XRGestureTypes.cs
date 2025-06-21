using UnityEngine;

namespace OOJUPlugin
{
    public enum GestureType
    {
        PointToSelect = 0,
        Pinch = 1,
        OpenPalm = 2,
        Tap = 3,
        Wave = 4
    }

    public enum InteractionType
    {
        FollowHand,
        InfiniteRotation,
        Highlight,
        PushAway,
        Heartbeat,
        Custom
    }

    [System.Serializable]
    public struct GestureData
    {
        public GestureType type;
        public Vector3 position;
        public Vector3 direction;
        public float confidence;
        public float intensity;
        public bool isLeftHand;
        
        public GestureData(GestureType gestureType, Vector3 pos, Vector3 dir, float conf = 1f, float intense = 1f, bool leftHand = false)
        {
            type = gestureType;
            position = pos;
            direction = dir;
            confidence = conf;
            intensity = intense;
            isLeftHand = leftHand;
        }
    }

    [System.Serializable]
    public class GestureInteractionConfig
    {
        public GestureType gesture;
        public InteractionType effect;
        public float intensity = 1f;
        public float duration = -1f; // -1 = infinite
        public bool requiresContact = true;
        public float triggerDistance = 0.1f;
    }
} 