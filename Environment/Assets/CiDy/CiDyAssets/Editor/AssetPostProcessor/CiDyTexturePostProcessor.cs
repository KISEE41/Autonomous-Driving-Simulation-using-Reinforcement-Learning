using UnityEngine;
using UnityEditor;

public class CiDyTexturePostProcessor : AssetPostprocessor
{

    void OnPreprocessTexture()
    {

        if (assetPath.Contains("GAIAMasks"))
        {
            TextureImporter importer = assetImporter as TextureImporter;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;

            Object asset = AssetDatabase.LoadAssetAtPath(importer.assetPath, typeof(Texture2D));
            if(asset)
                EditorUtility.SetDirty(asset);
        }

    }
}
