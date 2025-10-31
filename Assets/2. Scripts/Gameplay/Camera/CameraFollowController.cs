using UnityEngine;

/// <summary>
/// Controla el seguimiento de la cámara hacia un objetivo (jugador) con opciones de suavizado y límites.
/// Usa LateUpdate para ejecutarse después del movimiento del jugador.
/// Todos los datos configurables se almacenan en CameraFollowDataSO.
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private CameraFollowDataSO cameraData;

    [Header("Target")]
    [SerializeField] private Transform target;

    private Vector3 velocity = Vector3.zero;
    private Vector3 previousVelocity = Vector3.zero;
    private bool isFirstFrame = true;
    
    private void Start()
    {
        if (target == null || cameraData == null) return;

        transform.rotation = Quaternion.Euler(cameraData.fixedRotationAngles);

        Vector3 calculatedOffset = GetCalculatedOffset(cameraData.offset);
        transform.position = target.position + calculatedOffset;
    }

    private void LateUpdate()
    {
        if (target == null || cameraData == null)
            return;

        UpdatePosition();
        UpdateRotation();
    }

    /// <summary>
    /// Calcula el offset en world space según el espacio configurado.
    /// </summary>
    private Vector3 GetCalculatedOffset(Vector3 p_offset)
    {
        Quaternion camRot = Quaternion.Euler(cameraData.fixedRotationAngles);
        Vector3 forward = camRot * Vector3.forward;
        float distance = Mathf.Abs(p_offset.z);
        return -forward * distance;
    }

    
    private void UpdatePosition()
    {
        // 1. Calcular offset según el espacio configurado
        Vector3 calculatedOffset = GetCalculatedOffset(cameraData.offset);

        // 2. Calcular posición IDEAL (donde la cámara SIEMPRE debe terminar)
        Vector3 idealPosition = target.position + calculatedOffset;

        // 3. Mover la cámara hacia la posición ideal con suavizado
            Vector3 newPosition = Vector3.SmoothDamp(
                transform.position,
                idealPosition,
                ref velocity,
                cameraData.smoothTime,
                cameraData.maxFollowSpeed,
                Time.deltaTime
            );

            // 4. Limitar la aceleración para evitar movimientos bruscos iniciales
            if (!isFirstFrame)
            {
                Vector3 acceleration = (velocity - previousVelocity) / Time.deltaTime;

                if (acceleration.magnitude > cameraData.maxAcceleration)
                {
                    acceleration = acceleration.normalized * cameraData.maxAcceleration;

                    velocity = previousVelocity + acceleration * Time.deltaTime;

                    newPosition = transform.position + velocity * Time.deltaTime;
                }
            }

            previousVelocity = velocity;
            isFirstFrame = false;

            // 5. Limitar la distancia máxima desde la posición ideal
            float distanceFromIdeal = Vector3.Distance(newPosition, idealPosition);
            if (distanceFromIdeal > cameraData.maxDistanceFromIdeal)
            {
                Vector3 directionToIdeal = (idealPosition - newPosition).normalized;
                newPosition = idealPosition - directionToIdeal * cameraData.maxDistanceFromIdeal;

                velocity = (newPosition - transform.position) / Time.deltaTime;
            }

            transform.position = newPosition;
    }
    
    private void UpdateRotation()
    {
        transform.rotation = Quaternion.Euler(cameraData.fixedRotationAngles);
    }

}
