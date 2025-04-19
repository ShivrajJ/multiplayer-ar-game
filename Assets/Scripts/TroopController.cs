using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class TroopController : NetworkBehaviour
{
    public enum AIState
    {
        Idle,
        Chasing,
        Retreating,
        Attacking
    }

    [Header("Settings")] [SerializeField] private float detectionRange = 5f;
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("Navigation")] [SerializeField]
    private NavMeshAgent navAgent;

    [SerializeField] private float defenseRadius = 3f;

    [SerializeField] private AIState currentState;

    private Troop _troop;
    private readonly Collider[] _detectedEnemies = new Collider[10];
    private Health _currentTarget;
    private Animator _animator;
    private Boolean _chasingHomeBase;
    private Vector3 _homePosition;
    private float _lastAttackTime;

    private TroopManager.AIMode CurrentMode => TroopManager.Instance?.CurrentMode ?? TroopManager.AIMode.Attack;

    private void Awake()
    {
        _troop = GetComponent<Troop>();
        _animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            InitializeAI();
            navAgent.enabled = true;
        }

        _homePosition = transform.position;
    }

    private void Update()
    {
        if (!IsOwner) return;
        switch (currentState)
        {
            case AIState.Idle:
                HandleIdle();
                break;
            case AIState.Chasing:
                HandleChasing();
                break;
            case AIState.Retreating:
                HandleRetreating();
                break;
            case AIState.Attacking:
                HandleAttacking();
                break;
        }
    }

    public void StartRetreating()
    {
        ClearEnemy(false);
        currentState = AIState.Retreating;
        navAgent.SetDestination(_homePosition);
    }

    private void HandleRetreating()
    {
        if (navAgent.remainingDistance < 0.5f)
        {
            StopRetreating();
        }
    }

    private void StopRetreating()
    {
        currentState = AIState.Idle;
        SetInitialDestination();
    }

    private void HandleIdle()
    {
        switch (CurrentMode)
        {
            case TroopManager.AIMode.Defend:
                HandleIdleDefense();
                break;
            case TroopManager.AIMode.Attack:
                HandleIdleAttack();
                break;
        }
    }

    private void HandleIdleDefense()
    {
        // Patrol around home position
        _animator.SetBool("Walk", true);
        if (navAgent.remainingDistance < 0.25f)
        {
            Vector3 randomPoint = _homePosition + Random.insideUnitSphere * defenseRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
            }
        }

        DetectEnemies();
    }

    private void HandleIdleAttack()
    {
        DetectEnemies();
    }

    private void StartChasing()
    {
        currentState = AIState.Chasing;
        navAgent.isStopped = false;
        _animator.SetBool("Walk", false);
        _animator.SetBool("Run", true);
    }

    private void HandleChasing()
    {
        if (_currentTarget is null)
        {
            StopChasing();
            return;
        }

        float sqrDistance = Vector3.SqrMagnitude(transform.position - _currentTarget.transform.position);
        if (!_chasingHomeBase && sqrDistance > Math.Pow(detectionRange, 2))
        {
            ClearEnemy(false);
            return;
        }

        if (CurrentMode == TroopManager.AIMode.Defend)
        {
            float sqrDistanceToHome = Vector3.SqrMagnitude(transform.position - _homePosition);
            if (sqrDistanceToHome > Math.Pow(defenseRadius, 2))
            {
                ClearEnemy(false);
                return;
            }
        }

        if (sqrDistance <= _troop.AttackRange)
        {
            StartAttacking();
            return;
        }

        navAgent.SetDestination(_currentTarget.transform.position);
        DetectEnemies();
    }

    private void StopChasing()
    {
        currentState = AIState.Idle;
        _animator.SetBool("Run", false);
    }

    private void StartAttacking()
    {
        _animator.SetBool("Run", false);
        _animator.SetBool("Walk", false);
        currentState = AIState.Attacking;
        navAgent.isStopped = true;
    }

    private void HandleAttacking()
    {
        if (_currentTarget is null)
        {
            StopAttacking();
            return;
        }

        float sqrDistance = Vector3.SqrMagnitude(transform.position - _currentTarget.transform.position);
        if (sqrDistance > Math.Pow(_troop.AttackRange, 2))
        {
            StartChasing();
            return;
        }

        AttackEnemy(_currentTarget);
        DetectEnemies();
    }

    private void StopAttacking()
    {
        currentState = AIState.Idle;
        navAgent.isStopped = false;
    }

    private void InitializeAI()
    {
        SetInitialDestination();
    }

    private void DetectEnemies()
    {
        _currentTarget = null;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRange,
            _detectedEnemies,
            enemyLayerMask
        );

        for (int i = 0; i < count; i++)
            if (_detectedEnemies[i].TryGetComponent<Troop>(out Troop enemyTroop))
                if (enemyTroop.team != _troop.team && enemyTroop.health.health.Value > 0)
                {
                    SetEnemy(enemyTroop.health);
                    return;
                }

        if (CurrentMode == TroopManager.AIMode.Attack)
        {
            HomeBase enemyBase = GameManager.Instance.GetEnemyBase(OwnerClientId);
            if (enemyBase.TryGetComponent<Health>(out Health enemyBaseHealth)
                && enemyBaseHealth.health.Value > 0)
                SetEnemy(enemyBaseHealth);
        }
    }

    private void AttackEnemy(Health enemy)
    {
        transform.LookAt(enemy.transform.position);
        // Play attack animation

        // Deal damage if cooldown has passed
        if (Time.time - _lastAttackTime >= _troop.AttackCooldown)
        {
            _animator.SetBool("Attack", true);
            enemy.TakeDamageServerRpc(_troop.AttackDamage);
            _lastAttackTime = Time.time;
        }
        else
        {
            _animator.SetBool("Attack", false);
        }
    }

    private void SetInitialDestination()
    {
        if (CurrentMode == TroopManager.AIMode.Defend)
        {
            Vector3 randomPoint = _homePosition + Random.insideUnitSphere * defenseRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                navAgent.SetDestination(hit.position);
        }
    }

    private void SetEnemy(Health enemy)
    {
        _currentTarget = enemy;
        if (enemy.TryGetComponent<HomeBase>(out _))
        {
            _chasingHomeBase = true;
        }

        StartChasing();
        enemy.onDeath += ClearEnemy;
    }

    private void ClearEnemy(Boolean isDead)
    {
        if (_currentTarget) _currentTarget.onDeath -= ClearEnemy;
        _currentTarget = null;
        _chasingHomeBase = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_homePosition, defenseRadius);
    }
#endif
}