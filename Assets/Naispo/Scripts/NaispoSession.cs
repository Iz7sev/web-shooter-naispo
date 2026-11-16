using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
namespace WebSwing
{
    public enum NaispoPhase { Ready, AwaitHands, Countdown, Running, Paused, Ending, Finished }
    [Serializable] public sealed class NaispoCommand { public string action;public int level=1;public int duration=60; }
    [Serializable] public sealed class NaispoStatus
    {
        public string app="Webシューターnaispo",version="1.2.1",phase,reason,address;
        public int level,duration,score,best,throws;public bool mr;public float remaining,speed,distance;public bool left,right;public long revision;
    }
    public sealed class NaispoSession : MonoBehaviour
    {
        public static NaispoSession Active { get; private set; }
        public NaispoPhase Phase { get; private set; }=NaispoPhase.Ready;
        public int Level { get; private set; }=1;
        public int Duration { get; private set; }=120;
        public float Remaining { get; private set; }=120;
        public float RideSpeed { get; private set; }
        public string Reason { get; private set; }="スタッフがレベルを選んで開始してください";
        public bool AcceptHands=>Phase==NaispoPhase.Running;
        public bool Guided=>Level<=2;
        public bool MixedReality=>Level==4;
        public NaispoMRGame MRGame { get; private set; }
        public bool RouteControlled=>Phase!=NaispoPhase.Running||Guided;
        public bool HasBothHands=>checksHands||world&&world.input.IsHandTracked(0)&&world.input.IsHandTracked(1);
        public bool HasAnyHand=>checksHands||checksOneHand||world&&(world.input.IsHandTracked(0)||world.input.IsHandTracked(1));
        public long Revision { get; private set; }
        public NaispoOperatorServer Server { get; private set; }
        public DemoWorld world { get; private set; }
        public Vector3 RouteStart=>new Vector3(0,6,14)+routeOffset;
        Vector3 routeOffset;bool newVisitor;
        readonly List<GameObject> anchors=new List<GameObject>();
        TextMesh notice;Transform noticeRoot;float countdown,handsStable,lostTime,pulseUntil,pulseSpeed,nextStick,stopHold;
        bool oldPrimary,oldSecondary,oldMenu,checksHands,checksOneHand,initialized;
        float routeZ=14,routeHeight,verticalSpeed,liftUntil,liftSpeed;
        public static void Create(DemoWorld demo){var go=new GameObject("NAISPO exhibition session");go.AddComponent<NaispoSession>().world=demo;}
        void Awake(){Active=this;}
        void Start(){Initialize();}
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            if(!world)world=FindFirstObjectByType<DemoWorld>();
            world.motor.Exhibition=this;
            var oldSign=GameObject.Find("VR instructions");if(oldSign)oldSign.SetActive(false);
            var practice=FindFirstObjectByType<PracticeCourse>();if(practice)practice.gameObject.SetActive(false);
            Server=gameObject.AddComponent<NaispoOperatorServer>();Server.Initialize(this);
            MRGame=gameObject.AddComponent<NaispoMRGame>();MRGame.Initialize(this);
            BuildNotice();BuildAnchors();world.input.WalkEnabled=false;
            Prepare();gameObject.AddComponent<NaispoVignette>().Initialize(this,world.input.head.GetComponent<Camera>());
        }
        void BuildNotice()
        {
            noticeRoot=new GameObject("Naispo participant instructions").transform;
            noticeRoot.SetParent(world.input.head,false);noticeRoot.localPosition=new Vector3(0,-.07f,1.8f);
            notice=noticeRoot.gameObject.AddComponent<TextMesh>();notice.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            notice.anchor=TextAnchor.MiddleCenter;notice.alignment=TextAlignment.Center;notice.fontSize=64;notice.characterSize=.012f;notice.color=Color.white;
            notice.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(notice.font);
        }
        void BuildAnchors()
        {
            foreach(float z in new[]{32f,68f,104f,142f,180f,220f,258f})
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Naispo overhead web target";go.layer=8;
                go.transform.position=new Vector3(0,14,z);go.transform.localScale=Vector3.one*1.6f;
                var mat=new Material(Shader.Find("Standard")){color=new Color(.88f,.96f,1)};mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.2f,.5f,.6f));
                go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;anchors.Add(go);
            }
        }
        public bool Execute(NaispoCommand command)
        {
            if(command==null)return false;
            switch(command.action)
            {
                case "level":
                    if(Phase!=NaispoPhase.Ready&&Phase!=NaispoPhase.Finished)return false;
                    Level=Mathf.Clamp(command.level,1,4);Duration=MixedReality?60:120;Prepare();break;
                case "start":
                    if(Phase!=NaispoPhase.Ready&&Phase!=NaispoPhase.Finished)return false;
                    Prepare();Phase=NaispoPhase.AwaitHands;handsStable=0;Reason="コントローラーを置いて、参加者の両手をカメラの前へ";break;
                case "pause":
                    if(Phase!=NaispoPhase.Running&&Phase!=NaispoPhase.Countdown&&Phase!=NaispoPhase.AwaitHands)return false;
                    Pause("スタッフが一時停止しました");break;
                case "resume":
                    if(Phase!=NaispoPhase.Paused)return false;
                    Phase=NaispoPhase.Countdown;countdown=3;lostTime=0;Reason="カウントダウンして再開します";break;
                case "end":
                    Finish("スタッフが体験を終了しました");break;
                case "prepare":
                    if(Phase!=NaispoPhase.Ready&&Phase!=NaispoPhase.Finished)return false;
                    Prepare();break;
                default:return false;
            }
            Revision++;return true;
        }
        void Prepare()
        {
            world.input.ReleaseAll();world.motor.transform.rotation=Quaternion.identity;
            world.modes.SelectMode(MixedReality);world.motor.SetComfort(Level!=3);world.motor.SetWallWalkEnabled(Level==3);
            MRGame.ResetRound();
            routeOffset=Vector3.zero;newVisitor=true;world.motor.transform.rotation=Quaternion.identity;
            routeHeight=verticalSpeed=liftUntil=liftSpeed=0;
            world.input.WalkEnabled=Level==3;world.motor.SetSpawn(MixedReality?Vector3.zero:Guided?RouteStart:new Vector3(0,5.6f,-2));
            world.motor.Exhibition=this;world.motor.enabled=true;Phase=NaispoPhase.Ready;Remaining=Duration;RideSpeed=0;routeZ=14;
            pulseUntil=pulseSpeed=lostTime=countdown=0;Reason="準備完了。スタッフが開始を選択してください";
            foreach(var anchor in anchors)anchor.SetActive(Guided);
        }
        void Pause(string reason){Phase=NaispoPhase.Paused;Reason=reason;world.input.ReleaseAll();RideSpeed=verticalSpeed=0;pulseUntil=liftUntil=0;world.motor.StopForExhibition();}
        void Finish(string reason){if(MRGame)MRGame.SaveBest();Phase=NaispoPhase.Finished;Reason=reason;world.input.ReleaseAll();RideSpeed=verticalSpeed=0;pulseUntil=liftUntil=0;world.motor.StopForExhibition();Revision++;}
        public bool GuidedLaunch(int hand,float strength)
        {
            if(!AcceptHands||!Guided||!world.motor.IsAttached(hand)||!world.motor.IsGripping(hand))return false;
            float effort=Mathf.Clamp01(strength);
            pulseSpeed=Level==1?Mathf.Lerp(6.5f,8.5f,effort):Mathf.Lerp(9f,11.5f,effort);pulseUntil=Time.unscaledTime+(Level==1?1.6f:2f);
            liftSpeed=Level==1?Mathf.Lerp(3.8f,5.5f,effort):Mathf.Lerp(5f,7f,effort);liftUntil=Time.unscaledTime+(Level==1?.9f:1.1f);
            world.motor.Release();WebActionSound.Shoot(world.motor.BodyPoint);return true;
        }
        public Vector3 StepRide(SwingMotor motor,float dt,out Vector3 velocity)
        {
            Vector3 before=motor.transform.position;
            if(!Guided||Phase!=NaispoPhase.Running&&Phase!=NaispoPhase.Ending){velocity=Vector3.zero;return before;}
            float target=0;
            if(Phase==NaispoPhase.Running)
            {
                bool gripping=motor.IsGripping(0)||motor.IsGripping(1);
                target=gripping?(Level==1?5.2f:7f):0;
                if(Time.unscaledTime<pulseUntil)target=Mathf.Max(target,pulseSpeed);
            }
            target=Mathf.Min(target,Mathf.Sqrt(2f*(Level==1?2.4f:3f)*Mathf.Max(0,250-routeZ)));
            RideSpeed=Mathf.MoveTowards(RideSpeed,target,dt*(target>RideSpeed?(Level==1?2f:2.4f):(Level==1?2.4f:3f)));
            routeZ=Mathf.Min(250,routeZ+RideSpeed*dt);
            float verticalTarget=0;
            bool lifting=Phase==NaispoPhase.Running&&Time.unscaledTime<liftUntil;
            if(Phase==NaispoPhase.Running)
            {
                if(lifting)verticalTarget=liftSpeed;
                else
                {
                    bool attached=false;float targetHeight=0;
                    for(int hand=0;hand<2;hand++)if(motor.IsAttached(hand)&&motor.IsGripping(hand))
                    {attached=true;targetHeight=Mathf.Max(targetHeight,Mathf.Clamp(motor.GetAnchor(hand).y-3f-RouteStart.y,0,Level==1?12f:20f));}
                    float limit=Level==1?2.5f:3.5f;
                    // Grip reels toward the anchor; release settles under bounded gravity.
                    verticalTarget=attached?Mathf.Clamp((targetHeight-routeHeight)*1.1f,-limit,limit):routeHeight>.01f?-(Level==1?3.5f:4.5f):0;
                }
            }
            verticalSpeed=Mathf.MoveTowards(verticalSpeed,verticalTarget,dt*(Phase==NaispoPhase.Ending?(Level==1?4f:5f):lifting?(Level==1?5f:6f):(Level==1?2.8f:3.5f)));
            float ceiling=Level==1?16f:24f;
            routeHeight=Mathf.Clamp(routeHeight+verticalSpeed*dt,0,ceiling);
            if(routeHeight<=0&&verticalSpeed<0||routeHeight>=ceiling&&verticalSpeed>0)verticalSpeed=0;
            Vector3 desired=RouteStart+Vector3.forward*(routeZ-14)+Vector3.up*routeHeight;velocity=(desired-before)/Mathf.Max(.001f,dt);
            // Street boundary stops forward travel without shortening the session timer.
            if(Phase==NaispoPhase.Ending&&RideSpeed<.03f&&Mathf.Abs(verticalSpeed)<.03f)Finish("体験が終わりました。スタッフに交代してください");
            return desired;
        }
        void Update(){if(!initialized)return;PollControllers();Tick(Time.unscaledDeltaTime);RefreshNotice();}
        public void Tick(float dt)
        {
            dt=Mathf.Clamp(dt,0,.1f);
            if(Phase==NaispoPhase.AwaitHands)
            {
                handsStable=HasBothHands?handsStable+dt:0;
                if(handsStable>=.7f)
                {
                    if(newVisitor&&!MixedReality)
                    {
                        newVisitor=false;world.motor.transform.rotation=Quaternion.Euler(0,-world.input.head.localEulerAngles.y,0);
                        if(Guided)routeOffset=-Vector3.ProjectOnPlane(world.motor.transform.TransformVector(world.input.head.localPosition),Vector3.up);
                        world.motor.SetSpawn(Guided?RouteStart:new Vector3(0,5.6f,-2));
                    }
                    newVisitor=false;Phase=NaispoPhase.Countdown;countdown=3;Reason="まもなく開始します";Revision++;
                }
            }
            else if(Phase==NaispoPhase.Countdown)
            {
                countdown-=dt;if(countdown<=0){Phase=NaispoPhase.Running;Reason="体験中";lostTime=0;world.input.ReleaseAll();Revision++;}
            }
            else if(Phase==NaispoPhase.Running)
            {
                if(Server&&Server.PairedBefore&&!Server.OperatorConnected){Pause("Macの操作パネルへの接続が途切れたため停止しました");Revision++;return;}
                bool presence=true;var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence,out bool worn))presence=worn;
                lostTime=HasAnyHand&&presence?0:lostTime+dt;
                if(lostTime>1.5f){Pause("手の認識が途切れたため停止しました。スタッフが再開してください");Revision++;return;}
                Remaining=Mathf.Max(0,Remaining-dt);
                if(Remaining<=0){if(Guided){Phase=NaispoPhase.Ending;world.input.ReleaseAll();}else Finish("体験時間が終了しました");}
            }
        }
        void PollControllers()
        {
            bool primary=false,secondary=false,menu=false,grip=false;float axis=0;
            foreach(var node in new[]{XRNode.LeftHand,XRNode.RightHand})
            {
                var device=InputDevices.GetDeviceAtXRNode(node);
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out bool a))primary|=a;
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton,out bool b))secondary|=b;
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton,out bool m))menu|=m;
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton,out bool g))grip|=g;
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis,out Vector2 stick)&&Mathf.Abs(stick.x)>Mathf.Abs(axis))axis=stick.x;
            }
            ApplyStaffControls(axis,primary,secondary,Time.unscaledDeltaTime);
        }
        public void ApplyStaffControls(float axis,bool primary,bool secondary,float dt)
        {
            if(Phase==NaispoPhase.Ready||Phase==NaispoPhase.Finished)
            {
                if(Mathf.Abs(axis)>.65f&&Time.unscaledTime>nextStick){Execute(new NaispoCommand{action="level",level=(Level-1+(axis>0?1:3))%4+1,duration=Duration});nextStick=Time.unscaledTime+.45f;}
                if(primary&&!oldPrimary)Execute(new NaispoCommand{action="start"});
            }
            else if(Phase==NaispoPhase.Paused){if(primary&&!oldPrimary)Execute(new NaispoCommand{action="resume"});if(secondary&&!oldSecondary)Execute(new NaispoCommand{action="end"});}
            else if(Phase==NaispoPhase.AwaitHands||Phase==NaispoPhase.Countdown){if(secondary&&!oldSecondary)Execute(new NaispoCommand{action="end"});}
            // During play, ordinary controller buttons do not operate the participant.
            stopHold=primary&&secondary?stopHold+dt:0;
            if(stopHold>=1.5f&&Phase!=NaispoPhase.Ready&&Phase!=NaispoPhase.Finished){Execute(new NaispoCommand{action="end"});stopHold=0;}
            oldPrimary=primary;oldSecondary=secondary;
        }
        void RefreshNotice()
        {
            bool visible=Phase!=NaispoPhase.Running&&Phase!=NaispoPhase.Ending;noticeRoot.gameObject.SetActive(visible);
            if(!visible)return;
            if(MixedReality&&(Phase==NaispoPhase.Ready||Phase==NaispoPhase.Finished))
            {
                notice.text="MR / 60 SECOND THROW CHALLENGE\n"+(Phase==NaispoPhase.Finished?"SCORE "+MRGame.Score+"   BEST "+MRGame.Best+"\n":"")+
                    "Web > Small pull > Lift > Look + Open\nStick: VR / MR   A / X: start\n"+Server.Address+"\nPAIR CODE: "+Server.PairCode;return;
            }
            if(Phase==NaispoPhase.Ready||Phase==NaispoPhase.Finished)
                notice.text="WEB SHOOTER / NAISPO\nLEVEL "+Level+" / "+(Level==1?"GUIDED FLIGHT":Level==2?"BOOST FLIGHT":"FREE SWING")+"\nStick: level   A / X: start\n"+(Server?Server.Address+"\nPAIR CODE: "+Server.PairCode:"");
            else if(Phase==NaispoPhase.AwaitHands)notice.text="SHOW BOTH HANDS\nPut controllers down\nShoot > Fist > Pull";
            else if(Phase==NaispoPhase.Countdown)notice.text=Mathf.CeilToInt(countdown).ToString();
            else if(Phase==NaispoPhase.Paused)notice.text="PAUSED\nPlease wait for staff";
        }
        public string StatusJson()
        {
            return JsonUtility.ToJson(new NaispoStatus{phase=Phase.ToString(),reason=Reason,address=Server?Server.Address:"",level=Level,duration=Duration,mr=MixedReality,score=MRGame?MRGame.Score:0,best=MRGame?MRGame.Best:0,throws=MRGame?MRGame.Throws:0,remaining=Remaining,speed=Guided?RideSpeed:world.motor.Velocity.magnitude,distance=Guided?routeZ-14:0,left=world.input.IsHandTracked(0),right=world.input.IsHandTracked(1),revision=Revision});
        }
        void OnApplicationPause(bool paused){if(paused&&initialized&&Phase!=NaispoPhase.Ready&&Phase!=NaispoPhase.Finished){Pause("ヘッドセットが休止したため停止しました");if(Server)Server.PublishStatus(StatusJson());}}
#if UNITY_EDITOR
        public void CheckHands(bool value){checksHands=value;checksOneHand=false;}
        public void CheckOneHand(){checksHands=false;checksOneHand=true;}
#endif
        void OnDestroy(){if(Active==this)Active=null;foreach(var anchor in anchors)if(anchor){Destroy(anchor.GetComponent<Renderer>().sharedMaterial);Destroy(anchor);}if(noticeRoot)Destroy(noticeRoot.gameObject);}
    }
}
