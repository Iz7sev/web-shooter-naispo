using System.Collections.Generic;
using UnityEngine;
namespace WebSwing
{
    public sealed class RoomWebMarks : MonoBehaviour
    {
        static RoomWebMarks current;
        public static Transform Root=>current?current.transform:null;
        readonly Queue<GameObject> marks=new Queue<GameObject>();
        Material material;
        public int Count=>marks.Count;
        void OnEnable(){current=this;}
        void OnDisable(){if(current==this)current=null;}
        public static void Add(Vector3 point,Vector3 normal,Transform target)
        {
            if(current)current.Create(point,normal,target);
        }
        void Create(Vector3 point,Vector3 normal,Transform target)
        {
            while(marks.Count>=16){var oldest=marks.Dequeue();if(oldest)Destroy(oldest);}
            var go=new GameObject("Surface spiderweb");go.transform.SetPositionAndRotation(point+normal*.012f,Quaternion.LookRotation(normal));
            go.transform.SetParent(target?target:transform,true);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;
            if(!material)material=new Material(Shader.Find("Sprites/Default")){color=new Color(.95f,.97f,1,.85f)};
            line.sharedMaterial=material;line.startWidth=line.endWidth=.0025f;
            var points=new List<Vector3>();float radius=target?.23f:.48f;
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;points.Add(Vector3.zero);points.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius);points.Add(Vector3.zero);}
            for(int ring=1;ring<=4;ring++)for(int i=0;i<=32;i++)
            {
                float a=i*Mathf.PI/16;float r=radius*ring/4*(.94f+.06f*Mathf.Cos(a*8));
                points.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));
            }
            line.positionCount=points.Count;line.SetPositions(points.ToArray());marks.Enqueue(go);
        }
        void OnDestroy(){foreach(var mark in marks)if(mark)Destroy(mark);if(material)Destroy(material);}
    }
}
