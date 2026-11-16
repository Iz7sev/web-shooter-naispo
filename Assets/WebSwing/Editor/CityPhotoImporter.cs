using UnityEditor;
using UnityEngine;
namespace WebSwing.Editor
{
    public sealed class CityPhotoImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/CityPhoto/"))return;
            var importer=(TextureImporter)assetImporter;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;
            importer.textureCompression=TextureImporterCompression.Compressed;
            if(assetPath.Contains("_Normal"))importer.textureType=TextureImporterType.NormalMap;
        }
    }
}
