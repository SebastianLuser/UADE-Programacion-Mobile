/*
using Scripts.FSM.Models;
using UnityEngine;
using Game.AI.Steering;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianFleeState", menuName = "Main/FSM/Civilian States/Flee State")]
    public class CivilianFleeState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Initialize safe timer (tracks time spent in safe conditions)
                civilian.SafeTimer = 0f;
                civilian.SetCurrentMaxSpeed(civilian.FleeSpeed);

                if (civilian.EnableDebugLogs)
                    MyLogger.LogInfo($"Civilian {civilian.name}: Entered Flee State - Fleeing at speed {civilian.FleeSpeed:F1}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Perform flee movement only - Decision Tree handles transitions
                PerformFleeMovement(civilian);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                if (civilian.EnableDebugLogs)
                    MyLogger.LogInfo($"Civilian {civilian.name}: Exited Flee State - Reached safety, returning to Idle");
            }
        }

        private void PerformFleeMovement(Civilian civilian)
        {
            if (civilian.Player == null) return;

            Vector3 playerPosition = civilian.Player.position;

            // Use Steering.Flee for direct sustained flee behavior
            Vector3 steering = Steering.Flee(
                civilian.transform.position,
                playerPosition,
                civilian.CurrentVelocity,
                civilian.FleeSpeed
            );

            // Apply through the movement funnel (ApplySteering -> ObstacleAvoidance.GetDir2 -> move)
            civilian.ApplySteering(steering);
        }

    }
}
*/
using Scripts.FSM.Models;
using UnityEngine;
using Game.AI.Steering;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianFleeState", menuName = "Main/FSM/Civilian States/Flee State")]
    public class CivilianFleeState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                civilian.SafeTimer = 0f;
                civilian.SetCurrentMaxSpeed(civilian.FleeSpeed);

                // A*: asegurar init (no hace alloc si ya estaba)
                civilian.EnsureFleePathfindingInitialized();

                Debug.Log($"------------------------- Civilian scaping using A* -------------------------------");

                if (civilian.EnableDebugLogs)
                    MyLogger.LogInfo($"Civilian {civilian.name}: Entered Flee State - Fleeing at speed {civilian.FleeSpeed:F1}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                PerformFleeMovement(civilian);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian && civilian.EnableDebugLogs)
            {
                MyLogger.LogInfo($"Civilian {civilian.name}: Exited Flee State");
            }
        }

        private void PerformFleeMovement(Civilian civilian)
        {
            // 1) Intentar A* por nodos (mobile-friendly)
            if (civilian.HasFleeGraph)
            {
                civilian.RecomputeFleePathIfNeeded(Time.time);

                if (civilian.HasFleePath)
                {
                    var steering = civilian.TickFleePathSteering();
                    civilian.ApplySteering(steering);

                    if (civilian.PathLenForDebug > 0)
                    {
                        int ci = Mathf.Clamp(civilian.PathFollower.CurrentIndex, 0, civilian.PathLenForDebug - 1);
                        var curWp = civilian.WorldPathForDebug[ci];
                        var last = civilian.WorldPathForDebug[civilian.PathLenForDebug - 1];
                        Debug.Log($"[FLEE A*] pathLen={civilian.PathLenForDebug} ci={ci} " +
                                  $"distCur={Vector3.Distance(civilian.transform.position, curWp):0.00} " +
                                  $"distLast={Vector3.Distance(civilian.transform.position, last):0.00} " +
                                  $"ReachedEnd={civilian.PathFollower.ReachedEnd} " +
                                  $"Ended={civilian.FleePathReachedEnd()}");
                    }
                    else
                    {
                        Debug.Log("[FLEE] Sin path -> Fallback Flee");
                    }



                    //Debug.Log($"------------------------- Civilian scaping using A* -------------------------------");
                    // Señal suave para DT: si llegó al punto seguro, empieza a acumular tiempo "a salvo".
                    if (civilian.FleePathReachedEnd())
                    {
                        civilian.SafeTimer += Time.deltaTime;
                        Debug.Log($"------------------------- Civilian already escaped using A* -------------------------------");
                    }
                    else
                    {
                        civilian.SafeTimer = 0f;
                        
                        Debug.Log($"------------------------- Civilian escaping using A* -------------------------------");
                    }

                    return; // usamos la ruta; no hace falta fallback
                }
            }

            // 2) Fallback: huida directa del jugador
            if (civilian.Player != null)
            {
                //Debug.Log($"+++++++++++++++++++++++++ Civilian scaping not using A* +++++++++++++++++++++++++++++++");
                Vector3 steering = Steering.Flee(
                    civilian.transform.position,
                    civilian.Player.position,
                    civilian.CurrentVelocity,
                    civilian.FleeSpeed
                );
                civilian.ApplySteering(steering);
            }
        }
    }
}