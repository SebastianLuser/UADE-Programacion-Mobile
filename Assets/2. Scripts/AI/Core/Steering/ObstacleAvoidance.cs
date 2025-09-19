using UnityEngine;

/// <summary>
/// ObstacleAvoidance:
/// Computes an adjusted movement direction to steer an NPC away from the nearest obstacle
/// within a scan radius and forward angle.
/// </summary>
public class ObstacleAvoidance
{
    Transform npcTransform;
    float _radius;
    float _angle;
    float _personalArea;
    LayerMask _obsMask;
    Collider[] _colls;

    public ObstacleAvoidance(Transform entity, float radius, float angle, float personalArea, LayerMask obsMask, int countMaxObs = 5)
    {
        npcTransform = entity;
        _radius = radius;
        _angle = angle;
        _obsMask = obsMask;
        _colls = new Collider[countMaxObs];
        _personalArea = personalArea;
    }

    public Vector3 GetDir(Vector3 currDir, bool calculateY = true)
    {
        int count = Physics.OverlapSphereNonAlloc(npcTransform.position, _radius, _colls, _obsMask);

        Collider nearColl = null;
        float nearCollDistance = 0;
        Vector3 nearClosetPoint = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var currColl = _colls[i];
            Vector3 closetPoint = currColl.ClosestPoint(npcTransform.position);
            if (!calculateY) closetPoint.y = npcTransform.position.y;
            Vector3 dirToColl = closetPoint - npcTransform.position;
            float distance = dirToColl.magnitude;
            float currAngle = Vector3.Angle(dirToColl, currDir);
            if (currAngle > _angle / 2) continue;

            if (nearColl == null || distance < nearCollDistance)
            {
                nearColl = currColl;
                nearCollDistance = distance;
                nearClosetPoint = closetPoint;
            }
        }

        if (nearColl == null)
        {
            Debug.Log(currDir);
            return currDir;
        }

        Vector3 relativePos = npcTransform.InverseTransformPoint(nearClosetPoint);
        Vector3 dirToClosetPoint = (nearClosetPoint - npcTransform.position).normalized;
        Vector3 newDir;
        if (relativePos.x < 0)
        {
            newDir = Vector3.Cross(npcTransform.up, dirToClosetPoint);
        }
        else
        {
            newDir = -Vector3.Cross(npcTransform.up, dirToClosetPoint);
        }
        Debug.DrawRay(npcTransform.position, newDir, Color.red);
       
        // Distance to the closest obstacle clamped after subtracting personal area
        var clampedDistance = Mathf.Clamp(nearCollDistance - _personalArea, 0, _radius);
        // Inverted: closer obstacle => larger value
        var inversedClampedDistance = _radius - clampedDistance;
        // Normalized to 0..1 (0 = far, 1 = very close)
        var proportionalDistance = inversedClampedDistance / _radius;
        // Blend current direction toward avoidance direction
        return Vector3.Lerp(currDir, newDir, proportionalDistance);
    }

    public Vector3 GetDir2(Vector3 currDir, bool calculateY = true)
    {
        if (Physics.SphereCast(npcTransform.position, _personalArea, currDir, out RaycastHit hit, _radius, _obsMask))
        {
            var hitPoint = hit.point;
            var hitDistance = hit.distance;

            Vector3 relativePos = npcTransform.InverseTransformPoint(hitPoint);
            Vector3 dirToClosetPoint = (hitPoint - npcTransform.position).normalized;
            Vector3 newDir;
            if (relativePos.x < 0)
            {
                newDir = Vector3.Cross(npcTransform.up, dirToClosetPoint);
            }
            else
            {
                newDir = -Vector3.Cross(npcTransform.up, dirToClosetPoint);
            }
            Debug.DrawRay(npcTransform.position, newDir, Color.red);
            
            // Distance to hit point clamped after subtracting personal area
            var clampedDistance = Mathf.Clamp(hitDistance - _personalArea, 0, _radius);
            // Inverted: closer obstacle => larger value
            var inversedClampedDistance = _radius - clampedDistance; 
            // Normalized to 0..1 (0 = far, 1 = very close)
            var proportionalDistance = inversedClampedDistance / _radius;
            // Lerp using normalized directions, then restore original magnitude
            return Vector3.Lerp(currDir.normalized, newDir.normalized, proportionalDistance).normalized * currDir.magnitude;
        }
        return currDir;
    }
}
