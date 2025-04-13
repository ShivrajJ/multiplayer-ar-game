using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

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
    private float _timeSinceLastIncome;
    private float _incomeTimeSeconds = 2f;

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
        InProgress,
        GameOver
    }

    public void OnHomeBaseDeath(HomeBase homeBase)
    {
        _losingTeam = homeBase.team;
        currentGameState.Value = GameState.GameOver;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (currentGameState.Value == GameState.InProgress)
        {
            _timeSinceLastIncome += Time.deltaTime;
            if (_timeSinceLastIncome >= _incomeTimeSeconds)
            {
                AddCoins();
                _timeSinceLastIncome = 0f;
            }
        }
    }

    private void AddCoins()
    {
        HomeBase redBase = HomeBases[Team.Red];
        HomeBase blueBase = HomeBases[Team.Blue];

        redBase.gold.Value += redBase.income.Value;
        blueBase.gold.Value += blueBase.income.Value;
    }
}