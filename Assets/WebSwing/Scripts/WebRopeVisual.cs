using UnityEngine;
using UnityEngine.Rendering;
namespace WebSwing
{
    public sealed class WebRopeVisual : MonoBehaviour
    {
        const int Segments=72,Sides=12;
        readonly Vector3[] vertices=new Vector3[(Segments+1)*Sides],normals=new Vector3[(Segments+1)*Sides];
        readonly Vector3[] centers=new Vector3[Segments+1];
        readonly LineRenderer[] fibers=new LineRenderer[2];
        LineRenderer contact;Mesh mesh;MeshRenderer core;Material material;
        readonly Vector4[] tangents=new Vector4[(Segments+1)*Sides];
        public Transform VisualRoot { get; private set; }
        void Awake()
        {
            VisualRoot=new GameObject("Silk visual instance").transform;VisualRoot.SetParent(transform,false);
            material=new Material(Shader.Find("Standard")){color=new Color(.96f,.97f,1)};
            material.SetFloat("_Glossiness",.42f);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.white*.09f);
            material.mainTexture=Resources.Load<Texture2D>("WebSwing/CityPhoto/Silk_Color");
            material.SetTexture("_BumpMap",Resources.Load<Texture2D>("WebSwing/CityPhoto/Silk_Normal"));
            material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");
            var tube=new GameObject("Braided silk core",typeof(MeshFilter),typeof(MeshRenderer));tube.transform.SetParent(VisualRoot,false);
            mesh=new Mesh{name="Dynamic twelve-sided braided silk"};mesh.MarkDynamic();
            var uv=new Vector2[vertices.Length];var triangles=new int[Segments*Sides*6];
            for(int j=0;j<=Segments;j++)for(int s=0;s<Sides;s++)uv[j*Sides+s]=new Vector2(j*.5f,s/(float)Sides);
            int n=0;for(int j=0;j<Segments;j++)for(int s=0;s<Sides;s++)
            {
                int a=j*Sides+s,b=j*Sides+(s+1)%Sides,c=a+Sides,d=b+Sides;
                triangles[n++]=a;triangles[n++]=b;triangles[n++]=c;triangles[n++]=b;triangles[n++]=d;triangles[n++]=c;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;tube.GetComponent<MeshFilter>().sharedMesh=mesh;
            core=tube.GetComponent<MeshRenderer>();core.sharedMaterial=material;core.shadowCastingMode=ShadowCastingMode.Off;core.receiveShadows=false;
            for(int i=0;i<2;i++)fibers[i]=Line("Fine silk winding",Segments+1,.0022f);
            contact=Line("Silk adhesion patch",156,.003f);Hide();
        }
        LineRenderer Line(string name,int count,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(VisualRoot,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.positionCount=count;line.startWidth=line.endWidth=width;line.numCapVertices=2;
            line.generateLightingData=true;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.textureMode=LineTextureMode.Tile;return line;
        }
        public void Draw(Vector3 start,Vector3 end,Vector3 normal,float tension,float age,Vector3? grab=null)
        {
            Vector3 delta=end-start;float distance=delta.magnitude,progress=Mathf.Clamp01(age*110f/Mathf.Max(distance,.1f));
            float taut=Mathf.Clamp01(tension),sag=progress>=1?Mathf.Min(distance*.035f,.8f)*(1-taut):0;
            Vector3 axis=distance>.001f?delta/distance:Vector3.forward;
            Vector3 side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.right:Vector3.up).normalized;
            for(int j=0;j<=Segments;j++)
            {
                float t=j/(float)Segments*progress,envelope=Mathf.Sin(t*Mathf.PI);
                Vector3 point=grab.HasValue?(t<.5f?Vector3.Lerp(start,grab.Value,t*2):Vector3.Lerp(grab.Value,end,(t-.5f)*2)):Vector3.Lerp(start,end,t);
                float wave=Mathf.Exp(-age*5f)*(1-taut*.7f)*Mathf.Min(.12f,distance*.012f);
                point+=side*Mathf.Sin(t*20-age*28)*wave*envelope-Vector3.up*(4*t*(1-t)*(grab.HasValue?0:sag));centers[j]=point;
            }
            core.enabled=progress>.002f;
            for(int j=0;j<=Segments;j++)
            {
                Vector3 tangent=(centers[Mathf.Min(Segments,j+1)]-centers[Mathf.Max(0,j-1)]).normalized;
                Vector3 radial=Vector3.ProjectOnPlane(side,tangent).normalized,other=Vector3.Cross(tangent,radial).normalized;
                float t=j/(float)Segments,radius=Mathf.Lerp(.009f,.0045f,t)*(1+.06f*Mathf.Sin(t*distance*8));
                for(int s=0;s<Sides;s++)
                {
                    float a=s*Mathf.PI*2/Sides+t*distance*3;Vector3 fiberNormal=radial*Mathf.Cos(a)+other*Mathf.Sin(a);
                    int k=j*Sides+s;vertices[k]=transform.InverseTransformPoint(centers[j]+fiberNormal*radius*(1+.15f*Mathf.Cos(a*3)));normals[k]=transform.InverseTransformDirection(fiberNormal);
                    Vector3 tangentLocal=transform.InverseTransformDirection(tangent);tangents[k]=new Vector4(tangentLocal.x,tangentLocal.y,tangentLocal.z,1);
                }
                for(int i=0;i<2;i++)
                {
                    fibers[i].enabled=core.enabled;float a=t*distance*18+i*Mathf.PI;
                    fibers[i].SetPosition(j,centers[j]+(radial*Mathf.Cos(a)+other*Mathf.Sin(a))*radius*1.2f);
                }
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.RecalculateBounds();
            contact.enabled=progress>=1;
            if(contact.enabled)
            {
                Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,normal.sqrMagnitude>.1f?normal:Vector3.up);
                Vector3 center=end+normal*.012f;int k=0;
                for(int i=0;i<8;i++)
                {float a=i*Mathf.PI/4,r=Mathf.Clamp(.18f+distance*.003f,.2f,.42f)*(1+.1f*Mathf.Sin(i*2.7f));contact.SetPosition(k++,center);contact.SetPosition(k++,center+rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r));contact.SetPosition(k++,center);}
                for(int ring=1;ring<=4;ring++)for(int i=0;i<=32;i++)
                {float a=i*Mathf.PI/16,r=ring*.25f*Mathf.Clamp(.18f+distance*.003f,.2f,.42f)*(.94f+.06f*Mathf.Cos(a*8)+.025f*Mathf.Sin(a*3+ring));contact.SetPosition(k++,center+rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r));}
            }
        }
        public void Hide(){if(core)core.enabled=false;foreach(var line in fibers)if(line)line.enabled=false;if(contact)contact.enabled=false;}
        void OnDestroy(){if(VisualRoot)Destroy(VisualRoot.gameObject);if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
