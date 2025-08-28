using UnityEngine;

public class LevelManager : BaseManager
{
    [Header("Scene Settings")]
    [SerializeField] private Vector3 groundScale = new Vector3(10f, 1f, 10f);
    [SerializeField] private Color groundColor = Color.gray;
    
    private GameObject ground;
    
    protected override void OnInitialize()
    {
        SetupScene();
        ServiceLocator.Register<LevelManager>(this);
    }
    
    private void SetupScene()
    {
        CreateGround();
        Debug.Log("Scene setup completed");
    }
    
    private void CreateGround()
    {
        if (ground != null)
        {
            Debug.LogWarning("Ground already exists!");
            return;
        }
        
        ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = groundScale;
        ground.GetComponent<Renderer>().material.color = groundColor;
        
        var groundCollider = ground.GetComponent<Collider>();
        if (groundCollider != null)
        {
            groundCollider.isTrigger = false;
        }
    }
    
    public void SetGroundColor(Color color)
    {
        groundColor = color;
        if (ground != null)
        {
            ground.GetComponent<Renderer>().material.color = color;
        }
    }
    
    public void SetGroundScale(Vector3 scale)
    {
        groundScale = scale;
        if (ground != null)
        {
            ground.transform.localScale = scale;
        }
    }
    
    protected override void OnShutdown()
    {
        if (ground != null)
        {
            Destroy(ground);
        }
        ServiceLocator.Unregister<LevelManager>();
    }
}