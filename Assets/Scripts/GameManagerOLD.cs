using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Quaternion = UnityEngine.Quaternion;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class GameManagerOLD : NetworkBehaviour
{
    public static GameManagerOLD Instance;
    public Transform LocalMapAnchor { get; set; }
    public GameObject homeBasePrefab;
    public GameObject mapPrefab;
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.WaitingForPlayers);
    public Dictionary<Team, HomeBase> HomeBases;
    public NetworkVariable<float> universalScale = new NetworkVariable<float>(0.3f);
    public Team team;

    private Team _losingTeam;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject _mapGameObject;

    private void Awake()
    {
        if (Instance is null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        HomeBases = new Dictionary<Team, HomeBase>();
        // planeManager = FindObjectsByType<ARPlaneManager>(FindObjectsSortMode.None)[0];
    }

    public override void OnNetworkSpawn()
    {
        currentGameState.OnValueChanged += HandleGameStateChange;
        HandleGameStateChange(currentGameState.Value, currentGameState.Value);
    }

    private void HandleGameStateChange(GameState oldState, GameState newState)
    {
        switch (newState)
        {
            case GameState.WaitingForPlayers:
                // Wait for Players, show host their IP address
                if (!IsHost)
                {
                }

                String ipAddress = GetLocalIPv4();
                Debug.Log($"Host IP Address: {ipAddress}");
                // Show IP address to host
                // Show "Waiting for players" screen
                // if (IsHost)
                // {
                //     team = Team.Red;
                //     RegisterHomeBaseServerRpc(team, NetworkManager.Singleton.LocalClientId);
                // }
                // else
                // {
                //     team = Team.Blue;
                //     RegisterHomeBaseServerRpc(team, NetworkManager.Singleton.LocalClientId);
                // }

                break;
            case GameState.PlacingHomeBase:
                planeManager.enabled = true;
                TouchSimulation.Enable();
                EnhancedTouchSupport.Enable();
                Touch.onFingerDown += PlaceDownHomeBaseOnPlane;
                // Show "Place your home base" screen
                // Show AR planes
                break;
            case GameState.InProgress:
                TroopManager.Instance.SpawnTroop(0, HomeBases[team].transform);
                break;
            case GameState.GameOver:
                // Show Game Over screen, show "victory" and "defeat" text
                break;
        }
    }

    private void PlaceDownHomeBaseOnPlane(Finger finger)
    {
        if (finger.index != 0) return;

        if (raycastManager.Raycast(finger.currentTouch.screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            int closestHitIndex = -1;
            for (int hitIndex = 0; hitIndex < hits.Count; hitIndex++)
            {
                // If the plane is a horizontal plane facing up (ground)
                if (planeManager.GetPlane(hits[hitIndex].trackableId).alignment == PlaneAlignment.HorizontalUp)
                {
                    if (closestHitIndex == -1 || hits[hitIndex].distance < hits[closestHitIndex].distance)
                    {
                        closestHitIndex = hitIndex;
                    }
                }
            }

            if (closestHitIndex != -1)
            {
                ARPlane plane = planeManager.GetPlane(hits[closestHitIndex].trackableId);
                ARAnchor arAnchor = new GameObject("AR Anchor").AddComponent<ARAnchor>();
                _mapGameObject = Instantiate(mapPrefab, plane.center, Quaternion.identity, arAnchor.transform);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc()
    {
        currentGameState.Value = GameState.PlacingHomeBase;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterHomeBaseServerRpc(Team clientTeam, ulong clientId)
    {
        HomeBase homeBase = HomeBases[clientTeam];
        homeBase.NetworkObject.ChangeOwnership(clientId);
        if (NetworkManager.ConnectedClients.Count >= 2) currentGameState.Value = GameState.InProgress;
    }

    public void RegisterHomeBase(HomeBase homeBase)
    {
        if (HomeBases.ContainsKey(homeBase.team))
        {
            HomeBases[homeBase.team] = homeBase;
        }
        else
        {
            HomeBases.TryAdd(homeBase.team, homeBase);
        }
    }

    public HomeBase GetEnemyBase(ulong ownerClientId)
    {
        return team switch
        {
            Team.Red => HomeBases[Team.Blue],
            Team.Blue => HomeBases[Team.Red],
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public enum GameState
    {
        WaitingForPlayers,
        PlacingHomeBase,
        InProgress,
        GameOver
    }

    public void OnHomeBaseDeath(HomeBase homeBase)
    {
        _losingTeam = homeBase.team;
        currentGameState.Value = GameState.GameOver;
    }

    private string GetLocalIPv4()
    {
        return Dns.GetHostEntry(Dns.GetHostName())
            .AddressList.First(
                f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .ToString();
    }

    // #region Test
    //
    // public void LocalPlayerPlacedMap()
    // {
    //     if (currentGameState.Value != GameState.PlacingHomeBase) return; // Only relevant during placement
    //
    //     // Find the local player's network state script
    //     if (NetworkManager.Singleton.LocalClient != null &&
    //         NetworkManager.Singleton.LocalClient.PlayerObject != null &&
    //         NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent<PlayerNetworkState>(out var playerState))
    //     {
    //         Debug.Log("Local player placed map. Informing server...");
    //         playerState.NotifyServerMapPlaced(); // Tell the server this client is ready
    //     }
    //     else
    //     {
    //         Debug.LogError("Could not find local PlayerNetworkState to notify server!");
    //     }
    //
    //     // Optionally, transition local state immediately to WaitingForPlayers for quicker UI feedback
    //     // The server will ultimately control the authoritative game state transition.
    //     // UpdateLocalState(GameState.WaitingForPlayers); // Uncomment for immediate local feedback
    // }
    //
    // #endregion
    //
    // public void RegisterPlayer(ulong ownerClientId, PlayerNetworkState playerNetworkState)
    // {
    //     if (!IsServer) return;
    // }
}