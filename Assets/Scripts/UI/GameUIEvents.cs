using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

public class GameUIEvents : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private TroopData[] troops;
    [SerializeField] private VectorImage homeBaseIcon;
    
    private Button _defendButton;
    private Button _attackButton;
    public Button homeBaseUpgradeButton;
    private Label _homeBaseUpgradeLabel;
    private Label _homeBaseHealthLabel;
    private Label _goldLabel;
    private ScrollView _scrollView;

    private void OnEnable()
    {
        PopulateUIReferences();
        PopulateTopButtons();
        RegisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        _defendButton.RegisterCallback<ClickEvent>(OnDefendButton);
        _attackButton.RegisterCallback<ClickEvent>(OnAttackButton);
    }

    private void PopulateUIReferences()
    {
        uiDocument = uiDocument != null ? uiDocument : GetComponent<UIDocument>();
        _defendButton = uiDocument.rootVisualElement.Q<Button>("defendButton");
        _attackButton = uiDocument.rootVisualElement.Q<Button>("attackButton");
        _scrollView = uiDocument.rootVisualElement.Q<ScrollView>("TroopsView");
        _homeBaseHealthLabel = uiDocument.rootVisualElement.Q<Label>("health-label");
        _goldLabel = uiDocument.rootVisualElement.Q<Label>("gold-label");
    }

    private void PopulateTopButtons()
    {
        // Create the Troop Spawning buttons
        foreach (TroopData troop in troops)
        {
            Button troopBtn = new TroopButton(troop);
            CreateButtonInScrollView(troopBtn, troop.price);
        }
        
        // Create the Castle Upgrade button
        Button castleUpgradeButton = new Button
        {
            iconImage = Background.FromVectorImage(homeBaseIcon)
        };
        VisualElement container = CreateButtonInScrollView(castleUpgradeButton, 0f);
        homeBaseUpgradeButton = container.Children().First() as Button;
        _homeBaseUpgradeLabel = container.Children().Last() as Label;
    }

    private VisualElement CreateButtonInScrollView(Button button, float price)
    {
        button.AddToClassList("troop-button");
        VisualElement container = new VisualElement();
        container.AddToClassList("troop-container");
        _scrollView.Add(container);
        container.Add(button);
        Label label = new Label
        {
            text = price > 0 ? price.ToString(CultureInfo.CurrentCulture) : "-"
        };
        label.AddToClassList("troop-price-label");
        container.Add(label);
        return container;
    }

    public void AddHealthLabel(HomeBase homeBase)
    {
        if (!homeBase.IsOwner) return;
        Assert.IsNotNull(_homeBaseHealthLabel, "Health label not set");
        homeBase.health.health.OnValueChanged += UpdateHealthLabel;
        UpdateHealthLabel(homeBase.health.health.Value, homeBase.health.health.Value);
    }

    public void RemoveHealthLabel(HomeBase homeBase)
    {
        Assert.IsNotNull(_homeBaseHealthLabel, "Health label not set");
        homeBase.health.health.OnValueChanged -= UpdateHealthLabel;
    }

    private void UpdateHealthLabel(float prevHealth, float newHealth)
    {
        _homeBaseHealthLabel.text = newHealth.ToString(CultureInfo.CurrentCulture);
    }

    public void AddGoldLabel(HomeBase homeBase)
    {
        if (!homeBase.IsOwner) return;
        Assert.IsNotNull(_goldLabel, "Gold label not set");
        homeBase.gold.OnValueChanged += UpdateGoldLabel;
        UpdateGoldLabel(homeBase.gold.Value, homeBase.gold.Value);
    }

    public void RemoveGoldLabel(HomeBase homeBase)
    {
        Assert.IsNotNull(_goldLabel, "Gold label not set");
        homeBase.gold.OnValueChanged -= UpdateGoldLabel;
    }

    private void UpdateGoldLabel(float prevGold, float newGold)
    {
        _goldLabel.text = newGold.ToString(CultureInfo.CurrentCulture);
    }

    public void UpgradeHomeBaseLabel(float newCost)
    {
        Assert.IsNotNull(_homeBaseUpgradeLabel, "_homeBaseUpgradeLabel is null!");
        _homeBaseUpgradeLabel.text = newCost.ToString(CultureInfo.CurrentCulture);
    }

    private void OnDisable()
    {
        _defendButton.UnregisterCallback<ClickEvent>(OnDefendButton);
        _attackButton.UnregisterCallback<ClickEvent>(OnAttackButton);
    }

    private void OnDefendButton(ClickEvent evt)
    {
        TroopManager.Instance.SetMode(TroopManager.AIMode.Defend);
    }

    private void OnAttackButton(ClickEvent evt)
    {
        TroopManager.Instance.SetMode(TroopManager.AIMode.Attack);
    }
}