using System.Collections;
using Services;
using Services.MicroServices.EventsServices;
using Services.MicroServices.EventsServices.CustomEvents;
using Services.MicroServices.GameStateService;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player collector that automatically picks up collectable items and updates UI
/// </summary>
public class PlayerCollector : MonoBehaviour, ICollector
{
    [Header("UI Settings")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private string pointsFormat = "Points: {0}";

    [Header("Escape Settings")]
    [SerializeField] private TMP_Text escapeText;
    [SerializeField] private string escapeMessage = "Ya puedes escapar!";
    [SerializeField] private int escapeThreshold = 100;

    [Header("Health Settings")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private int maxHealth = 100;

    [SerializeField] private GameObject ragdoll;
    [SerializeField] private GameObject armL;
    [SerializeField] private GameObject armR;

    private int _totalPoints = 0;
    private bool _canEscape = false;
    private int _currentHealth;
    private bool _isDead;
    private MainCharacter _mainCharacter;
    private PlayerTouchMovement _playerMovement;

    /// <summary>
    /// Total points collected (read-only)
    /// </summary>
    public int TotalPoints => _totalPoints;

    /// <summary>
    /// Whether player can escape (read-only)
    /// </summary>
    public bool CanEscape => _canEscape;

    void Start()
    {
        _mainCharacter = GetComponent<MainCharacter>();
        _playerMovement = GetComponent<PlayerTouchMovement>();
        _isDead = false;

        if (_mainCharacter != null)
        {
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(_mainCharacter.MaxHealth));
            _currentHealth = Mathf.Clamp(Mathf.RoundToInt(_mainCharacter.CurrentHealth), 0, maxHealth);
        }
        else
        {
            _currentHealth = maxHealth;
        }

        UpdateHealthBar();
        UpdatePointsDisplay();
    }

    /// <summary>
    /// Add points to the total and update UI
    /// </summary>
    /// <param name="points">Points to add</param>
    public void AddPoints(int points)
    {
        _totalPoints += points;

        // Analytics Event 1: Item collected
        if (UGS_Analytics.Instance != null)
        {
            UGS_Analytics.Instance.LogItemCollected(points, _totalPoints);
        }

        if (!_canEscape && _totalPoints >= escapeThreshold)
        {
            _canEscape = true;

            // Analytics Event 2: Escape unlocked
            if (UGS_Analytics.Instance != null)
            {
                UGS_Analytics.Instance.LogEscapeUnlocked(_totalPoints, Time.timeSinceLevelLoad);
            }
        }

        UpdatePointsDisplay();
    }

    /// <summary>
    /// Update the points display text
    /// </summary>
    private void UpdatePointsDisplay()
    {
        if (pointsText != null)
        {
            pointsText.text = string.Format(pointsFormat, _totalPoints);
        }

        if (escapeText != null)
        {
            if (_canEscape)
            {
                escapeText.text = escapeMessage;
                escapeText.gameObject.SetActive(true);
            }
            else
            {
                escapeText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Detect collectable items and bullets on trigger enter
    /// </summary>
    /// <param name="other">The collider that entered the trigger</param>
    void OnTriggerEnter(Collider other)
    {
        var collectable = other.GetComponent<ICollectable>();
        if (collectable != null)
        {
            collectable.Collect(this);
            return;
        }

    }

    public void SyncHealth(float currentHealth, float maxHealthValue)
    {
        if (_isDead)
        {
            return;
        }

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealthValue));
        _currentHealth = Mathf.Clamp(Mathf.RoundToInt(currentHealth), 0, maxHealth);

        UpdateHealthBar();
        if (_currentHealth <= 0)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// Update the health bar UI
    /// </summary>
    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.value = (float)_currentHealth / maxHealth;
        }
    }

    /// <summary>
    /// Handle player death and restart game
    /// </summary>
    public void HandleDeath()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        _currentHealth = 0;
        UpdateHealthBar();

        // Analytics Event 3: Player death
        if (UGS_Analytics.Instance != null)
        {
            UGS_Analytics.Instance.LogPlayerDeath(_totalPoints, _currentHealth);
        }

        ServiceLocator.Get<IEventService>().DispatchEvent(new GameResultEvent(false, _totalPoints));
        ServiceLocator.Get<IGameStateService>().ChangeState(GameState.GameOver);

        if (_playerMovement)
        {
            _playerMovement.enabled = false;
        }

        GetComponent<MeshCollider>().enabled = false;
        GetComponent<MeshRenderer>().enabled = false;
        armR.SetActive(false);
        armL.SetActive(false);
        
        ragdoll.SetActive(true);
    }
}
