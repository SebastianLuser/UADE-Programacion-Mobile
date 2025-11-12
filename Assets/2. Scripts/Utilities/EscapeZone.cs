using UnityEngine;
using Services;
using Services.MicroServices.EventsServices;
using Services.MicroServices.EventsServices.CustomEvents;
using Services.MicroServices.GameStateService;

/// <summary>
/// Escape zone that activates when player can escape and handles scene restart
/// </summary>
public class EscapeZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private GameObject escapePlane;
    [SerializeField] private Material greenMaterial;

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
        // No-op now that we use global results UI
    }

    private void ShowEscapeUI(PlayerCollector playerCollector)
    {
        var playerMovement = playerCollector.GetComponent<PlayerTouchMovement>();
        if (playerMovement)
        {
            playerMovement.enabled = false;
        }

        if (UGS_Analytics.Instance != null)
        {
            UGS_Analytics.Instance.LogEscapeZoneReached(playerCollector.TotalPoints, Time.timeSinceLevelLoad);
        }

        playerCollector.gameObject.SetActive(false);

        ServiceLocator.Get<IEventService>().DispatchEvent(new GameResultEvent(true, playerCollector.TotalPoints));
        ServiceLocator.Get<IGameStateService>().ChangeState(GameState.Victory);
    }
}
