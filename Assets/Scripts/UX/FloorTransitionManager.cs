using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public enum FloorState
{
    None,       // All floors visible, default camera
    Ground,     // Ground floor active, others hidden
    First,      // First floor active, others hidden
    Roof        // Roof active, others hidden
}

public enum CameraViewState
{
    Default,        // Main overview camera
    FloorNormal,    // Normal side view of selected floor
    FloorTopView    // Top-down view of selected floor
}

[System.Serializable]
public class FloorData
{
    [Header("Floor Information")]
    public string floorName;
    public GameObject floorObject;

    [Header("Floor Entity ID")]
    [Tooltip("Entity ID for this floor from the server (used for alarm filtering)")]
    public string floorEntityId = "";

    [Header("Button Reference")]
    [Tooltip("The button that triggers transition to this floor (optional)")]
    public UnityEngine.UI.Button floorButton;

    [Header("Position Transforms")]
    public Transform originPosition;     // Where floor normally sits (Origin_Ground, etc.)
    public Transform outPosition;        // Where floor goes when hidden (OutPosition_Ground, etc.)
    public Transform cameraPosition;     // Camera position for this floor (CamPos_Ground, etc.)
    public Transform topViewPosition;    // Top-down camera position for this floor (TopView_Ground, etc.)

    [Header("Stored Origin Values (Auto-populated)")]
    [SerializeField] private Vector3 storedOriginPosition;    // Immutable origin position
    [SerializeField] private Quaternion storedOriginRotation; // Immutable origin rotation

    // Public accessors for stored origin values
    public Vector3 StoredOriginPosition => storedOriginPosition;
    public Quaternion StoredOriginRotation => storedOriginRotation;

    // Method to set stored origin values (called once at initialization)
    public void SetStoredOriginValues(Vector3 position, Quaternion rotation)
    {
        storedOriginPosition = position;
        storedOriginRotation = rotation;
    }
}

[System.Serializable]
public class NPCBuildingData
{
    [Header("Building Information")]
    public string buildingName;
    public GameObject buildingObject;

    [Header("Hide Direction (Choose Axis)")]
    [Tooltip("Move building along positive X-axis to hide")]
    public bool hideOnPositiveX = false;
    [Tooltip("Move building along negative X-axis to hide")]
    public bool hideOnNegativeX = false;

    [Tooltip("Move building along positive Y-axis to hide")]
    public bool hideOnPositiveY = false;
    [Tooltip("Move building along negative Y-axis to hide")]
    public bool hideOnNegativeY = false;

    [Tooltip("Move building along positive Z-axis to hide")]
    public bool hideOnPositiveZ = false;
    [Tooltip("Move building along negative Z-axis to hide")]
    public bool hideOnNegativeZ = false;

    [Header("Hide Distance")]
    [Tooltip("How far to move the building (default: 1000 units)")]
    public float hideDistance = 1000f;

    [Header("Stored Origin Values (Auto-populated)")]
    [SerializeField] private Vector3 storedOriginPosition;    // Immutable origin position
    [SerializeField] private Quaternion storedOriginRotation; // Immutable origin rotation

    // Public accessors for stored origin values
    public Vector3 StoredOriginPosition => storedOriginPosition;
    public Quaternion StoredOriginRotation => storedOriginRotation;

    // Method to set stored origin values (called once at initialization)
    public void SetStoredOriginValues(Vector3 position, Quaternion rotation)
    {
        storedOriginPosition = position;
        storedOriginRotation = rotation;
    }

    // Calculate target hide position based on boolean axes
    public Vector3 CalculateHidePosition()
    {
        Vector3 offset = Vector3.zero;

        // X-axis
        if (hideOnPositiveX) offset.x += hideDistance;
        if (hideOnNegativeX) offset.x -= hideDistance;

        // Y-axis
        if (hideOnPositiveY) offset.y += hideDistance;
        if (hideOnNegativeY) offset.y -= hideDistance;

        // Z-axis
        if (hideOnPositiveZ) offset.z += hideDistance;
        if (hideOnNegativeZ) offset.z -= hideDistance;

        return storedOriginPosition + offset;
    }
}

public class FloorTransitionManager : MonoBehaviour
{
    [Header("Floor Configuration")]
    [SerializeField] private List<FloorData> floors = new List<FloorData>();

    [Header("NPC Buildings")]
    [SerializeField] private List<NPCBuildingData> npcBuildings = new List<NPCBuildingData>();
    [Tooltip("Animation speed for all NPC buildings (default: 0.75s)")]
    [SerializeField] private float npcBuildingSpeed = 0.75f;

    [Header("Camera Configuration")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform defaultCameraPosition; // CamPos_Default
    [SerializeField] private CameraController cameraController; // Camera controller reference

    [Header("Ground Plane")]
    [Tooltip("Ground plane object to hide during floor transitions")]
    [SerializeField] private GameObject groundPlane;

    [Header("Alarm Integration")]
    [Tooltip("Reference to MasterAlarm for floor-based alarm filtering")]
    [SerializeField] private MasterAlarm masterAlarm;

    [Header("Animation Settings")]
    [SerializeField] private float animationSpeedMultiplier = 1.0f;  // Master speed control
    [SerializeField] private float floorMoveDuration = 0.75f;
    [SerializeField] private float cameraDuration = 1.5f; // Increased for smoother movement
    [SerializeField] private float cameraWaitTime = 0.8f;           // How long to wait before moving camera
    [SerializeField] private Ease floorEase = Ease.InOutQuart;
    [SerializeField] private Ease cameraEase = Ease.InOutCubic; // Changed for smoother camera movement
    
    [Header("3-Step Animation Settings")]
    [SerializeField] private float liftDuration = 0.4f;      // Step 1: Lift up
    [SerializeField] private float forwardDuration = 0.3f;   // Step 2: Move forward  
    [SerializeField] private float exitDuration = 0.5f;     // Step 3: Exit backward
    
    [Header("Burger-Style Lift Heights")]
    [SerializeField] private float baseLiftHeight = 4f;      // Height for floor directly above/below selected
    [SerializeField] private float additionalLiftPerFloor = 1.5f; // Extra height for each floor further away
    [SerializeField] private float forwardOffset = 50f;     // X-axis forward movement
    [SerializeField] private float exitDistance = 500f;     // How far to move out of camera
    
    // State management
    private FloorState currentState = FloorState.None;
    private CameraViewState currentCameraView = CameraViewState.Default;
    private bool isTransitioning = false;

    // Root building entity ID for showing all alarms (when in None state)
    private const string ROOT_BUILDING_ENTITY_ID = "54549790-77e9-11ef-8f9b-033ad0625bc8";

    // Event system for UI updates
    public System.Action OnFloorStateChanged;
    
    private void Start()
    {
        InitializeSystem();
    }
    
    private void InitializeSystem()
    {
        Debug.Log("[FloorTransitionManager] Initializing floor transition system");

        // Auto-populate origin positions from current floor positions
        AutoPopulateOriginPositions();

        // Validate all floor data
        for (int i = 0; i < floors.Count; i++)
        {
            FloorData floor = floors[i];
            Debug.Log($"[FloorTransitionManager] Floor {i}: {floor.floorName}");

            if (floor.floorObject == null)
                Debug.LogError($"Floor {floor.floorName}: FloorObject is null!");
            if (floor.originPosition == null)
                Debug.LogError($"Floor {floor.floorName}: OriginPosition transform is null!");
            if (floor.outPosition == null)
                Debug.LogError($"Floor {floor.floorName}: OutPosition transform is null!");
            if (floor.cameraPosition == null)
                Debug.LogError($"Floor {floor.floorName}: CameraPosition transform is null!");
            if (floor.topViewPosition == null)
                Debug.LogError($"Floor {floor.floorName}: TopViewPosition transform is null!");
        }

        if (mainCamera == null)
            Debug.LogError("[FloorTransitionManager] Main Camera is null!");
        if (defaultCameraPosition == null)
            Debug.LogError("[FloorTransitionManager] Default Camera Position is null!");
        if (cameraController == null)
            Debug.LogWarning("[FloorTransitionManager] CameraController is null! Camera mode switching will not work.");

        // Set initial camera mode to constrained (no free panning at default view)
        DisableFreeExplorationMode();

        // Set all floors to origin positions (they should already be there)
        ResetAllFloorsToOrigin();
        StartCoroutine(ResetCameraToDefault());

        // Setup button listeners
        SetupButtonListeners();

        Debug.Log($"[FloorTransitionManager] ✅ System initialized. Current state: {currentState}");
        Debug.Log($"[FloorTransitionManager] Animation Speed Multiplier: {animationSpeedMultiplier}x");
    }

    private void SetupButtonListeners()
    {
        Debug.Log("[FloorTransitionManager] Setting up button listeners for floor buttons");

        for (int i = 0; i < floors.Count; i++)
        {
            FloorData floor = floors[i];
            if (floor.floorButton != null)
            {
                int floorIndex = i; // Capture for closure
                floor.floorButton.onClick.AddListener(() => OnFloorButtonClicked(floorIndex));
                Debug.Log($"[FloorTransitionManager] ✅ Added listener to button for {floor.floorName} (Index: {floorIndex})");
            }
            else
            {
                Debug.LogWarning($"[FloorTransitionManager] No button assigned for {floor.floorName}");
            }
        }
    }

    private void OnFloorButtonClicked(int floorIndex)
    {
        Debug.Log($"[FloorTransitionManager] 🎯 Floor button clicked: {floors[floorIndex].floorName} (Index: {floorIndex})");

        // FIRST: Hide ground plane before transition
        HideGroundPlane();

        // SECOND: Start floor transition animation
        SelectFloor(floorIndex);

        // THIRD: Update alarm filter AFTER animation completes (delayed to avoid jitter)
        // The floor transition provides visual feedback while alarms load in the background
        FloorData selectedFloor = floors[floorIndex];
        if (masterAlarm != null && !string.IsNullOrEmpty(selectedFloor.floorEntityId))
        {
            StartCoroutine(UpdateAlarmFilterAfterDelay(selectedFloor.floorEntityId, 1.5f));
        }
        else if (masterAlarm == null)
        {
            Debug.LogWarning("[FloorTransitionManager] MasterAlarm reference is not assigned - cannot update alarm filter");
        }
        else if (string.IsNullOrEmpty(selectedFloor.floorEntityId))
        {
            Debug.LogWarning($"[FloorTransitionManager] Floor '{selectedFloor.floorName}' has no Entity ID assigned - cannot filter alarms");
        }
    }

    private IEnumerator UpdateAlarmFilterAfterDelay(string entityId, float delay)
    {
        // Wait for floor transition animation to complete before updating alarms
        yield return new WaitForSeconds(delay);

        if (masterAlarm != null)
        {
            Debug.Log($"[FloorTransitionManager] Animation complete - now updating alarm filter to: {entityId}");
            masterAlarm.UpdateFloorEntityFilter(entityId);
        }
    }

    private void HideGroundPlane()
    {
        if (groundPlane != null)
        {
            if (groundPlane.activeSelf)
            {
                groundPlane.SetActive(false);
                Debug.Log("[FloorTransitionManager] ✅ Ground plane hidden");
            }
        }
        else
        {
            Debug.LogWarning("[FloorTransitionManager] Ground plane not assigned - cannot hide");
        }
    }

    private void ShowGroundPlane()
    {
        if (groundPlane != null)
        {
            if (!groundPlane.activeSelf)
            {
                groundPlane.SetActive(true);
                Debug.Log("[FloorTransitionManager] ✅ Ground plane shown");
            }
        }
    }
    
    private void AutoPopulateOriginPositions()
    {
        Debug.Log("[FloorTransitionManager] Auto-populating and storing immutable origin positions from current floor positions");
        
        for (int i = 0; i < floors.Count; i++)
        {
            FloorData floor = floors[i];
            if (floor.floorObject != null)
            {
                // Store the CURRENT position as immutable origin values
                Vector3 currentPos = floor.floorObject.transform.position;
                Quaternion currentRot = floor.floorObject.transform.rotation;
                
                // Store these values permanently in FloorData
                floor.SetStoredOriginValues(currentPos, currentRot);
                Debug.Log($"[FloorTransitionManager] 🔒 STORED immutable origin for {floor.floorName}: {currentPos}");
                
                // Create/update origin position transform (optional, for visual reference)
                if (floor.originPosition == null)
                {
                    GameObject originGO = new GameObject($"Origin_{floor.floorName}");
                    originGO.transform.position = currentPos;
                    originGO.transform.rotation = currentRot;
                    floor.originPosition = originGO.transform;
                    
                    Debug.Log($"[FloorTransitionManager] Created Origin_{floor.floorName} transform at: {currentPos}");
                }
                else
                {
                    // Update existing origin position transform (but we'll use stored values, not this transform)
                    floor.originPosition.position = currentPos;
                    floor.originPosition.rotation = currentRot;
                    
                    Debug.Log($"[FloorTransitionManager] Updated Origin_{floor.floorName} transform to: {currentPos}");
                }
            }
        }
        
        // Auto-create default camera position if it doesn't exist
        if (defaultCameraPosition == null && mainCamera != null)
        {
            GameObject defaultCamGO = new GameObject("CamPos_Default");
            defaultCamGO.transform.position = mainCamera.transform.position;
            defaultCamGO.transform.rotation = mainCamera.transform.rotation;
            defaultCameraPosition = defaultCamGO.transform;
            
            Debug.Log($"[FloorTransitionManager] Created CamPos_Default at position: {defaultCamGO.transform.position}");
        }

        Debug.Log("[FloorTransitionManager] ✅ Origin positions stored as immutable values - they will never change!");

        // Auto-populate NPC building origin positions
        AutoPopulateNPCBuildingOrigins();
    }

    private void AutoPopulateNPCBuildingOrigins()
    {
        Debug.Log("[FloorTransitionManager] Auto-populating NPC building origin positions");

        for (int i = 0; i < npcBuildings.Count; i++)
        {
            NPCBuildingData npcBuilding = npcBuildings[i];
            if (npcBuilding.buildingObject != null)
            {
                // Store the CURRENT position as immutable origin values
                Vector3 currentPos = npcBuilding.buildingObject.transform.position;
                Quaternion currentRot = npcBuilding.buildingObject.transform.rotation;

                // Store these values permanently
                npcBuilding.SetStoredOriginValues(currentPos, currentRot);
                Debug.Log($"[FloorTransitionManager] 🔒 STORED immutable origin for NPC Building '{npcBuilding.buildingName}': {currentPos}");
            }
            else
            {
                Debug.LogWarning($"[FloorTransitionManager] ⚠️ NPC Building at index {i} has null buildingObject!");
            }
        }

        Debug.Log($"[FloorTransitionManager] ✅ {npcBuildings.Count} NPC building origins stored!");
    }

    public void SelectFloor(int floorIndex, bool skipCameraMovement = false)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[FloorTransitionManager] Already transitioning, ignoring request");
            return;
        }

        if (floorIndex < 0 || floorIndex >= floors.Count)
        {
            Debug.LogError($"[FloorTransitionManager] Invalid floor index: {floorIndex}");
            return;
        }

        FloorState targetState = (FloorState)(floorIndex + 1); // Ground=1, First=2, Roof=3
        string floorName = floors[floorIndex].floorName;

        Debug.Log($"[FloorTransitionManager] SelectFloor called: {floorName} (Index: {floorIndex}, State: {targetState}, SkipCamera: {skipCameraMovement})");
        Debug.Log($"[FloorTransitionManager] Current state: {currentState} -> Target state: {targetState}");

        // Always transition to the selected floor (no toggle behavior)
        Debug.Log($"[FloorTransitionManager] Transitioning to {targetState}");
        StartCoroutine(TransitionToState(targetState, skipCameraMovement));
    }
    
    public void ResetToNoneState()
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[FloorTransitionManager] Already transitioning, ignoring reset request");
            return;
        }

        Debug.Log("[FloorTransitionManager] Manual reset to None state requested");

        currentCameraView = CameraViewState.Default; // Reset camera view state
        StartCoroutine(TransitionToState(FloorState.None));

        // Update alarm filter AFTER animation completes (delayed to avoid jitter)
        if (masterAlarm != null)
        {
            StartCoroutine(UpdateAlarmFilterAfterDelay(ROOT_BUILDING_ENTITY_ID, 1.5f));
        }
        else
        {
            Debug.LogWarning("[FloorTransitionManager] MasterAlarm reference is not assigned - cannot reset alarm filter");
        }
    }
    
    public void ToggleTopView()
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[FloorTransitionManager] Already transitioning, ignoring top view toggle");
            return;
        }
        
        if (currentState == FloorState.None)
        {
            Debug.LogWarning("[FloorTransitionManager] No floor selected - cannot enter top view");
            return;
        }
        
        Debug.Log($"[FloorTransitionManager] Toggling top view. Current camera state: {currentCameraView}");

        if (currentCameraView == CameraViewState.FloorTopView)
        {
            // Switch to normal view
            StartCoroutine(TransitionToNormalView());
        }
        else if (currentCameraView == CameraViewState.FloorNormal)
        {
            // Switch back to top view
            StartCoroutine(TransitionToTopView());
        }
    }
    
    private IEnumerator TransitionToState(FloorState targetState, bool skipCameraMovement = false)
    {
        isTransitioning = true;
        Debug.Log($"[FloorTransitionManager] 🎬 Starting transition: {currentState} -> {targetState} (SkipCamera: {skipCameraMovement})");

        // FIRST: Prepare camera by moving it to default position (only if NOT skipping camera)
        if (!skipCameraMovement && cameraController != null && targetState != FloorState.None)
        {
            Debug.Log("[FloorTransitionManager] Preparing camera for floor transition...");
            cameraController.PrepareForFloorTransition();

            // Wait for camera to reach default position
            while (cameraController.IsTransitioning)
            {
                yield return null;
            }
            Debug.Log("[FloorTransitionManager] Camera preparation complete");
        }
        else if (skipCameraMovement)
        {
            Debug.Log("[FloorTransitionManager] ⏭️ Skipping camera preparation - room navigation will handle it");
        }

        if (targetState == FloorState.None)
        {
            // Bring all floors back to origin and reset camera
            currentCameraView = CameraViewState.Default;
            yield return StartCoroutine(TransitionToNoneState());
        }
        else
        {
            // If already in another floor state, reset to None first
            if (currentState != FloorState.None && currentState != targetState)
            {
                Debug.Log("[FloorTransitionManager] Coming from another floor - resetting to None first");
                yield return StartCoroutine(TransitionToNoneState());
                yield return new WaitForSeconds(0.2f); // Small delay between transitions
            }

            // STEP 1: Hide NPC buildings FIRST before any floor transitions
            Debug.Log("[FloorTransitionManager] 🎬 STEP 1: Hiding NPC buildings first...");
            yield return StartCoroutine(HideNPCBuildings());
            Debug.Log("[FloorTransitionManager] ✅ NPC buildings cleared - area is ready!");

            // STEP 2: Now proceed with floor transitions
            int selectedFloorIndex = (int)targetState - 1;

            currentCameraView = CameraViewState.FloorTopView;
            yield return StartCoroutine(TransitionToFloorState(selectedFloorIndex, skipCameraMovement));
        }

        currentState = targetState;
        isTransitioning = false;

        Debug.Log($"[FloorTransitionManager] ✅ Transition completed. New state: {currentState}");

        // Notify UI components of state change
        OnFloorStateChanged?.Invoke();
    }
    
    private IEnumerator HideNPCBuildings()
    {
        Debug.Log("[FloorTransitionManager] 🏢 Hiding NPC buildings...");

        if (npcBuildings.Count == 0)
        {
            Debug.Log("[FloorTransitionManager] No NPC buildings to hide");
            yield break;
        }

        List<Coroutine> hideCoroutines = new List<Coroutine>();

        // Calculate actual duration using common speed variable
        float actualDuration = npcBuildingSpeed / animationSpeedMultiplier;

        // Move all NPC buildings to their calculated hide positions
        for (int i = 0; i < npcBuildings.Count; i++)
        {
            NPCBuildingData npcBuilding = npcBuildings[i];
            if (npcBuilding.buildingObject != null)
            {
                Vector3 hidePosition = npcBuilding.CalculateHidePosition();
                Debug.Log($"[FloorTransitionManager] Moving '{npcBuilding.buildingName}' from {npcBuilding.StoredOriginPosition} to {hidePosition} (duration: {actualDuration}s)");

                Transform buildingTransform = npcBuilding.buildingObject.transform;

                // Use DOTween to move building to hide position
                buildingTransform.DOMove(hidePosition, actualDuration).SetEase(floorEase).OnComplete(() =>
                {
                    // Deactivate building when it reaches hide position
                    npcBuilding.buildingObject.SetActive(false);
                    Debug.Log($"[FloorTransitionManager] 🚫 '{npcBuilding.buildingName}' deactivated at hide position");
                });
            }
        }

        // Wait for the animation duration to complete
        yield return new WaitForSeconds(actualDuration);

        Debug.Log("[FloorTransitionManager] ✅ All NPC buildings hidden and deactivated");
    }

    private IEnumerator ShowNPCBuildings()
    {
        Debug.Log("[FloorTransitionManager] 🏢 Showing NPC buildings...");

        if (npcBuildings.Count == 0)
        {
            Debug.Log("[FloorTransitionManager] No NPC buildings to show");
            yield break;
        }

        List<Coroutine> showCoroutines = new List<Coroutine>();

        // Calculate actual duration using common speed variable
        float actualDuration = npcBuildingSpeed / animationSpeedMultiplier;

        // Move all NPC buildings back to their origin positions
        for (int i = 0; i < npcBuildings.Count; i++)
        {
            NPCBuildingData npcBuilding = npcBuildings[i];
            if (npcBuilding.buildingObject != null)
            {
                // Activate building before moving it back
                if (!npcBuilding.buildingObject.activeSelf)
                {
                    npcBuilding.buildingObject.SetActive(true);
                    Debug.Log($"[FloorTransitionManager] ✅ '{npcBuilding.buildingName}' activated for return animation");
                }

                Debug.Log($"[FloorTransitionManager] Returning '{npcBuilding.buildingName}' from {npcBuilding.buildingObject.transform.position} to {npcBuilding.StoredOriginPosition} (duration: {actualDuration}s)");

                Transform buildingTransform = npcBuilding.buildingObject.transform;

                // Use DOTween to move building back to origin
                buildingTransform.DOMove(npcBuilding.StoredOriginPosition, actualDuration).SetEase(floorEase);
            }
        }

        // Wait for the animation duration to complete
        yield return new WaitForSeconds(actualDuration);

        Debug.Log("[FloorTransitionManager] ✅ All NPC buildings back to origin and visible");
    }

    private IEnumerator TransitionToNoneState()
    {
        Debug.Log("[FloorTransitionManager] Transitioning to None state - bringing all floors back");

        // Show ground plane when returning to default view
        ShowGroundPlane();

        // Disable free exploration mode when returning to default
        DisableFreeExplorationMode();

        List<Coroutine> floorCoroutines = new List<Coroutine>();

        // Bring all floors back to their STORED origin positions (immutable)
        for (int i = 0; i < floors.Count; i++)
        {
            FloorData floor = floors[i];
            if (floor.floorObject != null)
            {
                // Activate floor GameObject before moving it back
                if (!floor.floorObject.activeSelf)
                {
                    floor.floorObject.SetActive(true);
                    Debug.Log($"[FloorTransitionManager] ✅ {floor.floorName} - Activated for return animation");
                }

                Debug.Log($"[FloorTransitionManager] Returning {floor.floorName} to STORED origin position: {floor.StoredOriginPosition}");
                floorCoroutines.Add(StartCoroutine(MoveFloorToPosition(floor, floor.StoredOriginPosition, floorMoveDuration / animationSpeedMultiplier)));
            }
        }

        // Show NPC buildings back (simultaneously with floor movements)
        StartCoroutine(ShowNPCBuildings());

        // Reset camera to default position
        StartCoroutine(MoveCameraToPosition(defaultCameraPosition));

        // Wait for all floor movements to complete
        foreach (var coroutine in floorCoroutines)
        {
            yield return coroutine;
        }

        Debug.Log("[FloorTransitionManager] ✅ All floors returned to origin positions");
    }
    
    private IEnumerator TransitionToFloorState(int selectedFloorIndex, bool skipCameraMovement = false)
    {
        Debug.Log($"[FloorTransitionManager] Transitioning to floor state: {floors[selectedFloorIndex].floorName} (SkipCamera: {skipCameraMovement})");

        List<Coroutine> hideCoroutines = new List<Coroutine>();

        // Start burger-style lift animations for other floors
        for (int i = 0; i < floors.Count; i++)
        {
            if (i != selectedFloorIndex)
            {
                FloorData floor = floors[i];
                Debug.Log($"[FloorTransitionManager] Hiding {floor.floorName} with burger-style lift animation");

                // Calculate lift height based on floor position relative to selected floor
                float liftHeight = CalculateBurgerLiftHeight(i, selectedFloorIndex);

                hideCoroutines.Add(StartCoroutine(HideFloorWithBurgerLift(floor, liftHeight)));
            }
            else
            {
                FloorData selectedFloor = floors[i];
                Debug.Log($"[FloorTransitionManager] {selectedFloor.floorName} stays in place (selected - bottom bun)");
                // Ensure selected floor is at STORED origin position (immutable)
                if (selectedFloor.floorObject != null)
                {
                    selectedFloor.floorObject.transform.position = selectedFloor.StoredOriginPosition;
                    selectedFloor.floorObject.transform.rotation = selectedFloor.StoredOriginRotation;
                    Debug.Log($"[FloorTransitionManager] Set {selectedFloor.floorName} to stored origin: {selectedFloor.StoredOriginPosition}");
                }
            }
        }

        // Only move camera to top view if NOT skipping camera movement
        if (!skipCameraMovement)
        {
            // Wait for your custom camera delay time
            Debug.Log($"[FloorTransitionManager] Waiting {cameraWaitTime} seconds before moving camera...");
            yield return new WaitForSeconds(cameraWaitTime);
            Debug.Log("[FloorTransitionManager] ✅ Camera wait time complete! Now moving camera...");

            // NOW move camera to selected floor top view position
            FloorData targetFloor = floors[selectedFloorIndex];
            if (targetFloor.topViewPosition != null)
            {
                StartCoroutine(MoveCameraToPosition(targetFloor.topViewPosition));
            }

            // Enable free exploration mode for top view (allows panning)
            EnableFreeExplorationMode();
        }
        else
        {
            Debug.Log("[FloorTransitionManager] ⏭️ Skipping camera movement to top view - room navigation will control camera");
        }

        // Wait for all floor animations to complete
        foreach (var coroutine in hideCoroutines)
        {
            yield return coroutine;
        }

        Debug.Log($"[FloorTransitionManager] ✅ Floor state transition completed for {floors[selectedFloorIndex].floorName}");
    }
    
    private float CalculateBurgerLiftHeight(int floorIndex, int selectedFloorIndex)
    {
        // Calculate relative position to selected floor
        int relativePosition = floorIndex - selectedFloorIndex;
        
        if (relativePosition == 0)
        {
            // This is the selected floor - shouldn't happen in current logic
            return 0f;
        }
        
        float liftHeight;
        
        if (relativePosition > 0)
        {
            // Floor is ABOVE selected floor - lift UP (positive Y)
            int floorsAbove = relativePosition;
            liftHeight = baseLiftHeight + (additionalLiftPerFloor * (floorsAbove - 1));
            Debug.Log($"[FloorTransitionManager] 🔺 {floors[floorIndex].floorName} is {floorsAbove} floors ABOVE selected → Lift UP: +{liftHeight}");
        }
        else
        {
            // Floor is BELOW selected floor - lift DOWN (negative Y) 
            int floorsBelow = Math.Abs(relativePosition);
            liftHeight = -(baseLiftHeight + (additionalLiftPerFloor * (floorsBelow - 1)));
            Debug.Log($"[FloorTransitionManager] 🔻 {floors[floorIndex].floorName} is {floorsBelow} floors BELOW selected → Lift DOWN: {liftHeight}");
        }
        
        return liftHeight;
    }
    
    private IEnumerator HideFloorWithBurgerLift(FloorData floor, float liftHeight)
    {
        if (floor.floorObject == null)
            yield break;

        Transform floorTransform = floor.floorObject.transform;
        Vector3 startPos = floor.StoredOriginPosition; // Use immutable stored position

        Debug.Log($"[FloorTransitionManager] 🍔 {floor.floorName} - Burger Lift Animation starting from: {startPos}");

        // Step 1: Lift to calculated height (Y-axis only, preserve X and Z)
        // Positive liftHeight = UP, Negative liftHeight = DOWN
        Vector3 liftPos = new Vector3(startPos.x, startPos.y + liftHeight, startPos.z);
        string direction = liftHeight >= 0 ? "UP" : "DOWN";
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 1: Lifting {direction} by {Math.Abs(liftHeight)} → {liftPos}");
        yield return floorTransform.DOMove(liftPos, liftDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();

        // Step 2: Move forward (X-axis movement, preserve lifted Y)
        Vector3 forwardPos = new Vector3(startPos.x + forwardOffset, startPos.y + liftHeight, startPos.z);
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 2: Moving forward to {forwardPos}");
        yield return floorTransform.DOMove(forwardPos, forwardDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();

        // Step 3: Quick exit far away (preserve lifted Y, move far in X)
        Vector3 exitPos = new Vector3(startPos.x + exitDistance, startPos.y + liftHeight, startPos.z);
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 3: Exiting to {exitPos}");
        yield return floorTransform.DOMove(exitPos, exitDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();

        // Step 4: Deactivate floor GameObject when it reaches out position
        floor.floorObject.SetActive(false);
        Debug.Log($"[FloorTransitionManager] 🚫 {floor.floorName} - Deactivated at out position");

        Debug.Log($"[FloorTransitionManager] ✅ 🍔 {floor.floorName} - Burger lift animation completed at height {liftHeight}");
    }
    
    private IEnumerator HideFloorWithBurgerLiftPhased(FloorData floor, float liftHeight, List<Coroutine> liftPhaseCoroutines, List<Coroutine> exitPhaseCoroutines)
    {
        if (floor.floorObject == null)
            yield break;
        
        Transform floorTransform = floor.floorObject.transform;
        Vector3 startPos = floor.StoredOriginPosition;
        
        Debug.Log($"[FloorTransitionManager] 🍔 {floor.floorName} - Phased Burger Lift Animation starting from: {startPos}");
        
        // Phase 1: Lift (Y-axis) + Forward (X-axis) - CAMERA WAITS FOR THIS
        var liftPhaseCoroutine = StartCoroutine(BurgerLiftPhase(floor, liftHeight));
        liftPhaseCoroutines.Add(liftPhaseCoroutine);
        yield return liftPhaseCoroutine;
        
        // Phase 2: Exit (far away) - CAMERA DOESN'T WAIT FOR THIS
        var exitPhaseCoroutine = StartCoroutine(BurgerExitPhase(floor, liftHeight));
        exitPhaseCoroutines.Add(exitPhaseCoroutine);
        yield return exitPhaseCoroutine;
        
        Debug.Log($"[FloorTransitionManager] ✅ 🍔 {floor.floorName} - Phased animation completed");
    }
    
    private IEnumerator BurgerLiftPhase(FloorData floor, float liftHeight)
    {
        Transform floorTransform = floor.floorObject.transform;
        Vector3 startPos = floor.StoredOriginPosition;
        
        // Step 1: Lift to calculated height (Y-axis only)
        Vector3 liftPos = new Vector3(startPos.x, startPos.y + liftHeight, startPos.z);
        string direction = liftHeight >= 0 ? "UP" : "DOWN";
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Lift Phase: Lifting {direction} by {Math.Abs(liftHeight)} → {liftPos}");
        yield return floorTransform.DOMove(liftPos, liftDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();

        // Step 2: Move forward slightly (X-axis movement, preserve lifted Y)
        Vector3 forwardPos = new Vector3(startPos.x + forwardOffset, startPos.y + liftHeight, startPos.z);
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Lift Phase: Moving forward to {forwardPos}");
        yield return floorTransform.DOMove(forwardPos, forwardDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();
        
        Debug.Log($"[FloorTransitionManager] ✅ {floor.floorName} - Lift phase completed! (Camera can move now)");
    }
    
    private IEnumerator BurgerExitPhase(FloorData floor, float liftHeight)
    {
        Transform floorTransform = floor.floorObject.transform;
        Vector3 startPos = floor.StoredOriginPosition;
        
        // Step 3: Quick exit far away (preserve lifted Y, move far in X)
        Vector3 exitPos = new Vector3(startPos.x + exitDistance, startPos.y + liftHeight, startPos.z);
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Exit Phase: Exiting to {exitPos}");
        yield return floorTransform.DOMove(exitPos, exitDuration / animationSpeedMultiplier).SetEase(floorEase).WaitForCompletion();
        
        Debug.Log($"[FloorTransitionManager] ✅ {floor.floorName} - Exit phase completed!");
    }
    
    private IEnumerator HideFloorWithThreeSteps(FloorData floor)
    {
        if (floor.floorObject == null || floor.originPosition == null || floor.outPosition == null)
            yield break;
        
        Transform floorTransform = floor.floorObject.transform;
        Vector3 startPos = floor.StoredOriginPosition; // Use immutable stored position
        Vector3 endPos = floor.outPosition.position;
        
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - 3-Step Animation: {startPos} -> {endPos}");
        
        // Step 1: Lift up slightly
        Vector3 liftPos = startPos + Vector3.up * 50f; // Configurable lift amount
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 1: Lifting to {liftPos}");
        yield return floorTransform.DOMove(liftPos, liftDuration / animationSpeedMultiplier).SetEase(Ease.OutCirc).WaitForCompletion();
        
        // Step 2: Move forward
        Vector3 forwardPos = liftPos + Vector3.forward * 100f; // Configurable forward amount
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 2: Moving forward to {forwardPos}");
        yield return floorTransform.DOMove(forwardPos, forwardDuration / animationSpeedMultiplier).SetEase(Ease.InOutSine).WaitForCompletion();
        
        // Step 3: Quick exit to out position
        Debug.Log($"[FloorTransitionManager] {floor.floorName} - Step 3: Exiting to {endPos}");
        yield return floorTransform.DOMove(endPos, exitDuration / animationSpeedMultiplier).SetEase(Ease.InExpo).WaitForCompletion();
        
        Debug.Log($"[FloorTransitionManager] ✅ {floor.floorName} - 3-step animation completed");
    }
    
    private IEnumerator MoveFloorToPosition(FloorData floor, Vector3 targetPosition, float duration)
    {
        if (floor.floorObject == null) yield break;
        
        Debug.Log($"[FloorTransitionManager] Moving {floor.floorName} to {targetPosition}");
        yield return floor.floorObject.transform.DOMove(targetPosition, duration).SetEase(floorEase).WaitForCompletion();
        Debug.Log($"[FloorTransitionManager] ✅ {floor.floorName} moved to target position");
    }
    
    private IEnumerator MoveCameraToPosition(Transform targetTransform)
    {
        if (targetTransform == null) yield break;

        Debug.Log($"[FloorTransitionManager] 🎥 Moving camera via CameraController to {targetTransform.name}");

        // Calculate actual duration (respecting speed multiplier)
        float actualDuration = cameraDuration / animationSpeedMultiplier;

        // Use CameraController for centralized camera movement
        bool moveCompleted = false;
        if (cameraController != null)
        {
            cameraController.MoveToPositionSmooth(targetTransform, actualDuration, () => moveCompleted = true);
        }
        else
        {
            Debug.LogWarning("[FloorTransitionManager] CameraController not found! Using fallback direct movement");
            // Fallback to direct movement
            Transform cameraTransform = mainCamera.transform;
            cameraTransform.DOMove(targetTransform.position, actualDuration).SetEase(cameraEase);
            yield return cameraTransform.DORotateQuaternion(targetTransform.rotation, actualDuration).SetEase(cameraEase).WaitForCompletion();
            moveCompleted = true;
        }

        // Wait for movement to complete
        while (!moveCompleted)
        {
            yield return null;
        }

        Debug.Log($"[FloorTransitionManager] ✅ Camera moved to {targetTransform.name} via CameraController");
    }
    
    private void ResetAllFloorsToOrigin()
    {
        for (int i = 0; i < floors.Count; i++)
        {
            FloorData floor = floors[i];
            if (floor.floorObject != null)
            {
                // Use immutable stored origin values
                floor.floorObject.transform.position = floor.StoredOriginPosition;
                floor.floorObject.transform.rotation = floor.StoredOriginRotation;
                Debug.Log($"[FloorTransitionManager] Reset {floor.floorName} to stored origin: {floor.StoredOriginPosition}");
            }
        }
    }
    
    private IEnumerator ResetCameraToDefault()
    {
        if (defaultCameraPosition != null)
        {
            Debug.Log("[FloorTransitionManager] 🎥 Resetting camera to default via CameraController");

            // Calculate actual duration (respecting speed multiplier)
            float actualDuration = cameraDuration / animationSpeedMultiplier;

            // Use CameraController for centralized camera movement
            bool resetCompleted = false;
            if (cameraController != null)
            {
                cameraController.MoveToPositionSmooth(defaultCameraPosition, actualDuration, () => resetCompleted = true);
            }
            else
            {
                Debug.LogWarning("[FloorTransitionManager] CameraController not found! Using fallback direct reset");
                // Fallback to direct movement
                Transform cameraTransform = mainCamera.transform;
                cameraTransform.DOMove(defaultCameraPosition.position, actualDuration).SetEase(cameraEase);
                yield return cameraTransform.DORotateQuaternion(defaultCameraPosition.rotation, actualDuration).SetEase(cameraEase).WaitForCompletion();
                resetCompleted = true;
            }

            // Wait for reset to complete
            while (!resetCompleted)
            {
                yield return null;
            }

            Debug.Log("[FloorTransitionManager] ✅ Camera reset to default via CameraController");
        }
    }
    
    private IEnumerator TransitionToTopView()
    {
        isTransitioning = true;
        Debug.Log("[FloorTransitionManager] 🔄 Transitioning to top view");
        
        int currentFloorIndex = (int)currentState - 1;
        FloorData currentFloor = floors[currentFloorIndex];
        
        if (currentFloor.topViewPosition != null)
        {
            yield return StartCoroutine(MoveCameraToPosition(currentFloor.topViewPosition));
            currentCameraView = CameraViewState.FloorTopView;
            Debug.Log($"[FloorTransitionManager] ✅ Now in top view for {currentFloor.floorName}");
        }
        else
        {
            Debug.LogError($"[FloorTransitionManager] No top view position set for {currentFloor.floorName}!");
        }
        
        isTransitioning = false;
        
        // Notify UI components of camera view change
        OnFloorStateChanged?.Invoke();
    }
    
    private IEnumerator TransitionToNormalView()
    {
        isTransitioning = true;
        Debug.Log("[FloorTransitionManager] 🔄 Transitioning back to normal view");
        
        int currentFloorIndex = (int)currentState - 1;
        FloorData currentFloor = floors[currentFloorIndex];
        
        if (currentFloor.cameraPosition != null)
        {
            yield return StartCoroutine(MoveCameraToPosition(currentFloor.cameraPosition));
            currentCameraView = CameraViewState.FloorNormal;
            Debug.Log($"[FloorTransitionManager] ✅ Back to normal view for {currentFloor.floorName}");
        }
        else
        {
            Debug.LogError($"[FloorTransitionManager] No camera position set for {currentFloor.floorName}!");
        }
        
        isTransitioning = false;
        
        // Notify UI components of camera view change
        OnFloorStateChanged?.Invoke();
    }
    
    // Public properties
    public bool IsTransitioning => isTransitioning;
    public FloorState CurrentState => currentState;
    public CameraViewState CurrentCameraView => currentCameraView;
    public int CurrentFloorIndex => currentState == FloorState.None ? -1 : (int)currentState - 1;
    public bool CanUseTopView => currentState != FloorState.None;

    // Camera mode control methods
    private void EnableFreeExplorationMode()
    {
        if (cameraController != null)
        {
            cameraController.SetCameraMode(true); // true = free exploration with panning
            Debug.Log("[FloorTransitionManager] ✅ Enabled free exploration mode - panning available");
        }
    }

    private void DisableFreeExplorationMode()
    {
        if (cameraController != null)
        {
            cameraController.SetCameraMode(false); // false = constrained mode
            Debug.Log("[FloorTransitionManager] 🔒 Disabled free exploration mode - constrained camera");
        }
    }
}