using UnityEngine;

// https://easings.net/

public enum Interpolation
{
    Linear,
    EaseInOut,
    EaseInQuad,
    EaseOutQuad,
    EaseOutBounce

}

/*
    Useful if you don't want generate memory garbage
 */

public static class InterpolationCurve
{
    public static float GetInterpolatedStep(float step, Interpolation type = Interpolation.Linear)
    {
        if (step < 0 || 1 < step)
        {
            Debug.LogWarning("Step it's not in 0-1 range");
            return Mathf.Min(Mathf.Max(step, 0.0f), 1.0f);
        }

        switch (type)
        {
            case Interpolation.Linear:
                return Linear(step);

            case Interpolation.EaseInOut:
                return EaseInOut(step);            
            
            case Interpolation.EaseInQuad:
                return EaseInQuad(step);

            case Interpolation.EaseOutQuad:
                return EaseOutQuad(step);

            case Interpolation.EaseOutBounce:
                return EaseOutBounce(step);


            default:
                Debug.LogWarning("Type not implemented");
                return 0.5f;
        }
    }

    private static float Linear(float step)
    {
        return Mathf.Clamp01(step);
    }

    private static float EaseInOut(float step)
    {
        return -(Mathf.Cos(Mathf.PI * step) - 1.0f) / 2.0f;      
    }

    private static float EaseInQuad(float step)
    {
        return step * step;
    }

    private static float EaseOutQuad(float step) 
    {
        return 1 - (1 - step) * (1 - step);
    }

    private static float EaseOutBounce(float step)
    {
        float n1 = 7.5625f; // constant
        float d1 = 2.75f; // constant

        if (step < 1 / d1)
        {
            return n1 * step * step;
        }
        else if (step < 2 / d1)
        {
            return n1 * (step -= 1.5f / d1) * step + 0.75f;
        }
        else if (step < 2.5f / d1)
        {
            return n1 * (step -= 2.25f / d1) * step + 0.9375f;
        }
        else
        {
            return n1 * (step -= 2.625f / d1) * step + 0.984375f;
        }
    }
}
