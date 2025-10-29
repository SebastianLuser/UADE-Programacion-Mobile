using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;
using System.Collections;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachineSplineDolly))]
public class TutorialSplineSequence : MonoBehaviour
{
    public Transform lookAtTarget;
    public SplineContainer firstSpline;
    public SplineContainer nextSpline;

    public GameObject canvasToDisable;

    public float firstLegSeconds = 10f;
    public float secondLegSeconds = 10f;

    public enum EasingType { Linear, EaseIn, EaseOut, EaseInOut, SmoothStep }
    public EasingType easingType = EasingType.Linear;

    public int startPriority = 50;
    public int endPriority = 9;
    public bool debugLog = false;

    CinemachineCamera cam;
    CinemachineSplineDolly dolly;
    CinemachineRotationComposer rot;
    Coroutine seq;
    bool driving;
    float targetPosKnot;

    void Awake()
    {
        cam   = GetComponent<CinemachineCamera>();
        dolly = GetComponent<CinemachineSplineDolly>();
        rot   = GetComponent<CinemachineRotationComposer>();

        if (rot) rot.enabled = false;
        cam.LookAt = null;

        if (firstSpline) dolly.Spline = firstSpline;
    }

    void Start()
    {
        cam.Priority.Value = startPriority;
        canvasToDisable.SetActive(false);
        PlayTutorial();
    }

    public void PlayTutorial()
    {
        if (seq != null) StopCoroutine(seq);
        seq = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (!ValidateSpline(dolly.Spline, "primer")) yield break;

        float endA = Mathf.Min(4f, MaxKnotInclusive(dolly.Spline));
        yield return MoveKnotRange(0f, endA, Mathf.Max(0.01f, firstLegSeconds));

        if (lookAtTarget)
        {
            cam.LookAt = lookAtTarget;
            if (rot) rot.enabled = true;
        }

        if (nextSpline)
        {
            dolly.Spline = nextSpline;
            targetPosKnot = 0f;
            yield return null;

            if (!ValidateSpline(nextSpline, "segundo")) yield break;

            float endB = Mathf.Min(4f, MaxKnotInclusive(nextSpline));
            yield return MoveKnotRange(4f, 0f, Mathf.Max(0.01f, secondLegSeconds));
        }

        if (rot) rot.enabled = false;
        cam.LookAt = null;
        cam.Priority.Value = endPriority;

        driving = false;
        seq = null;
        
        canvasToDisable.SetActive(true);
    }

    IEnumerator MoveKnotRange(float fromKnot, float toKnot, float seconds)
    {
        driving = true;
        targetPosKnot = fromKnot;

        // esperar a que haya deltaTime (>0) por si la escena arranca pausada
        yield return null;
        int guard = 0;
        while (Time.deltaTime == 0f && Time.unscaledDeltaTime == 0f && guard++ < 120)
            yield return null;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = Time.unscaledDeltaTime;

            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / seconds);
            float w = EvaluateEasing(u);

            targetPosKnot = Mathf.Lerp(fromKnot, toKnot, w);

            if (debugLog)
                Debug.Log($"[TSS] target={targetPosKnot:0.###} actual={dolly.CameraPosition:0.###}");

            yield return null;
        }

        targetPosKnot = toKnot;
    }

    float EvaluateEasing(float u)
    {
        switch (easingType)
        {
            default:
            case EasingType.Linear:     return u;
            case EasingType.SmoothStep: return u * u * (3f - 2f * u); // smootherstep básico
            case EasingType.EaseIn:     return u * u;
            case EasingType.EaseOut:    return 1f - (1f - u) * (1f - u);
            case EasingType.EaseInOut:  return (u < 0.5f) ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
        }
    }

    void LateUpdate()
    {
        if (!driving) return;

        dolly.AutomaticDolly.Enabled = false;
        dolly.PositionUnits = PathIndexUnit.Knot;
        dolly.Damping.Enabled = false;

        dolly.CameraPosition = targetPosKnot;
    }

    float MaxKnotInclusive(SplineContainer sc)
    {
        var s = sc.Spline;
        return s.Closed ? s.Count : Mathf.Max(0, s.Count - 1);
    }

    bool ValidateSpline(SplineContainer sc, string label)
    {
        if (!sc) { Debug.LogError($"[TSS] {label} spline = NULL"); return false; }
        int c = sc.Spline.Count;
        if (c < 5) { Debug.LogError($"[TSS] {label} spline necesita >= 5 knots (tiene {c})"); return false; }
        return true;
    }
}
