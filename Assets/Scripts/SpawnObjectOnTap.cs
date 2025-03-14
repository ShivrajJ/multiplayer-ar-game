using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[RequireComponent(typeof(ARRaycastManager), typeof(ARPlaneManager))]
public class SpawnObjectOnTap : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private ARRaycastManager arRaycastManager;
    private ARPlaneManager arPlaneManager;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
    }

    private void OnEnable()
    {
        TouchSimulation.Enable();
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += FingerDown;
    }

    private void FingerDown(Finger finger)
    {
        if (finger.index != 0) return;

        if (arRaycastManager.Raycast(finger.currentTouch.screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            int closestHitIndex = -1;
            for (int hitIndex = 0; hitIndex < hits.Count; hitIndex++)
            {
                // If the plane is a horizontal plane facing up (ground)
                if (arPlaneManager.GetPlane(hits[hitIndex].trackableId).alignment == PlaneAlignment.HorizontalUp)
                {
                    if (closestHitIndex == -1 || hits[hitIndex].distance < hits[closestHitIndex].distance)
                    {
                        closestHitIndex = hitIndex;
                    }
                }
            }
            if (closestHitIndex != -1)
            {
                Pose pose = hits[closestHitIndex].pose;
                Instantiate(prefab, pose.position, pose.rotation);
            }
        }
    }

    private void OnDisable()
    {
        TouchSimulation.Disable();
        EnhancedTouchSupport.Disable();
    }
}
