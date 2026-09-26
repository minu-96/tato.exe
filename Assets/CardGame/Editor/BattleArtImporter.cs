using UnityEditor;

/// <summary>
/// 전투 아트·카드 일러스트 폴더에 <b>새로 넣은 그림</b>을 Sprite(Single)로 가져온다.
///
/// 이 프로젝트는 새 그림이 Multiple 모드로 들어와 자동 분할되는 일이 반복됐다.
/// 그러면 그림 한 장이 여러 조각으로 쪼개지고(엉뚱한 조각이 연결되기도 했다),
/// 투명 여백이 잘려 적·카드마다 크기가 달라진다. 이 폴더들의 그림은 전부 "통째로 한 장"이다.
///
/// 처음 가져올 때만 적용한다 — 이미 있는 그림의 설정을 인스펙터에서 바꾸면 그대로 둔다.
/// </summary>
public class BattleArtImporter : AssetPostprocessor
{
    static readonly string[] Folders =
    {
        "Assets/Resorces/Battle/",
        "Assets/Resorces/Launcher/card_illustrations_39/",
    };

    void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing) return;   // 처음 가져올 때만
        bool target = false;
        foreach (var f in Folders)
            if (assetPath.StartsWith(f)) { target = true; break; }
        if (!target) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
    }
}
