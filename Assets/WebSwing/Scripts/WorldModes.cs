using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace WebSwing
{
    public sealed class WorldModes : MonoBehaviour
    {
        public bool IsMR { get; private set; }
        public bool MenuVisible => menu && menu.activeSelf;
        public Transform city;
        DemoWorld world;
        Camera camera;
        ARCameraManager passthrough;
        ARSession session;
        GameObject room,menu;
        Material panelMaterial,buttonMaterial,hoverMaterial;
        readonly Dictionary<Collider,int> buttons=new Dictionary<Collider,int>();
        readonly LineRenderer[] rays=new LineRenderer[2];
        readonly Material[] pointerMaterials=new Material[2];
        readonly Collider[] hovered=new Collider[2];
        TextMesh status,hint;
        Transform buttonRoot;
        bool settingsPage,menuMotorEnabled=true;
        readonly float[] dwell=new float[2],lastPoint=new float[2];
        readonly bool[] selected=new bool[2];
        readonly Transform[] progress=new Transform[2];
        readonly Dictionary<int,TextMesh> labels=new Dictionary<int,TextMesh>();
        public bool ComfortEnabled { get; private set; }=false;
        bool cityStart=true;
        RoomSurfaces surfaces;
        bool passthroughReported;
        float lastAction;
        float roomSpawnTime;int roomSpawnShots;
        public void SavedRoomReady()
        {
            if(IsMR&&room&&Time.unscaledTime-roomSpawnTime<8&&roomSpawnShots==world.shooter.ShotSequence+world.leftShooter.ShotSequence)
                SpawnRoomObjects();
        }
        void Start()
        {
            if(NaispoSession.Active)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
                SelectMode(!intent.Call<bool>("getBooleanExtra","web_vr",false));
#endif
        }
        public void Initialize(DemoWorld demo)
        {
            world=demo; camera=demo.input.head.GetComponent<Camera>();
            var origin=demo.motor.gameObject.AddComponent<XROrigin>();
            var offset=new GameObject("Camera floor offset"); offset.transform.SetParent(demo.motor.transform,false);
            camera.transform.SetParent(offset.transform,false);
            origin.Camera=camera; origin.CameraFloorOffsetObject=offset;
            origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor; origin.CameraYOffset=0;
            var ar=new GameObject("AR Session"); session=ar.AddComponent<ARSession>(); session.enabled=false;
            passthrough=camera.gameObject.AddComponent<ARCameraManager>(); passthrough.enabled=false;
            camera.allowHDR=false;
            surfaces=demo.motor.gameObject.AddComponent<RoomSurfaces>(); surfaces.Initialize(this,session);
            world.motor.SetComfort(false);
            BuildMenu(); HideMenu();
            Debug.Log("WEB_MODES_READY: VR city / MR room. Either hand peace sign for 1.5 seconds opens menu.");
        }
        void Update()
        {
            if(IsMR && passthrough.subsystem!=null && passthrough.subsystem.running && !passthroughReported)
            { passthroughReported=true; Debug.Log("WEB_PASSTHROUGH_READY: Meta camera subsystem running."); }
            if(!MenuVisible) return;
            for(int i=0;i<2;i++) if(Time.unscaledTime-lastPoint[i]>.15f) ClearPointer(i);
            status.text=settingsPage?"SETTINGS":(IsMR?"MR ROOM":"VR CITY");
            if(labels.TryGetValue(3,out var action)) action.text=IsMR?"RECALL BOXES":"WALL: "+(world.motor.WallWalkEnabled?"ON":"OFF");
            if(labels.TryGetValue(4,out var walk)) walk.text="ARM WALK: "+(world.input.WalkEnabled?"ON":"OFF");
            if(labels.TryGetValue(6,out var comfort)) comfort.text="COMFORT: "+(ComfortEnabled?"ON":"OFF");
            if(labels.TryGetValue(9,out var lift))lift.text="THROW: "+(world.shooter.EasyThrow?"AIM":"MOTION");
            if(labels.TryGetValue(8,out var travel))travel.text=IsMR?"RESCAN ROOM":(cityStart?"GO TO TRAINING":"GO TO CITY");
            hint.text="Point at a button for 1 second\n"+(IsMR?surfaces.Status:"Fist: reel / pull: launch / near wall + arm swings: climb");
        }
        public void SelectMode(bool mr)
        {
            world.input.ReleaseAll(); IsMR=mr; passthroughReported=false;
            if(city) city.gameObject.SetActive(!mr);
            world.motor.SetMovementLocked(mr);if(!mr)world.motor.SetWallWalkEnabled(true); RenderSettings.fog=!mr;
            camera.clearFlags=mr?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
            camera.backgroundColor=mr?Color.clear:new Color(.57f,.65f,.72f,1);
            if(XRSettings.isDeviceActive) { session.enabled=mr; passthrough.enabled=mr; }
            surfaces.SetRoomActive(mr);
            if(mr) SpawnRoomObjects(); else if(room) room.SetActive(false);
            HideMenu(); Debug.Log("WEB_MODE_CHANGED: "+(mr?"MR / physical-room coordinates":"VR / city locomotion"));
        }
        public void ResetPosition()
        {
            world.input.ReleaseAll(); world.motor.ResetPlayer();
            foreach(var cargo in FindObjectsByType<CargoReset>(FindObjectsSortMode.None))
                cargo.Recall();
            if(IsMR) SpawnRoomObjects(); HideMenu();
        }
        public void RecallObjects()
        {
            world.input.ReleaseAll();
            foreach(var cargo in FindObjectsByType<CargoReset>(FindObjectsSortMode.None))cargo.Recall();
            foreach(var target in FindObjectsByType<MRBreakable>(FindObjectsSortMode.None))target.Restore();
        }
        public void SpawnRoomObjects()
        {
            roomSpawnTime=Time.unscaledTime;roomSpawnShots=world.shooter.ShotSequence+world.leftShooter.ShotSequence;
            if(room) { room.SetActive(false); Destroy(room); }
            room=new GameObject("MR / room cargo");
            room.AddComponent<RoomWebMarks>();
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            Vector3 center=Vector3.ProjectOnPlane(camera.transform.position,Vector3.up)+forward*1.7f;
            for(int i=0;i<4;i++)
            {
                Vector3 p=center+right*((i%2-.5f)*.75f)+forward*(i/2*.7f); p.y=.24f;
                if(surfaces.TryPlacement(p,.24f,out var placed)) p=placed;
                var crate=CityEnvironment.SpawnCrate(p,2,i);
                crate.transform.SetParent(room.transform,true); crate.transform.localScale=Vector3.one*.42f;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="MR virtual floor collider";
            floor.transform.SetParent(room.transform,false); floor.transform.position=new Vector3(center.x,-.06f,center.z);
            floor.transform.localScale=new Vector3(30,.1f,30); floor.GetComponent<Renderer>().enabled=false;
            floor.SetActive(!surfaces.HasFloor);
            surfaces.FallbackFloor=floor;
            RoomToy.Create(room.transform,center+forward*1.1f);
            for(int i=0;i<3;i++)
            {
                Vector3 basePosition=center+right*((i-1)*.65f)+forward*2f;basePosition.y=0;
                if(surfaces.TryPlacement(basePosition,0,out var placedVase))basePosition=placedVase;
                var stand=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stand.name="Vase pedestal";stand.layer=9;
                stand.transform.SetParent(room.transform,true);stand.transform.position=basePosition+Vector3.up*.38f;stand.transform.localScale=new Vector3(.28f,.38f,.28f);
                stand.GetComponent<Renderer>().sharedMaterial=CityEnvironment.MetalSurface;
                MRBreakable.Create(room.transform,basePosition+Vector3.up*1.0f,i==0?new Color(.2f,.8f,.85f):i==1?new Color(.95f,.5f,.38f):new Color(.95f,.8f,.2f));
            }
            Vector3 chairPosition=center-right*1.35f+forward*.2f;chairPosition.y=0;
            if(surfaces.TryPlacement(chairPosition,0,out var chairPlaced))chairPosition=chairPlaced;
            MRFurniture.Chair(room.transform,chairPosition,Quaternion.LookRotation(-forward));
            Vector3 doorPosition=center+right*1.5f+forward*.8f;doorPosition.y=0;
            if(surfaces.TryPlacement(doorPosition,0,out var doorPlaced))doorPosition=doorPlaced;
            MRFurniture.Door(room.transform,doorPosition,Quaternion.LookRotation(-forward));
            Debug.Log("WEB_MR_OBJECTS_READY: four crates in front of the user, floor at tracking origin.");
        }
        public void ShowMenu()
        {
            if(NaispoSession.Active)return;
            if(!MenuVisible)menuMotorEnabled=world.motor.enabled;
            world.input.ReleaseAll(); settingsPage=false; BuildPage();
            world.motor.enabled=false;
            menu.transform.SetParent(null,true);
            menu.transform.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(0,-.04f,1.2f)),camera.transform.rotation);
            menu.SetActive(true); Cursor.lockState=CursorLockMode.None;
        }
        void LateUpdate()
        {
            if(!MenuVisible) return;
            // Keep the menu nearby, with a dead zone so small head movements do not move the target.
            Vector3 target=camera.transform.TransformPoint(new Vector3(0,-.04f,1.2f));
            if(Vector3.Distance(menu.transform.position,target)>.28f || Quaternion.Angle(menu.transform.rotation,camera.transform.rotation)>20)
            {
                menu.transform.position=Vector3.Lerp(menu.transform.position,target,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
                menu.transform.rotation=Quaternion.Slerp(menu.transform.rotation,camera.transform.rotation,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
            }
        }
        public void HideMenu()
        {
            if(MenuVisible && world)world.motor.enabled=menuMotorEnabled;
            if(menu) menu.SetActive(false);
            ClearPointer(0); ClearPointer(1);
        }
        public void ClearPointer(int hand)
        {
            if(hand<0||hand>1) return;
            hovered[hand]=null; dwell[hand]=0; selected[hand]=false;
            if(rays[hand])rays[hand].enabled=false;
            if(progress[hand])progress[hand].gameObject.SetActive(false);
            RefreshHighlights();
        }
        void RefreshHighlights()
        {
            foreach(var item in buttons) if(item.Key)
                item.Key.GetComponent<Renderer>().sharedMaterial=item.Key==hovered[0]||item.Key==hovered[1]?hoverMaterial:buttonMaterial;
        }
        public void Point(int hand,Ray ray,bool desktopClick)
        {
            if(!MenuVisible || hand<0 || hand>1) return;
            lastPoint[hand]=Time.unscaledTime;
            Collider target=null; Vector3 end=ray.origin+ray.direction*2;
            Physics.SyncTransforms();
            if(Physics.Raycast(ray,out RaycastHit hit,3,1<<10,QueryTriggerInteraction.Collide) && buttons.ContainsKey(hit.collider)) { target=hit.collider; end=hit.point; }
            if(target!=hovered[hand]) { dwell[hand]=0; selected[hand]=false; hovered[hand]=target; }
            RefreshHighlights();
            rays[hand].enabled=true; rays[hand].SetPosition(0,ray.origin); rays[hand].SetPosition(1,end);
            progress[hand].gameObject.SetActive(target!=null);
            if(!target) return;
            dwell[hand]+=Mathf.Min(Time.unscaledDeltaTime,.05f);
            float fill=Mathf.Clamp01(dwell[hand]/1f);
            // World-sized bar, separate for each hand; one hand cannot advance the other's timer.
            progress[hand].SetParent(target.transform,false);
            progress[hand].localScale=new Vector3(Mathf.Max(.001f,fill),.055f,1.3f);
            progress[hand].localPosition=new Vector3((fill-1)*.5f,-.38f,-1.3f-hand*.15f);
            bool click=desktopClick&&!XRSettings.isDeviceActive;
            if(selected[hand] || (!click&&fill<1) || Time.unscaledTime-lastAction<.4f) return;
            selected[hand]=true; lastAction=Time.unscaledTime;
            Execute(buttons[target]);
        }
        void Execute(int id)
        {
            switch(id)
            {
                case 0: SelectMode(false); break;
                case 1: SelectMode(true); break;
                case 2: ResetPosition(); break;
                case 3:
                    if(IsMR)RecallObjects();
                    else { world.input.ReleaseAll(); world.motor.SetWallWalkEnabled(!world.motor.WallWalkEnabled); world.input.WalkEnabled=true; }
                    break;
                case 4: world.input.WalkEnabled=!world.input.WalkEnabled; break;
                case 5: HideMenu(); break;
                case 6: world.input.ReleaseAll(); ComfortEnabled=!ComfortEnabled; world.motor.SetComfort(ComfortEnabled); break;
                case 8:
                    if(IsMR)surfaces.RequestScan();
                    else{world.input.ReleaseAll();cityStart=!cityStart;world.motor.SetSpawn(cityStart?new Vector3(0,5.6f,-2):new Vector3(0,.12f,-38));HideMenu();}
                    break;
                case 9: world.input.ReleaseAll();world.shooter.EasyThrow=world.leftShooter.EasyThrow=!world.shooter.EasyThrow;break;
                case 10: settingsPage=true; BuildPage(); break;
                case 11: settingsPage=false; BuildPage(); break;
            }
        }
        void BuildPage()
        {
            ClearPointer(0); ClearPointer(1);
            foreach(var bar in progress) if(bar)bar.SetParent(menu.transform,false);
            buttons.Clear(); labels.Clear();
            if(buttonRoot) { buttonRoot.gameObject.SetActive(false); Destroy(buttonRoot.gameObject); }
            buttonRoot=new GameObject("Menu page").transform; buttonRoot.SetParent(menu.transform,false);
            int[] ids=settingsPage?new[]{4,6,9,8,11,5}:new[]{0,1,2,3,10,5};
            string[] names=settingsPage?new[]{"ARM WALK","COMFORT","THROW: AIM","RESCAN ROOM","BACK","CLOSE"}:new[]{"VR CITY","MR ROOM","RESET POSITION",IsMR?"RECALL BOXES":"WALL: OFF","SETTINGS","CLOSE"};
            for(int i=0;i<ids.Length;i++)
            {
                var button=GameObject.CreatePrimitive(PrimitiveType.Cube); button.name=names[i]; button.layer=10;
                button.transform.SetParent(buttonRoot,false);
                button.transform.localPosition=new Vector3(i%2==0?-.225f:.225f,.16f-i/2*.16f,-.025f);
                button.transform.localScale=new Vector3(.41f,.135f,.018f);
                button.GetComponent<Renderer>().sharedMaterial=buttonMaterial;
                var collider=button.GetComponent<Collider>(); collider.isTrigger=true; buttons.Add(collider,ids[i]);
                labels[ids[i]]=Text(buttonRoot,button.transform.localPosition+new Vector3(0,.006f,-.022f),names[i],.020f);
            }
        }
        void BuildMenu()
        {
            panelMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.025f,.045f,.065f,1)};
            buttonMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.10f,.19f,.25f,1)};
            hoverMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.12f,.53f,.65f,1)};
            menu=new GameObject("Hand menu");
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube); panel.transform.SetParent(menu.transform,false);
            panel.transform.localScale=new Vector3(.96f,.78f,.02f); Destroy(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial=panelMaterial;
            status=Text(menu.transform,new Vector3(0,.31f,-.04f),"WEB MENU",.03f);
            hint=Text(menu.transform,new Vector3(0,-.30f,-.04f),"Point for 1 second",.012f);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Menu pointer"); go.transform.SetParent(transform,false);
                pointerMaterials[i]=new Material(hoverMaterial){color=HandAim.ColorForHand(i)};
                rays[i]=go.AddComponent<LineRenderer>(); rays[i].sharedMaterial=pointerMaterials[i];
                rays[i].positionCount=2; rays[i].startWidth=.003f; rays[i].endWidth=.002f; rays[i].enabled=false;
                var bar=GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name="Selection progress "+i;
                Destroy(bar.GetComponent<Collider>()); bar.GetComponent<Renderer>().sharedMaterial=pointerMaterials[i];
                progress[i]=bar.transform; bar.transform.SetParent(menu.transform,false); bar.SetActive(false);
            }
            BuildPage();
        }
        void OnDestroy()
        {
            if(menu)Destroy(menu); if(panelMaterial)Destroy(panelMaterial);
            if(buttonMaterial)Destroy(buttonMaterial); if(hoverMaterial)Destroy(hoverMaterial);
            foreach(var material in pointerMaterials)if(material)Destroy(material);
        }
        static TextMesh Text(Transform parent,Vector3 p,string value,float size)
        {
            var go=new GameObject("Menu text"); go.transform.SetParent(parent,false); go.transform.localPosition=p;
            var text=go.AddComponent<TextMesh>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=64; text.characterSize=size*.25f; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
            text.color=new Color(.89f,.95f,1); text.text=value;
            go.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(text.font); return text;
        }
    }
}
