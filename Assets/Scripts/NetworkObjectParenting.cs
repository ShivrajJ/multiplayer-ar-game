using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.AI;

/// <summary>
/// Attach this script to networked prefabs (like the Player Character)
/// that need to be parented under the locally placed AR map anchor when spawned.
/// It also handles setting the initial local position relative to the anchor.
/// </summary>
public class NetworkObjectParenting : NetworkBehaviour
{
    // Attempts to find the local map anchor and parent this object to it.
    public void TryParentAndPosition(Vector3 position, Quaternion rotation)
    {
        // Don't run if position is already set or if this is the server (server doesn't have a *local* anchor)
        if (!IsOwner) return;

        // Find the local map anchor reference from the GameManager
        Transform networkAnchor = GameManager.Instance?.NetworkAnchorObject.transform;

        if (networkAnchor != null)
        {
            Debug.Log(
                $"NetworkObjectParenting ({gameObject.name}): Found LocalMapAnchor. Parenting and positioning...");

            // Parent this NetworkObject to the local anchor
            NetworkObject.TrySetParent(networkAnchor, false); // false = keep local orientation

            NetworkTransform nt = GetComponent<NetworkTransform>();
            if (nt is not null)
            {
                nt.InLocalSpace = true;
            }

            // Set the local position and rotation based on the synced NetworkVariables
            transform.localPosition = position;
            transform.localRotation = rotation;
            if (TryGetComponent<NavMeshAgent>(out NavMeshAgent navAgent))
            {
                navAgent.enabled = true;
            }

            Debug.Log(
                $"NetworkObjectParenting ({gameObject.name}): Set localPosition to {transform.localPosition}, localRotation to {transform.localRotation.eulerAngles}");

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
}