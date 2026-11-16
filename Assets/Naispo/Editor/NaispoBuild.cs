using UnityEditor;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class NaispoBuild
    {
        [MenuItem("Naispo/Switch To Android")]
        public static void Android(){EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);}
        [MenuItem("Naispo/Build Quest APK")]
        public static void Build(){ConfigureSurfaces();EditorUserBuildSettings.selectedBuildTargetGroup=BuildTargetGroup.Android;ProjectSetup.BuildQuestApk();}
        public static void ConfigureSurfaces()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/WebSwing/PhotoSurfaces"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                importer.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.isReadable=false;importer.anisoLevel=4;
                importer.wrapMode=TextureWrapMode.Repeat;
                var settings=importer.GetPlatformTextureSettings("Android");settings.name="Android";settings.overridden=true;
                settings.maxTextureSize=1024;settings.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
