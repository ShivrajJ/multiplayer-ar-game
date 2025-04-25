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

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public GameObject homeBasePrefab;
    public ARPlaneManager planeManager;
    public NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.WaitingForPlayers);
    public Dictionary<Team, HomeBase> HomeBases;
    public NetworkVariable<float> universalScale = new NetworkVariable<float>(0.3f);
    public Team team;
    private Team _losingTeam;

    [Header("UI References")]
    [SerializeField] private UIDocument gameUI;
    [SerializeField] private GameObject victoryUIObject;
    [SerializeField] private GameObject defeatUIObject;

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
        gameUI ??= FindAnyObjectByType<UIDocument>(FindObjectsInactive.Exclude);
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
                if (IsHost)
                {
                    team = Team.Red;
                    RegisterHomeBaseServerRpc(team, NetworkManager.Singleton.LocalClientId);
                }
                else
                {
                    team = Team.Blue;
                    RegisterHomeBaseServerRpc(team, NetworkManager.Singleton.LocalClientId);
                }

                break;
            case GameState.InProgress:
                TroopManager.Instance.SpawnTroop(0, HomeBases[team].transform);
                // Enable plane detection
                // planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                // WRAP IN COROUTINE (so it can be done over time)
                // find first plane
                // Spawn Home Bases
                // Disable plane detection
                // planeManager.requestedDetectionMode = PlaneDetectionMode.None;
                // END COROUTINE
                break;
            case GameState.GameOver:
                // Show Game Over screen, show "victory" and "defeat" text
                if (IsServer) EndGameClientRpc(_losingTeam);
                
                Time.timeScale = 0;
                // Stop everything
                break;
        }
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

    public HomeBase GetEnemyBase()
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
        InProgress,
        GameOver
    }

    public void OnHomeBaseDeath(HomeBase homeBase)
    {
        _losingTeam = homeBase.team;
        currentGameState.Value = GameState.GameOver;
    }

    public Team GetEnemyTeam()
    {
        return team switch
        {
            Team.Red => Team.Blue,
            Team.Blue => Team.Red,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public Transform GetBaseTransform()
    {
        return HomeBases[team].transform;
    }

    [ClientRpc(RequireOwnership = false)]
    private void EndGameClientRpc(Team losingTeam)
    {
        if (team == losingTeam)
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