using UnityEngine;
using UnityEngine.Rendering;
namespace WebSwing
{
    public sealed class NaispoVignette : MonoBehaviour
    {
        NaispoSession session;GameObject mask;Mesh mesh;Material material;Color[] colors;float opacity;
        public void Initialize(NaispoSession owner,Camera camera)
        {
            session=owner;mask=new GameObject("Naispo comfort peripheral mask",typeof(MeshFilter),typeof(MeshRenderer));mask.transform.SetParent(camera.transform,false);mask.transform.localPosition=Vector3.forward*.3f;
            const int sides=48;var vertices=new Vector3[4*sides];colors=new Color[vertices.Length];var triangles=new int[3*sides*6];int k=0;
            float[] radius={.19f,.25f,.4f,.9f};
            for(int r=0;r<4;r++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices[r*sides+i]=new Vector3(Mathf.Cos(a)*radius[r],Mathf.Sin(a)*radius[r],0);}
            for(int r=0;r<3;r++)for(int i=0;i<sides;i++){int a=r*sides+i,b=r*sides+(i+1)%sides,c=a+sides,d=b+sides;triangles[k++]=a;triangles[k++]=c;triangles[k++]=b;triangles[k++]=b;triangles[k++]=c;triangles[k++]=d;}
            mesh=new Mesh{name="Naispo comfort mask"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors=colors;mesh.RecalculateBounds();mask.GetComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Shader.Find("Sprites/Default")){renderQueue=4000};mask.GetComponent<Renderer>().sharedMaterial=material;mask.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        void LateUpdate()
        {
            if(!session||!mask)return;float target=session.Guided&&(session.Phase==NaispoPhase.Running||session.Phase==NaispoPhase.Ending)?Mathf.Clamp01(session.RideSpeed/.8f)*(session.Level==1?.8f:.5f):0;
            opacity=Mathf.MoveTowards(opacity,target,Time.unscaledDeltaTime*1.5f);mask.SetActive(opacity>.001f);
            for(int i=0;i<colors.Length;i++)colors[i]=new Color(0,0,0,(i/48==0?0:i/48==1?.15f:i/48==2?.65f:1)*opacity);mesh.colors=colors;
        }
        void OnDestroy(){if(mask)Destroy(mask);if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
