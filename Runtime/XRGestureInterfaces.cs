using UnityEngine;

namespace OOJUPlugin
{
    public interface IGestureDetector
    {
        GestureType Type { get; }
        bool IsDetected { get; }
        float Confidence { get; }
        Vector3 Position { get; }
        Vector3 Direction { get; }
        bool IsLeftHand { get; }
        
        void Initialize();
        void UpdateDetection();
        event System.Action<GestureData> OnGestureDetected;
        event System.Action<GestureData> OnGestureReleased;
    }

    public interface IInteractionEffect
    {
        InteractionType Type { get; }
        bool IsActive { get; }
        
        void Execute(GameObject target, GestureData gestureData);
        void Stop(GameObject target);
        void Update(GameObject target, GestureData gestureData);
    }

    public interface IXRGestureResponder
    {
        void OnGestureTriggered(GestureType gesture, GestureData data, GameObject target);
        void OnGestureReleased(GestureType gesture, GestureData data, GameObject target);
        bool CanRespondToGesture(GestureType gesture);
    }
} 