using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "TroopData", menuName = "Troops/New Troop")]
public class TroopData : ScriptableObject
{
    public Image icon;
    public string troopName;
    public GameObject prefab;
    public float price;
    public float spawnTime;
}