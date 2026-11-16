using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace WebSwing.Editor
{
    // Local exhibition app: no Meta store identity or entitlement requirement.
    public sealed class MetaAppIdentity : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(file);
            var application = document.SelectSingleNode("/manifest/application") as XmlElement;
            if (application == null) throw new InvalidOperationException("Android application element missing.");
            XmlElement identity = null;
            foreach (XmlNode node in application.ChildNodes)
                if (node is XmlElement element && element.Name == "meta-data" &&
                    element.GetAttribute("name", Android) == "com.oculus.app_id") identity = element;
            if (identity != null) application.RemoveChild(identity);
            // Unity declares the icon in the launcher module, not unityLibrary.
            const string launcherIcon="@mipmap/app_icon";
            foreach (XmlNode node in application.ChildNodes)
                if (node is XmlElement activity && activity.Name == "activity" &&
                    activity.GetAttribute("name", Android) == "com.unity3d.player.UnityPlayerActivity")
                {
                    activity.SetAttribute("label", Android, "Webシューターnaispo");
                    activity.SetAttribute("icon", Android, launcherIcon);
                    activity.SetAttribute("enabled", Android, "true");
                    activity.SetAttribute("exported", Android, "true");
                }
            document.Save(file);
            // OpenXR generates this library separately. Use the public device names;
            // its default list includes the retired Quest 1 and internal code names.
            var xrFile = Path.Combine(path, "xrmanifest.androidlib/AndroidManifest.xml");
            if (!File.Exists(xrFile)) throw new FileNotFoundException("Generated XR manifest missing.", xrFile);
            var xrDocument = new XmlDocument();
            xrDocument.Load(xrFile);
            var xrApplication = xrDocument.SelectSingleNode("/manifest/application") as XmlElement;
            if (xrApplication == null) throw new InvalidOperationException("XR application element missing.");
            bool devicesFound = false;
            foreach (XmlNode node in xrApplication.ChildNodes)
                if (node is XmlElement element && element.Name == "meta-data" &&
                    element.GetAttribute("name", Android) == "com.oculus.supportedDevices")
                {
                    element.SetAttribute("value", Android, "quest3|quest3s");
                    devicesFound = true;
                }
            if (!devicesFound) throw new InvalidOperationException("XR supportedDevices entry missing.");
            xrDocument.Save(xrFile);
            UnityEngine.Debug.Log("NAISPO_LOCAL_IDENTITY_OK");
        }
    }
}

