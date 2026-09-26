using UnityEngine;

public class AspectRatioController : MonoBehaviour
{
    public static AspectRatioController Instance { get; private set; }

    // 모아모아는 16:9. 창 크기 자체는 DisplaySettings(런처)가 정한다 — 타이틀 ◀▶로 고른 값.
    // 여기서는 창을 끌어서 비율이 틀어졌을 때만 16:9로 되돌린다.
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