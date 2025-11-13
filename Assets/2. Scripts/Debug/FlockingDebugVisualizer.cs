using UnityEngine;
using FlockingSystem;
using System.Collections.Generic;

/// <summary>
/// Visual debugger for flocking system.
/// Shows neighbor connections, forces, and detection radius in Scene view.
/// Attach to any GameObject with FlockingEntity component.
/// </summary>
public class FlockingDebugVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private bool showDetectionRadius = true;
    [SerializeField] private bool showNeighborConnections = true;
    [SerializeField] private bool showFlockingForce = true;
    [SerializeField] private bool showVelocity = true;
    [SerializeField] private bool showLabels = true;

    [Header("Colors")]
    [SerializeField] private Color detectionRadiusColor = new Color(0f, 1f, 0f, 0.2f);
    [SerializeField] private Color neighborConnectionColor = Color.cyan;
    [SerializeField] private Color flockingForceColor = Color.yellow;
    [SerializeField] private Color velocityColor = Color.blue;
    [SerializeField] private Color separationColor = Color.red;
    [SerializeField] private Color cohesionColor = Color.green;
    [SerializeField] private Color alignmentColor = Color.magenta;

    [Header("Size Settings")]
    [SerializeField] private float forceVectorScale = 2f;
    [SerializeField] private float velocityVectorScale = 1f;

    private FlockingEntity flockingEntity;
    private Guard guard;
    private Services.MicroServices.FlockingService.IFlockingService flockingService;

    private void Awake()
    {
        flockingEntity = GetComponent<FlockingEntity>();
        guard = GetComponent<Guard>();

        if (flockingEntity == null)
        {
            Debug.LogWarning($"FlockingDebugVisualizer on {gameObject.name}: No FlockingEntity found", this);
        }
    }

    private void Start()
    {
        flockingService = Services.ServiceLocator.Get<Services.MicroServices.FlockingService.IFlockingService>();
    }

    private void OnDrawGizmos()
    {
        if (flockingEntity == null || flockingEntity.Profile == null)
            return;

        Vector3 position = transform.position + Vector3.up * 0.5f; // Offset slightly up for visibility

        // Detection Radius
        if (showDetectionRadius)
        {
            DrawDetectionRadius(position);
        }

        // Neighbor Connections
        if (showNeighborConnections && Application.isPlaying && flockingService != null)
        {
            DrawNeighborConnections(position);
        }

        // Flocking Force Vector
        if (showFlockingForce && Application.isPlaying)
        {
            DrawFlockingForce(position);
        }

        // Velocity Vector
        if (showVelocity && Application.isPlaying)
        {
            DrawVelocity(position);
        }
    }

    private void DrawDetectionRadius(Vector3 position)
    {
        Gizmos.color = detectionRadiusColor;

        // Draw circle at ground level
        DrawCircle(transform.position, flockingEntity.Profile.detectionRadius, 32);

        // Draw vertical indicator
        Gizmos.color = new Color(detectionRadiusColor.r, detectionRadiusColor.g, detectionRadiusColor.b, 0.5f);
        Gizmos.DrawLine(position, position + Vector3.up * 2f);
    }

    private void DrawNeighborConnections(Vector3 position)
    {
        if (flockingService == null)
            return;

        List<FlockingEntity> neighbors = flockingService.GetNeighbors(
            flockingEntity,
            flockingEntity.Profile.detectionRadius
        );

        Gizmos.color = neighborConnectionColor;

        int maxNeighbors = flockingEntity.Profile.maxNeighbors > 0
            ? flockingEntity.Profile.maxNeighbors
            : neighbors.Count;

        for (int i = 0; i < Mathf.Min(neighbors.Count, maxNeighbors); i++)
        {
            if (neighbors[i] == null)
                continue;

            Vector3 neighborPos = neighbors[i].transform.position + Vector3.up * 0.5f;

            // Draw line to neighbor
            Gizmos.DrawLine(position, neighborPos);

            // Draw sphere at neighbor
            Gizmos.DrawWireSphere(neighborPos, 0.3f);

            // Draw label with distance
            if (showLabels)
            {
                float distance = Vector3.Distance(transform.position, neighbors[i].transform.position);
                DrawLabel(neighborPos + Vector3.up, $"{distance:F1}m", Color.white);
            }
        }

        // Draw neighbor count label
        if (showLabels)
        {
            DrawLabel(position + Vector3.up * 3f, $"Neighbors: {Mathf.Min(neighbors.Count, maxNeighbors)}", Color.cyan);
        }
    }

    private void DrawFlockingForce(Vector3 position)
    {
        Vector3 flockingForce = flockingEntity.GetFlockingForce();

        if (flockingForce.sqrMagnitude < 0.01f)
            return;

        // Draw combined flocking force
        Gizmos.color = flockingForceColor;
        DrawArrow(position, flockingForce * forceVectorScale, 0.3f);

        // Draw label with magnitude
        if (showLabels)
        {
            Vector3 labelPos = position + flockingForce.normalized * forceVectorScale + Vector3.up * 0.5f;
            DrawLabel(labelPos, $"Flock: {flockingForce.magnitude:F2}", flockingForceColor);
        }
    }

    private void DrawVelocity(Vector3 position)
    {
        Vector3 velocity = Vector3.zero;

        if (guard != null)
        {
            velocity = guard.CurrentVelocity;
        }

        if (velocity.sqrMagnitude < 0.01f)
            return;

        Gizmos.color = velocityColor;
        DrawArrow(position, velocity * velocityVectorScale, 0.25f);

        // Draw label with speed
        if (showLabels)
        {
            Vector3 labelPos = position + velocity.normalized * velocityVectorScale + Vector3.down * 0.5f;
            DrawLabel(labelPos, $"Speed: {velocity.magnitude:F2}", velocityColor);
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            Gizmos.DrawLine(point1, point2);
        }
    }

    private void DrawArrow(Vector3 position, Vector3 direction, float arrowHeadSize)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 endPos = position + direction;

        // Draw main line
        Gizmos.DrawLine(position, endPos);

        // Draw arrowhead
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

        Gizmos.DrawLine(endPos, endPos + right * arrowHeadSize);
        Gizmos.DrawLine(endPos, endPos + left * arrowHeadSize);
    }

    private void DrawLabel(Vector3 position, string text, Color color)
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        UnityEditor.Handles.Label(position, text, style);
#endif
    }

    // ==================== CONTEXT MENU DEBUG ====================

    [ContextMenu("Print Flocking Status")]
    private void PrintFlockingStatus()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Must be in Play mode to print flocking status");
            return;
        }

        Debug.Log("=== FLOCKING STATUS ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"FlockingEntity Present: {flockingEntity != null}");

        if (flockingEntity != null && flockingEntity.Profile != null)
        {
            var profile = flockingEntity.Profile;
            Debug.Log($"Profile: {profile.name}");
            Debug.Log($"Detection Radius: {profile.detectionRadius}");
            Debug.Log($"Max Speed: {profile.maxSpeed}");
            Debug.Log($"Max Force: {profile.maxForce}");
            Debug.Log($"Max Neighbors: {profile.maxNeighbors}");

            Debug.Log($"\nCurrent Flocking Force: {flockingEntity.GetFlockingForce()}");
            Debug.Log($"Flocking Force Magnitude: {flockingEntity.GetFlockingForce().magnitude:F3}");
        }

        if (guard != null)
        {
            Debug.Log($"\nGuard Use Flocking: {guard.GetType().GetField("useFlocking", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(guard)}");
            Debug.Log($"Guard Should Flock: {guard.ShouldFlock()}");
            Debug.Log($"Guard Current Velocity: {guard.CurrentVelocity}");
            Debug.Log($"Guard Max Speed: {guard.MaxSpeed}");
        }

        if (flockingService != null)
        {
            var neighbors = flockingService.GetNeighbors(flockingEntity, flockingEntity.Profile.detectionRadius);
            Debug.Log($"\nNeighbors Found: {neighbors.Count}");

            for (int i = 0; i < neighbors.Count; i++)
            {
                float distance = Vector3.Distance(transform.position, neighbors[i].transform.position);
                Debug.Log($"  Neighbor {i + 1}: {neighbors[i].name} at distance {distance:F2}");
            }
        }

        Debug.Log("======================");
    }

    [ContextMenu("Toggle All Visualization")]
    private void ToggleAllVisualization()
    {
        bool newState = !showDetectionRadius;
        showDetectionRadius = newState;
        showNeighborConnections = newState;
        showFlockingForce = newState;
        showVelocity = newState;
        showLabels = newState;

        Debug.Log($"All visualization {(newState ? "enabled" : "disabled")}");
    }

    [ContextMenu("Show Only Forces")]
    private void ShowOnlyForces()
    {
        showDetectionRadius = false;
        showNeighborConnections = false;
        showFlockingForce = true;
        showVelocity = true;
        showLabels = true;

        Debug.Log("Showing only force vectors");
    }

    [ContextMenu("Show Only Neighbors")]
    private void ShowOnlyNeighbors()
    {
        showDetectionRadius = true;
        showNeighborConnections = true;
        showFlockingForce = false;
        showVelocity = false;
        showLabels = true;

        Debug.Log("Showing only neighbor connections");
    }
}
