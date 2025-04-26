using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HomeBaseUpgrades", menuName = "Scriptable Objects/HomeBaseUpgrades")]
public class HomeBaseUpgrades : ScriptableObject
{
    public List<HomeBase.Upgrade> upgrades;
}
