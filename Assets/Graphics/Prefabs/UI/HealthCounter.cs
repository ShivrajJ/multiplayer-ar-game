using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class HealthCounter : MonoBehaviour
{
    [SerializeField] private Slider mainSlider;
    [SerializeField] private TextMeshProUGUI sliderText;
    [SerializeField] private GameObject referencedEntity;
    private Health healthSystem;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        if (referencedEntity == null || !referencedEntity.TryGetComponent(out healthSystem))
        {
            healthSystem = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Health>();
        }

        if(mainSlider == null) mainSlider = GetComponent<Slider>();
        if (!healthSystem.IsOwner)
        {
            mainSlider.fillRect.GetComponent<Image>().color = new Color(0.6f, 0.3f, 1, 1);
        }
        else
        {
            mainSlider.fillRect.GetComponent<Image>().color = Color.red;
        }

        mainSlider.maxValue = healthSystem.maxHealth.Value;
        mainSlider.value = healthSystem.health.Value;
        if(sliderText) sliderText.text = mainSlider.maxValue.ToString();
        
        healthSystem.health.OnValueChanged += UpdateHealthCounter;
        
        if (healthSystem != null)
        {
            UpdateHealthCounter(healthSystem.maxHealth.Value, healthSystem.health.Value);
            healthSystem.health.OnValueChanged += UpdateHealthCounter;
            healthSystem.maxHealth.OnValueChanged += UpdateMaxHealthCounter;
            if(sliderText)
            {
                sliderText.text = mainSlider.maxValue.ToString();
                if(sliderText) mainSlider.onValueChanged.AddListener((value) => { sliderText.text = value.ToString("0"); });
            }
        }
        else
        {
            Debug.LogError("HealthSystem script not found in the scene.");
        }
    }

    // Method to update the health counter UI
    public void UpdateHealthCounter(float oldHealth, float newHealth)
    {
        mainSlider.value = newHealth;
    }

    public void UpdateMaxHealthCounter(float oldHealth, float newHealth)
    {
        mainSlider.maxValue = newHealth;
    }
}