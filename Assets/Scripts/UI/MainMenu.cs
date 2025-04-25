using UnityEngine;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public UIDocument uiDocument;
    public UIDocument hostUIScreen;
    public UIDocument clientUIScreen;
    private Button hostButton;
    private Button clientButton;

    public void OnEnable()
    {
        uiDocument ??= GetComponent<UIDocument>();
        hostButton = uiDocument.rootVisualElement.Q<Button>("host-button");
        clientButton = uiDocument.rootVisualElement.Q<Button>("client-button");

        if (hostUIScreen is null || clientUIScreen is null)
        {
            Debug.LogError("Missing Host or Client UI screen reference");
            return;
        }
        
        hostButton.RegisterCallback<ClickEvent>(OnHostButtonClicked);
        clientButton.RegisterCallback<ClickEvent>(OnClientButtonClicked);
    }

    private void OnDisable()
    {
        hostButton.UnregisterCallback<ClickEvent>(OnHostButtonClicked);
        clientButton.UnregisterCallback<ClickEvent>(OnClientButtonClicked);
    }

    private void OnHostButtonClicked(ClickEvent clickEvent)
    {
        hostUIScreen.gameObject.SetActive(true);
    }

    private void OnClientButtonClicked(ClickEvent clickEvent)
    {
        clientUIScreen.gameObject.SetActive(true);
    }
}
