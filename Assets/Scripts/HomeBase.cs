using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class HomeBase : NetworkBehaviour
{
    [Serializable]
    public struct Upgrade : INetworkSerializable, IEquatable<Upgrade>
    {
        public float income;
        public float cost;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref income);
            serializer.SerializeValue(ref cost);
        }

        public bool Equals(Upgrade other)
        {
            return income.Equals(other.income) && cost.Equals(other.cost);
        }

        public override bool Equals(object obj)
        {
            return obj is Upgrade other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(income, cost);
        }
    }

    public Health health;
    public NetworkVariable<float> gold = new(100f);
    public Team team;
    [SerializeField] private HomeBaseUpgrades upgradesData;
    [SerializeReference] public NetworkList<Upgrade> upgrades;
    public NetworkVariable<int> upgradeIndex;
    private float _income;
    private float _timeSinceLastIncome;

    public Action<float> OnUpgrade;

    private void Awake()
    {
        upgrades = new NetworkList<Upgrade>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        health.onDeath += OnDeath;
        upgradeIndex.OnValueChanged += UpdateIncome;
        if (IsOwner)
        {
            team = IsHost ? Team.Red : Team.Blue;
            Map map = FindAnyObjectByType<Map>();
            Transform homeBasePoint = team == Team.Red ? map.redHomeBasePoint : map.blueHomeBasePoint;
            transform.localPosition = homeBasePoint.position;
            transform.localRotation = homeBasePoint.rotation;
        }

        GameManager.Instance.RegisterHomeBase(this);
        if (IsOwner)
        {
            InitializeUI();
        }
        if (IsServer)
        {
            foreach (Upgrade upgrade in upgradesData.upgrades)
            {
                upgrades.Add(upgrade);
            }
            upgradeIndex.Value = 0;
        }

        UpdateIncome(0, 0);
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        base.OnOwnershipChanged(previous, current);
        if (IsOwner)
        {
            InitializeUI();
            UpdateIncome(upgradeIndex.Value, upgradeIndex.Value);
        }
        else
        {
            UninitializeUI();
        }
    }

    private void InitializeUI()
    {
        Debug.Log($"Initializing for NetObjID: {NetworkObjectId}");
        GameUIEvents gameUIEvents = GameManager.Instance.GameUIGameObject.GetComponent<GameUIEvents>();
        OnUpgrade += gameUIEvents.UpgradeHomeBaseLabel;
        gameUIEvents.homeBaseUpgradeButton.clicked += UpgradeBaseServerRpc;
        gameUIEvents.AddHealthLabel(this);
        gameUIEvents.AddGoldLabel(this);
    }
    
    private void UninitializeUI()
    {
        Debug.Log($"Uninitializing for NetObjID: {NetworkObjectId}");
        GameUIEvents gameUIEvents = GameManager.Instance.GameUIGameObject.GetComponent<GameUIEvents>();
        OnUpgrade -= gameUIEvents.UpgradeHomeBaseLabel;
        gameUIEvents.homeBaseUpgradeButton.clicked -= UpgradeBaseServerRpc;
        gameUIEvents.RemoveHealthLabel(this);
        gameUIEvents.RemoveGoldLabel(this);
    }


    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        health.onDeath -= OnDeath;
        upgradeIndex.OnValueChanged -= UpdateIncome;
        if (IsOwner)
        {
            GameUIEvents gameUIEvents = GameManager.Instance.GameUIGameObject.GetComponent<GameUIEvents>();
            OnUpgrade -= gameUIEvents.UpgradeHomeBaseLabel;
            gameUIEvents.homeBaseUpgradeButton.clicked -= UpgradeBaseServerRpc;
        }
    }

    private void UpdateIncome(int prevIndex, int newIndex)
    {
        if (newIndex < 0 || newIndex >= upgrades.Count) return;
        _income = upgrades[newIndex].income;
        OnUpgrade?.Invoke(newIndex + 1 >= upgrades.Count ? 0f : upgrades[newIndex + 1].cost);
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
        if (GameManager.Instance.LocalGameState == GameManager.GameState.InProgress)
        {
            _timeSinceLastIncome += Time.deltaTime;
            if (_timeSinceLastIncome >= GameManager.Instance.incomeTimeSeconds)
            {
                gold.Value += _income;
                _timeSinceLastIncome = 0f;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpgradeBaseServerRpc()
    {
        UpgradeBase();
    }

    private void UpgradeBase()
    {
        if (!IsServer) return;

        if (upgradeIndex.Value + 1 < upgrades.Count)
        {
            Upgrade newUpgrade = upgrades[upgradeIndex.Value + 1];
            if (gold.Value >= newUpgrade.cost)
            {
                upgradeIndex.Value += 1;
                gold.Value -= newUpgrade.cost;
            }
        }
    }
}