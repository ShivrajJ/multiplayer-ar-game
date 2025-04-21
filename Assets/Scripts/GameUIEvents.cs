using UnityEngine;
using UnityEngine.UIElements;

public class GameUIEvents : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Button _defendButton;
    private Button _attackButton;

    void Start()
    {
        uiDocument = uiDocument != null ? uiDocument : GetComponent<UIDocument>();
        _defendButton = uiDocument.rootVisualElement.Q<Button>("defendButton");
        _attackButton = uiDocument.rootVisualElement.Q<Button>("attackButton");

        _defendButton.RegisterCallback<ClickEvent>(OnDefendButton);
        _attackButton.RegisterCallback<ClickEvent>(OnAttackButton);
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