using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientUIEvents : MonoBehaviour
{
    public UIDocument uiDocument;
    public Boot bootScript;
    private TextField _ipField;
    private TextField _portField;
    private Button _clientButton;
    private Button _backButton;

    private void OnEnable()
    {
        uiDocument ??= GetComponent<UIDocument>();
        bootScript ??= FindAnyObjectByType<Boot>();
        
        _ipField = uiDocument.rootVisualElement.Q<TextField>("ip-field");
        _portField = uiDocument.rootVisualElement.Q<TextField>("port-field");
        _clientButton = uiDocument.rootVisualElement.Q<Button>("client-button");
        _backButton = uiDocument.rootVisualElement.Q<Button>("back-button");

        _clientButton.RegisterCallback<ClickEvent>(OnClientButtonClicked);
        _backButton.RegisterCallback<ClickEvent>(OnBackButtonClicked);
    }

    private void OnDisable()
    {
        _clientButton.UnregisterCallback<ClickEvent>(OnClientButtonClicked);
        _backButton.UnregisterCallback<ClickEvent>(OnBackButtonClicked);
    }

    private void OnClientButtonClicked(ClickEvent clickEvent)
    {
        string ipAddress = _ipField.text;
        string port = _portField.text;

        bootScript.SetIPAddress(ipAddress);
        bootScript.SetPort(port);
        bootScript.StartConnection(Boot.ConnectionType.Client);
    }
    
    private void OnBackButtonClicked(ClickEvent clickEvent)
    {
        gameObject.SetActive(false);
    }
}
