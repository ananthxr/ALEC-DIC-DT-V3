using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Enhanced Floor Button Stacking System with Rooms Support
///
/// FEATURES:
/// 1. Main Floor Button - Primary button that expands to show floor selection
/// 2. Floor Buttons - Stack vertically below main button for floor selection
/// 3. Horizontal Action Buttons - Slide horizontally from selected floor button
/// 4. NEW: Rooms Button - Appears to the LEFT of main button when expanded
/// 5. NEW: Rooms Scroll View - Scrollable list of room buttons for room selection
///
/// WORKFLOW:
/// - Click Main Floor Button → Floor buttons stack down + Rooms button slides left
/// - Click Floor Button → Horizontal action buttons slide right from that floor
/// - Click Rooms Button → Transition to scrollable room selection view
/// - In Rooms View → User can scroll through and select rooms
///
/// ANIMATION SYSTEM:
/// - Uses DOTween for smooth animations
/// - Staggered timing for cascade effects
/// - Configurable easing curves for each element type
/// </summary>

[System.Serializable]
public class FloorButtonData
{
    public GameObject floorButton;
    public List<GameObject> horizontalButtons = new List<GameObject>();
}

public class FloorButtonStacking : MonoBehaviour
{
    [Header("Floor Button Settings")]
    [SerializeField] private Button mainFloorButton;
    [SerializeField] private List<FloorButtonData> floorButtonsData = new List<FloorButtonData>(); // Each floor with its horizontal buttons
    [SerializeField] private float stackDistance = 250f;
    [SerializeField] private float firstButtonPadding = 100f;
    [SerializeField] private float baseAnimationDuration = 0.8f;
    [SerializeField] private float delayBetweenButtons = 0.1f;
    [SerializeField] private Ease animationEase = Ease.InFlash;

    [Header("Button State Management")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color pressedButtonColor = Color.gray;

    [Header("MasterUI Integration")]
    [SerializeField] private MasterUI masterUI;

    [Header("Floor Transition")]
    [SerializeField] private FloorTransitionManager floorTransitionManager;

    [Header("Horizontal Action Buttons Configuration")]
    [SerializeField] private float horizontalButtonSpacing = 150f; // Spacing between horizontal buttons
    [SerializeField] private bool useAbsoluteYPosition = false; // If true, all horizontal buttons use same Y position
    [SerializeField] private float horizontalButtonVerticalOffset = 0f; // If useAbsoluteYPosition=false: offset from floor button. If true: absolute Y position
    [SerializeField] private float horizontalButtonAnimationDuration = 0.5f;
    [SerializeField] private Ease horizontalAnimationEase = Ease.OutCubic;

    [Header("Rooms Button Configuration")]
    [SerializeField] private GameObject roomsButton; // The button that appears to the left
    [SerializeField] private float roomsButtonLeftDistance = 200f; // Distance to the left of main button
    [SerializeField] private float roomsButtonAnimationDuration = 0.5f;
    [SerializeField] private Ease roomsButtonAnimationEase = Ease.OutCubic;

    [Header("Rooms Scroll View Configuration")]
    [SerializeField] private GameObject roomsScrollView; // The scrollable panel containing room buttons
    [SerializeField] private GameObject roomsContentPanel; // The content panel inside scroll view that holds the buttons
    [SerializeField] private RoomScrollViewController roomScrollViewController; // Controller that manages room data
    [SerializeField] private float scrollViewAnimationDuration = 0.5f;
    [SerializeField] private Ease scrollViewAnimationEase = Ease.OutCubic;

    // State management
    private bool isExpanded = false;
    private bool horizontalButtonsExpanded = false;
    private bool roomsButtonExpanded = false; // Track if rooms button is visible
    private bool roomsScrollViewExpanded = false; // Track if scroll view is shown
    private int currentExpandedFloorIndex = -1; // Which floor button has expanded horizontal buttons
    private Vector2 mainButtonPosition;
    private Vector2 roomsButtonOriginalPosition;
    private Button roomsButtonComponent;

    // Floor button caching
    private List<GameObject> floorButtons = new List<GameObject>(); // Extracted from floorButtonsData
    private List<Vector2> originalFloorPositions = new List<Vector2>();
    private List<Image> floorButtonImages = new List<Image>();
    private List<Button> floorButtonComponents = new List<Button>();
    private int currentSelectedFloorIndex = -1; // -1 means no floor selected

    // Horizontal button caching - per floor
    private List<List<GameObject>> horizontalButtonsPerFloor = new List<List<GameObject>>();
    private List<List<Vector2>> originalHorizontalPositionsPerFloor = new List<List<Vector2>>();
    private List<List<Image>> horizontalButtonImagesPerFloor = new List<List<Image>>();
    private List<List<Button>> horizontalButtonComponentsPerFloor = new List<List<Button>>();

    private void Start()
    {
        InitializeComponents();
        SetupInitialState();
    }

    private void InitializeComponents()
    {
        if (mainFloorButton == null)
            mainFloorButton = GetComponent<Button>();

        mainButtonPosition = ((RectTransform)mainFloorButton.transform).anchoredPosition;

        // Initialize rooms button
        if (roomsButton != null)
        {
            roomsButtonOriginalPosition = ((RectTransform)roomsButton.transform).anchoredPosition;
            roomsButtonComponent = roomsButton.GetComponent<Button>();
            if (roomsButtonComponent == null)
                roomsButtonComponent = roomsButton.GetComponentInChildren<Button>();

            if (roomsButtonComponent != null)
            {
                roomsButtonComponent.onClick.AddListener(OnRoomsButtonClicked);
                Debug.Log("[FloorButtonStacking] Rooms button listener added");
            }
        }

        // Initialize rooms scroll view
        if (roomsScrollView != null)
        {
            roomsScrollView.SetActive(false);
        }

        // Extract floor buttons and setup caching from floorButtonsData
        foreach (FloorButtonData floorData in floorButtonsData)
        {
            if (floorData.floorButton != null)
            {
                floorButtons.Add(floorData.floorButton);
                originalFloorPositions.Add(((RectTransform)floorData.floorButton.transform).anchoredPosition);

                // Cache Image component (for color changes)
                Image buttonImage = floorData.floorButton.GetComponent<Image>();
                if (buttonImage == null)
                    buttonImage = floorData.floorButton.GetComponentInChildren<Image>();
                floorButtonImages.Add(buttonImage);

                // Cache Button component
                Button buttonComponent = floorData.floorButton.GetComponent<Button>();
                if (buttonComponent == null)
                    buttonComponent = floorData.floorButton.GetComponentInChildren<Button>();
                floorButtonComponents.Add(buttonComponent);

                // Setup horizontal buttons for this floor
                SetupHorizontalButtonsForFloor(floorData.horizontalButtons);
            }
        }

        mainFloorButton.onClick.AddListener(ToggleFloorButtons);

        // Add click listeners to floor buttons
        SetupFloorButtonListeners();

        // Initialize all button colors to normal
        UpdateAllButtonColors();
    }

    private void SetupInitialState()
    {
        // Hide rooms button initially
        if (roomsButton != null)
        {
            RectTransform roomsRect = (RectTransform)roomsButton.transform;
            roomsRect.DOKill();
            roomsButton.SetActive(false);
            roomsRect.anchoredPosition = mainButtonPosition; // Start at main button position
        }

        // Hide all floor buttons initially
        foreach (GameObject floorButton in floorButtons)
        {
            RectTransform buttonRect = (RectTransform)floorButton.transform;
            buttonRect.DOKill();
            floorButton.SetActive(false);
            buttonRect.anchoredPosition = mainButtonPosition;
        }

        // Hide all horizontal buttons initially for all floors
        foreach (List<GameObject> horizontalButtonsList in horizontalButtonsPerFloor)
        {
            foreach (GameObject horizontalButton in horizontalButtonsList)
            {
                if (horizontalButton != null)
                {
                    RectTransform buttonRect = (RectTransform)horizontalButton.transform;
                    buttonRect.DOKill();
                    horizontalButton.SetActive(false);
                }
            }
        }
    }

    private void SetupHorizontalButtonsForFloor(List<GameObject> horizontalButtons)
    {
        Debug.Log($"[FloorButtonStacking] Setting up {horizontalButtons.Count} horizontal buttons for floor {horizontalButtonsPerFloor.Count}");

        List<GameObject> floorHorizontalButtons = new List<GameObject>();
        List<Vector2> floorOriginalPositions = new List<Vector2>();
        List<Image> floorButtonImages = new List<Image>();
        List<Button> floorButtonComponents = new List<Button>();

        int floorIndex = horizontalButtonsPerFloor.Count; // Current floor being setup

        foreach (GameObject horizontalButton in horizontalButtons)
        {
            if (horizontalButton != null)
            {
                floorHorizontalButtons.Add(horizontalButton);
                floorOriginalPositions.Add(((RectTransform)horizontalButton.transform).anchoredPosition);

                // Cache Image component
                Image buttonImage = horizontalButton.GetComponent<Image>();
                if (buttonImage == null)
                    buttonImage = horizontalButton.GetComponentInChildren<Image>();
                floorButtonImages.Add(buttonImage);

                // Cache Button component
                Button buttonComponent = horizontalButton.GetComponent<Button>();
                if (buttonComponent == null)
                    buttonComponent = horizontalButton.GetComponentInChildren<Button>();
                floorButtonComponents.Add(buttonComponent);
            }
        }

        // Add to per-floor collections
        horizontalButtonsPerFloor.Add(floorHorizontalButtons);
        originalHorizontalPositionsPerFloor.Add(floorOriginalPositions);
        horizontalButtonImagesPerFloor.Add(floorButtonImages);
        horizontalButtonComponentsPerFloor.Add(floorButtonComponents);

        // Setup listeners for this floor's horizontal buttons
        SetupHorizontalButtonListenersForFloor(floorIndex, floorButtonComponents);
    }

    private void SetupHorizontalButtonListenersForFloor(int floorIndex, List<Button> buttonComponents)
    {
        Debug.Log($"[FloorButtonStacking] Setting up listeners for {buttonComponents.Count} horizontal buttons on floor {floorIndex}");

        for (int i = 0; i < buttonComponents.Count; i++)
        {
            Button horizontalButtonComponent = buttonComponents[i];

            if (horizontalButtonComponent != null)
            {
                int capturedFloorIndex = floorIndex; // Capture for closure
                int buttonIndex = i; // Capture for closure
                horizontalButtonComponent.onClick.AddListener(() => OnHorizontalButtonClicked(capturedFloorIndex, buttonIndex));
                Debug.Log($"[FloorButtonStacking] Added listener: Floor {floorIndex}, Horizontal Button {i}");
            }
        }
    }

    private void OnHorizontalButtonClicked(int floorIndex, int buttonIndex)
    {
        Debug.Log($"[FloorButtonStacking] Horizontal button {buttonIndex} on floor {floorIndex} clicked!");

        // NOTE: The button itself handles the transition via ButtonTransitionTrigger component
        // FloorButtonStacking only handles the UI animation (collapse)

        // Collapse horizontal buttons after selection
        if (horizontalButtonsExpanded)
        {
            CollapseHorizontalButtons();
        }

        // The ButtonTransitionTrigger component on the button will automatically:
        // 1. Find the TransitionTarget component on the same button
        // 2. Find the FloorTransitionManager in the scene
        // 3. Call floorTransitionManager.TransitionToTarget(transitionTarget)
        Debug.Log($"[FloorButtonStacking] Button's ButtonTransitionTrigger component will handle the floor transition");
    }

    public void ToggleFloorButtons()
    {
        Debug.Log($"[FloorButtonStacking] Main floor button clicked - isExpanded: {isExpanded}");

        // CLOSED-LOOP: If rooms scroll view is open, close it first
        if (roomsScrollViewExpanded)
        {
            Debug.Log("[FloorButtonStacking] Rooms scroll view is open - closing it before toggling floor buttons");
            HideRoomsScrollView();
            return; // Exit - don't toggle floor buttons this time, just close the scroll view
        }

        if (isExpanded)
        {
            Debug.Log("[FloorButtonStacking] Collapsing floor buttons");
            CollapseFloorButtons();
        }
        else
        {
            Debug.Log("[FloorButtonStacking] Expanding floor buttons");
            ExpandFloorButtons();
        }

        UpdateAllButtonColors();

        // Notify MasterUI
        if (masterUI != null)
        {
            masterUI.UpdateUIElementState("FloorButtonStack", isExpanded);
        }
    }

    private void OnRoomsButtonClicked()
    {
        Debug.Log("[FloorButtonStacking] Rooms button clicked!");

        // Transition from floor buttons view to rooms scroll view
        ShowRoomsScrollView();
    }

    private void ShowRoomsScrollView()
    {
        if (roomsScrollView == null)
        {
            Debug.LogWarning("[FloorButtonStacking] Rooms scroll view is not assigned!");
            return;
        }

        Debug.Log("[FloorButtonStacking] Showing rooms scroll view");

        roomsScrollViewExpanded = true;

        // Hide floor buttons and rooms button
        CollapseFloorButtons();
        CollapseRoomsButton();

        // Show the scroll view with animation
        roomsScrollView.SetActive(true);

        // Populate the scroll view with room data
        if (roomScrollViewController != null)
        {
            Debug.Log("[FloorButtonStacking] Populating room list from JSON");
            roomScrollViewController.PopulateRoomList();
        }
        else
        {
            Debug.LogWarning("[FloorButtonStacking] RoomScrollViewController is not assigned - cannot populate rooms");
        }

        // Add fade-in animation for scroll view
        CanvasGroup scrollViewCanvasGroup = roomsScrollView.GetComponent<CanvasGroup>();
        if (scrollViewCanvasGroup == null)
        {
            scrollViewCanvasGroup = roomsScrollView.AddComponent<CanvasGroup>();
        }

        scrollViewCanvasGroup.alpha = 0f;
        scrollViewCanvasGroup.DOFade(1f, scrollViewAnimationDuration)
            .SetEase(scrollViewAnimationEase)
            .SetUpdate(true);
    }

    public void HideRoomsScrollView()
    {
        if (roomsScrollView == null || !roomsScrollViewExpanded)
        {
            Debug.Log("[FloorButtonStacking] Rooms scroll view already hidden or not assigned");
            return;
        }

        Debug.Log("[FloorButtonStacking] Hiding rooms scroll view and returning to floor buttons view");

        roomsScrollViewExpanded = false;

        CanvasGroup scrollViewCanvasGroup = roomsScrollView.GetComponent<CanvasGroup>();
        if (scrollViewCanvasGroup != null)
        {
            scrollViewCanvasGroup.DOFade(0f, scrollViewAnimationDuration)
                .SetEase(scrollViewAnimationEase)
                .SetUpdate(true)
                .OnComplete(() => roomsScrollView.SetActive(false));
        }
        else
        {
            roomsScrollView.SetActive(false);
        }

        // CLOSED-LOOP: Don't automatically expand floor buttons
        // User can click main button again to expand if needed
        // This prevents unwanted state conflicts
        Debug.Log("[FloorButtonStacking] Scroll view closed. Click main button to expand floor buttons again.");
    }

    private void ExpandFloorButtons()
    {
        isExpanded = true;

        // Calculate target positions for floor buttons
        List<Vector2> targetPositions = CalculateStackPositions();

        // Animate floor buttons with staggered start times
        for (int i = 0; i < floorButtons.Count; i++)
        {
            GameObject floorButton = floorButtons[i];
            Vector2 targetPosition = targetPositions[i];
            RectTransform buttonRect = (RectTransform)floorButton.transform;

            buttonRect.DOKill();
            floorButton.SetActive(true);

            float buttonDelay = i * delayBetweenButtons;

            buttonRect.DOAnchorPos(targetPosition, baseAnimationDuration)
                .SetEase(animationEase)
                .SetDelay(buttonDelay)
                .SetUpdate(true);
        }

        // Also expand the rooms button to the left
        ExpandRoomsButton();
    }

    private void ExpandRoomsButton()
    {
        if (roomsButton == null)
        {
            return;
        }

        Debug.Log("[FloorButtonStacking] Expanding rooms button to the left");

        roomsButtonExpanded = true;

        RectTransform roomsRect = (RectTransform)roomsButton.transform;
        roomsRect.DOKill();
        roomsButton.SetActive(true);

        // Calculate target position (to the left of main button)
        Vector2 targetPosition = mainButtonPosition + Vector2.left * roomsButtonLeftDistance;

        roomsRect.DOAnchorPos(targetPosition, roomsButtonAnimationDuration)
            .SetEase(roomsButtonAnimationEase)
            .SetUpdate(true);
    }

    private void CollapseRoomsButton()
    {
        if (roomsButton == null || !roomsButtonExpanded)
        {
            return;
        }

        Debug.Log("[FloorButtonStacking] Collapsing rooms button");

        roomsButtonExpanded = false;

        RectTransform roomsRect = (RectTransform)roomsButton.transform;
        roomsRect.DOKill();

        GameObject buttonToHide = roomsButton;

        roomsRect.DOAnchorPos(mainButtonPosition, roomsButtonAnimationDuration)
            .SetEase(roomsButtonAnimationEase)
            .SetUpdate(true)
            .OnComplete(() => buttonToHide.SetActive(false));
    }

    private void CollapseFloorButtons()
    {
        isExpanded = false;

        // Also collapse any expanded horizontal buttons
        if (horizontalButtonsExpanded)
        {
            CollapseHorizontalButtons();
        }

        // Also collapse the rooms button
        CollapseRoomsButton();

        // Animate floor buttons back in reverse order
        for (int i = 0; i < floorButtons.Count; i++)
        {
            GameObject floorButton = floorButtons[i];
            RectTransform buttonRect = (RectTransform)floorButton.transform;

            buttonRect.DOKill();

            // Reverse order delay for smooth collapse (top buttons start first)
            float buttonDelay = (floorButtons.Count - 1 - i) * delayBetweenButtons;

            GameObject buttonToHide = floorButton;

            buttonRect.DOAnchorPos(mainButtonPosition, baseAnimationDuration)
                .SetEase(animationEase)
                .SetDelay(buttonDelay)
                .SetUpdate(true)
                .OnComplete(() => buttonToHide.SetActive(false));
        }
    }

    private List<Vector2> CalculateStackPositions()
    {
        List<Vector2> positions = new List<Vector2>();

        for (int i = 0; i < floorButtons.Count; i++)
        {
            // Add padding before first button, then use consistent stackDistance
            float totalDistance = firstButtonPadding + (stackDistance * (i + 1));
            Vector2 targetPosition = mainButtonPosition + Vector2.down * totalDistance;
            positions.Add(targetPosition);
        }

        return positions;
    }

    private void SetupFloorButtonListeners()
    {
        Debug.Log($"[FloorButtonStacking] Setting up listeners for {floorButtons.Count} floor buttons");

        for (int i = 0; i < floorButtonComponents.Count; i++)
        {
            Button floorButtonComponent = floorButtonComponents[i];

            if (floorButtonComponent != null)
            {
                int floorIndex = i; // Capture for closure
                floorButtonComponent.onClick.AddListener(() => OnFloorButtonClicked(floorIndex));
                Debug.Log($"[FloorButtonStacking] Added listener: Button {i} -> Floor Index {floorIndex}");
            }
        }
    }

    private void OnFloorButtonClicked(int floorIndex)
    {
        Debug.Log($"[FloorButtonStacking] Floor button {floorIndex} clicked!");

        // Check if clicking the same floor button again (toggle behavior)
        if (currentExpandedFloorIndex == floorIndex && horizontalButtonsExpanded)
        {
            Debug.Log($"[FloorButtonStacking] Same floor button {floorIndex} clicked - collapsing horizontal buttons");
            CollapseHorizontalButtons();
            currentExpandedFloorIndex = -1;
            return;
        }

        // Collapse any existing horizontal buttons from other floors
        if (horizontalButtonsExpanded && currentExpandedFloorIndex != floorIndex)
        {
            Debug.Log($"[FloorButtonStacking] Collapsing horizontal buttons from floor {currentExpandedFloorIndex}");
            CollapseHorizontalButtons();
        }

        // Expand horizontal buttons at the clicked floor button's position
        currentExpandedFloorIndex = floorIndex;
        ExpandHorizontalButtonsAtFloor(floorIndex);

        // Notify MasterUI of floor selection
        if (masterUI != null)
        {
            masterUI.UpdateFloorButtonState(floorIndex, false);
        }
    }

    private void ExpandHorizontalButtonsAtFloor(int floorIndex)
    {
        // Validate floor index
        if (floorIndex < 0 || floorIndex >= horizontalButtonsPerFloor.Count)
        {
            Debug.LogWarning($"[FloorButtonStacking] Invalid floor index {floorIndex} or no horizontal buttons for this floor");
            return;
        }

        List<GameObject> horizontalButtons = horizontalButtonsPerFloor[floorIndex];

        if (horizontalButtons.Count == 0)
        {
            Debug.LogWarning($"[FloorButtonStacking] No horizontal buttons assigned for floor {floorIndex}!");
            return;
        }

        horizontalButtonsExpanded = true;

        // Get the position of the clicked floor button
        Vector2 floorButtonPosition;
        if (floorIndex >= 0 && floorIndex < floorButtons.Count)
        {
            floorButtonPosition = ((RectTransform)floorButtons[floorIndex].transform).anchoredPosition;
        }
        else
        {
            Debug.LogWarning($"[FloorButtonStacking] Invalid floor index {floorIndex}");
            return;
        }

        Debug.Log($"[FloorButtonStacking] Expanding {horizontalButtons.Count} horizontal buttons at floor {floorIndex} position: {floorButtonPosition}");

        // Calculate starting position based on positioning mode
        Vector2 startPosition;
        if (useAbsoluteYPosition)
        {
            // Use absolute Y position (all horizontal buttons at same height)
            startPosition = new Vector2(floorButtonPosition.x, horizontalButtonVerticalOffset);
        }
        else
        {
            // Use relative offset from floor button
            startPosition = floorButtonPosition + Vector2.up * horizontalButtonVerticalOffset;
        }

        // Animate horizontal buttons sliding out to the right from the floor button
        for (int i = 0; i < horizontalButtons.Count; i++)
        {
            GameObject horizontalButton = horizontalButtons[i];
            if (horizontalButton == null) continue;

            RectTransform buttonRect = (RectTransform)horizontalButton.transform;

            // Kill existing animations
            buttonRect.DOKill();

            // Set starting position at the floor button with vertical offset
            buttonRect.anchoredPosition = startPosition;
            horizontalButton.SetActive(true);

            // Calculate target position (slide to the right at the same height with offset applied)
            float horizontalOffset = (i + 1) * horizontalButtonSpacing;
            Vector2 targetPosition = startPosition + Vector2.right * horizontalOffset;

            // Animate with staggered delay
            float buttonDelay = i * delayBetweenButtons;
            buttonRect.DOAnchorPos(targetPosition, horizontalButtonAnimationDuration)
                .SetEase(horizontalAnimationEase)
                .SetDelay(buttonDelay)
                .SetUpdate(true);

            Debug.Log($"[FloorButtonStacking] Floor {floorIndex}, Horizontal button {i} sliding from {startPosition} to {targetPosition}");
        }
    }

    private void CollapseHorizontalButtons()
    {
        if (!horizontalButtonsExpanded || currentExpandedFloorIndex < 0)
        {
            Debug.Log("[FloorButtonStacking] No horizontal buttons to collapse");
            return;
        }

        // Validate floor index
        if (currentExpandedFloorIndex >= horizontalButtonsPerFloor.Count)
        {
            Debug.LogWarning($"[FloorButtonStacking] Invalid currentExpandedFloorIndex {currentExpandedFloorIndex}");
            horizontalButtonsExpanded = false;
            return;
        }

        List<GameObject> horizontalButtons = horizontalButtonsPerFloor[currentExpandedFloorIndex];

        horizontalButtonsExpanded = false;

        // Get the floor button position to collapse back to
        Vector2 floorButtonPosition;
        if (currentExpandedFloorIndex >= 0 && currentExpandedFloorIndex < floorButtons.Count)
        {
            floorButtonPosition = ((RectTransform)floorButtons[currentExpandedFloorIndex].transform).anchoredPosition;
        }
        else
        {
            floorButtonPosition = mainButtonPosition;
        }

        // Calculate collapse position based on positioning mode
        Vector2 collapsePosition;
        if (useAbsoluteYPosition)
        {
            // Use absolute Y position (same as expansion)
            collapsePosition = new Vector2(floorButtonPosition.x, horizontalButtonVerticalOffset);
        }
        else
        {
            // Use relative offset from floor button
            collapsePosition = floorButtonPosition + Vector2.up * horizontalButtonVerticalOffset;
        }

        Debug.Log($"[FloorButtonStacking] Collapsing {horizontalButtons.Count} horizontal buttons from floor {currentExpandedFloorIndex} back to {collapsePosition}");

        // Animate horizontal buttons sliding back (in reverse order)
        for (int i = 0; i < horizontalButtons.Count; i++)
        {
            GameObject horizontalButton = horizontalButtons[i];
            if (horizontalButton == null || !horizontalButton.activeInHierarchy) continue;

            RectTransform buttonRect = (RectTransform)horizontalButton.transform;

            // Kill existing animations
            buttonRect.DOKill();

            // Reverse order delay for smooth collapse (farthest buttons start first)
            float buttonDelay = (horizontalButtons.Count - 1 - i) * delayBetweenButtons;

            GameObject buttonToHide = horizontalButton;

            buttonRect.DOAnchorPos(collapsePosition, horizontalButtonAnimationDuration)
                .SetEase(horizontalAnimationEase)
                .SetDelay(buttonDelay)
                .SetUpdate(true)
                .OnComplete(() => buttonToHide.SetActive(false));
        }
    }

    private void UpdateAllButtonColors()
    {
        // Keep all buttons in normal color state (no visual selection)
        for (int i = 0; i < floorButtonImages.Count; i++)
        {
            Image buttonImage = floorButtonImages[i];
            if (buttonImage != null)
            {
                buttonImage.color = normalButtonColor;
            }
        }
    }

    // Public properties
    public bool IsExpanded => isExpanded;
    public int CurrentSelectedFloorIndex => currentSelectedFloorIndex;

    private void OnDestroy()
    {
        // Kill all DOTween animations on rooms button
        if (roomsButton != null)
        {
            ((RectTransform)roomsButton.transform).DOKill();
        }

        // Kill all DOTween animations on floor buttons
        foreach (GameObject floorButton in floorButtons)
        {
            if (floorButton != null)
            {
                ((RectTransform)floorButton.transform).DOKill();
            }
        }

        // Kill all DOTween animations on horizontal buttons for all floors
        foreach (List<GameObject> horizontalButtonsList in horizontalButtonsPerFloor)
        {
            foreach (GameObject horizontalButton in horizontalButtonsList)
            {
                if (horizontalButton != null)
                {
                    ((RectTransform)horizontalButton.transform).DOKill();
                }
            }
        }

        if (mainFloorButton != null)
            mainFloorButton.onClick.RemoveListener(ToggleFloorButtons);

        if (roomsButtonComponent != null)
            roomsButtonComponent.onClick.RemoveListener(OnRoomsButtonClicked);

        // Remove floor button listeners
        for (int i = 0; i < floorButtonComponents.Count; i++)
        {
            Button floorButtonComponent = floorButtonComponents[i];
            if (floorButtonComponent != null)
            {
                floorButtonComponent.onClick.RemoveAllListeners();
            }
        }

        // Remove horizontal button listeners for all floors
        foreach (List<Button> horizontalButtonComponentsList in horizontalButtonComponentsPerFloor)
        {
            foreach (Button horizontalButtonComponent in horizontalButtonComponentsList)
            {
                if (horizontalButtonComponent != null)
                {
                    horizontalButtonComponent.onClick.RemoveAllListeners();
                }
            }
        }
    }
}
