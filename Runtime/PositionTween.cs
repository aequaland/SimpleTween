using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/*
    Some Objects don't have local movement like rigidbody or character controller, so to simplify things, only world coordinates are accepted
    Rigidbody needs to be Kinematic to work properly
    RectTransform takes into account the pivot (center, corner, right, etc)
    Must be enabled: PackageManager -> Build-in -> Physics (also Physics2D)
 */

/*
    TODO: Hacer una funcion ReturnPosition que devuelva el objeto a su posición anterior (util para botones, etc)
    TODO: Auto-enable PackageManager -> Build-in -> Physics (also Physics2D)
 */

public static class PositionTween
{
    // Dictionary holding the Objects currently used by the active tween
    static Dictionary<int, object[]> _TweenValues = new Dictionary<int, object[]>(); // object[] index order is in -> TweenValues

    private enum TweenValues
    {
        Coroutine,      // Coroutine
        OriginalPos,    // Vector3
        TargetPos,      // Vector3
        AnimationTime,  // float
        Interpolation,  // Interpolation
        EndAction       // UnityAction
    }

    /* Base of all the tweens */
    private static IEnumerator RelocateCoroutine<T>(T unityObject, Vector3 targetPosition, float moveTime, Interpolation interpolation) where T : UnityEngine.Object
    {
        Vector3 originalPos = GetPosition(unityObject);

        float progress = 0f;
        float step;
        Vector3 newPos;

        do
        {
            // Add the new tiny extra amount
            progress += Time.deltaTime / moveTime;
            progress = Mathf.Clamp01(progress);

            // Apply changes
            step = InterpolationCurve.GetInterpolatedStep(progress, interpolation);
            newPos = Vector3.Lerp(originalPos, targetPosition, step);

            SetPosition(unityObject, newPos);


            // Wait for the next frame
            yield return null;
        }
        while (progress < 1f); // Keep moving while we don't reach any goal
    }

    #region Move
    private static Coroutine DoMove<T>(T unityObject, Vector3 targetPosition, float moveTime, Interpolation interpolation, UnityAction endAction) where T : UnityEngine.Object
    {
        /* Validate */
        if (unityObject == null)
        {
            Debug.LogWarning("Component <b>not initialized</b>. It is null");
            return null;
        }

        Vector3 currentPos = GetPosition(unityObject);

        if (targetPosition.Equals(currentPos))
        {
            Debug.LogWarning("Trying to move to our <b>current position</b>");
            return null;
        }

        moveTime = Mathf.Max(0f, moveTime); // Force positive values


        /* Override check */
        if (_TweenValues.ContainsKey(unityObject.GetInstanceID()))
        {
            return null;
            //return OverrideTween(unityObject, targetColor, recolorTime, interpolation, endAction); // If it tweening, override the new values.
        }


        /* Do the tween */
        Coroutine moveCoroutine = TweenMono.Instance.StartCoroutine(MoveCoroutine(unityObject, targetPosition, moveTime, interpolation, endAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { moveCoroutine, currentPos, targetPosition, moveTime, interpolation, endAction });

        return moveCoroutine;
    }

    private static IEnumerator MoveCoroutine<T>(T unityObject, Vector3 targetPosition, float moveTime, Interpolation interpolation, UnityAction endAction) where T : UnityEngine.Object
    {
        yield return RelocateCoroutine(unityObject, targetPosition, moveTime, interpolation); // Nested coroutine can be stopped if the main coroutine is stopped only if we dont use StartCoroutine in the nested coroutine (Ienumerator)

        endAction?.Invoke();

        // Remove the data
        _TweenValues.Remove(unityObject.GetInstanceID());
    }

    //private static Coroutine OverrideMove<T>(T unityObject, Vector3 newTargetPosition, float newMoveTime, Interpolation newInterpolation, UnityAction endAction) where T : UnityEngine.Object
    //{
    //    object[] oldValues;

    //    if (!_TweenValues.TryGetValue(unityObject.GetInstanceID(), out oldValues))
    //    {
    //        Debug.LogWarning("Trying to override an <b>non-existent</b> dictionary entry");
    //        return null;
    //    }

    //    // Init old values     
    //    Vector3 originalPosition = (Vector3)oldValues[(int)TweenValues.OriginalPos];
    //    Vector3 oldTargetPosition = (Vector3)oldValues[(int)TweenValues.TargetPos];
    //    float oldMoveTime = (float)oldValues[(int)TweenValues.AnimationTime];
    //    Interpolation oldInterpolation = (Interpolation)oldValues[(int)TweenValues.Interpolation];


    //    /* CASE 1: Same values */
    //    if (oldTargetPosition == newTargetPosition && oldMoveTime == newMoveTime && oldInterpolation == newInterpolation)
    //    {
    //        Debug.LogWarning("Trying to override the <b>same values</b>");
    //        return null;
    //    }

    //    // Stop previous coroutine
    //    TweenMono.Instance.StopCoroutine((Coroutine)oldValues[(int)TweenValues.Coroutine]);

    //    // Get values
    //    Vector3 currentPosition = GetPosition(unityObject);

    //    // New values
    //    float updatedPositionTime = newMoveTime;
    //    Vector3 newOriginalPoition = oldTargetPosition;

    //    /* CASE 2: Same target position but different options values */
    //    if (oldTargetPosition == newTargetPosition)
    //    {

    //        //Debug.LogWarning($"{unityObject.name}: We are already moving to that target");

    //        newOriginalPoition = originalPosition; // Keep the original color

    //        if (oldMoveTime != newMoveTime) // Has different move time -> Change speed
    //        {

    //            //Debug.LogWarning($"{unityObject.name}: Changing move speed");

    //            float step = GetColorInterpolationStep(originalPosition, newTargetPosition, currentPosition);

    //            updatedPositionTime = newMoveTime * (1.0f - step);
    //        }

    //        if (oldInterpolation != newInterpolation)
    //        {
    //            // TODO
    //            // Forzamos el color del step al que seria interpolado con esa nueva interpolación y continuamos con la nueva interpolación (con la misma velocidad)...
    //        }
    //    }
    //    /* CASE 3: Target is same as original(previous) color but different options values */
    //    else if (originalPosition == newTargetColor)
    //    {

    //        //Debug.LogWarning($"{unityObject.name}: We are morphing to previous/original color");

    //        float step = GetColorInterpolationStep(oldTargetPosition, originalPosition, currentPosition);

    //        updatedPositionTime = newRecolorTime * (1.0f - step);

    //        if (oldInterpolation != newInterpolation)
    //        {
    //            // TODO
    //            // Forzamos el color del step al que seria interpolado con esa nueva interpolación y continuamos con la nueva interpolación (con la misma velocidad)...
    //        }
    //    }


    //    /* Start tween */
    //    Coroutine overrideCoroutine = TweenMono.Instance.StartCoroutine(RecolorCoroutine(unityObject, newTargetColor, updatedPositionTime, newInterpolation, newEndAction));

    //    _TweenValues[unityObject.GetInstanceID()] = new object[] { overrideCoroutine, newOriginalPoition, newTargetColor, newRecolorTime, newInterpolation, newEndAction };

    //    return overrideCoroutine;
    //}

    #endregion

    #region Utils 
    private static Vector3 GetPosition<T>(T unityObject) where T : UnityEngine.Object
    {
        switch (unityObject)
        {
            case Rigidbody rigidbody:

                return rigidbody.position;

            case RectTransform rectTransform:

                return rectTransform.transform.position;

            case Transform transform:

                return transform.transform.position;

            default:
                Debug.LogWarning("This type is not implemented!");
                return Vector3.zero;
        }
    }

    private static void SetPosition<T>(T unityObject, Vector3 newPos) where T : UnityEngine.Object
    {
        Debug.Log(newPos);

        switch (unityObject)
        {
            case Rigidbody rigidbody:

                rigidbody.MovePosition(newPos); // Use transform to move, but also have collisions. Use it when isKinematic is enabled!
                break;

            case RectTransform rectTransform:

                rectTransform.position = newPos;
                break;

            case Transform transform:

                transform.position = newPos;
                break;

            default:
                Debug.LogWarning("This type is not implemented!");
                break;
        }
    }

    public static bool IsPointOnLine(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        return (lineStart - point).normalized == (lineEnd - point).normalized;
    }

    public static float GetPositionInterpolationStep(Vector3 startPos, Vector3 targetPos, Vector3 interpolatedPos)
    {
        // Evade 0 division
        if (startPos == targetPos)
        {
            Debug.Log("Start and target has the same values");
            return 0f;
        }

        return Vector3.Distance(startPos, interpolatedPos) / Vector3.Distance(startPos, targetPos); // Should we clamp 01 the value?
    }

    #endregion

    #region Extensions
    #region Move types extended

    /* === REMEMBER: Every new type, add it also in the GetPosition() and SetPosition() functions === */

    // Rigidbody
    public static Coroutine Move(this Rigidbody rigidbody, Vector3 targetPosition, float moveTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoMove(rigidbody, targetPosition, moveTime, interpolation, endAction);
    }

    // RectTransform
    public static Coroutine Move(this RectTransform rectTransform, Vector3 targetPosition, float moveTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoMove(rectTransform, targetPosition, moveTime, interpolation, endAction);
    }

    // Transform
    public static Coroutine Move(this Transform Transform, Vector3 targetPosition, float moveTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoMove(Transform, targetPosition, moveTime, interpolation, endAction);
    }

    #endregion
    #endregion


}
