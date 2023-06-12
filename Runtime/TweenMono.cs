using UnityEngine;


public class TweenMono : MonoBehaviour
{
    private static MonoBehaviour _Monobehaviour;

    public static MonoBehaviour ColorMono { get { return _Monobehaviour; } }

    // Parar todas las corutines cuando se cambia de escena?

    #region Singleton
    private static TweenMono _Instance = null; // This value is shared for all instances
    public static TweenMono Instance
    {
        get
        {
            return _Instance;
        }
    }
    private void CheckSingleton()
    {
        if (_Instance != null && _Instance != this) //If the instance we got is from another
        {
            Destroy(this.gameObject);
            return;
        }
        else
        {
            _Instance = this;
        }

        this.transform.parent = null;   //Unparent for the sake of the DontDestroyOnLoad

        DontDestroyOnLoad(this.gameObject);
    }
    #endregion

    private void Awake()
    {
        CheckSingleton(); // Doble check in case there are multiple instances in the scene

        _Monobehaviour = GetComponent<MonoBehaviour>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // Runs after a scene gets loaded (can change to before)
    public static void StartThis() 
    {
        if (_Instance != null)
        {
            Debug.LogWarning($"The GameObject of this script is going to be unparented and moved into the \"DontDestroyOnLoad\" area. Check that you don't have any dependent behaviours in that GameObject");
            return;
        }

        Debug.Log("Initializing TweenMono");
        new GameObject("TweenMono", typeof(TweenMono));
    }

}
