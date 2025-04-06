using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(TroopController))]
public class Troop : NetworkBehaviour
{
    private TroopData _data;

    public TroopData Data
    {
        get => _data;
        set
        {
            if (IsServer)
            {
                _data = value;
            }
        }
    }

    [Header("Settings")] [SerializeField] private float attackDamage = 5.0f;
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float deathTimeSeconds = 5;
    [SerializeField] private float killRewardRatio = 0.5f;
    public float AttackDamage => attackDamage;
    public float AttackRange => attackRange;
    public float AttackCooldown => attackCooldown;

    [Header("Properties")] public Health health;
    public Team team;

    public Boolean IsDead => health.IsDead;
    private TroopController _controller;

    private void Awake()
    {
        _controller = GetComponent<TroopController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            health.onDeath += OnDeath;
        }

        if (IsOwner)
        {
            team = GameManager.Instance.team;
        }
    }

    private void OnDeath(bool obj)
    {
        if (!IsServer) return;
        // Add gold to enemy base
        GameManager.Instance.HomeBases[team == Team.Red ? Team.Blue : Team.Red].gold.Value +=
            _data.price * killRewardRatio;
        // Play death animation
        // Remove from list of troops
        TroopManager.Instance.OnTroopDeath(this);
        // Start Coroutine to destroy the troop
        StartCoroutine(DestroyTroop());
    }

    private IEnumerator DestroyTroop()
    {
        yield return new WaitForSeconds(deathTimeSeconds);
        NetworkObject.Despawn(true);
    }

    public void StartRetreating()
    {
        _controller.StartRetreating();
    }
}