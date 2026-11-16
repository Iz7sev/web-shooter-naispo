using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace WebSwing
{
    public static class CityDetailedTower
    {
        internal sealed class Geometry
        {
            public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> triangles=new List<int>();
            public void Box(Vector3 center,Vector3 size,bool metres=false)
            {
                for(int face=0;face<6;face++)
                {
                    Vector3 n=face==0?Vector3.forward:face==1?Vector3.back:face==2?Vector3.right:face==3?Vector3.left:face==4?Vector3.up:Vector3.down;
                    Vector3 a=face<2?Vector3.right:face<4?Vector3.back:Vector3.right,b=Vector3.Cross(n,a);
                    Vector3 normalOffset=Vector3.Scale(n,size)*.5f,ac=Vector3.Scale(a,size)*.5f,bc=Vector3.Scale(b,size)*.5f;
                    int k=vertices.Count;vertices.Add(center+normalOffset-ac-bc);vertices.Add(center+normalOffset+ac-bc);vertices.Add(center+normalOffset+ac+bc);vertices.Add(center+normalOffset-ac+bc);
                    for(int i=0;i<4;i++)normals.Add(n);
                    float u=metres?ac.magnitude:1,v=metres?bc.magnitude:1;
                    uv.Add(Vector2.zero);uv.Add(new Vector2(u,0));uv.Add(new Vector2(u,v));uv.Add(new Vector2(0,v));
                    triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);triangles.Add(k);triangles.Add(k+2);triangles.Add(k+3);
                }
            }
            public Mesh Mesh(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;}
        }
        static Material concrete,brick,frame;static readonly Material[] glazing=new Material[4];
        static Material Surface(string texture,Color color,float smooth,float metallic=0)
        {
            var mat=new Material(Shader.Find("Standard")){color=color,enableInstancing=true};mat.SetFloat("_Glossiness",smooth);mat.SetFloat("_Metallic",metallic);
            if(texture!=null)
            {
                mat.mainTexture=Resources.Load<Texture2D>("WebSwing/CityPhoto/"+texture+"_Color");
                var normal=Resources.Load<Texture2D>("WebSwing/CityPhoto/"+texture+"_Normal");
                if(normal){mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.35f);}
            }
            return mat;
        }
        static void Materials()
        {
            if(!concrete)concrete=Surface("Concrete034",new Color(.87f,.86f,.82f),.16f);
            if(!brick)brick=Surface("Bricks051",new Color(.82f,.76f,.7f),.12f);
            if(!frame)frame=Surface(null,new Color(.21f,.23f,.24f),.45f,.55f);
            for(int i=0;i<4;i++)if(!glazing[i])
            {
                glazing[i]=Surface(null,i==3?new Color(.53f,.47f,.32f):Color.Lerp(new Color(.07f,.13f,.19f),new Color(.24f,.34f,.42f),i*.32f),.88f,.45f);
                if(i==3){glazing[i].EnableKeyword("_EMISSION");glazing[i].SetColor("_EmissionColor",new Color(.19f,.15f,.075f));}
            }
        }
        public static bool Build(Transform parent,Vector3 center,Vector3 size,int seed,Material proxyMaterial)
        {
            Materials();var root=new GameObject("City building / detailed tower LOD");root.transform.SetParent(parent,false);root.transform.position=center;root.layer=8;
            root.AddComponent<BoxCollider>().size=size;var geometry=new Geometry[6];for(int i=0;i<geometry.Length;i++)geometry[i]=new Geometry();
            var wall=geometry[0];var trim=geometry[1];wall.Box(Vector3.zero,size,true);
            // Window recesses and thin frame boxes are merged into six meshes per tower.
            for(float y=-size.y*.5f+5;y<size.y*.5f-1.5f;y+=3.15f)
            {
                for(int face=0;face<4;face++)
                {
                    bool front=face<2;float span=front?size.x:size.z;int count=Mathf.Max(2,Mathf.FloorToInt(span/2.25f));
                    for(int c=0;c<count;c++)
                    {
                        float along=(c-(count-1)*.5f)*(span-1.2f)/count;
                        Vector3 position=front?new Vector3(along,y,(face==0?1:-1)*(size.z*.5f+.045f)):new Vector3((face==2?1:-1)*(size.x*.5f+.045f),y,along);
                        Vector3 border=front?new Vector3(1.72f,2.28f,.12f):new Vector3(.12f,2.28f,1.72f);
                        Vector3 pane=front?new Vector3(1.52f,2.08f,.04f):new Vector3(.04f,2.08f,1.52f);
                        Vector3 outward=front?new Vector3(0,0,face==0?1:-1):new Vector3(face==2?1:-1,0,0);
                        trim.Box(position,border);geometry[2+((c+face+Mathf.RoundToInt(y)+seed*3)%4+4)%4].Box(position+outward*.08f,pane);
                    }
                }
                trim.Box(new Vector3(0,y-1.42f,0),new Vector3(size.x+.22f,.14f,size.z+.22f));
            }
            trim.Box(new Vector3(0,size.y*.5f+.15f,0),new Vector3(size.x+.45f,.3f,size.z+.45f));
            for(int i=0;i<3;i++)
            {
                trim.Box(new Vector3((i-1)*3,size.y*.5f+.9f,2),new Vector3(2,1.5f,2.8f));
                for(int j=0;j<4;j++)wall.Box(new Vector3((i-1)*3-.7f+j*.46f,size.y*.5f+1.68f,2),new Vector3(.08f,.08f,2.2f),true);
            }
            trim.Box(new Vector3(-size.x*.28f,size.y*.5f+2.3f,-size.z*.23f),new Vector3(.12f,4.6f,.12f));
            var meshes=new List<Mesh>();var near=new List<Renderer>();
            for(int i=0;i<geometry.Length;i++)
            {
                var go=new GameObject(i==0?"Photo-textured tower shell":i==1?"Merged window frames and roof equipment":"Merged reflective glazing",typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(root.transform,false);
                var mesh=geometry[i].Mesh("City tower merged geometry");meshes.Add(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=i==0?(seed%2==0?concrete:brick):i==1?frame:glazing[i-2];renderer.shadowCastingMode=i==0?ShadowCastingMode.On:ShadowCastingMode.Off;near.Add(renderer);
            }
            root.AddComponent<CityTowerMeshLifetime>().meshes=meshes.ToArray();
            var proxy=GameObject.CreatePrimitive(PrimitiveType.Cube);proxy.name="Distant tower proxy";proxy.transform.SetParent(root.transform,false);proxy.transform.localScale=size;
            proxy.GetComponent<Collider>().enabled=false;Object.Destroy(proxy.GetComponent<Collider>());var distantMaterial=new Material(Shader.Find("Standard")){mainTexture=Resources.Load<Texture2D>("WebSwing/CityPhoto/SkylineFacade"),color=new Color(.8f,.83f,.85f)};
            distantMaterial.mainTextureScale=new Vector2(Mathf.Max(1,size.x/8),Mathf.Max(1,size.y/8));
            proxy.GetComponent<Renderer>().sharedMaterial=distantMaterial;
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.12f,near.ToArray()),new LOD(.003f,new[]{proxy.GetComponent<Renderer>()})});lod.RecalculateBounds();return true;
        }
    }
    public sealed class CityTowerMeshLifetime : MonoBehaviour
    {
        public Mesh[] meshes;
        void OnDestroy(){if(meshes!=null)foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
