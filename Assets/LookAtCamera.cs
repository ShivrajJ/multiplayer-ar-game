using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private Camera _targetCamera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (Camera.main is null)
        {
            Debug.LogError("Main Camera is Null!");
            enabled = false;
        }
        _targetCamera = Camera.main;
    }

    // Update is called once per frame
    private void Update()
    {
        if (_targetCamera is null) return;
        transform.LookAt(_targetCamera.transform);
        transform.Rotate(0,180,0);

    }
}
