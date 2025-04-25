using System;
using Unity.Netcode;
using UnityEngine;

public class HomeBase : NetworkBehaviour
{
    public Health health;
    public NetworkVariable<float> gold = new(100f);
    public NetworkVariable<float> income = new(10f);
    public Team team;
    private float _timeSinceLastIncome;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        health.onDeath += OnDeath;
        GameManager.Instance.RegisterHomeBase(this);
    }

    private void OnDeath(bool isDead)
    {
        if (IsServer)
        {
            GameManager.Instance.OnHomeBaseDeath(this);
        }
    }
    
    private void Update()
    {
        if (!IsServer) return;
        if (GameManager.Instance.currentGameState.Value == GameManager.GameState.InProgress)
        {
            _timeSinceLastIncome += Time.deltaTime;
            if (_timeSinceLastIncome >= GameManager.Instance.incomeTimeSeconds)
            {
                gold.Value += income.Value;
                _timeSinceLastIncome = 0f;
            }
        }
    }

    public void UpgradeBase()
    {
        Debug.Log("Upgrade Base");
    }
}