using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// The basic idea here is that this is where we manage everything about troops, like spawning troops, etc.
public class TroopManager : NetworkBehaviour
{
    public enum AIMode
    {
        Attack,
        Defend
    }

    public static TroopManager Instance; // Singleton instance of the troop manager

    // List of all the available troop prefabs
    [SerializeField] private List<TroopData> availableTroops;
    [SerializeField] private AIMode currentMode;
    [SerializeField] private List<Troop> troops = new List<Troop>(); // Local list of player's troops
    private Team team => GameManager.Instance?.team ?? Team.Red;
    private Coroutine spawningCoroutine;

    public List<TroopData> AvailableTroops => availableTroops; // Public access to the available troops

    public AIMode CurrentMode // Current mode of the troops
    {
        get => currentMode;
        set
        {
            currentMode = value;
            if (value == AIMode.Defend)
            {
                foreach (Troop troop in troops)
                {
                    troop.StartRetreating();
                }
            }
        }
    }

    private void Awake()
    {
        if (Instance is null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        CurrentMode = AIMode.Defend;
    }

    // Spawn Troop
    public void SpawnTroop(int troopIndex)
    {
        SpawnTroop(troopIndex, GameManager.Instance.GetBaseTransform());
    }
    
    public void SpawnTroop(int troopIndex, Transform homeBaseTransform)
    {
        if (!IsClient) return;
        Vector3 spawnPosition = UnityEngine.Random.insideUnitSphere * 2.0f + homeBaseTransform.position;
        int tries = 0;
        while (tries < 5 && !NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            spawnPosition = UnityEngine.Random.insideUnitSphere * 2.0f + homeBaseTransform.position;
            tries++;
        }

        spawnPosition.y = homeBaseTransform.position.y;
        Debug.Log("Spawn position: " + spawnPosition);
        StartSpawningTroopServerRpc(troopIndex, team, spawnPosition, homeBaseTransform.rotation,
            NetworkManager.LocalClientId);
    }

    public void SetMode(AIMode mode)
    {
        CurrentMode = mode;
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartSpawningTroopServerRpc(int troopIndex, Team clientTeam, Vector3 position, Quaternion rotation,
        ulong clientId)
    {
        // Check if player has enough resources to spawn a troop
        if (troopIndex >= availableTroops.Count)
        {
            Debug.LogError("Invalid troop index: " + troopIndex);
            return;
        }

        TroopData troopData = availableTroops[troopIndex];
        Debug.Log("Checking if player has enough gold to spawn troop: " + troopData.price);
        Debug.Log("Player has: " + GameManager.Instance.HomeBases[clientTeam].gold.Value);
        if (GameManager.Instance.HomeBases[clientTeam].gold.Value >= troopData.price)
        {
            GameManager.Instance.HomeBases[clientTeam].gold.Value -= troopData.price;
            Debug.Log("Starting spawn for troop: " + troopData.prefab.name);
            StartCoroutine(TroopSpawnWaitCoroutine(troopIndex, position, rotation, clientId));
        }
    }

    private IEnumerator TroopSpawnWaitCoroutine(int troopIndex, Vector3 position, Quaternion rotation, ulong clientId)
    {
        float waitTime = availableTroops[troopIndex].spawnTime;
        float startTime = Time.time;
        while (Time.time - startTime < waitTime)
        {
            // Update the UI progress bar
            Debug.Log("Waiting for spawn...");
            yield return null;
        }

        Debug.Log("Starting spawn...");
        SpawnTroop(troopIndex, team, position, rotation, clientId);
    }

    private void SpawnTroop(int troopIndex, Team clientTeam, Vector3 position, Quaternion rotation, ulong clientId)
    {
        if (!IsServer) return;
        TroopData troopData = AvailableTroops[troopIndex];
        GameObject prefab = troopData.prefab;
        // Instantiate a new troop
        GameObject newTroop = Instantiate(prefab, position, rotation);
        Debug.Log("Spawning " + newTroop.name + "at: " + position + " with rotation: " + rotation);
        newTroop.transform.localScale = Vector3.one * GameManager.Instance.universalScale.Value;
        Troop troop = newTroop.GetComponent<Troop>();
        troop.team = clientTeam;
        troop.DataIndex = troopIndex;
        // troop.Data = troopData; // Automatically set by setting index
        NetworkObject newTroopNetworkObject = newTroop.GetComponent<NetworkObject>();
        newTroopNetworkObject.SpawnWithOwnership(clientId);
    }

    public void OnTroopDeath(Troop troop)
    {
        troops.Remove(troop);
    }

    public void AddTroop(Troop troop)
    {
        troops.Add(troop);
    }
}