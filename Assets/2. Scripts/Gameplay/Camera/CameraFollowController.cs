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

    // Variables privadas para SmoothDamp
    private Vector3 velocity = Vector3.zero;
    private Vector3 previousVelocity = Vector3.zero;
    private Vector3 previousIdealPosition;
    private bool isFirstFrame = true;

    private void Start()
    {
        if (target == null || cameraData == null)
            return;

        // Establecer rotación fija inmediatamente
        transform.rotation = Quaternion.Euler(cameraData.fixedRotationAngles);

        // Calcular y establecer posición ideal inmediatamente
        Quaternion cameraRotation = Quaternion.Euler(cameraData.fixedRotationAngles);
        Vector3 calculatedOffset = cameraRotation * cameraData.offset;
        transform.position = target.position + calculatedOffset;
    }

    private void LateUpdate()
    {
        // Validar que el target y cameraData no sean null
        if (target == null || cameraData == null)
            return;

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        // 1. Calcular offset relativo a la rotación fija de la cámara
        Quaternion cameraRotation = Quaternion.Euler(cameraData.fixedRotationAngles);
        Vector3 calculatedOffset = cameraRotation * cameraData.offset;

        // 2. Calcular posición IDEAL (donde la cámara SIEMPRE debe terminar)
        Vector3 idealPosition = target.position + calculatedOffset;

        // 3. Mover la cámara hacia la posición ideal con suavizado SmoothDamp
        // La cámara SIEMPRE converge a idealPosition, solo toma tiempo

        // Calcular nueva posición con SmoothDamp
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
            // Calcular el cambio de velocity (aceleración)
            Vector3 acceleration = (velocity - previousVelocity) / Time.deltaTime;

            // Si la aceleración supera el límite, clampearla
            if (acceleration.magnitude > cameraData.maxAcceleration)
            {
                acceleration = acceleration.normalized * cameraData.maxAcceleration;

                // Recalcular velocity con la aceleración limitada
                velocity = previousVelocity + acceleration * Time.deltaTime;

                // Recalcular posición con el velocity limitado
                newPosition = transform.position + velocity * Time.deltaTime;
            }
        }

        // Guardar velocity para el siguiente frame
        previousVelocity = velocity;
        isFirstFrame = false;

        // 5. Limitar la distancia máxima desde la posición ideal
        // Esto evita que la cámara se aleje demasiado del jugador
        float distanceFromIdeal = Vector3.Distance(newPosition, idealPosition);
        if (distanceFromIdeal > cameraData.maxDistanceFromIdeal)
        {
            // Clampear la posición para que esté dentro del radio permitido
            Vector3 directionToIdeal = (idealPosition - newPosition).normalized;
            newPosition = idealPosition - directionToIdeal * cameraData.maxDistanceFromIdeal;

            // Ajustar velocity para reflejar el clamp
            velocity = (newPosition - transform.position) / Time.deltaTime;
        }

        // Aplicar la nueva posición
        transform.position = newPosition;
    }

    /// <summary>
    /// Asigna un nuevo objetivo para que la cámara lo siga.
    /// </summary>
    public void SetTarget(Transform p_newTarget)
    {
        target = p_newTarget;

        // Resetear variables para evitar saltos bruscos al cambiar de objetivo
        velocity = Vector3.zero;
        previousVelocity = Vector3.zero;
        previousIdealPosition = Vector3.zero;
        isFirstFrame = true;

        // Reposicionar inmediatamente al nuevo target
        if (p_newTarget != null && cameraData != null)
        {
            transform.rotation = Quaternion.Euler(cameraData.fixedRotationAngles);
            Quaternion cameraRotation = Quaternion.Euler(cameraData.fixedRotationAngles);
            Vector3 calculatedOffset = cameraRotation * cameraData.offset;
            transform.position = p_newTarget.position + calculatedOffset;
        }
    }

    /// <summary>
    /// Cambia el perfil de configuración de la cámara en runtime.
    /// Útil para cambiar entre diferentes comportamientos de cámara.
    /// </summary>
    public void SetCameraData(CameraFollowDataSO p_newCameraData)
    {
        cameraData = p_newCameraData;

        // Resetear velocity al cambiar de perfil
        velocity = Vector3.zero;
    }

    // Propiedades públicas de solo lectura
    public Transform Target => target;
    public CameraFollowDataSO CameraData => cameraData;
}
