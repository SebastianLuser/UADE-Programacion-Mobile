using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private int bulletDamage = 20;
    [SerializeField] private string sceneToRestart;

    private int _totalPoints = 0;
    private bool _canEscape = false;
    private int _currentHealth;

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
        _currentHealth = maxHealth;
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

        if (!_canEscape && _totalPoints >= escapeThreshold)
        {
            _canEscape = true;
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

        // Check for bullet collision
        if (other.CompareTag("EnemyBullet") || other.name.Contains("Bullet") || other.name.Contains("bullet"))
        {
            TakeDamage(bulletDamage);
            Destroy(other.gameObject);
        }
    }

    /// <summary>
    /// Take damage and update health bar
    /// </summary>
    /// <param name="damage">Amount of damage to take</param>
    private void TakeDamage(int damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0, _currentHealth);

        UpdateHealthBar();

        if (_currentHealth <= 0)
        {
            Die();
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
    private void Die()
    {
        Debug.Log("Player died! Restarting game...");

        if (!string.IsNullOrEmpty(sceneToRestart))
        {
            SceneManager.LoadScene(sceneToRestart);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}