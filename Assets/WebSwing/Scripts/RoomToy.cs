using UnityEngine;

namespace WebSwing
{
    // This is a virtual hanging target; no physical lamp is actuated or tracked.
    public sealed class RoomToy : MonoBehaviour
    {
        Material paint,glass,light;
        TextMesh score;
        int hits;
        public int Hits => hits;
        float lastHit,flashUntil;
        public static void Create(Transform parent,Vector3 floorCenter)
        {
            var anchor=new GameObject("Virtual chandelier / fixed anchor"); anchor.transform.SetParent(parent,false);
            anchor.transform.position=floorCenter+Vector3.up*2.3f;
            var fixedBody=anchor.AddComponent<Rigidbody>(); fixedBody.isKinematic=true;
            var lamp=GameObject.CreatePrimitive(PrimitiveType.Cube); lamp.name="Virtual chandelier / hit with virtual cargo";
            lamp.layer=8; lamp.transform.SetParent(parent,false); lamp.transform.position=anchor.transform.position-Vector3.up*.4f;
            lamp.transform.localScale=Vector3.one;
            var collider=lamp.GetComponent<BoxCollider>(); collider.size=new Vector3(.28f,.28f,.28f);
            Object.Destroy(lamp.GetComponent<MeshRenderer>()); Object.Destroy(lamp.GetComponent<MeshFilter>());
            var rb=lamp.AddComponent<Rigidbody>(); rb.mass=2.4f; rb.angularDamping=1.3f;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation=RigidbodyInterpolation.Interpolate;
            var hinge=lamp.AddComponent<HingeJoint>(); hinge.connectedBody=fixedBody; hinge.anchor=Vector3.up*.4f;
            hinge.axis=Vector3.right; hinge.useLimits=true; hinge.limits=new JointLimits{min=-30,max=30};
            var toy=lamp.AddComponent<RoomToy>();
            toy.paint=Material(new Color(.55f,.34f,.12f),.78f,.65f);
            toy.glass=Material(new Color(.73f,.87f,.92f),.2f,.9f);
            toy.light=Material(new Color(1f,.86f,.58f),0,.35f);
            toy.light.EnableKeyword("_EMISSION"); toy.light.SetColor("_EmissionColor",new Color(1,.62f,.22f)*1.4f);
            // Emissive bulbs replace six expensive realtime lights and transparent glass layers.
            Part(lamp.transform,PrimitiveType.Sphere,"Central brass body",Vector3.zero,new Vector3(.25f,.28f,.25f),toy.paint);
            Rod(lamp.transform,new Vector3(0,.1f,0),new Vector3(0,.4f,0),.018f,toy.paint);
            Ring(lamp.transform,new Vector3(0,-.02f,0),.34f,.015f,toy.paint);
            for(int link=0;link<5;link++)
            {
                var chain=Ring(lamp.transform,new Vector3(0,.18f+link*.045f,0),.025f,.006f,toy.paint);
                chain.localRotation=Quaternion.Euler(90,link%2*90,0);
            }
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 a=radial*.12f,b=radial*.25f-Vector3.up*.1f,c=radial*.43f-Vector3.up*.03f,d=radial*.44f+Vector3.up*.1f;
                Rod(lamp.transform,a,b,.018f,toy.paint); Rod(lamp.transform,b,c,.018f,toy.paint); Rod(lamp.transform,c,d,.018f,toy.paint);
                Part(lamp.transform,PrimitiveType.Cylinder,"Candle cup",d,new Vector3(.095f,.014f,.095f),toy.paint);
                Part(lamp.transform,PrimitiveType.Cylinder,"Ivory candle",d+Vector3.up*.06f,new Vector3(.038f,.055f,.038f),toy.light);
                var bulb=Part(lamp.transform,PrimitiveType.Sphere,"Warm candle bulb",d+Vector3.up*.14f,new Vector3(.038f,.065f,.038f),toy.light);
                bulb.layer=8; bulb.AddComponent<SphereCollider>().radius=.8f;
                Part(lamp.transform,PrimitiveType.Sphere,"Crystal drop",c-Vector3.up*.11f,new Vector3(.045f,.15f,.045f),toy.glass);
                var arm=new GameObject("Virtual arm collision"); arm.layer=8; arm.transform.SetParent(lamp.transform,false);
                arm.transform.localPosition=(a+d)*.5f; arm.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d-a);
                var cap=arm.AddComponent<CapsuleCollider>(); cap.radius=.035f; cap.height=(d-a).magnitude+.07f;
            }
            Part(lamp.transform,PrimitiveType.Sphere,"Central pendant",new Vector3(0,-.28f,0),new Vector3(.08f,.25f,.08f),toy.glass);
            var label=new GameObject("Virtual target score"); label.transform.SetParent(lamp.transform,false);
            label.transform.localPosition=new Vector3(0,-.5f,-.46f);
            toy.score=label.AddComponent<TextMesh>(); toy.score.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toy.score.anchor=TextAnchor.MiddleCenter; toy.score.fontSize=48; toy.score.characterSize=.018f;
            label.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(toy.score.font); toy.score.text="VIRTUAL TARGET / 0";
        }
        void OnCollisionEnter(Collision collision)
        {
            if(!collision.rigidbody||!collision.rigidbody.GetComponent<CargoReset>()||Time.time-lastHit<.4f) return;
            lastHit=Time.time; flashUntil=Time.time+.2f; hits++; score.text="HIT! / "+hits;WebActionSound.ChandelierHit(transform.position);
            if(NaispoSession.Active&&NaispoSession.Active.MRGame)NaispoSession.Active.MRGame.TargetHit(collision.rigidbody,collision.relativeVelocity);
        }
        static Material Material(Color color,float metallic,float smooth)
        {
            var mat=new Material(Shader.Find("Standard")){color=color,enableInstancing=true};
            mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Glossiness",smooth); return mat;
        }
        static GameObject Part(Transform parent,PrimitiveType type,string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=p; go.transform.localScale=size;
            var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }
        static void Rod(Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {
            var go=Part(parent,PrimitiveType.Cylinder,"Curved brass arm segment",(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),material);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static Transform Ring(Transform parent,Vector3 p,float radius,float tube,Material material)
        {
            const int sides=32,section=6;
            var vertices=new Vector3[sides*section]; var triangles=new int[sides*section*6];
            for(int i=0;i<sides;i++) for(int j=0;j<section;j++)
            {
                float a=i*Mathf.PI*2/sides,b=j*Mathf.PI*2/section;
                vertices[i*section+j]=new Vector3(Mathf.Cos(a)*(radius+tube*Mathf.Cos(b)),tube*Mathf.Sin(b),Mathf.Sin(a)*(radius+tube*Mathf.Cos(b)));
                int k=(i*section+j)*6,v=i*section+j,n=((i+1)%sides)*section+j,w=i*section+(j+1)%section,z=((i+1)%sides)*section+(j+1)%section;
                triangles[k]=v;triangles[k+1]=w;triangles[k+2]=n;triangles[k+3]=n;triangles[k+4]=w;triangles[k+5]=z;
            }
            var mesh=new Mesh{name="Chandelier ring"}; mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Brass ring",typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);go.transform.localPosition=p;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        void Update() { if(light)light.SetColor("_EmissionColor",Time.time<flashUntil?Color.white*2:new Color(1,.62f,.22f)*1.4f); }
        void OnDestroy()
        {
            if(paint)Destroy(paint);if(glass)Destroy(glass);if(light)Destroy(light);
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh && filter.sharedMesh.name=="Chandelier ring")Destroy(filter.sharedMesh);
        }
    }
}
