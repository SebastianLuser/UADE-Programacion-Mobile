using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        //_radius = Mathf.Min(_radius, 1);
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
        Debug.Log("NewDir" + newDir);
        Debug.DrawRay(npcTransform.position, newDir, Color.red);

        var clampedDistance = Mathf.Clamp(nearCollDistance - _personalArea, 0, _radius); //Distancia clampeada hacia la colision más cercana
        var inversedClampedDistance = _radius - clampedDistance; //Invierto el valor sobre radio
        var proportionalDistance = inversedClampedDistance / _radius; // Lo convierto a valor entre 0 y 1
        return Vector3.Lerp(currDir, newDir, proportionalDistance); // Interpolo entre la direccion actual y la direccion de avoidance
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
            Debug.Log("NewDir" + newDir);
            Debug.DrawRay(npcTransform.position, newDir, Color.red);

            var clampedDistance = Mathf.Clamp(hitDistance - _personalArea, 0, _radius); //Distancia clampeada hacia la colision más cercana
            var inversedClampedDistance = _radius - clampedDistance; //Invierto el valor sobre radio
            var proportionalDistance = inversedClampedDistance / _radius; // Lo convierto a valor entre 0 y 1
            
            return Vector3.Lerp(currDir.normalized, newDir.normalized, proportionalDistance).normalized * currDir.magnitude; // Interpolo entre la direccion actual y la direccion de avoidance
        }
        return currDir;
    }
}
