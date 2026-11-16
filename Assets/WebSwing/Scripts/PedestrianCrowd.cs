using UnityEngine;
using UnityEngine.Rendering;

namespace WebSwing
{
    public sealed class PedestrianCrowd:MonoBehaviour
    {
        sealed class Walker
        {
            public Transform root,leftLeg,rightLeg,leftArm,rightArm;
            public float x,z,phase,speed;
        }
        Walker[] walkers;
        Transform player;
        Material skin,trousers,shoes;
        Material[] shirts;
        public int Count=>walkers.Length;
        public Vector3 Position(int i)=>walkers[i].root.position;
        public Vector3 Forward(int i)=>walkers[i].root.forward;
        void OnEnable(){if(walkers!=null)foreach(var w in walkers)SnapToRoute(w);}
        static void SnapToRoute(Walker w)
        {
            float t=(Time.time*w.speed+w.phase)%40;
            w.root.position=new Vector3(w.x,.23f,w.z+(t<20?t:40-t)-10);
            w.root.rotation=Quaternion.LookRotation(t<20?Vector3.forward:Vector3.back);
        }
        public static void Create(DemoWorld world)
        {
            var go=new GameObject("Sidewalk pedestrians");go.transform.SetParent(CityEnvironment.Root,false);
            go.AddComponent<PedestrianCrowd>().Initialize(world.motor.transform);
        }
        void Initialize(Transform viewer)
        {
            player=viewer;skin=Mat(new Color(.61f,.42f,.3f));trousers=Mat(new Color(.12f,.16f,.22f));shoes=Mat(new Color(.055f,.055f,.06f));
            shirts=new[]{Mat(new Color(.18f,.42f,.68f)),Mat(new Color(.64f,.22f,.16f)),Mat(new Color(.4f,.48f,.27f))};
            walkers=new Walker[6];
            for(int i=0;i<walkers.Length;i++)
            {
                var root=new GameObject("Walking citizen "+(i+1)).transform;root.SetParent(transform,false);
                var w=new Walker{root=root,x=i%2==0?-11.2f:11.2f,z=22+i/2*35,phase=i*.9f,speed=.65f+i*.055f};walkers[i]=w;
                root.position=new Vector3(w.x,.23f,w.z);
                Part(root,PrimitiveType.Capsule,new Vector3(0,1.14f,0),new Vector3(.4f,.34f,.26f),shirts[i%3]);
                Part(root,PrimitiveType.Sphere,new Vector3(0,1.65f,0),Vector3.one*.27f,skin);
                for(int side=-1;side<=1;side+=2)
                {
                    var leg=Pivot(root,new Vector3(side*.105f,.85f,0));
                    Part(leg,PrimitiveType.Capsule,new Vector3(0,-.205f,0),new Vector3(.14f,.205f,.14f),trousers);
                    Part(leg,PrimitiveType.Capsule,new Vector3(0,-.58f,0),new Vector3(.12f,.18f,.12f),trousers);
                    Part(leg,PrimitiveType.Cube,new Vector3(0,-.78f,.06f),new Vector3(.14f,.10f,.26f),shoes);
                    var arm=Pivot(root,new Vector3(side*.245f,1.42f,0));
                    Part(arm,PrimitiveType.Capsule,new Vector3(0,-.16f,0),new Vector3(.10f,.16f,.1f),shirts[i%3]);
                    Part(arm,PrimitiveType.Capsule,new Vector3(0,-.43f,.025f),new Vector3(.075f,.135f,.075f),skin);
                    Part(arm,PrimitiveType.Sphere,new Vector3(0,-.59f,.04f),Vector3.one*.095f,skin);
                    if(side<0){w.leftLeg=leg;w.leftArm=arm;}else{w.rightLeg=leg;w.rightArm=arm;}
                }
                var lod=root.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                SnapToRoute(w);
            }
        }
        static Material Mat(Color color)=>new Material(Shader.Find("Standard")){color=color,enableInstancing=true};
        static Transform Pivot(Transform parent,Vector3 p){var go=new GameObject("Walk joint");go.transform.SetParent(parent,false);go.transform.localPosition=p;return go.transform;}
        static void Part(Transform parent,PrimitiveType type,Vector3 p,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);go.name="Citizen visual";go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void Update()
        {
            if(walkers==null)return;
            foreach(var w in walkers)
            {
                float t=(Time.time*w.speed+w.phase)%40;
                float direction=t<20?1:-1,z=w.z+(t<20?t:40-t)-10;
                float x=w.x;
                if(Mathf.Abs(player.position.y-.23f)<2&&Mathf.Abs(player.position.z-z)<1.2f&&Mathf.Abs(player.position.x-x)<.7f)x+=Mathf.Sign(x)*.55f;
                w.root.position=Vector3.MoveTowards(w.root.position,new Vector3(x,.23f,z),Time.deltaTime*1.6f);
                w.root.rotation=Quaternion.Slerp(w.root.rotation,Quaternion.LookRotation(Vector3.forward*direction),Time.deltaTime*5);
                if((player.position-w.root.position).sqrMagnitude>55*55)continue;
                float swing=Mathf.Sin(Time.time*(5+w.speed)+w.phase)*22;
                w.leftLeg.localRotation=Quaternion.Euler(swing,0,0);w.rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
                w.leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,-5);w.rightArm.localRotation=Quaternion.Euler(swing*.7f,0,5);
            }
        }
        void OnDestroy(){if(skin)Destroy(skin);if(trousers)Destroy(trousers);if(shoes)Destroy(shoes);if(shirts!=null)foreach(var m in shirts)if(m)Destroy(m);}
    }
}
