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
    public int stuckFrameThreshold = 20;   // frames sin mejorar -> avanzar de todas formas
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

    public Vector3 Tick(Vector3 pos, Vector3 vel, float maxSpeed, Vector3[] worldPath)
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
    }

    public bool ReachedEnd => _cursor >= _len - 1;
}
