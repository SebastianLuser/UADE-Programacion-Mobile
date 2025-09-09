using UnityEngine;

public static class PlayerSetup
{
    public static GameObject CreatePlayerCapsule(Vector3 position = default)
    {
        // Create the main player GameObject
        GameObject player = new GameObject("Player");
        player.transform.position = position;
        
        // Add capsule primitive as visual representation
        GameObject capsuleVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsuleVisual.name = "Visual";
        capsuleVisual.transform.SetParent(player.transform);
        capsuleVisual.transform.localPosition = Vector3.zero;
        
        // Get the capsule collider from the primitive
        CapsuleCollider capsuleCollider = capsuleVisual.GetComponent<CapsuleCollider>();
        
        // Add Rigidbody for physics
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 5f; // Add drag for more responsive movement
        rb.freezeRotation = true; // Prevent physics rotation
        
        // Move the collider to the main player object
        CapsuleCollider playerCollider = player.AddComponent<CapsuleCollider>();
        playerCollider.height = capsuleCollider.height;
        playerCollider.radius = capsuleCollider.radius;
        playerCollider.center = capsuleCollider.center;
        
        // Remove the collider from the visual (we just want the mesh)
        Object.DestroyImmediate(capsuleCollider);
        
        // Add the PlayerController script
        PlayerController playerController = player.AddComponent<PlayerController>();
        
        // Set up ground detection parameters
        playerController.SetGroundCheckDistance(0.6f);
        
        // Create a ground check point
        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0, -0.5f, 0);
        
        // Set the ground check point reference
        var groundCheckField = typeof(PlayerController).GetField("groundCheckPoint", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        groundCheckField?.SetValue(playerController, groundCheck.transform);
        
        // Tag the player
        player.tag = "Player";
        
        Debug.Log($"Player capsule created at position {position}");
        return player;
    }
    
    public static GameObject CreateGround(Vector3 position = default, Vector3 scale = default)
    {
        if (scale == default)
            scale = new Vector3(20f, 1f, 20f);
            
        // Create ground plane
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = position;
        ground.transform.localScale = scale;
        
        // Set ground layer (default layer 0 should work)
        ground.layer = 0;
        
        // Make it static for better performance
        ground.isStatic = true;
        
        // Change color to make it distinguishable
        Renderer renderer = ground.GetComponent<Renderer>();
        Material groundMaterial = new Material(Shader.Find("Standard"));
        groundMaterial.color = new Color(0.5f, 0.8f, 0.5f); // Light green
        renderer.material = groundMaterial;
        
        Debug.Log($"Ground created at position {position} with scale {scale}");
        return ground;
    }
    
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // DISABLED
    public static void SetupPrototypeScene()
    {
        // Only run if no player exists in scene
        if (GameObject.FindObjectOfType<PlayerController>() != null)
            return;
            
        // Create ground - DISABLED
        // CreateGround(new Vector3(0, -0.5f, 0), new Vector3(20f, 1f, 20f));
        
        // Create player
        CreatePlayerCapsule(new Vector3(0, 2f, 0));
        
        Debug.Log("Prototype scene setup complete!");
    }
}