using System;
using UnityEngine;

namespace WebSwing
{
    // Transport-independent prototype seam. A BLE adapter must subscribe to PacketReady.
    // No Bluetooth connection or actuator is enabled by this component on its own.
    public sealed class WebHaptics : MonoBehaviour
    {
        public event Action<int,byte[]> PacketReady;
        public bool Armed { get; private set; }
        WebInput input;
        WebShooter[] hands;
        readonly int[] shots=new int[2];
        readonly bool[] active=new bool[2],received=new bool[2];
        readonly byte[] sequence=new byte[2],lastInputSequence=new byte[2];
        float nextSample;
        bool focused=true;
        public void Initialize(WebInput source,WebShooter left,WebShooter right)
        { input=source; hands=new[]{left,right}; }
        public void SetArmed(bool value) { Armed=value; }
        public void ReceiveDevicePacket(int hand,byte[] data)
        {
            // Call from the Unity main thread after the BLE notification is received.
            if(!focused||hand<0||hand>1||!Valid(data)||data[3]!=0x10) return;
            if(received[hand]&&lastInputSequence[hand]==data[2]) return;
            received[hand]=true; lastInputSequence[hand]=data[2];
            input.ReceiveExternalTrigger(hand,(data[4]&3)==3 && (data[5]&1)!=0);
        }
        void Update()
        {
            if(hands==null||Time.unscaledTime<nextSample) return;
            nextSample=Time.unscaledTime+.05f;
            for(int hand=0;hand<2;hand++)
            {
                var web=hands[hand]; bool enabled=Armed&&focused&&input.IsHandTracked(hand);
                byte state=0;
                if(web.ShotSequence!=shots[hand]) state=1;
                else if(active[hand]&&!web.Active) state=4;
                else if(web.Active) state=(byte)(web.Tension01>.15f?3:2);
                shots[hand]=web.ShotSequence; active[hand]=web.Active;
                PacketReady?.Invoke(hand,Packet(sequence[hand]++,enabled?state:(byte)0,
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(web.Tension01)*255),enabled?(byte)1:(byte)0));
            }
        }
        public static byte[] Packet(byte sequence,byte state,byte amplitude,byte flags)
        {
            var result=new byte[]{0x57,0x53,sequence,state,amplitude,flags,0,0};
            ushort crc=Crc(result,6); result[6]=(byte)crc; result[7]=(byte)(crc>>8); return result;
        }
        public static bool Valid(byte[] data) => data!=null&&data.Length==8&&data[0]==0x57&&data[1]==0x53&&
            Crc(data,6)==(ushort)(data[6]|data[7]<<8);
        static ushort Crc(byte[] data,int count)
        {
            ushort crc=0xffff;
            for(int i=0;i<count;i++) { crc^=data[i]; for(int bit=0;bit<8;bit++)crc=(ushort)((crc&1)!=0?(crc>>1)^0xa001:crc>>1); }
            return crc;
        }
        void OnApplicationFocus(bool value) { focused=value; if(!value)Armed=false; }
        void OnDisable() { Armed=false; }
    }
}
