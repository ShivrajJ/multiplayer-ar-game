using Unity.Netcode;

public class HomeBase : NetworkBehaviour
{
    public Health health;
    public NetworkVariable<float> gold = new(100f);
    public NetworkVariable<float> income = new(10f);
    public Team team;

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
}
