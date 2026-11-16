using UnityEngine;
using UnityEngine.XR;

namespace WebSwing
{
    public sealed class DemoWorld : MonoBehaviour
    {
        public WebInput input;
        public WebShooter shooter;
        public SwingMotor motor;
        public WebShooter leftShooter;
        public WorldModes modes;
        GUIStyle title, text;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<DemoWorld>() != null) return;
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) listener.enabled=false;
            Build();
        }
        public static void Build()
        {
            var world = new GameObject("WEB / SWING LAB").AddComponent<DemoWorld>();
            Physics.gravity = Vector3.down * 9.81f;
            CityEnvironment.Build();
            var player = new GameObject("Player Origin"); player.transform.position = new Vector3(0,5.6f,-2);
            var controller = player.AddComponent<CharacterController>(); controller.radius=.25f; controller.height=1.65f;
            controller.center=Vector3.up*.825f; controller.skinWidth=.025f;
            var cameraObject = new GameObject("Tracked Head"); cameraObject.tag="MainCamera";
            cameraObject.transform.SetParent(player.transform,false); cameraObject.transform.localPosition=Vector3.up*1.65f;
            var camera = cameraObject.AddComponent<Camera>(); camera.nearClipPlane=.08f; camera.farClipPlane=360;
            camera.backgroundColor = new Color(.57f,.65f,.72f); camera.clearFlags=CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            var aim = new GameObject("Wrist Aim").transform; aim.SetParent(player.transform,false);
            var motor = player.AddComponent<SwingMotor>(); motor.head=camera.transform;
            var shooter = player.AddComponent<WebShooter>(); shooter.motor=motor; shooter.hand=aim;
            shooter.handIndex=1;
            var leftAim=new GameObject("Left Wrist Aim").transform; leftAim.SetParent(player.transform,false);
            var leftShooter=player.AddComponent<WebShooter>(); leftShooter.motor=motor; leftShooter.hand=leftAim; leftShooter.handIndex=0;
            var input = player.AddComponent<WebInput>(); input.head=camera.transform; input.aim=aim; input.motor=motor; input.shooter=shooter; input.leftShooter=leftShooter;
            world.motor=motor; world.shooter=shooter; world.input=input;
            world.leftShooter=leftShooter;
            var haptics=world.gameObject.AddComponent<WebHaptics>(); haptics.Initialize(input,leftShooter,shooter);
            var modes=world.gameObject.AddComponent<WorldModes>(); modes.city=CityEnvironment.Root;
            world.modes=modes; input.modes=modes; modes.Initialize(world);
            PracticeCourse.Create(world);
            PedestrianCrowd.Create(world);
            // In-world instructions remain visible in a headset (IMGUI does not).
            var sign = new GameObject("VR instructions"); sign.transform.position=new Vector3(4.3f,7.7f,6.35f);
            sign.transform.SetParent(CityEnvironment.Root,true);
            var label = sign.AddComponent<TextMesh>(); label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(label.font);
            label.characterSize=.035f; label.fontSize=64; label.color=new Color(.95f,.93f,.84f);
            label.text="WEB / TWO HANDS\nWeb pose: ATTACH / FIST: SWING\nOPEN: RELEASE / keeps your momentum\nPull objects / close fingers / look + open: THROW\nPeace sign 1.5 seconds: MENU";
            NaispoSession.Create(world);
            Debug.Log("WEB_WORLD_READY: towers, crates, player and instructions created.");
        }
        void OnGUI()
        {
            if (XRSettings.isDeviceActive || NaispoSession.Active) return;
            if (title == null)
            {
                title=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.4f,.95f,1);
                text=new GUIStyle(GUI.skin.label){fontSize=16}; text.normal.textColor=Color.white;
            }
            GUI.Box(new Rect(18,18,480,166),GUIContent.none);
            GUI.Label(new Rect(32,25,450,38),"WEB / SWING LAB",title);
            GUI.Label(new Rect(32,68,450,28),shooter.State+"  |  "+motor.Velocity.magnitude.ToString("F1")+" m/s",text);
            GUI.Label(new Rect(32,99,450,28),"Click: web toggle   /   T: throw carried cargo",text);
            GUI.Label(new Rect(32,126,450,28),"WASD: move   Space: jump   R: reset   Esc: cursor",text);
            GUI.Label(new Rect(32,153,450,28),input.TrackingStatus,text);
            GUI.Label(new Rect(Screen.width/2-8,Screen.height/2-14,30,30),"+",title);
        }
    }
}

