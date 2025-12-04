using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "AllyTakeCoverState", menuName = "Main/FSM/Ally States/Take Cover")]
    public class AllyTakeCoverState : State
    {
        [SerializeField] private float suppressFireCooldown = 0.45f;
        [SerializeField] private float threatMoveThreshold = 1.75f;
        [SerializeField] private float coverProbeHeightOffset = 0.5f;
        [SerializeField] private float fallbackCoverDistance = 2f;

        private readonly RaycastHit[] m_coverHits = new RaycastHit[6];
        private float m_nextSuppressFireTime;
        private Vector3 m_lastThreatPosition;

        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Ally ally)
            {
                ally.StateTimer = 0f;
                m_nextSuppressFireTime = 0f;
                m_lastThreatPosition = Vector3.zero;
                ally.LastTimeTookCover = Time.time;
                ally.ClearCoverPoint();

                Vector3 threat = GetThreatPosition(ally);
                if (threat != Vector3.zero)
                {
                    ally.LastKnownGuardPosition = threat;
                }

                AcquireCover(ally, true, threat);
                MyLogger.LogDebug($"Ally {ally.name}: Enter TakeCover");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is not Ally ally) return;

            ally.StateTimer += Time.deltaTime;

            Vector3 threat = GetThreatPosition(ally);
            if (threat != Vector3.zero)
            {
                ally.LastKnownGuardPosition = threat;
            }

            if (ShouldReacquireCover(ally, threat))
            {
                AcquireCover(ally, false, threat);
            }

            MoveToCover(ally);
            AimAndSuppress(ally, threat);
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Ally ally)
            {
                ally.ClearCoverPoint();
                MyLogger.LogDebug($"Ally {ally.name}: Exit TakeCover");
            }
        }

        private Vector3 GetThreatPosition(Ally ally)
        {
            Transform target = ally.GetTargetTransform();
            if (target != null && ally.CanSeeGuard(ally.GetCurrentTarget()))
            {
                return target.position;
            }

            return ally.LastKnownGuardPosition;
        }

        private bool ShouldReacquireCover(Ally ally, Vector3 reference)
        {
            if (!ally.HasCoverPoint) return true;

            bool timeElapsed = ally.StateTimer >= ally.CoverRepositionCooldown;
            bool threatMoved = (reference - m_lastThreatPosition).sqrMagnitude >= threatMoveThreshold * threatMoveThreshold;

            return timeElapsed && threatMoved;
        }

        private void AcquireCover(Ally ally, bool force, Vector3 referencePosition)
        {
            if (referencePosition == Vector3.zero && !force && ally.HasCoverPoint) return;

            Vector3 awayDir = (ally.transform.position - referencePosition).normalized;
            if (awayDir.sqrMagnitude < 0.01f)
            {
                awayDir = -ally.transform.forward;
            }

            Vector3 origin = ally.transform.position + Vector3.up * coverProbeHeightOffset;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                ally.CoverProbeRadius,
                awayDir,
                m_coverHits,
                ally.CoverProbeDistance,
                ally.ObstaclesMask);

            if (hitCount > 0)
            {
                RaycastHit bestHit = m_coverHits[0];
                for (int i = 1; i < hitCount; i++)
                {
                    if (m_coverHits[i].distance < bestHit.distance)
                    {
                        bestHit = m_coverHits[i];
                    }
                }

                Vector3 impactPoint = bestHit.point != Vector3.zero
                    ? bestHit.point
                    : bestHit.collider.bounds.ClosestPoint(ally.transform.position);

                Vector3 threatDir = referencePosition != Vector3.zero
                    ? (referencePosition - impactPoint).normalized
                    : ally.transform.forward;
                threatDir.y = 0f;
                if (threatDir.sqrMagnitude < 0.01f) threatDir = ally.transform.forward;

                Vector3 coverPoint = impactPoint - threatDir * ally.CoverOffsetFromObstacle;
                coverPoint.y = ally.transform.position.y;

                ally.SetCoverPoint(coverPoint);
                ally.SetCoverDebug(bestHit.collider, impactPoint, bestHit.normal);
            }
            else
            {
                Vector3 fallback = ally.transform.position + awayDir * Mathf.Max(fallbackCoverDistance, ally.CoverOffsetFromObstacle);
                fallback.y = ally.transform.position.y;
                ally.SetCoverPoint(fallback);
                ally.SetCoverDebug(null, Vector3.zero, Vector3.zero);
            }

            ally.StateTimer = 0f;
            m_lastThreatPosition = referencePosition;
        }

        private void MoveToCover(Ally ally)
        {
            if (!ally.HasCoverPoint) return;

            ally.MoveTowardsPoint(
                ally.CoverPoint,
                ally.FollowSpeed,
                ally.CoverArrivalTolerance);
        }

        private void AimAndSuppress(Ally ally, Vector3 threat)
        {
            Vector3 lookTarget = threat != Vector3.zero ? threat : ally.LastKnownGuardPosition;
            Vector3 aimDir = lookTarget - ally.transform.position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.1f)
            {
                ally.FaceDirectionTowards(aimDir.normalized);
            }

            if (Time.time < m_nextSuppressFireTime) return;

            float distance = lookTarget != Vector3.zero
                ? Vector3.Distance(ally.transform.position, lookTarget)
                : Mathf.Infinity;

            if (distance <= ally.AttackRange + 1.5f && ally.CanShoot())
            {
                ally.Shoot(aimDir.normalized);
                m_nextSuppressFireTime = Time.time + suppressFireCooldown;
            }
        }
    }
}
