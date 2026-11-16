using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace WebSwing
{
    // Uses the user's saved Space Setup; it does not scan arbitrary live objects.
    public sealed class RoomSurfaces : MonoBehaviour
    {
        const string ScenePermission="com.oculus.permission.USE_SCENE";
        ARPlaneManager planes;
        ARSession session;
        WorldModes owner;
        Camera head;
        bool active,permissionRequested;
        float nextRefresh;
        public GameObject FallbackFloor;
        readonly HashSet<ARPlane> configured=new HashSet<ARPlane>();
        public string Status { get; private set; }="Loading saved room / scan only if room changed";
        public bool HasPlanes { get; private set; }
        public bool HasFloor { get; private set; }
        public void Initialize(WorldModes modes,ARSession arSession)
        {
            owner=modes; session=arSession; head=GetComponentInChildren<Camera>();
            // AR Foundation managers must remain disabled until scene permission is granted.
            bool wasActive=gameObject.activeSelf; gameObject.SetActive(false);
            planes=gameObject.AddComponent<ARPlaneManager>(); planes.enabled=false;
            planes.requestedDetectionMode=PlaneDetectionMode.Horizontal|PlaneDetectionMode.Vertical;
            gameObject.SetActive(wasActive);
        }
        bool Permitted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(ScenePermission);
#else
            return false;
#endif
        }
        public void SetRoomActive(bool value)
        {
            active=value;
            planes.enabled=value&&XRSettings.isDeviceActive&&Permitted();
            foreach(var plane in planes.trackables) plane.gameObject.SetActive(value);
            if(value)
            {
                Status=Permitted()?"Loading saved room / no new scan required":"Allow spatial data once / virtual floor available";
                RequestPermission(false);
            }
        }
        // Saved scene geometry is owned and localized by Quest across app sessions.
        // Never replay raw room coordinates or automatically open scene capture.
        public void RequestScan()
        {
            if(!XRSettings.isDeviceActive){Status="Room scan requires Quest";return;}
            if(Permitted())BeginCapture();else RequestPermission(true);
        }
        void RequestPermission(bool scanAfterPermission)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!active||!XRSettings.isDeviceActive||Permitted()||permissionRequested||(!scanAfterPermission&&PlayerPrefs.GetInt("WebSwing.ScenePermissionDeclined",0)==1))return;
            permissionRequested=true;
            var callbacks=new PermissionCallbacks();
            callbacks.PermissionGranted+=_=>
            {
                permissionRequested=false;PlayerPrefs.SetInt("WebSwing.ScenePermissionDeclined",0);PlayerPrefs.Save();planes.enabled=active;
                if(scanAfterPermission)BeginCapture();else Status="Loading saved room / no new scan required";
            };
            callbacks.PermissionDenied+=_=>{permissionRequested=false;PlayerPrefs.SetInt("WebSwing.ScenePermissionDeclined",1);PlayerPrefs.Save();Status="Virtual floor / allow spatial data to load saved room";};
            Permission.RequestUserPermission(ScenePermission,callbacks);
#endif
        }
        void BeginCapture()
        {
            if(!active) return;
            planes.enabled=Permitted();
            var meta=session.subsystem as MetaOpenXRSessionSubsystem;
            bool requested=meta!=null&&meta.TryRequestSceneCapture();
            Status=requested?"Complete Quest room setup, then choose SPAWN":"Room setup not ready; check Quest Space Setup";
        }
        void Update()
        {
            if(!active||Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.5f;
            if(!Permitted())RequestPermission(false);
            if(Permitted()&&!planes.enabled&&XRSettings.isDeviceActive) planes.enabled=true;
            HasPlanes=false; bool floor=false; int count=0;
            foreach(var plane in planes.trackables)
            {
                if(!plane.gameObject.activeSelf) plane.gameObject.SetActive(true);
                bool tracked=plane.trackingState!=TrackingState.None;
                if(configured.Add(plane))
                {
                    plane.gameObject.layer=9;
                    plane.gameObject.AddComponent<MeshCollider>();
                    plane.gameObject.AddComponent<ARPlaneMeshVisualizer>();
                }
                var collider=plane.GetComponent<MeshCollider>(); collider.enabled=tracked;
                if(!tracked) continue;
                count++;
                if(Vector3.Dot(plane.transform.up,Vector3.up)>.8f)
                {
                    HasPlanes=true;
                    floor|=(plane.classifications&PlaneClassifications.Floor)!=0;
                }
            }
            bool firstFloor=floor&&!HasFloor;HasFloor=floor;
            if(FallbackFloor) FallbackFloor.SetActive(!floor);
            if(firstFloor)owner.SavedRoomReady();
            if(count>0) Status="ROOM: "+count+" surfaces / SPAWN places cargo on floor or table";
        }
        public bool TryPlacement(Vector3 desired,float halfHeight,out Vector3 position)
        {
            position=desired; float highest=float.NegativeInfinity;
            if(!planes||!planes.enabled) return false;
            foreach(var plane in planes.trackables)
            {
                if(plane.trackingState==TrackingState.None||Vector3.Dot(plane.transform.up,Vector3.up)<.9f) continue;
                if((plane.classifications&(PlaneClassifications.Floor|PlaneClassifications.Table))==0) continue;
                Vector3 local=plane.transform.InverseTransformPoint(desired); local.y=0;
                Vector3 world=plane.transform.TransformPoint(local);
                if(world.y>head.transform.position.y-.25f||world.y<highest) continue;
                // Keep the whole crate footprint inside the scanned boundary, including near edges.
                bool fits=true;
                for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
                    fits&=Inside(plane,new Vector2(local.x+x*.30f,local.z+z*.30f));
                if(!fits) continue;
                highest=world.y; position=world+Vector3.up*(halfHeight+.015f);
            }
            return !float.IsNegativeInfinity(highest);
        }
        static bool Inside(ARPlane plane,Vector2 p)
        {
            var boundary=plane.boundary; bool inside=false;
            for(int i=0,j=boundary.Length-1;i<boundary.Length;j=i++)
            {
                Vector2 a=boundary[i],b=boundary[j];
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }
    }
}
