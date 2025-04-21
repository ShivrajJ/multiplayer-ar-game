using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class TroopController : NetworkBehaviour
{
    private static readonly int Run = Animator.StringToHash("Run");
    private static readonly int Walk = Animator.StringToHash("Walk");
    private static readonly int Attack = Animator.StringToHash("Attack 0");

    public enum AIState
    {
        Idle,
        Chasing,
        Retreating,
        Attacking
    }

    [Header("Settings")] [SerializeField] private float detectionRange = 5f;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Navigation")] [SerializeField]
    private NavMeshAgent navAgent;

    [SerializeField] private float defenseRadius = 3f;

    [SerializeField] private AIState currentState;

    private Troop _troop;
    private readonly Collider[] _detectedEnemies = new Collider[10];
    [SerializeField] private Health _currentTarget;
    private NetworkAnimator _networkAnimator;
    private Boolean _chasingHomeBase;
    private Vector3 _homePosition;
    private float _lastAttackTime;
    private float _lastAIUpdateTime;

    private TroopManager.AIMode CurrentMode => TroopManager.Instance?.CurrentMode ?? TroopManager.AIMode.Defend;

    private void Awake()
    {
        _troop = GetComponent<Troop>();
        _networkAnimator = GetComponent<NetworkAnimator>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _homePosition = transform.position;
        if (IsOwner)
        {
            InitializeAI();
            navAgent.enabled = true;
            _troop.health.onDeath += OnDeath;
        }
    }

    private void OnDeath(Boolean isDead)
    {
        // Stop all actions
        switch (currentState)
        {
            case AIState.Idle:
                break;
            case AIState.Chasing:
                StopChasing();
                break;
            case AIState.Retreating:
                StopRetreating();
                break;
            case AIState.Attacking:
                StopAttacking();
                break;
        }

        navAgent.isStopped = true;
        navAgent.enabled = false;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_troop.IsDead) return;
        if (navAgent.remainingDistance == 0f)
        {
            navAgent.ResetPath();
            _networkAnimator.Animator.SetBool(Walk, false);
            _networkAnimator.Animator.SetBool(Run, false);
        }
        else
        {
            switch (currentState)
            {
                case AIState.Idle:
                    _networkAnimator.Animator.SetBool(Walk, true);
                    _networkAnimator.Animator.SetBool(Run, false);
                    break;
                case AIState.Chasing:
                case AIState.Retreating:
                    _networkAnimator.Animator.SetBool(Walk, false);
                    _networkAnimator.Animator.SetBool(Run, true);
                    break;
            }
        }

        if (Time.time - _lastAIUpdateTime < updateInterval) return;
        _lastAIUpdateTime = Time.time;
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
        navAgent.enabled = true;
        navAgent.isStopped = false;
        _networkAnimator.ResetTrigger(Attack);
        _networkAnimator.Animator.SetBool(Run, true);
        navAgent.SetDestination(_homePosition);
    }

    private void HandleRetreating()
    {
        if (navAgent.remainingDistance < 0.15f)
        {
            StopRetreating();
        }

        if (CurrentMode == TroopManager.AIMode.Attack)
        {
            DetectEnemies();
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
        _networkAnimator.Animator.SetBool(Walk, true);
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
        _networkAnimator.Animator.SetBool(Walk, false);
        _networkAnimator.Animator.SetBool(Run, true);
    }

    private void HandleChasing()
    {
        if (_currentTarget is null)
        {
            Debug.Log("No target, stopping chase");
            StopChasing();
            return;
        }

        float sqrDistance = Vector3.SqrMagnitude(transform.position - _currentTarget.transform.position);
        switch (CurrentMode)
        {
            case TroopManager.AIMode.Defend:
                float sqrDistanceToHome = Vector3.SqrMagnitude(transform.position - _homePosition);
                if (sqrDistanceToHome >= Math.Pow(defenseRadius, 2))
                {
                    ClearEnemy(false);
                    return;
                }

                break;
            case TroopManager.AIMode.Attack:
                if (!_chasingHomeBase && sqrDistance > Math.Pow(detectionRange, 2))
                {
                    ClearEnemy(false);
                    return;
                }

                break;
        }

        SetClosestDestination(_currentTarget.transform.position);
        if (sqrDistance <= Math.Pow(_troop.AttackRange, 2))
        {
            Debug.Log("Attacking enemy");
            StartAttacking();
            return;
        }

        DetectEnemies();
    }

    private void StopChasing()
    {
        currentState = AIState.Idle;
        _networkAnimator.Animator.SetBool(Run, false);
    }

    private void StartAttacking()
    {
        _networkAnimator.Animator.SetBool(Run, false);
        _networkAnimator.Animator.SetBool(Walk, false);
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
            Debug.Log("OUT OF RANGE, CHASING");
            StartChasing();
            return;
        }

        AttackEnemy(_currentTarget);
        // SetClosestDestination(_currentTarget?.transform.position, _troop.AttackRange);
        // navAgent.isStopped = true;
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
            HomeBase enemyBase = GameManager.Instance.GetEnemyBase();
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
            _networkAnimator.SetTrigger(Attack);
            enemy.TakeDamageServerRpc(_troop.AttackDamage);
            _lastAttackTime = Time.time;
        }
    }

    private void SetInitialDestination()
    {
        if (CurrentMode == TroopManager.AIMode.Defend)
        {
            Vector3 randomPoint = _homePosition + Random.insideUnitSphere * defenseRadius;
            SetClosestDestination(randomPoint);
        }
    }

    private void SetClosestDestination(Vector3? targetPoint, float maxDistance = 2f)
    {
        if (targetPoint is null) return;
        if (NavMesh.SamplePosition((Vector3)targetPoint, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            navAgent.SetDestination(hit.position);
    }

    private void SetEnemy(Health enemy)
    {
        if (_currentTarget == enemy) return;
        _currentTarget = enemy;
        if (enemy.TryGetComponent<HomeBase>(out _))
        {
            _chasingHomeBase = true;
        }
        else
        {
            _chasingHomeBase = false;
        }

        enemy.onDeath += ClearEnemy;
        if (currentState != AIState.Attacking) StartChasing();
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