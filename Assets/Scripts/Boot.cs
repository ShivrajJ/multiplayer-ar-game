using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Boot : MonoBehaviour
{
    public enum ConnectionType
    {
        Server,
        Host,
        Client
    }

    [SerializeField] private string sceneToLoad = "Test";
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private GameObject networkDetailsPanel;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;

    private string ipAddress = "127.0.0.1";
    private string port = "7777";
    private ConnectionType connectionType;

    public void StartHost()
    {
        connectionType = ConnectionType.Host;
        networkDetailsPanel.SetActive(true);
        networkDetailsPanel.GetComponentInChildren<TMP_Text>().SetText("HOST");
        if (ipInputField)
            ipInputField.text = "127.0.0.1";
        if (portInputField)
            portInputField.text = "7777";
    }

    public void StartClient()
    {
        connectionType = ConnectionType.Client;
        networkDetailsPanel.SetActive(true);
        networkDetailsPanel.GetComponentInChildren<TMP_Text>().SetText("CLIENT");
    }

    public void StartConnection()
    {
        StartConnection(connectionType);
    }

    public void SetIPAddress(String newIPAddress)
    {
        ipAddress = newIPAddress;
    }

    public void SetPort(String newPort)
    {
        port = newPort;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUILayout.BeginVertical();

        // Radio buttons for Host, Client, Server
        connectionType =
            (ConnectionType)GUILayout.SelectionGrid((int)connectionType, new string[] { "Server", "Host", "Client" },
                3);

        // Input fields for IP and Port
        GUILayout.Label("IP Address");
        ipAddress = GUILayout.TextField(ipAddress, 25);

        GUILayout.Label("Port");
        port = GUILayout.TextField(port, 6);

        // Button to confirm the selection
        if (GUILayout.Button("Confirm"))
        {
            StartConnection(connectionType);
        }

        GUILayout.EndVertical();
    }
#endif

    private void StartConnection(ConnectionType connType)
    {
        NetworkManager.Singleton.GetComponent<UnityTransport>()
            .SetConnectionData(ipAddress, ushort.Parse(port), "0.0.0.0");
        switch (connType)
        {
            case ConnectionType.Server:
                if (NetworkManager.Singleton.StartServer())
                {
                    Debug.Log("Successfully started server on: " + ipAddress + ":" + port);
                }

                NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
                break;
            case ConnectionType.Host:
                if (NetworkManager.Singleton.StartHost())
                {
                    Debug.Log("Successfully started host on: " + ipAddress + ":" + port);
                }

                NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
                break;
            case ConnectionType.Client:
                if (NetworkManager.Singleton.StartClient())
                {
                    Debug.Log("Successfully started client connected to: " + ipAddress + ":" + port);
                }

                break;
        }
    }
}