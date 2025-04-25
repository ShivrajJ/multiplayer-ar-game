using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(TroopController))]
public class Troop : NetworkBehaviour
{
    private static readonly int Death = Animator.StringToHash("Death");
    private int _dataIndex;

    public int DataIndex
    {
        get => _dataIndex;
        set
        {
            _dataIndex = value;
            Data = TroopManager.Instance.AvailableTroops[_dataIndex];
        }
    }

    public TroopData Data
    {
        get
        {
            if (_data is null)
            {
                _data = TroopManager.Instance.AvailableTroops[_dataIndex];
            }

            return _data;
        }
        set => _data = value;
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

    public Boolean IsDead => health.isDead;
    private TroopController _controller;
    private TroopData _data;

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
            TroopManager.Instance.AddTroop(this);
        }
        else
        {
            team = GameManager.Instance.GetEnemyTeam();
        }
    }

    private void OnDeath(bool obj)
    {
        if (!IsServer) return;
        // Add gold to enemy base
        GameManager.Instance.HomeBases[team == Team.Red ? Team.Blue : Team.Red].gold.Value +=
            Data.price * killRewardRatio;
        // Play death animation

        Animator animator = GetComponent<Animator>();
        animator.SetBool("Run", false);
        animator.SetBool("Walk", false);
        GetComponent<NetworkAnimator>().ResetTrigger("Attack");
        animator.SetBool(Death, true);
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