using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Attach this script to networked prefabs (like the Player Character)
/// that need to be parented under the locally placed AR map anchor when spawned.
/// It also handles setting the initial local position relative to the anchor.
/// </summary>
public class NetworkObjectParenting : NetworkBehaviour
{
    // NetworkVariable to synchronize the intended initial local position and rotation.
    // Set by the server right after spawning. Clients read this ONCE on spawn.
    private NetworkVariable<Vector3> initialLocalPosition = new NetworkVariable<Vector3>(Vector3.zero,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<Quaternion> initialLocalRotation = new NetworkVariable<Quaternion>(Quaternion.identity,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool initialPositionSet = false; // Flag to ensure we only set position once

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe to changes ONLY if not the server, as server sets it.
        // And only if position hasn't been set yet.
        if (!IsServer && !initialPositionSet)
        {
            initialLocalPosition.OnValueChanged += HandleInitialPositionChanged;
            initialLocalRotation.OnValueChanged += HandleInitialRotationChanged;
        }

        // Attempt initial parenting immediately
        TryParentAndPosition();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        initialLocalPosition.OnValueChanged -= HandleInitialPositionChanged;
        initialLocalRotation.OnValueChanged -= HandleInitialRotationChanged;
    }

    // Called when the NetworkVariable for position changes
    private void HandleInitialPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (initialPositionSet) return; // Already done
        Debug.Log($"NetworkObjectParenting ({gameObject.name}): Received Initial Local Position {newValue}");
        TryParentAndPosition();
    }

    // Called when the NetworkVariable for rotation changes
    private void HandleInitialRotationChanged(Quaternion previousValue, Quaternion newValue)
    {
        if (initialPositionSet) return; // Already done
        Debug.Log(
            $"NetworkObjectParenting ({gameObject.name}): Received Initial Local Rotation {newValue.eulerAngles}");
        TryParentAndPosition();
    }


    // Attempts to find the local map anchor and parent this object to it.
    private void TryParentAndPosition()
    {
        // Don't run if position is already set or if this is the server (server doesn't have a *local* anchor)
        if (initialPositionSet || !IsOwner) return;

        // Find the local map anchor reference from the GameManager
        Transform networkAnchor = GameManager.Instance?.NetworkAnchorObject.transform;

        if (networkAnchor != null)
        {
            Debug.Log(
                $"NetworkObjectParenting ({gameObject.name}): Found LocalMapAnchor. Parenting and positioning...");

            // Parent this NetworkObject to the local anchor
            NetworkObject.TrySetParent(networkAnchor, false); // false = keep local orientation

            // Set the local position and rotation based on the synced NetworkVariables
            transform.localPosition = initialLocalPosition.Value;
            transform.localRotation = initialLocalRotation.Value;

            Debug.Log(
                $"NetworkObjectParenting ({gameObject.name}): Set localPosition to {transform.localPosition}, localRotation to {transform.localRotation.eulerAngles}");

            initialPositionSet = true; // Mark as done

            // Unsubscribe now that we have the initial pose
            // initialLocalPosition.OnValueChanged -= HandleInitialPositionChanged;
            // initialLocalRotation.OnValueChanged -= HandleInitialRotationChanged;

            // --- IMPORTANT: NetworkTransform Configuration ---
            // If using NetworkTransform, ensure it's configured to sync 'In Local Space'
            // or respects parenting. If you encounter issues where objects don't sync correctly
            // relative to the anchor, you might need to implement custom position/rotation
            // synchronization using NetworkVariables within your player movement scripts,
            // instead of relying solely on NetworkTransform after this initial setup.
            NetworkTransform nt = GetComponent<NetworkTransform>();
            if (nt != null)
            {
                // Check nt.InLocalSpace setting in the Inspector. It SHOULD be true for this setup.
                // If NetworkTransform still causes issues, disable its Position/Rotation sync
                // and implement your own sync logic using NetworkVariables updated in FixedUpdate.
                Debug.LogWarning(
                    $"NetworkObjectParenting ({gameObject.name}): Ensure NetworkTransform is set to sync 'In Local Space' in the Inspector for correct relative movement.");
            }
        }
        else
        {
            // Anchor might not be ready yet (e.g., GameManager hasn't set it).
            // This could happen due to network timing.
            // We'll rely on the OnValueChanged callbacks to try again when data arrives.
            Debug.LogWarning(
                $"NetworkObjectParenting ({gameObject.name}): LocalMapAnchor not found yet in GameManager. Waiting for NetworkVariable change...");
        }
    }

    // --- Server-Side Method ---
    // Call this from the server (e.g., GameManager) immediately after spawning the object.
    [ServerRpc(RequireOwnership = false)] // Allow server to call this on any object it just spawned
    public void SetInitialPoseServerRpc(Vector3 localPos, Quaternion localRot)
    {
        if (!IsServer) return;

        initialLocalPosition.Value = localPos;
        initialLocalRotation.Value = localRot;
        Debug.Log($"Server setting initial pose for {gameObject.name}: Pos={localPos}, Rot={localRot.eulerAngles}");

        // Server doesn't parent locally, but clients will react to the variable change.
    }
}