using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(TroopController))]
public class Troop : NetworkBehaviour
{
    [Header("Settings")] [SerializeField] private float attackDamage = 5.0f;
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    public float AttackDamage => attackDamage;
    public float AttackRange => attackRange;
    public float AttackCooldown => attackCooldown;

    public Team team;

    private TroopController _controller;

    private void Awake()
    {
        _controller = GetComponent<TroopController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            team = GameManager.Instance.team;
        }
    }

    [Header("Properties")] public Health health;

    public void StartRetreating()
    {
        _controller.StartRetreating();
    }
}