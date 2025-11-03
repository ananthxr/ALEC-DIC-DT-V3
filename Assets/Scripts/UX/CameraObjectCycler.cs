using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CameraObjectCycler : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float movementDuration = 1.5f;

    [Header("UI References")]
    [SerializeField] private Button scrollThroughButton;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button backToHomeButton;
    [SerializeField] private TextMeshProUGUI objectNameLabel;

    [Header("References")]
    [SerializeField] private MaterialTransitionManager materialTransitionManager;

    private List<GameObject> activeElements = new List<GameObject>();
    private int currentIndex = 0;
    private bool isCycling = false;
    private bool isMoving = false;

    // Store original camera position/rotation for "Back to Home"
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;

    // Cache CameraController reference
    private CameraController cameraController;

    private void Awake()
    {
        // Find CameraController in scene
        cameraController = FindObjectOfType<CameraController>();
        if (cameraController == null)
        {
            Debug.LogWarning("CameraObjectCycler: CameraController not found in scene!");
        }

        // Store original camera position
        if (targetCamera != null)
        {
            originalCameraPosition = targetCamera.transform.position;
            originalCameraRotation = targetCamera.transform.rotation;
        }

        // Setup button listeners
        if (scrollThroughButton != null)
            scrollThroughButton.onClick.AddListener(OnScrollThroughPressed);

        if (leftButton != null)
            leftButton.onClick.AddListener(OnLeftPressed);

        if (rightButton != null)
            rightButton.onClick.AddListener(OnRightPressed);

        if (backToHomeButton != null)
            backToHomeButton.onClick.AddListener(OnBackToHomePressed);

        // Hide cycling UI initially
        SetCyclingUIActive(false);

        // Show scroll through button initially
        if (scrollThroughButton != null)
            scrollThroughButton.gameObject.SetActive(false);
    }

    private void SetCyclingUIActive(bool active)
    {
        if (leftButton != null)
            leftButton.gameObject.SetActive(active);

        if (rightButton != null)
            rightButton.gameObject.SetActive(active);

        if (objectNameLabel != null)
            objectNameLabel.gameObject.SetActive(active);
    }

    public void ShowScrollThroughButton()
    {
        if (scrollThroughButton != null)
        {
            scrollThroughButton.gameObject.SetActive(true);
        }
    }

    public void HideScrollThroughButton()
    {
        if (scrollThroughButton != null)
        {
            scrollThroughButton.gameObject.SetActive(false);
        }
    }

    private void OnScrollThroughPressed()
    {
        if (materialTransitionManager == null)
        {
            Debug.LogWarning("CameraObjectCycler: MaterialTransitionManager reference not assigned!");
            return;
        }

        // Get active elements from MaterialTransitionManager
        activeElements = GetActiveElementsFromManager();

        if (activeElements.Count == 0)
        {
            Debug.LogWarning("CameraObjectCycler: No active elements found!");
            return;
        }

        // Disable CameraController to prevent it from fighting for control
        if (cameraController != null)
        {
            cameraController.enabled = false;
            Debug.Log("CameraObjectCycler: Disabled CameraController for cycling mode");
        }

        // Start cycling mode
        isCycling = true;
        currentIndex = 0;

        // Hide scroll through button
        HideScrollThroughButton();

        // Show cycling UI
        SetCyclingUIActive(true);

        // Move to first object
        MoveToObject(currentIndex);

        Debug.Log($"CameraObjectCycler: Started cycling with {activeElements.Count} objects");
    }

    private List<GameObject> GetActiveElementsFromManager()
    {
        List<GameObject> elements = new List<GameObject>();

        // Use reflection to access private activeElements list
        var field = typeof(MaterialTransitionManager).GetField("activeElements",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var activeElementsList = field.GetValue(materialTransitionManager) as List<GameObject>;
            if (activeElementsList != null)
            {
                elements.AddRange(activeElementsList);
            }
        }

        return elements;
    }

    private void OnLeftPressed()
    {
        if (!isCycling || isMoving || activeElements.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = 0; // Stop at first object
        }

        MoveToObject(currentIndex);
        UpdateButtonStates();
    }

    private void OnRightPressed()
    {
        if (!isCycling || isMoving || activeElements.Count == 0) return;

        currentIndex++;
        if (currentIndex >= activeElements.Count)
        {
            currentIndex = activeElements.Count - 1; // Stop at last object
        }

        MoveToObject(currentIndex);
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (leftButton != null)
        {
            // Blur/disable left button if at first object
            leftButton.interactable = (currentIndex > 0);
        }

        if (rightButton != null)
        {
            // Blur/disable right button if at last object
            rightButton.interactable = (currentIndex < activeElements.Count - 1);
        }
    }

    private void MoveToObject(int index)
    {
        if (index < 0 || index >= activeElements.Count) return;

        GameObject targetObject = activeElements[index];
        if (targetObject == null)
        {
            Debug.LogWarning($"CameraObjectCycler: Object at index {index} is null!");
            return;
        }

        // Find Cycle_Camera_Position transform
        Transform cameraPositionTransform = FindCameraPosition(targetObject);

        if (cameraPositionTransform == null)
        {
            Debug.LogWarning($"CameraObjectCycler: 'Cycle_Camera_Position' transform not found on {targetObject.name}");
            return;
        }

        // Update label
        if (objectNameLabel != null)
        {
            objectNameLabel.text = targetObject.name;
        }

        // Move camera
        StartCoroutine(MoveCameraToTarget(cameraPositionTransform.position, cameraPositionTransform.rotation));

        Debug.Log($"CameraObjectCycler: Moving to {targetObject.name} (Index: {index + 1}/{activeElements.Count})");
    }

    private Transform FindCameraPosition(GameObject obj)
    {
        // Search for Cycle_Camera_Position in the object and its children
        Transform[] allTransforms = obj.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.name == "Cycle_Camera_Position")
            {
                return t;
            }
        }

        return null;
    }

    private IEnumerator MoveCameraToTarget(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (targetCamera == null) yield break;

        isMoving = true;

        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;

        float elapsed = 0f;

        while (elapsed < movementDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / movementDuration;

            // Smooth interpolation using SmoothStep
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        // Ensure final position/rotation is exact
        targetCamera.transform.position = targetPosition;
        targetCamera.transform.rotation = targetRotation;

        isMoving = false;

        // Update button states after movement
        UpdateButtonStates();
    }

    private void OnBackToHomePressed()
    {
        if (!isCycling) return;

        // Move camera back to home and then re-enable controls
        StartCoroutine(ReturnToHomeSequence());
    }

    private IEnumerator ReturnToHomeSequence()
    {
        Debug.Log("CameraObjectCycler: Starting return to home sequence");

        // Move camera back to original position
        if (targetCamera != null)
        {
            yield return StartCoroutine(MoveCameraToTarget(originalCameraPosition, originalCameraRotation));
        }

        // After camera movement completes, cleanup and re-enable controller
        isCycling = false;

        // Hide cycling UI
        SetCyclingUIActive(false);

        // Show scroll through button again
        ShowScrollThroughButton();

        // Re-enable CameraController and sync its state
        if (cameraController != null)
        {
            // Sync the camera controller's internal target values before re-enabling
            cameraController.ResetToDefault();
            cameraController.enabled = true;
            Debug.Log("CameraObjectCycler: Re-enabled CameraController");
        }

        // Deactivate highlight if MaterialTransitionManager is active
        if (materialTransitionManager != null && materialTransitionManager.isHighlightActive)
        {
            materialTransitionManager.DeactivateHighlight();
        }

        Debug.Log("CameraObjectCycler: Returned to home view");
    }

    private void OnDestroy()
    {
        // Re-enable camera controller if we're being destroyed while cycling
        if (cameraController != null && isCycling)
        {
            cameraController.enabled = true;
        }

        // Remove listeners
        if (scrollThroughButton != null)
            scrollThroughButton.onClick.RemoveListener(OnScrollThroughPressed);

        if (leftButton != null)
            leftButton.onClick.RemoveListener(OnLeftPressed);

        if (rightButton != null)
            rightButton.onClick.RemoveListener(OnRightPressed);

        if (backToHomeButton != null)
            backToHomeButton.onClick.RemoveListener(OnBackToHomePressed);
    }
}
