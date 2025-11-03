using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Search filter UI component for room search
/// Attach to an InputField to enable real-time search with relevance ranking
/// </summary>
public class RoomSearchFilter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button clearButton; // Optional: Clear search button
    [SerializeField] private TextMeshProUGUI resultCountText; // Optional: Shows "X results"

    [Header("Controller Reference")]
    [SerializeField] private RoomScrollViewController roomScrollViewController;

    [Header("Search Settings")]
    [SerializeField] private float searchDelay = 0.3f; // Delay before triggering search (debounce)
    [SerializeField] private int minCharactersForSearch = 2; // Minimum characters before searching

    // State
    private string currentSearchQuery = "";
    private float lastSearchTime = 0f;
    private bool searchPending = false;

    private void Awake()
    {
        // Auto-find components
        if (searchInputField == null)
            searchInputField = GetComponent<TMP_InputField>();

        // Setup listeners
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(ClearSearch);
        }
    }

    private void Start()
    {
        // Find RoomScrollViewController if not assigned
        if (roomScrollViewController == null)
        {
            roomScrollViewController = FindObjectOfType<RoomScrollViewController>();
        }

        // Hide clear button initially
        if (clearButton != null)
        {
            clearButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Debounced search - wait for user to stop typing
        if (searchPending && Time.time - lastSearchTime >= searchDelay)
        {
            searchPending = false;
            ExecuteSearch();
        }
    }

    private void OnSearchTextChanged(string newText)
    {
        currentSearchQuery = newText;
        lastSearchTime = Time.time;
        searchPending = true;

        // Show/hide clear button
        if (clearButton != null)
        {
            clearButton.gameObject.SetActive(!string.IsNullOrEmpty(newText));
        }
    }

    private void ExecuteSearch()
    {
        if (roomScrollViewController == null)
        {
            Debug.LogWarning("[RoomSearchFilter] RoomScrollViewController is not assigned!");
            return;
        }

        Debug.Log($"[RoomSearchFilter] Searching for: '{currentSearchQuery}'");

        // Trigger search with relevance ranking
        int resultCount = roomScrollViewController.SearchRooms(currentSearchQuery);

        // Update result count text
        if (resultCountText != null)
        {
            if (string.IsNullOrEmpty(currentSearchQuery))
            {
                resultCountText.text = $"{resultCount} rooms";
            }
            else
            {
                resultCountText.text = $"{resultCount} results";
            }
        }

        Debug.Log($"[RoomSearchFilter] Found {resultCount} results");
    }

    public void ClearSearch()
    {
        Debug.Log("[RoomSearchFilter] Clearing search");

        if (searchInputField != null)
        {
            searchInputField.text = "";
        }

        currentSearchQuery = "";
        searchPending = false;

        if (clearButton != null)
        {
            clearButton.gameObject.SetActive(false);
        }

        // Reset to show all rooms
        if (roomScrollViewController != null)
        {
            roomScrollViewController.SearchRooms("");
        }
    }

    // Public method to trigger search programmatically
    public void SetSearchQuery(string query)
    {
        if (searchInputField != null)
        {
            searchInputField.text = query;
        }
    }

    private void OnDestroy()
    {
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.RemoveListener(OnSearchTextChanged);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(ClearSearch);
        }
    }
}
