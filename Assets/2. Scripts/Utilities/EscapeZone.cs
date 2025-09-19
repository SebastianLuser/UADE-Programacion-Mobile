using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Escape zone that activates when player can escape and handles scene restart
/// </summary>
public class EscapeZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private GameObject escapePlane;
    [SerializeField] private Material greenMaterial;

    [Header("UI Settings")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button restartButton;
    [SerializeField] private GameObject escapeUI;
    [SerializeField] private string scoreFormat = "Final Score: {0}";

    [Header("Scene Settings")]
    [SerializeField] private string sceneToLoad;

    private PlayerCollector _playerCollector;
    private Renderer _planeRenderer;
    private Material _originalMaterial;

    void Start()
    {
        _playerCollector = FindObjectOfType<PlayerCollector>();

        if (escapePlane != null)
        {
            _planeRenderer = escapePlane.GetComponent<Renderer>();
            if (_planeRenderer != null)
            {
                _originalMaterial = _planeRenderer.material;
            }
        }

        if (escapeUI != null)
        {
            escapeUI.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    void Update()
    {
        if (_playerCollector != null && _planeRenderer != null)
        {
            if (_playerCollector.CanEscape)
            {
                if (greenMaterial != null)
                {
                    _planeRenderer.material = greenMaterial;
                }
            }
            else
            {
                _planeRenderer.material = _originalMaterial;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var playerCollector = other.GetComponent<PlayerCollector>();
        if (playerCollector != null)
        {
            if (playerCollector.CanEscape)
            {
                ShowEscapeUI(playerCollector);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        var playerCollector = other.GetComponent<PlayerCollector>();
        if (playerCollector != null)
        {
            if (escapeUI != null)
            {
                escapeUI.SetActive(false);
            }
        }
    }

    private void ShowEscapeUI(PlayerCollector playerCollector)
    {
        if (escapeUI != null)
        {
            escapeUI.SetActive(true);

            if (scoreText != null)
            {
                scoreText.text = string.Format(scoreFormat, playerCollector.TotalPoints);
            }
        }
    }

    private void RestartGame()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}