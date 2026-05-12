using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.TextCore; // GlyphMetrics ve diğerleri için

public class SpriteToTMPAsset : EditorWindow
{
    [MenuItem("Tools/Antigravity/Create Combined Sprite Asset (Fixed Size)")]
    public static void CreateAsset()
    {
        Object[] selectedObjects = Selection.objects;
        List<Sprite> sprites = new List<Sprite>();
        foreach (var obj in selectedObjects)
        {
            if (obj is Sprite s) sprites.Add(s);
            else if (obj is Texture2D tex)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
            }
        }
        
        sprites = sprites.OrderBy(s => s.name).ToList();

        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("Hata", "Lütfen önce ikonlarını seç!", "Tamam");
            return;
        }

        string savePath = EditorUtility.SaveFilePanelInProject("Kaydet", "CombinedBuffs_Fixed", "png", "Dosya ismi girin");
        if (string.IsNullOrEmpty(savePath)) return;

        int targetSize = 256;
        Texture2D combinedTexture = new Texture2D(2048, 2048);
        Texture2D[] normalizedTextures = sprites.Select(s => RescaleTexture(GetReadableTexture(s), targetSize, targetSize)).ToArray();

        Rect[] rects = combinedTexture.PackTextures(normalizedTextures, 2, 2048);
        byte[] bytes = combinedTexture.EncodeToPNG();
        File.WriteAllBytes(savePath, bytes);
        AssetDatabase.ImportAsset(savePath);

        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;

        List<SpriteMetaData> metaData = new List<SpriteMetaData>();
        for (int i = 0; i < rects.Length; i++)
        {
            SpriteMetaData meta = new SpriteMetaData();
            meta.name = sprites[i].name;
            meta.rect = new Rect(
                rects[i].x * combinedTexture.width, 
                rects[i].y * combinedTexture.height, 
                rects[i].width * combinedTexture.width, 
                rects[i].height * combinedTexture.height
            );
            meta.alignment = (int)SpriteAlignment.Center;
            metaData.Add(meta);
        }
        importer.spritesheet = metaData.ToArray();
        importer.SaveAndReimport();

        // TMP Sprite Asset Oluştur
        Object newTex = AssetDatabase.LoadMainAssetAtPath(savePath);
        Selection.activeObject = newTex;
        EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Sprite Asset");

        // OLUŞAN ASSET'İ OTOMATİK DÜZELT
        string assetPath = Path.ChangeExtension(savePath, "asset");
        AssetDatabase.Refresh();
        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
        if (spriteAsset != null)
        {
            // HATA ÇÖZÜMÜ: FaceInfo bir struct olduğu için doğrudan değiştirilemez. 
            // Bir kopyasını alıp değiştirip geri atıyoruz.
            FaceInfo faceInfo = spriteAsset.faceInfo;
            faceInfo.pointSize = targetSize;
            faceInfo.ascentLine = targetSize;
            faceInfo.baseline = 0;
            faceInfo.descentLine = (int)(-targetSize * 0.2f);
            faceInfo.scale = 1;
            spriteAsset.faceInfo = faceInfo;
            
            // Glyph Metrics güncellemesi
            foreach (var glyph in spriteAsset.spriteGlyphTable)
            {
                glyph.metrics = new GlyphMetrics(targetSize, targetSize, 0, targetSize, targetSize);
            }
            
            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog("Başarılı", 
            $"{sprites.Count} ikon başarıyla birleştirildi ve hepsi {targetSize}px boyutuna eşitlendi!", "Harika!");
    }

    private static Texture2D RescaleTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private static Texture2D GetReadableTexture(Sprite sprite)
    {
        Rect r = sprite.textureRect;
        Texture2D source = sprite.texture;
        RenderTexture tmp = RenderTexture.GetTemporary((int)r.width, (int)r.height, 0);
        Graphics.Blit(source, tmp, new Vector2(1, 1), new Vector2(r.x / source.width, r.y / source.height));
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tmp;
        Texture2D myTexture2D = new Texture2D((int)r.width, (int)r.height);
        myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        myTexture2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);
        return myTexture2D;
    }
}
