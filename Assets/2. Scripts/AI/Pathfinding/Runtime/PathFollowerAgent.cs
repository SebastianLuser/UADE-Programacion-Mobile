/*using UnityEngine;

public class PathFollowerAgent
{
    public float waypointReachDist = 0.5f;
    public float slowingDistance = 1.0f;

    int _len = 0, _cursor = 0;
    public int CurrentIndex => _cursor;               // <-- exponer cursor actual
    public void ReseedCursor(int idx)                 // <-- permitir setear cursor
    {
        _cursor = Mathf.Clamp(idx, 0, Mathf.Max(0, _len - 1));
    }

    public int BuildWorldPath(GraphAsset g, int[] idxPath, int idxLen, Vector3[] worldOut)
    {
        _len = idxLen; _cursor = 0;                   // mantener puro (reset al recomputar)
        for (int i = 0; i < idxLen; i++) worldOut[i] = g.nodePositions[idxPath[i]];
        return _len;
    }

    public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len) return Vector3.zero;
        Vector3 target = worldPath[_cursor];
        Vector3 steer = Game.AI.Steering.Steering.Arrive(pos, vel, target, maxSpeed, slowingDistance);
        if ((pos - target).sqrMagnitude <= waypointReachDist * waypointReachDist && _cursor < _len - 1) _cursor++;
        return steer;
    }

    public bool ReachedEnd => _cursor >= _len - 1;
}*/
using UnityEngine;

public class PathFollowerAgent
{
    // Tunings
    public float waypointReachDist = 0.6f;   // subí un poco el reach (0.6–0.8 ayuda mucho)
    public float slowingDistance = 1.2f;   // 1.0–1.5 típico
    public int stuckFrameThreshold = 60;//20;   // frames sin mejorar -> avanzar de todas formas
    public float progressEpsilon = 0.001f; // tolerancia para detectar “no mejora”

    // Internos
    int _len = 0, _cursor = 0;
    float _lastSqDist = float.PositiveInfinity;
    int _stuckFrames = 0;

    public int CurrentIndex => _cursor;  // para debug
    public void ReseedCursor(int idx)
    {
        _cursor = Mathf.Clamp(idx, 0, Mathf.Max(0, _len - 1));
        _lastSqDist = float.PositiveInfinity;
        _stuckFrames = 0;
    }

    public int BuildWorldPath(GraphAsset g, int[] idxPath, int idxLen, Vector3[] worldOut)
    {
        _len = idxLen;
        _cursor = 0;
        _lastSqDist = float.PositiveInfinity;
        _stuckFrames = 0;

        for (int i = 0; i < idxLen; i++)
            worldOut[i] = g.nodePositions[idxPath[i]];

        return _len;
    }

    /*public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len) return Vector3.zero;

        // Objetivo actual
        Vector3 target = worldPath[_cursor];

        // Distancias y vectores (asumimos plano XZ; fuerza y=0 si tu juego es top-down 3D)
        Vector3 toWp = target - pos;
        toWp.y = 0f;
        float sqDist = toWp.sqrMagnitude;
        float reachR2 = waypointReachDist * waypointReachDist;

        // 1) ¿Llegué?
        if (sqDist <= reachR2)
        {
            if (_cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos); toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
            }
            else
            {
                // ya en el último
                return Vector3.zero;
            }
        }

        // 2) ¿Sobrepasé el waypoint? (overshoot)
        // Si el producto punto es negativo, el objetivo quedó atrás -> avanzar
        if (_cursor < _len - 1 && Vector3.Dot(toWp, vel) < 0f)
        {
            _cursor++;
            _lastSqDist = float.PositiveInfinity;
            _stuckFrames = 0;
            target = worldPath[_cursor];
            toWp = (target - pos); toWp.y = 0f;
            sqDist = toWp.sqrMagnitude;
        }

        // 3) Anti-atasco: si no mejoro distancia varios frames, avanzo igual
        if (sqDist > _lastSqDist - progressEpsilon)
        {
            _stuckFrames++;
            if (_stuckFrames >= stuckFrameThreshold && _cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos); toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
            }
        }
        else
        {
            _stuckFrames = 0;          // hubo progreso
            _lastSqDist = sqDist;      // guardo distancia actual como “mejor”
        }

        // 4) Steering: Seek si lejos, Arrive si cerca
        Vector3 steer;
        float slowR2 = slowingDistance * slowingDistance;
        if (sqDist > slowR2)
        {
            // Seek clásico (ir recto a target a maxSpeed)
            // Usamos el mismo helper Arrive con slowingDistance=0 para evitar duplicar code
            steer = Game.AI.Steering.Steering.Seek(pos, vel, target, maxSpeed);
        }
        else
        {
            // Arrive suave al waypoint
            steer = Game.AI.Steering.Steering.Arrive(pos, vel, target, maxSpeed, slowingDistance);
        }

        // Siempre fuerza Y=0 si tu locomoción es XZ
        steer.y = 0f;
        return steer;
    }*/
    /*public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len)
        {
            Debug.LogWarning($"PathFollower: Invalid state (len={_len}, cursor={_cursor})");
            return Vector3.zero;
        }

        // Objetivo actual
        Vector3 target = worldPath[_cursor];

        // Distancias (XZ plane)
        Vector3 toWp = target - pos;
        toWp.y = 0f;
        float sqDist = toWp.sqrMagnitude;
        float reachR2 = waypointReachDist * waypointReachDist;

        // === DEBUG ===
        Debug.Log($"PathFollower: cursor={_cursor}/{_len}, " +
                  $"target={target}, " +
                  $"dist={Mathf.Sqrt(sqDist):F2}, " +
                  $"reachDist={waypointReachDist}");

        // 1) ¿Llegué?
        if (sqDist <= reachR2)
        {
            if (_cursor < _len - 1)
            {
                _cursor++;
                Debug.Log($"PathFollower: OK Reached WP, advancing to {_cursor}");
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos); toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
            }
            else
            {
                Debug.Log($"PathFollower: At last waypoint, stopping");
                return Vector3.zero;
            }
        }

        // 2) ¿Sobrepasé el waypoint?
        if (_cursor < _len - 1 && Vector3.Dot(toWp, vel) < 0f)
        {
            _cursor++;
            Debug.Log($"PathFollower: Overshot WP, advancing to {_cursor}");
            _lastSqDist = float.PositiveInfinity;
            _stuckFrames = 0;
            target = worldPath[_cursor];
            toWp = (target - pos); toWp.y = 0f;
            sqDist = toWp.sqrMagnitude;
        }

        // 3) Anti-atasco
        if (sqDist > _lastSqDist - progressEpsilon)
        {
            _stuckFrames++;
            if (_stuckFrames >= stuckFrameThreshold && _cursor < _len - 1)
            {
                _cursor++;
                Debug.LogWarning($"PathFollower: STUCK! Force advancing to {_cursor}");
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos); toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
            }
        }
        else
        {
            _stuckFrames = 0;
            _lastSqDist = sqDist;
        }

        // 4) Steering: Seek si lejos, Arrive si cerca
        Vector3 steer;
        float slowR2 = slowingDistance * slowingDistance;

        if (sqDist > slowR2)
        {
            // Seek clásico
            steer = Game.AI.Steering.Steering.Seek(
                pos,      // posición actual
                target,   // waypoint objetivo
                vel,      // velocidad actual
                maxSpeed  // velocidad máxima
            );
        }
        else
        {
            // Arrive suave
            steer = Game.AI.Steering.Steering.Arrive(
                pos,             // posición actual
                target,          // waypoint objetivo
                vel,             // velocidad actual
                maxSpeed,        // velocidad máxima
                slowingDistance  // distancia de frenado
            );
        }

        // Siempre fuerza Y=0 si tu locomoción es XZ
        steer.y = 0f;
        return steer;
    }*/

    /*public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len) return Vector3.zero;

        // Objetivo actual
        Vector3 target = worldPath[_cursor];

        // Distancias (XZ plane - FORZAR Y=0 desde el inicio)
        Vector3 toWp = target - pos;
        toWp.y = 0f; // <- CRÍTICO
        float sqDist = toWp.sqrMagnitude;
        float reachR2 = waypointReachDist * waypointReachDist;

        // 1) ¿Llegué al waypoint actual?
        if (sqDist <= reachR2)
        {
            if (_cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f; // <- CRÍTICO
                sqDist = toWp.sqrMagnitude;

                Debug.Log($"PathFollower: ok Reached WP, advancing to {_cursor}/{_len}");
            }
            else
            {
                // Ya en el último
                return Vector3.zero;
            }
        }

        // 2) ¿Sobrepasé waypoints? (loop para múltiples saltos)
        int overshootCount = 0;
        while (_cursor < _len - 1 && Vector3.Dot(toWp, vel) < 0f && overshootCount < 5)
        {
            _cursor++;
            _lastSqDist = float.PositiveInfinity;
            _stuckFrames = 0;
            target = worldPath[_cursor];
            toWp = (target - pos);
            toWp.y = 0f; // <- CRÍTICO
            sqDist = toWp.sqrMagnitude;
            overshootCount++;

            Debug.Log($"PathFollower: Overshot WP, advancing to {_cursor}/{_len}");
        }

        // 3) Anti-atasco
        if (sqDist > _lastSqDist - progressEpsilon)
        {
            _stuckFrames++;
            if (_stuckFrames >= stuckFrameThreshold && _cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f; // <- CRÍTICO
                sqDist = toWp.sqrMagnitude;

                Debug.LogWarning($"PathFollower: STUCK ({_stuckFrames} frames), forcing advance to {_cursor}");
            }
        }
        else
        {
            _stuckFrames = 0;
            _lastSqDist = sqDist;
        }

        // 4) Steering: Seek si lejos, Arrive si cerca
        Vector3 steer;
        float slowR2 = slowingDistance * slowingDistance;

        if (sqDist > slowR2)
        {
            // Seek directo (usar firma correcta)
            steer = Game.AI.Steering.Steering.Seek(
                pos,      // posición actual
                target,   // waypoint objetivo
                vel,      // velocidad actual
                maxSpeed  // velocidad máxima
            );
        }
        else
        {
            // Arrive suave
            steer = Game.AI.Steering.Steering.Arrive(
                pos,             // posición actual
                target,          // waypoint objetivo
                vel,             // velocidad actual
                maxSpeed,        // velocidad máxima
                slowingDistance  // distancia de frenado
            );
        }

        // CRÍTICO: Forzar Y=0 antes de devolver
        steer.y = 0f;
        return steer;
    }*/

    /*public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len) return Vector3.zero;

        Vector3 target = worldPath[_cursor];
        Vector3 toWp = target - pos;
        toWp.y = 0f;
        float sqDist = toWp.sqrMagnitude;
        float reachR2 = waypointReachDist * waypointReachDist;

        // 1) ¿Llegué al waypoint?
        if (sqDist <= reachR2)
        {
            if (_cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
                Debug.Log($"PathFollower: ok Reached WP {_cursor - 1}, advancing to {_cursor}");
            }
            else
            {
                return Vector3.zero;
            }
        }

        // 2) Detección de overshoot MEJORADA
        // Solo avanzar si:
        // a) La velocidad apunta en dirección opuesta al waypoint (dot < 0)
        // b) Y estamos MUY cerca del waypoint (dentro de 2x reach distance)
        // c) Y la velocidad es significativa (no estamos quietos contra un obstáculo)
        float overshootThreshold = waypointReachDist * 2.5f; // 2.5x el reach
        float overshootSqThreshold = overshootThreshold * overshootThreshold;

        if (_cursor < _len - 1 &&
            sqDist < overshootSqThreshold &&  // NUEVO: Solo si estamos cerca
            vel.sqrMagnitude > 0.1f &&        // NUEVO: Solo si nos movemos
            Vector3.Dot(toWp, vel) < -0.2f)   // NUEVO: Más tolerancia (-0.2 en vez de 0)
        {
            _cursor++;
            _lastSqDist = float.PositiveInfinity;
            _stuckFrames = 0;
            target = worldPath[_cursor];
            toWp = (target - pos);
            toWp.y = 0f;
            sqDist = toWp.sqrMagnitude;
            Debug.Log($"PathFollower: Overshot WP {_cursor - 1}, advancing to {_cursor}");
        }

        // 3) Anti-atasco MEJORADO
        // Solo activar si llevamos mucho tiempo sin mejorar Y nos estamos moviendo
        if (sqDist > _lastSqDist - progressEpsilon)
        {
            _stuckFrames++;

            // NUEVO: Verificar que realmente estamos atorados (velocidad muy baja)
            bool reallyStuck = vel.magnitude < maxSpeed * 0.2f; // < 20% de velocidad máxima

            if (reallyStuck && _stuckFrames >= stuckFrameThreshold && _cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
                Debug.LogWarning($"PathFollower: STUCK detected ({_stuckFrames} frames, vel={vel.magnitude:F2}), forcing advance to {_cursor}");
            }
        }
        else
        {
            _stuckFrames = 0;
            _lastSqDist = sqDist;
        }

        // 4) Steering
        Vector3 steer;
        float slowR2 = slowingDistance * slowingDistance;

        if (sqDist > slowR2)
        {
            steer = Game.AI.Steering.Steering.Seek(pos, target, vel, maxSpeed);
        }
        else
        {
            steer = Game.AI.Steering.Steering.Arrive(pos, target, vel, maxSpeed, slowingDistance);
        }

        steer.y = 0f;
        return steer;
    }*/
    public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
    {
        if (_len <= 0 || _cursor >= _len) return Vector3.zero;

        Vector3 target = worldPath[_cursor];
        Vector3 toWp = target - pos;
        toWp.y = 0f;
        float sqDist = toWp.sqrMagnitude;

        // REACH DINÁMICO: más tolerante si vamos rápido, más estricto si vamos lento
        float speedFactor = Mathf.Clamp01(vel.magnitude / maxSpeed);
        float dynamicReach = waypointReachDist * (1.0f + speedFactor * 0.5f); // 1.0x a 1.5x reach
        float reachR2 = dynamicReach * dynamicReach;

        // 1) ¿Llegué al waypoint?
        if (sqDist <= reachR2)
        {
            if (_cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
                Debug.Log($"PathFollower: ok Reached WP {_cursor - 1}, moving to {_cursor}");
            }
            else
            {
                return Vector3.zero;
            }
        }

        // 2) Overshoot ULTRA conservador
        // Solo si estamos MUY cerca Y claramente pasamos de largo
        float overshootDist = dynamicReach * 3.0f; // 3x reach
        float overshootSqDist = overshootDist * overshootDist;

        if (_cursor < _len - 1 &&
            sqDist < overshootSqDist &&
            vel.sqrMagnitude > 1.0f && // Velocidad significativa
            Vector3.Dot(toWp, vel) < -0.5f) // MUY claramente hacia atrás
        {
            _cursor++;
            _lastSqDist = float.PositiveInfinity;
            _stuckFrames = 0;
            target = worldPath[_cursor];
            toWp = (target - pos);
            toWp.y = 0f;
            sqDist = toWp.sqrMagnitude;
            Debug.Log($"PathFollower: Overshot WP {_cursor - 1}, moving to {_cursor}");
        }

        // 3) Anti-stuck MUY tolerante
        if (sqDist > _lastSqDist - progressEpsilon)
        {
            _stuckFrames++;

            // Solo avanzar si REALMENTE estamos atorados (velocidad casi 0)
            bool reallyStuck = vel.magnitude < maxSpeed * 0.1f; // < 10% velocidad

            if (reallyStuck && _stuckFrames >= stuckFrameThreshold && _cursor < _len - 1)
            {
                _cursor++;
                _lastSqDist = float.PositiveInfinity;
                _stuckFrames = 0;
                target = worldPath[_cursor];
                toWp = (target - pos);
                toWp.y = 0f;
                sqDist = toWp.sqrMagnitude;
                Debug.LogWarning($"PathFollower: STUCK {_stuckFrames} frames, forcing advance to {_cursor}");
            }
        }
        else
        {
            _stuckFrames = 0;
            _lastSqDist = sqDist;
        }

        // 4) Steering
        Vector3 steer;
        float slowR2 = slowingDistance * slowingDistance;

        if (sqDist > slowR2)
        {
            steer = Game.AI.Steering.Steering.Seek(pos, target, vel, maxSpeed);
        }
        else
        {
            steer = Game.AI.Steering.Steering.Arrive(pos, target, vel, maxSpeed, slowingDistance);
        }

        steer.y = 0f;
        return steer;
    }

    public bool ReachedEnd => _cursor >= _len - 1;
}
