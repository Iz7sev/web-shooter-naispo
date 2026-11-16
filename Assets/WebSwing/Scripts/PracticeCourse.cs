using UnityEngine;

namespace WebSwing
{
    public sealed class PracticeCourse : MonoBehaviour
    {
        static PracticeCourse current;
        DemoWorld world;
        TextMesh instruction;
        Material[] rings=new Material[3];
        public int Stage { get; private set; }
        public static void Attached(Vector3 point,Collider target)
        {
            if(current&&current.Stage==0&&target&&target.name=="Practice web post"&&point.z<-17&&Mathf.Abs(point.x)<8)
                current.Advance();
        }
        public static void Create(DemoWorld world)
        {
            var root=new GameObject("Low practice course");root.transform.SetParent(CityEnvironment.Root,false);
            current=root.AddComponent<PracticeCourse>();current.world=world;
            var floor=new Material(Shader.Find("Standard")){color=new Color(.24f,.30f,.33f)};
            var blue=new Material(Shader.Find("Standard")){color=new Color(.12f,.52f,.7f)};
            Block(root.transform,"Practice pad",new Vector3(0,-.03f,-29),new Vector3(16,.06f,24),floor,false);
            Block(root.transform,"Low landing step 1",new Vector3(0,.15f,-31),new Vector3(3,.3f,3),floor,true);
            Block(root.transform,"Low landing step 2",new Vector3(0,.3f,-25.5f),new Vector3(3,.6f,3),floor,true);
            Block(root.transform,"Practice wall",new Vector3(4.5f,1.2f,-24),new Vector3(.35f,2.4f,5),blue,true);
            for(int i=0;i<2;i++)for(int side=-1;side<=1;side+=2)
                Block(root.transform,"Practice web post",new Vector3(side*2.7f,1.8f,-31+i*6),new Vector3(.3f,3.6f,.3f),blue,true);
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Practice checkpoint "+i);go.transform.SetParent(root.transform,false);
                var line=go.AddComponent<LineRenderer>();current.rings[i]=new Material(Shader.Find("Sprites/Default"));line.sharedMaterial=current.rings[i];
                line.startWidth=line.endWidth=.035f;line.positionCount=33;
                Vector3 center=i==2?new Vector3(3.5f,1.5f,-24):new Vector3(0,1.2f,-31+i*5.5f);
                for(int j=0;j<33;j++){float a=j*Mathf.PI/16;line.SetPosition(j,center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.65f);}
            }
            var label=new GameObject("Training instructions");label.transform.SetParent(root.transform,false);label.transform.position=new Vector3(0,2.5f,-33.8f);
            current.instruction=label.AddComponent<TextMesh>();current.instruction.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            current.instruction.fontSize=64;current.instruction.characterSize=.012f;current.instruction.anchor=TextAnchor.MiddleCenter;current.instruction.alignment=TextAlignment.Center;
            label.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(current.instruction.font);
            for(int i=0;i<2;i++){var box=CityEnvironment.SpawnCrate(new Vector3(-2+i*4,.5f,-35),2,i);box.transform.SetParent(root.transform,true);box.transform.localScale=Vector3.one*.6f;}
        }
        static void Block(Transform parent,string name,Vector3 position,Vector3 size,Material mat,bool target)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;
            go.layer=target?8:0;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        void Advance(){Stage++;ExperienceCue.Play(world.input.head.position,1400,.08f);}
        void Update()
        {
            if(!world||world.modes.IsMR)return;
            Vector3 player=world.motor.transform.position;
            if(Stage==1&&player.z>-34&&player.z<-17&&Mathf.Abs(player.x)<7)
            {Advance();world.motor.SetWallWalkEnabled(true);}
            if(Stage==2&&world.motor.WallAttached&&player.y>.6f&&player.z<-17)Advance();
            string[] instructions={"TRAINING 1 / HAND FORWARD AIM AT A BLUE POST\nLeft: CYAN / Right: ORANGE / pose to connect",
                "TRAINING 2 / PULL YOUR HAND TOWARD YOU\nClose FIST to reel + swing / OPEN PALM to release",
                "TRAINING 3 / FACE THE BLUE WALL\nMove close + swing arms to climb / camera stays upright",
                "TRAINING COMPLETE!\nMENU > SETTINGS > GO TO CITY\nThumbs up opens menu and pauses movement"};
            instruction.text=instructions[Mathf.Min(Stage,3)];
            for(int i=0;i<3;i++)rings[i].color=i<Stage?new Color(.2f,1,.45f):i==Stage?Color.yellow:new Color(.3f,.5f,.6f);
        }
        void OnDestroy(){foreach(var mat in rings)if(mat)Destroy(mat);if(current==this)current=null;}
    }
}
