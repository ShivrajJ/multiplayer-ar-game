using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class GameUIEvents : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private TroopData[] troops;
    [SerializeField] private VectorImage homeBaseIcon;
    
    private Button _defendButton;
    private Button _attackButton;
    private ScrollView _scrollView;

    void OnEnable()
    {
        PopulateUIReferences();
        PopulateTopButtons();
        RegisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        _defendButton.RegisterCallback<ClickEvent>(OnDefendButton);
        _attackButton.RegisterCallback<ClickEvent>(OnAttackButton);
        if (_scrollView.Children().Last() is Button castleUpgradeButton)
        {
            HomeBase homeBase = GameManager.Instance.HomeBases[GameManager.Instance.team];
            castleUpgradeButton.clicked += homeBase.UpgradeBase;
        }
    }

    private void PopulateUIReferences()
    {
        uiDocument = uiDocument != null ? uiDocument : GetComponent<UIDocument>();
        _defendButton = uiDocument.rootVisualElement.Q<Button>("defendButton");
        _attackButton = uiDocument.rootVisualElement.Q<Button>("attackButton");
        _scrollView = uiDocument.rootVisualElement.Q<ScrollView>("TroopsView");
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
        Button castleUpgradeButton = new Button();
        castleUpgradeButton.iconImage = Background.FromVectorImage(homeBaseIcon);
        // TODO: Add Homebase upgrade cost to label.
        CreateButtonInScrollView(castleUpgradeButton, 15f);
    }

    private void CreateButtonInScrollView(Button button, float price)
    {
        button.AddToClassList("troop-button");
        VisualElement container = new VisualElement();
        container.AddToClassList("troop-container");
        _scrollView.Add(container);
        container.Add(button);
        Label label = new Label(price.ToString(CultureInfo.CurrentCulture));
        label.AddToClassList("troop-price-label");
        container.Add(label);
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