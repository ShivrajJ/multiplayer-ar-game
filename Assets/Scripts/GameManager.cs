using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using System.Linq;

// Enum defining the different states of the game
public enum GameState
{
    Initializing, // Initial setup state
    PlacingMap, // Waiting for the local player to place their map anchor
    WaitingForPlayers, // Local map placed, waiting for others to be ready
    Gameplay, // All players ready, game is active
    GameOver // Game has ended (optional)
}

/// <summary>
/// Singleton GameManager to manage game state, player connections,
/// and coordinate the start of the game.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    [Header("Network Prefabs")] [SerializeField]
    private GameObject playerPrefab; // Prefab for the player character 

    [SerializeField] private GameObject networkAnchorPrefab; // Prefab for the network anchor object

    [Header("AR Components")] [SerializeField]
    private ARPlacementManager arPlacementManager; // Reference to the placement manager script

    // --- State Management ---
    // NetworkVariable to synchronize the current game state across all clients
    [SerializeField] private NetworkVariable<GameState> networkGameState = new NetworkVariable<GameState>(
        GameState.Initializing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<NetworkObjectReference> networkAnchorReference;
    public NetworkObject NetworkAnchorObject { get; set; }

    public Dictionary<Team, HomeBase> HomeBases;
    public NetworkVariable<float> universalScale = new NetworkVariable<float>(0.3f);
    public Team localTeam;
    private Team _losingTeam;
    // Local (non-networked) state for UI/logic specific to this client
    [SerializeField] private GameState _localGameState = GameState.Initializing;
    public GameState LocalGameState => _localGameState; // Public getter for local state

    // --- Player Management ---
    // Dictionary to track player states (server-side)
    private NetworkList<ulong> registeredPlayers;

    // --- Map Reference ---
    // Reference to the locally placed AR Anchor/Map Root.
    // This is set by ARPlacementManager after successful placement.
    public Transform LocalMapAnchor { get; set; }

    // --- Events ---
    public Action<GameState> OnGameStateChanged; // Event fired when local game state changes

    [Header("UI References")]
    [SerializeField] private UIDocument gameUI;
    [SerializeField] private GameObject victoryUIObject;
    [SerializeField] private GameObject defeatUIObject;
    
    public GameObject GameUIGameObject => gameUI.gameObject;
    
    #region Unity Lifecycle & Singleton
    private void Awake()
    {
        HomeBases = new Dictionary<Team, HomeBase>();
        registeredPlayers = new NetworkList<ulong>();

        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        HomeBases = new Dictionary<Team, HomeBase>();
        gameUI ??= FindAnyObjectByType<UIDocument>(FindObjectsInactive.Exclude);
        // planeManager = FindObjectsByType<ARPlaneManager>(FindObjectsSortMode.None)[0];
    }
    
    void Start()
    {
        // Subscribe to NetworkVariable changes
        networkGameState.OnValueChanged += OnNetworkStateChanged;
        networkAnchorReference.OnValueChanged += RegisterNetworkAnchor;

        // Initial local state update
        UpdateLocalState(networkGameState.Value);

        // --- Netcode Event Subscriptions ---
        // Subscribe to connection events *if* this GameManager exists before NetworkManager initialization
        // Otherwise, handle these in NetworkManager's callbacks directly if preferred.
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }
        else
        {
            Debug.LogError(
                "NetworkManager not found! Ensure GameManager is initialized after NetworkManager or handle events differently.");
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsHost) localTeam = Team.Red;
        else localTeam = Team.Blue;
        if (IsServer) HandleServerStarted();
    }

    public override void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        if (networkGameState != null)
        {
            networkGameState.OnValueChanged -= OnNetworkStateChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    #endregion
    
    
    private void RegisterNetworkAnchor(NetworkObjectReference prevAnchor, NetworkObjectReference newAnchor)
    {
        if (newAnchor.TryGet(out var networkObject))
        {
            NetworkAnchorObject = networkObject;
            NetworkAnchorObject.transform.position = LocalMapAnchor.transform.position;
            NetworkAnchorObject.transform.rotation = LocalMapAnchor.transform.rotation;
            Debug.Log($"Network Anchor Object registered: {NetworkAnchorObject.name}");
        }
        else
        {
            Debug.LogError("Failed to register Network Anchor Object.");
        }
    }

    [ClientRpc(RequireOwnership = false)]
    private void RegisterNetworkAnchorClientRpc(NetworkObjectReference newAnchor)
    {
        if (newAnchor.TryGet(out var networkObject))
        {
            NetworkAnchorObject = networkObject;
            Debug.Log($"Network Anchor Object registered: {NetworkAnchorObject.name}");
        }
        else
        {
            Debug.LogError("Failed to register Network Anchor Object.");
        }
    }

    #region State Management Logic

    // Called when the networked game state changes
    private void OnNetworkStateChanged(GameState previousValue, GameState newValue)
    {
        Debug.Log($"Network GameState Changed: {previousValue} -> {newValue}");
        UpdateLocalState(newValue);
    }

    // Updates the local game state and invokes the event
    private void UpdateLocalState(GameState newState)
    {
        if (_localGameState == newState) return; // No change

        _localGameState = newState;
        OnGameStateChanged?.Invoke(_localGameState); // Fire event

        // --- Handle State Transitions ---
        switch (_localGameState)
        {
            case GameState.Initializing:
                // Initial setup, wait for connection
                break;
            case GameState.PlacingMap:
                // Enable AR Plane detection and placement UI
                if (arPlacementManager != null)
                {
                    arPlacementManager.EnablePlacement();
                    Debug.Log("AR Placement Enabled.");
                }
                else Debug.LogError("ARPlacementManager reference missing in GameManager!");

                break;
            case GameState.WaitingForPlayers:
                if (NetworkAnchorObject is null)
                    SpawnNetworkAnchorServerRpc();
                SpawnPlayerObjectServerRpc(NetworkManager.Singleton.LocalClientId);
                // Disable AR placement UI, show waiting UI
                if (IsServer)
                    CheckAllReady(); // Check if all players are ready
                break;
            case GameState.Gameplay:
                // Disable waiting UI, enable gameplay controls
                if (arPlacementManager != null) arPlacementManager.DisablePlacement(); // Ensure placement is off
                // Server spawns players if they haven't been already
                if (IsServer) SpawnAllPlayerObjects();
                Debug.Log("Transitioning to Gameplay!");
                break;
            case GameState.GameOver:
                // Show Game Over screen, show "victory" and "defeat" text
                if (IsServer) EndGameClientRpc(_losingTeam);
                
                Time.timeScale = 0;
                // Stop everything
                break;
        }
    }

    // Call this from ARPlacementManager when the local player successfully places the map
    public void LocalPlayerPlacedMap()
    {
        if (_localGameState != GameState.PlacingMap) return; // Only relevant during placement

        RegisterPlayerServerRpc(NetworkManager.Singleton.LocalClientId);

        if (registeredPlayers.Count < 2) return; // Don't proceed if not enough players
        // All players have placed their maps, transition to WaitingForPlayers
        Debug.Log("All players placed their maps. Transitioning to WaitingForPlayers.");
        RequestGameStateChangeServerRpc(GameState.WaitingForPlayers);
    }

    // Server-side function to change the global game state
    [ServerRpc]
    public void RequestGameStateChangeServerRpc(GameState newState)
    {
        if (!IsServer) return; // Only server can change the state

        // Add any validation logic here if needed (e.g., can't go from Gameplay back to Placing)
        Debug.Log($"Server changing GameState to: {newState}");
        networkGameState.Value = newState;
    }

    #endregion

    #region Network Connection Handling (Server-Side)

    private void HandleServerStarted()
    {
        if (!IsServer) return;
        Debug.Log("Server Started. Initializing Game State.");
        // Set initial state once server is running
        networkGameState.Value = GameState.PlacingMap;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"Client connected: {clientId}");

        // Server needs to wait for the player object to be spawned and ready.
        // Player state registration often happens in PlayerNetworkState.OnNetworkSpawn
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterPlayerServerRpc(ulong clientId)
    {
        RegisterPlayer(clientId);
    }

    public void RegisterPlayer(ulong clientId)
    {
        if (!IsServer) return;
        if (!registeredPlayers.Contains(clientId))
        {
            registeredPlayers.Add(clientId);
            Debug.Log($"Player {clientId} registered with GameManager.");
            // Re-check readiness in case this was the last player needed
        }

        if (registeredPlayers.Count >= 2) // Check if we have enough players
        {
            Debug.Log("All players registered. Checking readiness...");
            networkGameState.Value = GameState.WaitingForPlayers;
        }
    }

    private void CheckAllReady()
    {
        if (!IsServer) return;
        if (HomeBases.Count < 2) return;

        RequestGameStateChangeServerRpc(GameState.Gameplay);
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"Client disconnected: {clientId}");
        if (registeredPlayers.Contains(clientId))
        {
            registeredPlayers.Remove(clientId);
        }

        // Optional: Add logic here if a player disconnecting should end the game or reset state
        // For now, we'll just remove them. If game was running, might need state change.
        if (_localGameState is GameState.Gameplay or GameState.WaitingForPlayers)
        {
            // Maybe reset to PlacingMap if not enough players? Or handle gracefully.
            Debug.LogWarning($"Player {clientId} disconnected during active game phase.");
            // Example: Reset if needed
            // if (connectedPlayers.Count < MIN_PLAYERS) RequestGameStateChangeServerRpc(GameState.PlacingMap);
        }
    }

    #endregion

    #region Player Spawning (Server-Side)

    // Stores which players have had their character spawned
    private HashSet<ulong> spawnedPlayerObjects = new HashSet<ulong>();

    [ServerRpc(RequireOwnership = false)]
    private void SpawnNetworkAnchorServerRpc()
    {
        if (NetworkAnchorObject is not null) return; // Already spawned

        NetworkAnchorObject = Instantiate(networkAnchorPrefab).GetComponent<NetworkObject>();
        NetworkAnchorObject.Spawn();
        networkAnchorReference.Value = new NetworkObjectReference(NetworkAnchorObject);
        RegisterNetworkAnchorClientRpc(networkAnchorReference.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerObjectServerRpc(ulong clientId)
    {
        SpawnPlayerObject(clientId);

        // Note: Positioning relative to the anchor happens on each client
        // in the NetworkObjectParenting script (or similar) on the player prefab itself.
        // The server doesn't know about the clients' local anchors.
    }

    private void SpawnPlayerObject(ulong clientId)
    {
        if (!IsServer) return;
        // Check if this player's character object has already been spawned
        if (spawnedPlayerObjects.Contains(clientId))
        {
            Debug.Log($"Player object for client {clientId} already spawned.");
            return;
        }

        if (playerPrefab is null)
        {
            Debug.LogError("Player Prefab is not assigned in GameManager!");
            return;
        }

        // Instantiate the player character prefab
        GameObject playerInstance =
            Instantiate(playerPrefab, NetworkAnchorObject.transform); // Instantiated on the server

        // Get the NetworkObject component
        NetworkObject playerNetworkObject = playerInstance.GetComponent<NetworkObject>();
        if (playerNetworkObject is null)
        {
            Debug.LogError("Player Prefab must have a NetworkObject component!");
            Destroy(playerInstance); // Clean up
            return;
        }

        // Spawn the object across the network, assigning ownership to the specific client
        playerNetworkObject.SpawnAsPlayerObject(clientId, true); // true = DestroyWithScene

        // Keep track that we've spawned this player's object
        spawnedPlayerObjects.Add(clientId);

        Debug.Log($"Spawned player object for client {clientId}.");
    }

    private void SpawnAllPlayerObjects()
    {
        if (!IsServer) return;

        Debug.Log($"Server spawning player objects for {registeredPlayers.Count} players.");

        foreach (ulong clientId in registeredPlayers)
        {
            SpawnPlayerObject(clientId);

            // Note: Positioning relative to the anchor happens on each client
            // in the NetworkObjectParenting script (or similar) on the player prefab itself.
            // The server doesn't know about the clients' local anchors.
        }
    }

    #endregion

    #region Public Network Control Methods (Called by UI Buttons)

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Host Started.");
        // ServerStarted event will trigger state changes
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Client Started.");
        // Client connection events and NetworkVariable sync handle state
    }

    #endregion


    public HomeBase GetEnemyBase(ulong ownerClientId)
    {
        return localTeam switch
        {
            Team.Red => HomeBases[Team.Blue],
            Team.Blue => HomeBases[Team.Red],
            _ => throw new ArgumentOutOfRangeException()
        };
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

    public HomeBase GetEnemyBase()
    {
        return localTeam switch
        {
            Team.Red => HomeBases[Team.Blue],
            Team.Blue => HomeBases[Team.Red],
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void OnHomeBaseDeath(HomeBase homeBase)
    {
        _losingTeam = homeBase.team;
        networkGameState.Value = GameState.GameOver;
    }

    public Team GetEnemyTeam()
    {
        return localTeam switch
        {
            Team.Red => Team.Blue,
            Team.Blue => Team.Red,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public Transform GetBaseTransform()
    {
        return HomeBases[localTeam].transform;
    }

    [ClientRpc(RequireOwnership = false)]
    private void EndGameClientRpc(Team losingTeam)
    {
        if (localTeam == losingTeam)
        {
            ShowGameOverScreen();
        }
        else
        {
            ShowVictoryScreen();
        }
    }
    
    private void ShowVictoryScreen()
    {
        gameUI.gameObject.SetActive(false);
        victoryUIObject.SetActive(true);
    }

    private void ShowGameOverScreen()
    {
        gameUI.gameObject.SetActive(false);
        defeatUIObject.SetActive(true);
    }
}