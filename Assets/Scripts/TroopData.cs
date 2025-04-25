using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "TroopData", menuName = "Troops/New Troop")]
public class TroopData : ScriptableObject
{
    public VectorImage icon;
    public string troopName;
    public GameObject prefab;
    public float price;
    public float spawnTime;
}