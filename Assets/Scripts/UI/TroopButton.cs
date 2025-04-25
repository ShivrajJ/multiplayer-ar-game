using System;
using Unity.Multiplayer.Tools.NetStats;
using UnityEngine.UIElements;

[UxmlElement]
public partial class TroopButton : Button
{
    public int Index => parent.parent.IndexOf(parent);
    private Action SpawnAction { get; set; }

    public TroopButton()
    {
    }

    public TroopButton(TroopData troopData)
    {
        iconImage = Background.FromVectorImage(troopData.icon);
        name = troopData.name + "-button";
        SpawnAction = () => TroopManager.Instance.SpawnTroop(Index);
        clicked += SpawnAction;
    }

    ~TroopButton()
    {
        clicked -= SpawnAction;
    }
}