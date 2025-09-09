using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private bool useSmoothing = true;
    
    private Transform target;
    
    private void Start()
    {
        FindPlayer();
    }
    
    private void FindPlayer()
    {
        // Try to find player through CharacterManager first
        if (ServiceLocator.TryGet<CharacterManager>(out var characterManager) && 
            characterManager.MainCharacter != null)
        {
            target = characterManager.MainCharacter.Transform;
            Debug.Log("CameraFollow: Found player through CharacterManager");
        }
        else
        {
            // Fallback: search for PlayerController in scene
            var playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                target = playerController.transform;
                Debug.Log("CameraFollow: Found player through scene search");
            }
            else
            {
                Debug.LogWarning("CameraFollow: No player found! Will keep searching...");
            }
        }
    }
    
    private void LateUpdate()
    {
        // Keep searching for target if not found
        if (target == null)
        {
            FindPlayer();
            return;
        }
        
        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;
        
        if (useSmoothing)
        {
            // Smooth follow
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 
                                            followSpeed * Time.deltaTime);
        }
        else
        {
            // Instant follow
            transform.position = desiredPosition;
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void SetFollowSpeed(float speed)
    {
        followSpeed = Mathf.Max(0f, speed);
    }
}