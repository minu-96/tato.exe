using UnityEngine;

public class AspectRatioController : MonoBehaviour
{
    public static AspectRatioController Instance { get; private set; }

    // 모아모아는 1280×720 · 16:9 고정. 런처의 실행 버튼(GameLaunchButton)과 두 씬의
    // CanvasScaler 기준 해상도도 같은 값으로 맞춰져 있다 — 바꿀 땐 세 곳을 같이 바꿀 것.
    public const int TargetWidth = 1280;
    public const int TargetHeight = 720;
    private const float TargetAspect = (float)TargetWidth / TargetHeight;   // 16:9

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
        Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.Windowed);
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