using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch; // Required for List<>
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handles AR plane detection, user input for placing the map anchor,
/// and instantiating the local map prefab.
/// </summary>
[RequireComponent(typeof(ARPlaneManager), typeof(ARRaycastManager))]
public class ARPlacementManager : MonoBehaviour
{
    // --- Inspector References ---
    [SerializeField] private GameObject mapPrefab; // The non-networked prefab representing the game map/area
    [SerializeField] private Camera arCamera; // The AR Camera (usually child of AR Session Origin)
    [SerializeField] private GameObject placementIndicatorPrefab; // Optional: Visual aid for where placement will occur

    // --- AR Components ---
    private ARPlaneManager planeManager;
    private ARRaycastManager raycastManager;
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>(); // List to store raycast results

    // --- State ---
    private bool isPlacementEnabled = false;
    private GameObject placementIndicatorInstance;
    private bool mapHasBeenPlaced = false; // Prevent multiple placements
    private ARPlane anchorPlane; // The plane that the map is anchored to (if any)

    void Awake()
    {
        // Get required AR components
        planeManager = GetComponent<ARPlaneManager>();
        raycastManager = GetComponent<ARRaycastManager>();

        if (arCamera == null)
        {
            Debug.LogError("AR Camera reference not set in ARPlacementManager!");
        }

        if (mapPrefab == null)
        {
            Debug.LogError("Map Prefab reference not set in ARPlacementManager!");
        }

        // Instantiate placement indicator if prefab is assigned
        if (placementIndicatorPrefab != null)
        {
            placementIndicatorInstance = Instantiate(placementIndicatorPrefab);
            placementIndicatorInstance.SetActive(false); // Initially hidden
        }
    }

    void Update()
    {
        // Check for touch input to place the map
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Mouse click detected for map placement.");
            PlaceMap(Mouse.current.position.ReadValue());
        }

        // Only run placement logic if enabled and map hasn't been placed yet
        if (!isPlacementEnabled || mapHasBeenPlaced)
        {
            if (placementIndicatorInstance != null && placementIndicatorInstance.activeSelf)
            {
                placementIndicatorInstance.SetActive(false); // Hide indicator if placement is disabled or done
            }

            return;
        }

        // Update placement indicator position
        UpdatePlacementIndicator();
    }

    // Updates the position and visibility of the placement indicator
    private void UpdatePlacementIndicator()
    {
        // Cast a ray from the center of the screen (or use touch position if preferred)
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        if (raycastManager.Raycast(screenCenter, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            // Get the pose (position and rotation) of the first hit
            Pose hitPose = raycastHits[0].pose;

            // Activate and position the indicator
            if (placementIndicatorInstance != null)
            {
                placementIndicatorInstance.SetActive(true);
                placementIndicatorInstance.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
            }
        }
        else
        {
            // Hide indicator if no valid plane is hit
            if (placementIndicatorInstance != null)
            {
                placementIndicatorInstance.SetActive(false);
            }
        }
    }

    private void PlaceMap(Finger finger)
    {
        Debug.Log("Placing map at finger position: " + finger.screenPosition);
        PlaceMap(finger.screenPosition);
    }

    // Performs the raycast and places the map if a valid plane is hit
    private void PlaceMap(Vector2 screenPosition)
    {
        if (mapHasBeenPlaced) return; // Don't place more than once

        // Raycast against detected planes at the touch/click position
        if (raycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            int closestHitIndex = -1;
            for (int hitIndex = 0; hitIndex < raycastHits.Count; hitIndex++)
            {
                // If the plane is a horizontal plane facing up (ground)
                if (planeManager.GetPlane(raycastHits[hitIndex].trackableId).alignment == PlaneAlignment.HorizontalUp)
                {
                    if (closestHitIndex == -1 || raycastHits[hitIndex].distance < raycastHits[closestHitIndex].distance)
                    {
                        closestHitIndex = hitIndex;
                    }
                }
            }

            if (closestHitIndex == -1) return;
            ARPlane plane = planeManager.GetPlane(raycastHits[closestHitIndex].trackableId);
            anchorPlane = plane;
            InstantiateAndPlaceMap(plane.center, plane.transform);
        }
        else
        {
            Debug.Log("Placement failed: No valid plane hit at screen position.");
        }
    }

    // Instantiates the map prefab and notifies the GameManager
    private void InstantiateAndPlaceMap(Vector3 position, Transform parentTransform)
    {
        if (mapPrefab is null)
        {
            Debug.LogError("Cannot instantiate map: Map Prefab is null!");
            return;
        }
        Camera mainCamera = Camera.main;
        if (mainCamera is null)
        {
            Debug.LogError("Main Camera not set!");
            return;
        }

        Vector3 forwardFlat = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
        Quaternion rotation = Quaternion.LookRotation(forwardFlat, parentTransform.up);

        // Instantiate the map locally (NOT networked)
        GameObject mapInstance = Instantiate(mapPrefab, position, rotation);
        Debug.Log($"Map prefab instantiated at {position}");

        // Parent the map instance to the anchor's transform if an anchor was created
        if (parentTransform != null)
        {
            mapInstance.transform.SetParent(parentTransform, true); // true = world position stays
            // Store the anchor/parent transform in the GameManager for other scripts to find
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LocalMapAnchor = mapInstance.transform; // Store the ANCHOR transform
                Debug.Log("LocalMapAnchor reference set in GameManager.");
            }
            else Debug.LogError("GameManager instance not found to store LocalMapAnchor!");
        }
        else
        {
            // If no anchor, the map exists at the world position but isn't anchored.
            // We still need a reference point for relative positioning. Use the map instance itself.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LocalMapAnchor = mapInstance.transform; // Store the MAP transform
            }
            else Debug.LogError("GameManager instance not found to store LocalMapAnchor!");
        }


        mapHasBeenPlaced = true; // Mark map as placed
        DisablePlacement(); // Turn off further placement attempts

        // Hide the placement indicator for good now
        if (placementIndicatorInstance != null)
        {
            placementIndicatorInstance.SetActive(false);
        }

        // --- Notify GameManager ---
        // Tell the GameManager that this local player has finished placement
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LocalPlayerPlacedMap();
        }
        else
        {
            Debug.LogError("GameManager instance not found! Cannot notify about map placement.");
        }
    }


    // Called by GameManager to enable placement mode
    public void EnablePlacement()
    {
        Debug.Log("ARPlacementManager: Enabling placement.");
        isPlacementEnabled = true;
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += PlaceMap;
        mapHasBeenPlaced = false; // Reset placement status if re-enabled

        // Enable plane detection visuals (optional, good for user feedback)
        SetPlaneDetection(true);
    }

    // Called by GameManager to disable placement mode
    public void DisablePlacement()
    {
        Debug.Log("ARPlacementManager: Disabling placement.");
        isPlacementEnabled = false;
        EnhancedTouchSupport.Disable();
        Touch.onFingerDown -= PlaceMap;

        // Disable plane detection visuals (optional, saves performance)
        SetPlaneDetection(false);

        // Hide placement indicator
        if (placementIndicatorInstance != null && placementIndicatorInstance.activeSelf)
        {
            placementIndicatorInstance.SetActive(false);
        }
    }

    // Helper to turn plane detection visuals on/off
    private void SetPlaneDetection(bool enabled)
    {
        if (planeManager != null)
        {
            planeManager.enabled = enabled; // Toggle the manager itself
            foreach (var plane in planeManager.trackables)
            {
                // Skip disabling the chosen plane
                if (anchorPlane is not null && plane.trackableId == anchorPlane.trackableId && !enabled)
                {
                    anchorPlane.GetComponent<Renderer>().enabled = false;
                    continue;
                }

                plane.gameObject.SetActive(enabled); // Toggle existing plane visuals
            }

            Debug.Log($"Plane detection set to: {enabled}");
        }
    }
}