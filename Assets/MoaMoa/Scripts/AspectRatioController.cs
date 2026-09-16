using UnityEngine;

public class AspectRatioController : MonoBehaviour
{
    public static AspectRatioController Instance { get; private set; }

    private const float TargetAspect = 16f / 9f;

    void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
            
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }

    void Update()
    {
        float currentAspect = (float)Screen.width / Screen.height;

        if (Mathf.Abs(currentAspect - TargetAspect) > 0.01f)
        {
            int newWidth = Screen.width;
            int newHeight = Mathf.RoundToInt(newWidth / TargetAspect);

            Screen.SetResolution(newWidth, newHeight, FullScreenMode.Windowed);
        }
    }
}