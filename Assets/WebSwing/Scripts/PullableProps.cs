using System.Collections.Generic;
using UnityEngine;

namespace WebSwing
{
    public static class PullableProps
    {
        static Material steel,white,orange,rubber;
        static PhysicsMaterial bounce,grip;
        static void Materials()
        {
            if(steel)return;
            steel=Mat(new Color(.12f,.4f,.58f),.65f);white=Mat(new Color(.9f,.91f,.88f),.1f);
            orange=Mat(new Color(1,.3f,.035f),0);rubber=Mat(new Color(.09f,.13f,.1f),0);
            bounce=new PhysicsMaterial("Rubber ball bounce"){bounciness=.65f,dynamicFriction=.45f,staticFriction=.5f,bounceCombine=PhysicsMaterialCombine.Maximum};
            grip=new PhysicsMaterial("Prop grip"){bounciness=.04f,dynamicFriction=.5f,staticFriction=.6f};
        }
        static Material Mat(Color color,float metal)
        {var m=new Material(Shader.Find("Standard")){color=color,enableInstancing=true};m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",.35f);return m;}
        public static GameObject Spawn(int kind,Vector3 position,float mass)
        {
            Materials();GameObject root;
            if(kind==1)
            {
                root=GameObject.CreatePrimitive(PrimitiveType.Cylinder);root.name="Pullable metal barrel";
                root.transform.localScale=new Vector3(.85f,.55f,.85f);root.GetComponent<Renderer>().sharedMaterial=steel;
                foreach(float y in new[]{-.8f,0f,.8f})Part(root.transform,PrimitiveType.Cylinder,new Vector3(0,y,0),new Vector3(1.025f,.035f,1.025f),white);
            }
            else if(kind==2)
            {
                root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name="Pullable rubber ball";root.GetComponent<Renderer>().sharedMaterial=rubber;
                for(int ring=0;ring<2;ring++)
                {
                    var go=new GameObject("Ball stripe");go.transform.SetParent(root.transform,false);go.transform.localRotation=Quaternion.Euler(ring*90,0,0);
                    var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=white;line.positionCount=33;line.startWidth=line.endWidth=.035f;
                    for(int i=0;i<33;i++){float a=i*Mathf.PI/16;line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.502f);}
                }
            }
            else
            {
                root=new GameObject("Pullable traffic cone",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
                var verts=new List<Vector3>();var colored=new List<int>();var band=new List<int>();
                float[] heights={-.6f,-.05f,.13f,.6f},radii={.45f,.255f,.187f,.035f};
                const int n=24;
                for(int j=0;j<4;j++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;verts.Add(new Vector3(Mathf.Cos(a)*radii[j],heights[j],Mathf.Sin(a)*radii[j]));}
                for(int j=0;j<3;j++)for(int i=0;i<n;i++)
                {int a=j*n+i,b=j*n+(i+1)%n,c=(j+1)*n+i,d=(j+1)*n+(i+1)%n;var list=j==1?band:colored;list.AddRange(new[]{a,c,b,b,c,d});}
                for(int i=1;i<n-1;i++)colored.AddRange(new[]{0,i,(i+1)});
                var mesh=new Mesh{name="Traffic cone mesh"};mesh.SetVertices(verts);mesh.subMeshCount=2;mesh.SetTriangles(colored,0);mesh.SetTriangles(band,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
                root.AddComponent<PropMeshCleanup>().mesh=mesh;
                root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<Renderer>().sharedMaterials=new[]{orange,white};root.GetComponent<MeshCollider>().sharedMesh=mesh;root.GetComponent<MeshCollider>().convex=true;
                Part(root.transform,PrimitiveType.Cube,new Vector3(0,-.62f,0),new Vector3(1.02f,.08f,1.02f),rubber,true);
            }
            root.layer=8;root.transform.position=position;root.transform.SetParent(CityEnvironment.Root,true);
            var rb=root.AddComponent<Rigidbody>();rb.mass=mass;rb.linearDamping=.12f;rb.angularDamping=kind==2?.25f:1.5f;
            rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            foreach(var collider in root.GetComponentsInChildren<Collider>())collider.sharedMaterial=kind==2?bounce:grip;
            root.AddComponent<CargoReset>().spawn=position;return root;
        }
        static void Part(Transform parent,PrimitiveType shape,Vector3 p,Vector3 scale,Material mat,bool collide=false)
        {
            var go=GameObject.CreatePrimitive(shape);go.name="Prop detail";go.layer=8;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide){var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);}
        }
    }
    public sealed class PropMeshCleanup:MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
