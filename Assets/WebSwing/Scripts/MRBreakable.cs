using UnityEngine;
namespace WebSwing
{
    public sealed class MRBreakable : MonoBehaviour
    {
        GameObject intact,debris;Material material;BoxCollider solid;
        float resetAt,spawnedAt;
        public bool Broken { get; private set; }
        public static MRBreakable Create(Transform parent,Vector3 position,Color color)
        {
            var go=new GameObject("MR breakable ceramic vase");go.layer=8;go.transform.SetParent(parent,false);go.transform.position=position;
            var item=go.AddComponent<MRBreakable>();
            item.material=new Material(Shader.Find("Standard")){color=color};item.material.SetFloat("_Glossiness",.5f);
            item.intact=new GameObject("Intact vase");item.intact.transform.SetParent(go.transform,false);
            item.Part(PrimitiveType.Sphere,Vector3.zero,new Vector3(.42f,.45f,.42f));
            item.Part(PrimitiveType.Cylinder,new Vector3(0,.23f,0),new Vector3(.17f,.12f,.17f));
            item.Part(PrimitiveType.Cylinder,new Vector3(0,.35f,0),new Vector3(.23f,.025f,.23f));
            item.solid=go.AddComponent<BoxCollider>();item.solid.center=Vector3.up*.07f;item.solid.size=new Vector3(.44f,.6f,.44f);
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;
            item.spawnedAt=Time.time;return item;
        }
        void Part(PrimitiveType type,Vector3 position,Vector3 scale)
        {
            var part=GameObject.CreatePrimitive(type);part.transform.SetParent(intact.transform,false);part.transform.localPosition=position;part.transform.localScale=scale;
            part.GetComponent<Collider>().enabled=false;Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }
        void OnCollisionEnter(Collision collision)
        {
            if(Time.time-spawnedAt<.4f)return;
            TryImpact(collision.rigidbody,collision.relativeVelocity);
        }
        public bool TryImpact(Rigidbody source,Vector3 velocity)
        {
            if(Broken||!source||!source.GetComponent<CargoReset>()||velocity.magnitude<1.8f||.5f*source.mass*velocity.sqrMagnitude<1.2f)return false;
            Broken=true;intact.SetActive(false);solid.enabled=false;resetAt=Time.time+10;
            if(NaispoSession.Active&&NaispoSession.Active.MRGame)NaispoSession.Active.MRGame.TargetHit(source,velocity);
            debris=new GameObject("Ceramic fragments");debris.transform.SetParent(transform,false);
            for(int i=0;i<8;i++)
            {
                var shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="Virtual ceramic shard";shard.layer=8;shard.transform.SetParent(debris.transform,false);
                Vector3 direction=new Vector3((i%2)*2-1,((i/2)%2)*2-1,(i/4)*2-1).normalized;
                shard.transform.localPosition=direction*.13f;shard.transform.localScale=new Vector3(.095f,.1f,.055f);
                shard.GetComponent<Renderer>().sharedMaterial=material;
                var body=shard.AddComponent<Rigidbody>();body.mass=.04f;body.linearDamping=.3f;body.angularDamping=.5f;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity=Vector3.ClampMagnitude(velocity*.18f+direction*.8f+Vector3.up*.4f,2.5f);body.angularVelocity=direction*3;
            }
            WebActionSound.BreakCeramic(transform.position);return true;
        }
        public void Restore()
        {
            if(debris){debris.SetActive(false);Destroy(debris);}
            Broken=false;resetAt=0;spawnedAt=Time.time;intact.SetActive(true);solid.enabled=true;
        }
        void Update(){if(Broken&&Time.time>=resetAt)Restore();}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
