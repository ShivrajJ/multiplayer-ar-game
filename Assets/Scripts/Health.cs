using System;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    public NetworkVariable<float> health = new NetworkVariable<float>(100f);
    public NetworkVariable<float> maxHealth = new NetworkVariable<float>(100f);

    public Action<Boolean> onDeath;
    public Boolean IsDead => health.Value <= 0;

    private void TakeDamage(float damage)
    {
        if (!IsServer) return;
        health.Value -= damage;
        if (health.Value <= 0)
        {
            health.Value = 0;
            Die();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage)
    {
        TakeDamage(damage);
    }

    public void Die()
    {
        onDeath?.Invoke(true);
    }
}