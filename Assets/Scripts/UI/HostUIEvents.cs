using System;
using UnityEngine;
using UnityEngine.UIElements;

public class HostUIEvents : MonoBehaviour
{
    public UIDocument uiDocument;
    public Boot bootScript;
    private TextField _ipField;
    private TextField _portField;
    private Button _hostButton;
    private Button _backButton;

    private void OnEnable()
    {
        uiDocument ??= GetComponent<UIDocument>();
        bootScript ??= FindAnyObjectByType<Boot>();
        
        _ipField = uiDocument.rootVisualElement.Q<TextField>("ip-field");
        _portField = uiDocument.rootVisualElement.Q<TextField>("port-field");
        _hostButton = uiDocument.rootVisualElement.Q<Button>("host-button");
        _backButton = uiDocument.rootVisualElement.Q<Button>("back-button");

        _hostButton.RegisterCallback<ClickEvent>(OnHostButtonClicked);
        _backButton.RegisterCallback<ClickEvent>(OnBackButtonClicked);
    }

    private void OnDisable()
    {
        _hostButton.UnregisterCallback<ClickEvent>(OnHostButtonClicked);
        _backButton.UnregisterCallback<ClickEvent>(OnBackButtonClicked);
    }

    private void OnHostButtonClicked(ClickEvent clickEvent)
    {
        string ipAddress = _ipField.text;
        string port = _portField.text;

        bootScript.SetIPAddress(ipAddress);
        bootScript.SetPort(port);
        bootScript.StartConnection(Boot.ConnectionType.Host);
    }

    private void OnBackButtonClicked(ClickEvent clickEvent)
    {
        gameObject.SetActive(false);
    }
}
