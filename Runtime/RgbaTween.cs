using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/*
    How works:
        - If we call a tween while the object is already working, we override the tween. 
        - If it's changing direction or has similar values, the override take some decitions to make it more smooth and responsive.
        - Open to changes. All Unity objects that has a color atribute can be integrated.
 
    TODO:
        - Override interpolations
        - Different interpolation curves:  https://www.wolframalpha.com  //  https://answers.unity.com/questions/1159921/move-object-for-a-to-b-with-bounce-effect.html
 */


public static class RgbaTween
{
    // Dictionary holding the Objects currently used by the active tween
    static Dictionary<int, object[]> _TweenValues = new Dictionary<int, object[]>(); // object[] index order is in -> TweenValues

    private enum TweenValues
    {
        Coroutine,      // Coroutine
        OriginalColor,  // Color
        TargetColor,    // Color
        AnimationTime,  // float
        Interpolation,  // Interpolation
        EndAction       // UnityAction
    }


    /* Base of all the tweens */
    private static IEnumerator MorphColorCoroutine<T>(T unityObject, Color targetColor, float recolorTime, Interpolation interpolation) where T : UnityEngine.Object
    {
        Color originalColor = GetColor(unityObject);
        float progress = 0f;
        float step;
        Color newColor;

        do
        {
            // Add the new tiny extra amount
            progress += Time.deltaTime / recolorTime;
            progress = Mathf.Clamp01(progress);

            // Apply changes
            step = InterpolationCurve.GetInterpolatedStep(progress, interpolation);
            newColor = Color.Lerp(originalColor, targetColor, step);

            SetColor(unityObject, newColor);

            // Wait for the next frame
            yield return null;
        }
        while (progress < 1f); // Keep moving while we don't reach any goal
    }

    #region Recolor
    private static Coroutine DoRecolor<T>(T unityObject, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null) where T : UnityEngine.Object
    {
        /* Validate */
        if (unityObject == null)
        {
            Debug.LogWarning("Component <b>not initialized</b>. It is null");
            return null;
        }

        Color currentColor = GetColor(unityObject);

        if (targetColor.Equals(currentColor))
        {
            Debug.LogWarning("Trying to recolor with the <b>same color</b>");
            return null;
        }

        recolorTime = Mathf.Max(0f, recolorTime); // Force positive values


        /* Override check */
        if (_TweenValues.ContainsKey(unityObject.GetInstanceID()))
        {
            return OverrideTween(unityObject, targetColor, recolorTime, interpolation, endAction); // If it tweening, override the new values.
        }


        /* Do the tween */
        Coroutine recolorCoroutine = TweenMono.Instance.StartCoroutine(RecolorCoroutine(unityObject, targetColor, recolorTime, interpolation, endAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { recolorCoroutine, currentColor, targetColor, recolorTime, interpolation, endAction });

        return recolorCoroutine;
    }


    private static IEnumerator RecolorCoroutine<T>(T unityObject, Color targetColor, float recolorTime, Interpolation interpolation, UnityAction endAction) where T : UnityEngine.Object
    {
        yield return MorphColorCoroutine(unityObject, targetColor, recolorTime, interpolation); // Nested coroutine can be stopped if the main coroutine is stopped only if we dont use StartCoroutine in the nested coroutine (Ienumerator)

        endAction?.Invoke();

        // Remove the data
        _TweenValues.Remove(unityObject.GetInstanceID());
    }

    private static Coroutine OverrideTween<T>(T unityObject, Color newTargetColor, float newRecolorTime, Interpolation newInterpolation, UnityAction newEndAction) where T : UnityEngine.Object
    {
        object[] oldValues;

        if (!_TweenValues.TryGetValue(unityObject.GetInstanceID(), out oldValues))
        {
            Debug.LogWarning("Trying to override an <b>non-existent</b> dictionary entry");
            return null;
        }

        // Init old values     
        Color originalColor = (Color)oldValues[(int)TweenValues.OriginalColor];
        Color oldTargetColor = (Color)oldValues[(int)TweenValues.TargetColor];
        float oldRecolorTime = (float)oldValues[(int)TweenValues.AnimationTime];
        Interpolation oldInterpolation = (Interpolation)oldValues[(int)TweenValues.Interpolation];


        /* CASE 1: Same values */
        if (oldTargetColor == newTargetColor && oldRecolorTime == newRecolorTime && oldInterpolation == newInterpolation)
        {
            Debug.LogWarning("Trying to override the <b>same values</b>");
            return null;
        }

        // Stop previous coroutine
        TweenMono.Instance.StopCoroutine((Coroutine)oldValues[(int)TweenValues.Coroutine]);

        // Get values
        Color currentColor = GetColor(unityObject);

        // New values
        float updatedRecolorTime = newRecolorTime;
        Color newOriginalColor = oldTargetColor;

        /* CASE 2: Same target color but different options values */
        if (oldTargetColor == newTargetColor)
        {

            //Debug.LogWarning($"{unityObject.name}: We are already morphing to that color");

            newOriginalColor = originalColor; // Keep the original color

            if (oldRecolorTime != newRecolorTime) // Has different recolor time -> Change speed
            {

                //Debug.LogWarning($"{unityObject.name}: Changing recolor speed");

                float step = GetColorInterpolationStep(originalColor, newTargetColor, currentColor);

                updatedRecolorTime = newRecolorTime * (1.0f - step);
            }

            if (oldInterpolation != newInterpolation)
            {
                // TODO
                // Forzamos el color del step al que seria interpolado con esa nueva interpolación y continuamos con la nueva interpolación (con la misma velocidad)...
            }
        }
        /* CASE 3: Target is same as original(previous) color but different options values */
        else if (originalColor == newTargetColor)
        {

            //Debug.LogWarning($"{unityObject.name}: We are morphing to previous/original color");

            float step = GetColorInterpolationStep(oldTargetColor, originalColor, currentColor);

            updatedRecolorTime = newRecolorTime * (1.0f - step);

            if (oldInterpolation != newInterpolation)
            {
                // TODO
                // Forzamos el color del step al que seria interpolado con esa nueva interpolación y continuamos con la nueva interpolación (con la misma velocidad)...
            }
        }


        /* Start tween */
        Coroutine overrideCoroutine = TweenMono.Instance.StartCoroutine(RecolorCoroutine(unityObject, newTargetColor, updatedRecolorTime, newInterpolation, newEndAction));

        _TweenValues[unityObject.GetInstanceID()] = new object[] { overrideCoroutine, newOriginalColor, newTargetColor, newRecolorTime, newInterpolation, newEndAction };

        return overrideCoroutine;
    }
    #endregion

    #region Fade
    private static Coroutine DoFade<T>(T unityObject, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null) where T : UnityEngine.Object
    {
        /* Check values */
        if (unityObject == null)
        {
            Debug.LogWarning("Component <b>not initialized</b>. It is null");
            return null;
        }

        Color currentColor = GetColor(unityObject);

        float targetAlpha = IsFadeOut(fadeMode, currentColor.a) ? 0f : 1f; // Get the target alpha via the fade mode

        Color targetColor = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);

        if (targetColor.Equals(currentColor))
        {
            Debug.LogWarning("Trying to fade with the <b>same alpha</b>");
            return null;
        }

        fadeTime = Mathf.Max(0f, fadeTime); // Force positive values


        /* Override check */
        if (_TweenValues.ContainsKey(unityObject.GetInstanceID()))
        {
            return OverrideTween(unityObject, targetColor, fadeTime, interpolation, endAction);
        }


        /* Start tween */
        Coroutine fadeCoroutine = TweenMono.Instance.StartCoroutine(RecolorCoroutine(unityObject, targetColor, fadeTime, interpolation, endAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { fadeCoroutine, currentColor, targetColor, fadeTime, interpolation, endAction });

        return fadeCoroutine;
    }

    private static bool IsFadeOut(FadeMode fadeMode, float currentAlpha)
    {
        switch (fadeMode)
        {
            case FadeMode.Toggle:
                // If we are near to 0, we want to toggle to fade in (false). Viceversa for near 1 (true)
                return !(Mathf.Round(currentAlpha) <= 0.0f);  // maxAlpha == Mathf.Min(Mathf.Max(currentAlpha, minAlpha), maxAlpha); -> To have max and min

            case FadeMode.In:
                return false;

            case FadeMode.Out:
                return true;

            default:
                Debug.LogWarning("FadeMode not implemented");
                return false;
        }
    }
    #endregion

    #region Flash
    private static Coroutine DoFlash<T>(T unityObject, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null) where T : UnityEngine.Object
    {
        /* Check values */
        if (unityObject == null)
        {
            Debug.LogWarning("Component <b>not initialized</b>. It is null");
            return null;
        }

        Color currentColor = GetColor(unityObject);

        if (flashColor.Equals(currentColor))
        {
            Debug.LogWarning("Trying to flash with the <b>current color</b>");
            return null;
        }

        // Force positive values
        loops = Mathf.Max(1, loops);
        goTime = Mathf.Max(0, goTime);
        backTime = Mathf.Max(0, backTime);
        middleWait = Mathf.Max(0, middleWait);
        loopWait = Mathf.Max(0, loopWait);


        /* Override check */
        if (_TweenValues.ContainsKey(unityObject.GetInstanceID()))
        {
            // There is no need to control this coroutine because it calls SoftStop and control himself
            return TweenMono.Instance.StartCoroutine(OverrideFlashCoroutine(unityObject, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));
        }


        /* Start tween */
        Coroutine flashCoroutine = TweenMono.Instance.StartCoroutine(FlashCoroutine(unityObject, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { flashCoroutine, currentColor, currentColor, (goTime + backTime) / 2f, goInterpolation, endBackAction }); // Original and target color are the same because we are looping (going and returning)
        return flashCoroutine;
    }

    private static IEnumerator FlashCoroutine<T>(T unityObject, Color flashColor, int loops, float goTime, float backTime, float middleWait, float loopWait, Interpolation goInterpolation, Interpolation backInterpolation, UnityAction endGoAction, UnityAction endBackAction) where T : UnityEngine.Object
    {
        int iterations = 0;

        // Save original color
        Color startColor = GetColor(unityObject);

        // Waits
        WaitForSeconds betweenWait = new WaitForSeconds(middleWait);
        WaitForSeconds endWait = new WaitForSeconds(loopWait);

        while (iterations < loops)
        {
            // Go
            yield return MorphColorCoroutine(unityObject, flashColor, goTime, goInterpolation);
            endGoAction?.Invoke();

            // Wait
            yield return betweenWait;

            // Return
            yield return MorphColorCoroutine(unityObject, startColor, backTime, backInterpolation);
            endBackAction?.Invoke();

            iterations++;

            // Wait
            yield return endWait;
        }

        // Remove the data
        _TweenValues.Remove(unityObject.GetInstanceID());
    }

    private static IEnumerator OverrideFlashCoroutine<T>(T unityObject, Color flashColor, int loops, float goTime, float backTime, float middleWait, float loopWait, Interpolation goInterpolation, Interpolation backInterpolation, UnityAction endGoAction, UnityAction endBackAction) where T : UnityEngine.Object
    {
        /* Wait until stop the ColorTween */
        yield return StopObject(unityObject);

        Color originalColor = GetColor(unityObject); // Get the original color before the flash start

        /* Do the new flash */
        Coroutine overrideCoroutine = TweenMono.Instance.StartCoroutine(FlashCoroutine(unityObject, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { overrideCoroutine, originalColor, originalColor, (goTime + backTime) / 2f, goInterpolation, endBackAction }); // Original and target color are the same because we are looping (going and returning)

        yield return overrideCoroutine;
    }
    #endregion

    #region Blink 

    private static Coroutine DoBlink<T>(T unityObject, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null) where T : UnityEngine.Object
    {
        /* Check values */
        if (unityObject == null)
        {
            Debug.LogWarning("Component <b>not initialized</b>. It is null");
            return null;
        }

        Color currentColor = GetColor(unityObject);

        // Force positive values
        loops = Mathf.Max(1, loops);
        goTime = Mathf.Max(0, goTime);
        backTime = Mathf.Max(0, backTime);
        middleWait = Mathf.Max(0, middleWait);
        loopWait = Mathf.Max(0, loopWait);


        /* Override check */
        if (_TweenValues.ContainsKey(unityObject.GetInstanceID()))
        {
            // There is no need to control this coroutine because it calls SoftStop and control himself
            return TweenMono.Instance.StartCoroutine(OverrideBlinkCoroutine(unityObject, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));
        }


        /* Start tween */
        Coroutine blinkCoroutine = TweenMono.Instance.StartCoroutine(BlinkCoroutine(unityObject, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { blinkCoroutine, currentColor, currentColor, (goTime + backTime) / 2f, goInterpolation, endBackAction }); // Original and target color are the same because we are looping (going and returning)
        return blinkCoroutine;
    }

    private static IEnumerator BlinkCoroutine<T>(T unityObject, BlinkMode blinkMode, int loops, float goTime, float backTime, float middleWait, float loopWait, Interpolation goInterpolation, Interpolation backInterpolation, UnityAction endGoAction, UnityAction endBackAction) where T : UnityEngine.Object
    {
        int iterations = 0;

        WaitForSeconds betweenWait = new WaitForSeconds(middleWait);
        WaitForSeconds endWait = new WaitForSeconds(loopWait);

        Color currentColor = GetColor(unityObject);
        Color goColor = new Color(currentColor.r, currentColor.g, currentColor.b, Convert.ToSingle(blinkMode == BlinkMode.InOut));
        Color backColor = new Color(currentColor.r, currentColor.g, currentColor.b, Convert.ToSingle(blinkMode != BlinkMode.InOut));

        while (iterations < loops)
        {
            // Go
            yield return MorphColorCoroutine(unityObject, goColor, goTime, goInterpolation);
            endGoAction?.Invoke();

            // Wait
            yield return betweenWait;

            // Return
            yield return MorphColorCoroutine(unityObject, backColor, backTime, backInterpolation);
            endBackAction?.Invoke();

            iterations++;

            // Wait
            yield return endWait;
        }

        // Remove the data
        _TweenValues.Remove(unityObject.GetInstanceID());
    }

    private static IEnumerator OverrideBlinkCoroutine<T>(T unityObject, BlinkMode blinkMode, int loops, float goTime, float backTime, float middleWait, float loopWait, Interpolation goInterpolation, Interpolation backInterpolation, UnityAction endGoAction, UnityAction endBackAction) where T : UnityEngine.Object
    {
        /* Wait until stop the ColorTween */
        yield return StopObject(unityObject);

        Color originalColor = GetColor(unityObject); // Get the original color before the flash start

        /* Do the new blink */
        Coroutine overrideCoroutine = TweenMono.Instance.StartCoroutine(BlinkCoroutine(unityObject, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction));

        _TweenValues.Add(unityObject.GetInstanceID(), new object[] { overrideCoroutine, originalColor, originalColor, (goTime + backTime) / 2f, goInterpolation, endBackAction }); // Original and target color are the same because we are looping (going and returning)

        yield return overrideCoroutine;
    }

    #endregion

    #region Stop

    #region Soft Stop 

    const float SOFT_STOP_TIME = 0.25f;

    public static Coroutine DoSoftStop<T>(T unityObject) where T : UnityEngine.Object
    {
        object[] tweenValues;

        if (!_TweenValues.TryGetValue(unityObject.GetInstanceID(), out tweenValues))
        {
            Debug.LogWarning("Trying to stop an <b>non-existent</b> tween");
            return null;
        }

        /* Stop current tween */
        TweenMono.Instance.StopCoroutine((Coroutine)tweenValues[(int)TweenValues.Coroutine]); // Works for recolor and the other tweens too                                                                                   


        /* Start a tween to go softly to the final color */
        Color targetColor;
        Color originalColor;
        Coroutine softStopCoroutine; // It's always a recolor coroutine
        UnityAction endAction = () => { Debug.Log($"Rgba <b>soft stop</b> finished for {unityObject.name}"); };

        /* Recolor quickly to the target color (Flash and Blink return to his original color)*/
        targetColor = (Color)tweenValues[(int)TweenValues.TargetColor];
        originalColor = (Color)tweenValues[(int)TweenValues.OriginalColor];

        softStopCoroutine = TweenMono.Instance.StartCoroutine(RecolorCoroutine(unityObject, targetColor, SOFT_STOP_TIME, Interpolation.Linear, endAction)); ;

        _TweenValues[unityObject.GetInstanceID()] = new object[] { softStopCoroutine, originalColor, targetColor, SOFT_STOP_TIME, Interpolation.Linear, endAction };

        return softStopCoroutine;
    }

    #endregion

    #region Forced Stop 

    #region Forced stop types extended
    // SpriteRenderer
    public static void ForcedStopColorTween(this SpriteRenderer spriteRenderer)
    {
        DoForcedStop(spriteRenderer);
    }

    // Image
    public static void ForcedStopColorTween(this Image image)
    {
        DoForcedStop(image);
    }

    // RawImage
    public static void ForcedStopColorTween(this RawImage image)
    {
        DoForcedStop(image);
    }

    // Material
    public static void ForcedStopColorTween(this Material material)
    {
        DoForcedStop(material);
    }

    // Text
    public static void ForcedStopColorTween(this Text text)
    {
        DoForcedStop(text);
    }

    // TextMeshProUGUI
    public static void ForcedStopColorTween(this TextMeshProUGUI text)
    {
        DoForcedStop(text);
    }

    // CanvasGroup
    public static void ForcedStopColorTween(this CanvasGroup canvasGroup)
    {
        DoForcedStop(canvasGroup);
    }
    #endregion

    public static void DoForcedStop<T>(T unityObject) where T : UnityEngine.Object
    {
        object[] tweenValues;

        if (!_TweenValues.TryGetValue(unityObject.GetInstanceID(), out tweenValues))
        {
            Debug.LogWarning("Trying to stop an <b>non-existent</b> tween");
            return;
        }

        TweenMono.Instance.StopCoroutine((Coroutine)tweenValues[(int)TweenValues.Coroutine]);

        // Clear values
        _TweenValues.Remove(unityObject.GetInstanceID());
    }

    #endregion

    private static Coroutine StopObject<T>(T unityObject) where T : UnityEngine.Object
    {
        switch (unityObject)
        {
            case SpriteRenderer spriteRenderer:
                return spriteRenderer.StopColorTween();

            case Image image:
                return image.StopColorTween();

            case RawImage rawImage:
                return rawImage.StopColorTween();

            case Text text:
                return text.StopColorTween();

            case TextMeshProUGUI textMeshPro:
                return textMeshPro.StopColorTween();

            case Material material:
                return material.StopColorTween();

            case CanvasGroup canvasGroup:
                return canvasGroup.StopColorTween();

            default:
                Debug.LogWarning("This type is not implemented!");
                return null;
        }
    }

    #endregion

    #region Utils
    public static float GetColorInterpolationStep(Color initialColor, Color finalColor, Color interpolatedColor)
    {
        /*
         * Get a value between 0.0f and 1.0f that represents the step value of the "interpolatedColor" between "initialColor" and "finalColor"
         * Ex: interpolatedColor = Color.Lerp(initialColor, finalColor, interpolationPosition);
         */

        float interpolationPosition = 0.0f;

        float initialColorDistance = Mathf.Abs(initialColor.r - interpolatedColor.r) + Mathf.Abs(initialColor.g - interpolatedColor.g) + Mathf.Abs(initialColor.b - interpolatedColor.b) + Mathf.Abs(initialColor.a - interpolatedColor.a);
        float finalColorDistance = Mathf.Abs(finalColor.r - interpolatedColor.r) + Mathf.Abs(finalColor.g - interpolatedColor.g) + Mathf.Abs(finalColor.b - interpolatedColor.b) + Mathf.Abs(finalColor.a - interpolatedColor.a);

        if (initialColorDistance + finalColorDistance != 0) // Evade 0 division
        {
            interpolationPosition = initialColorDistance / (initialColorDistance + finalColorDistance);
        }

        return interpolationPosition;
    }
    private static Color GetColor<T>(T unityObject) where T : UnityEngine.Object
    {
        switch (unityObject)
        {
            case SpriteRenderer spriteRenderer:
                return spriteRenderer.color;

            case Image image:
                return image.color;

            case RawImage rawImage:
                return rawImage.color;

            case Text text:
                return text.color;

            case TextMeshProUGUI textMeshPro:
                return textMeshPro.color;

            case Material material:
                return material.color;

            case CanvasGroup canvasGroup:
                return new Color(1f, 1f, 1f, canvasGroup.alpha);

            default:
                Debug.LogWarning("This type is not implemented!");
                return Color.magenta;
        }
    }

    private static void SetColor<T>(T unityObject, Color newColor) where T : UnityEngine.Object
    {
        switch (unityObject)
        {
            case SpriteRenderer spriteRenderer:
                spriteRenderer.color = newColor;
                break;

            case Image image:
                image.color = newColor;
                break;

            case RawImage rawImage:
                rawImage.color = newColor;
                break;

            case Text text:
                text.color = newColor;
                break;

            case TextMeshProUGUI textMeshPro:
                textMeshPro.color = newColor;
                break;

            case Material material:
                material.color = newColor;
                break;

            case CanvasGroup canvasGroup:
                canvasGroup.alpha = newColor.a;
                break;

            default:
                Debug.LogWarning("This type is not implemented!");
                break;
        }
    }
    #endregion

    #region Extensions
    #region Recolor types extended

    /* === REMEMBER: Every new type, add it also in the GetColor() and SetColor() functions === */

    // SpriteRenderer
    public static Coroutine Recolor(this SpriteRenderer spriteRenderer, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(spriteRenderer, targetColor, recolorTime, interpolation, endAction);
    }

    // Image
    public static Coroutine Recolor(this Image image, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(image, targetColor, recolorTime, interpolation, endAction);
    }

    // RawImage
    public static Coroutine Recolor(this RawImage rawImage, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(rawImage, targetColor, recolorTime, interpolation, endAction);
    }

    // Material
    public static Coroutine Recolor(this Material material, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(material, targetColor, recolorTime, interpolation, endAction);
    }

    // Text
    public static Coroutine Recolor(this Text text, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(text, targetColor, recolorTime, interpolation, endAction);
    }

    // TextMeshProUGUI
    public static Coroutine Recolor(this TextMeshProUGUI text, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(text, targetColor, recolorTime, interpolation, endAction);
    }

    // CanvasGroup (TODO)
    public static Coroutine Recolor(this CanvasGroup canvasGroup, Color targetColor = new Color(), float recolorTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoRecolor(canvasGroup, targetColor, recolorTime, interpolation, endAction);
    }

    #endregion

    #region Fade types extended

    // SpriteRenderer
    public static Coroutine Fade(this SpriteRenderer spriteRenderer, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(spriteRenderer, fadeMode, fadeTime, interpolation, endAction);
    }

    // Image
    public static Coroutine Fade(this Image image, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(image, fadeMode, fadeTime, interpolation, endAction);
    }

    // RawImage
    public static Coroutine Fade(this RawImage rawImage, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(rawImage, fadeMode, fadeTime, interpolation, endAction);
    }

    // Material
    public static Coroutine Fade(this Material material, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(material, fadeMode, fadeTime, interpolation, endAction);
    }

    // Text
    public static Coroutine Fade(this Text text, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(text, fadeMode, fadeTime, interpolation, endAction);
    }

    // TextMeshProUGUI
    public static Coroutine Fade(this TextMeshProUGUI text, FadeMode fadeMode = FadeMode.Toggle, float fadeTime = 1.0f, Interpolation interpolation = Interpolation.Linear, UnityAction endAction = null)
    {
        return DoFade(text, fadeMode, fadeTime, interpolation, endAction);
    }
    #endregion

    #region Flash types extended

    // SpriteRenderer
    public static Coroutine Flash(this SpriteRenderer spriteRenderer, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(spriteRenderer, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Image
    public static Coroutine Flash(this Image image, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(image, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // RawImage
    public static Coroutine Flash(this RawImage rawImage, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(rawImage, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Material
    public static Coroutine Flash(this Material material, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(material, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Text
    public static Coroutine Flash(this Text text, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(text, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // TextMeshProUGUI
    public static Coroutine Flash(this TextMeshProUGUI text, Color flashColor, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoFlash(text, flashColor, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }
    #endregion

    #region Blink types extended

    // SpriteRenderer
    public static Coroutine Blink(this SpriteRenderer spriteRenderer, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(spriteRenderer, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Image
    public static Coroutine Blink(this Image image, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(image, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // RawImage
    public static Coroutine Blink(this RawImage rawImage, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(rawImage, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Material
    public static Coroutine Blink(this Material material, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(material, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // Text
    public static Coroutine Blink(this Text text, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(text, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }

    // TextMeshProUGUI
    public static Coroutine Blink(this TextMeshProUGUI text, BlinkMode blinkMode = BlinkMode.InOut, int loops = int.MaxValue, float goTime = 0.5f, float backTime = 0.5f, float middleWait = 0f, float loopWait = 0f, Interpolation goInterpolation = Interpolation.Linear, Interpolation backInterpolation = Interpolation.Linear, UnityAction endGoAction = null, UnityAction endBackAction = null)
    {
        return DoBlink(text, blinkMode, loops, goTime, backTime, middleWait, loopWait, goInterpolation, backInterpolation, endGoAction, endBackAction);
    }


    #endregion

    #region Soft stop types extended
    // SpriteRenderer
    public static Coroutine StopColorTween(this SpriteRenderer spriteRenderer)
    {
        return DoSoftStop(spriteRenderer);
    }

    // Image
    public static Coroutine StopColorTween(this Image image)
    {
        return DoSoftStop(image);
    }

    // RawImage
    public static Coroutine StopColorTween(this RawImage image)
    {
        return DoSoftStop(image);
    }

    // Material
    public static Coroutine StopColorTween(this Material material)
    {
        return DoSoftStop(material);
    }

    // Text
    public static Coroutine StopColorTween(this Text text)
    {
        return DoSoftStop(text);
    }

    // TextMeshProUGUI
    public static Coroutine StopColorTween(this TextMeshProUGUI text)
    {
        return DoSoftStop(text);
    }

    // CanvasGroup
    public static Coroutine StopColorTween(this CanvasGroup canvasGroup)
    {
        return DoSoftStop(canvasGroup);
    }
    #endregion
    #endregion

}

public enum FadeMode
{
    Toggle,
    In,
    Out
}

public enum BlinkMode
{
    InOut,
    OutIn
}
