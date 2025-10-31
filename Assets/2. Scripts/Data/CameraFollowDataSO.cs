using UnityEngine;

/// <summary>
/// Espacio en el que se aplica el offset de posición.
/// </summary>
public enum CameraOffsetSpace
{
    WorldSpace,         // Offset en coordenadas del mundo (para isométricas/top-down fijas)
    TargetLocalSpace    // Offset relativo a la rotación del jugador (para third-person)
}

/// <summary>
/// ScriptableObject que contiene la configuración de seguimiento de cámara.
/// Permite crear diferentes perfiles de cámara para diferentes situaciones.
/// </summary>
[CreateAssetMenu(fileName = "CameraFollowData", menuName = "Game Data/Camera Follow Data")]
public class CameraFollowDataSO : ScriptableObject
{
    [Header("Position Settings")]
    [field: SerializeField] public Vector3 offset { get; private set; } = new Vector3(0f, 0f, -8f);
    [field: SerializeField, Range(0.05f, 1f)] public float smoothTime { get; private set; } = 0.25f;
    [field: SerializeField, Range(10f, 200f)] public float maxFollowSpeed { get; private set; } = 80f;
    [field: SerializeField, Range(10f, 200f)] public float maxAcceleration { get; private set; } = 50f;
    [field: SerializeField, Range(0.5f, 20f)] public float maxDistanceFromIdeal { get; private set; } = 3f;

    [Header("Rotation Settings")]
    [field: SerializeField] public Vector3 fixedRotationAngles { get; private set; } = new Vector3(70f, 0f, 0f);
}
