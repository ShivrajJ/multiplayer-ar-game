using System;
using Unity.Netcode;
using UnityEngine.Serialization;

public class Health : NetworkBehaviour
{
    public NetworkVariable<float> health = new NetworkVariable<float>(100f);
    public NetworkVariable<float> maxHealth = new NetworkVariable<float>(100f);

    public Action<Boolean> onDeath;
    public bool isDead = false;

    private void TakeDamage(float damage)
    {
        if (!IsServer || isDead) return;
        health.Value -= damage;
        if (health.Value <= 0)
        {
            health.Value = 0;
            DieClientRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage)
    {
        TakeDamage(damage);
    }

    [ClientRpc(RequireOwnership = false)]
    private void DieClientRpc()
    {
        Die();
    }

    private void Die()
    {
        isDead = true;
        onDeath?.Invoke(true);
    }
}