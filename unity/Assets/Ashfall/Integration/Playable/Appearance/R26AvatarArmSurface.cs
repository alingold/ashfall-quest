#if UNITY_6000_0_OR_NEWER || R26_AVATAR_CPU
using UnityEngine;
namespace Ashfall.Integration.Playable.Appearance
{
    // A capped sleeve with circular fillets. The inside of a tight turn
    // compresses before it reaches the bend axis, where a swept face would flip.
    internal sealed class R26AvatarArmSurface
    {
        internal const int Sides=12,Stride=Sides+1,Rings=27,WristStart=17,SleeveEnd=Rings*Stride,VertexCount=SleeveEnd+Stride+2;
        const int ShoulderStart=1,ElbowStart=8,WristEnd=25;
        static readonly float[] cos=new float[Stride],sin=new float[Stride];
        internal readonly Vector3[] Vertices=new Vector3[VertexCount],Normals=new Vector3[VertexCount];
        internal readonly Vector4[] Tangents=new Vector4[VertexCount];
        internal Vector3 UpperFrom,UpperTo,ForeFrom,ForeTo;
        static R26AvatarArmSurface()
        {
            for(int j=0;j<Sides;j++){float angle=j*Mathf.PI*2/Sides;cos[j]=Mathf.Cos(angle);sin[j]=Mathf.Sin(angle);}
            cos[Sides]=1;
        }
        internal static int[] Indices(bool cuff)
        {
            int first=cuff?WristStart:0,last=cuff?Rings-1:WristStart;
            var result=new int[(last-first)*Sides*6+Sides*3];int n=0;
            for(int r=first;r<last;r++)for(int j=0;j<Sides;j++)
            {
                int a=r*Stride+j,b=!cuff&&r+1==WristStart?SleeveEnd+j:a+Stride;
                result[n++]=a;result[n++]=a+1;result[n++]=b;
                result[n++]=a+1;result[n++]=b+1;result[n++]=b;
            }
            for(int j=0;j<Sides;j++)
            {
                int a=cuff?(Rings-1)*Stride+j:j;
                result[n++]=VertexCount-(cuff?1:2);result[n++]=cuff?a:a+1;result[n++]=cuff?a+1:a;
            }
            return result;
        }
        static Vector3 Across(Vector3 direction,Vector3 reference)
        {
            var v=Vector3.ProjectOnPlane(reference,direction);
            if(v.sqrMagnitude<.0001f)v=Vector3.ProjectOnPlane(Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right,direction);
            return v.normalized;
        }
        struct Bend
        {
            internal readonly Vector3 From,To,Start,End,Axis,Inside;
            internal readonly float Angle,Radius;
            internal Bend(Vector3 joint,Vector3 from,Vector3 to,float trim)
            {
                From=from;To=to;Start=joint-from*trim;End=joint+to*trim;
                var cross=Vector3.Cross(from,to);
                Axis=cross.sqrMagnitude>.00000001f?cross.normalized:Across(from,Vector3.up);
                Inside=Vector3.Cross(Axis,from);
                Angle=Mathf.Atan2(cross.magnitude,Vector3.Dot(from,to));
                Radius=Angle>.001f?trim/Mathf.Tan(Angle*.5f):float.PositiveInfinity;
            }
            internal Quaternion Rotation(float t){return Quaternion.AngleAxis(Angle*Mathf.Rad2Deg*t,Axis);}
            internal Vector3 Center(float t)
            {
                if(t==0)return Start;
                if(t==1)return End;
                if(Angle<=.001f)return Vector3.Lerp(Start,End,t);
                float a=Angle*t,h=Mathf.Sin(a*.5f);
                return Start+Radius*(From*Mathf.Sin(a)+Inside*(2*h*h));
            }
        }
        void Ring(int ring,Vector3 center,Vector3 direction,Vector3 across,float wide,float tall,Vector3 inside,float limit)
        {
            Vector3 up=Vector3.Cross(direction,across);
            for(int j=0;j<Sides;j++)
            {
                Vector3 radial=across*(wide*cos[j])+up*(tall*sin[j]);
                float inward=Vector3.Dot(radial,inside);
                if(inward>0)radial-=inside*(inward*(1-limit));
                Vertices[ring*Stride+j]=center+radial;
            }
            Vertices[ring*Stride+Sides]=Vertices[ring*Stride];
        }
        Vector3 Fillet(int first,int count,Bend bend,Vector3 across,Vector2 start,Vector2 end)
        {
            for(int i=0;i<count;i++)
            {
                float t=(float)i/(count-1);var q=bend.Rotation(t);var size=Vector2.Lerp(start,end,t);
                float limit=Mathf.Min(1,bend.Radius*.72f/Mathf.Max(size.x,size.y));
                Ring(first+i,bend.Center(t),i==count-1?bend.To:q*bend.From,q*across,size.x,size.y,q*bend.Inside,limit);
            }
            return bend.Rotation(1)*across;
        }
        internal void Pose(Vector3 seat,Vector3 shoulder,Vector3 elbow,Vector3 wrist,Vector3 handAxis,Vector3 handRight,float scale,Vector3 pole)
        {
            // The cuff finishes inside the solid glove back. The last ring
            // follows the wrist art's roll and scale, including held poses.
            // End the bend at the glove back. Keep its two tangent spans within 5 cm.
            float wristHalfSpan=Mathf.Clamp(.020f*scale,.0125f,.025f);
            Vector3 wristJoint=wrist-handAxis*wristHalfSpan;
            Vector3 upper=(elbow-shoulder).normalized,fore=(wristJoint-elbow).normalized;
            if((elbow-shoulder).sqrMagnitude<.0025f||Vector3.Dot(upper,fore)<-.996f)
            {
                // Keep the fallback away from the shoulder-wrist line even in a tightly folded pose.
                Vector3 reach=(wristJoint-shoulder).normalized;
                Vector3 lateral=Vector3.ProjectOnPlane(elbow-shoulder,reach);
                elbow+=(lateral.sqrMagnitude>.000001f?lateral.normalized:Across(reach,pole))*.06f;
                upper=(elbow-shoulder).normalized;fore=(wristJoint-elbow).normalized;
            }
            Vector3 mount=(shoulder-seat).normalized;
            if(Vector3.Dot(mount,upper)<-.996f){seat+=Across(mount,pole)*.045f;mount=(shoulder-seat).normalized;}
            float upperLength=Vector3.Distance(shoulder,elbow),foreLength=Vector3.Distance(elbow,wristJoint);
            float shoulderTrim=Mathf.Min(.078f,upperLength*.32f);
            float elbowTrim=Mathf.Min(.105f,Mathf.Min(upperLength,foreLength)*.36f);
            float wristTrim=Mathf.Min(wristHalfSpan,foreLength*.45f);
            var socket=new Bend(shoulder,mount,upper,shoulderTrim);
            var hinge=new Bend(elbow,upper,fore,elbowTrim);
            var cuff=new Bend(wristJoint,fore,handAxis,wristTrim);
            Vector3 x=Across(mount,pole);
            Ring(0,seat,mount,x,.048f,.048f,Vector3.zero,1);
            x=Fillet(ShoulderStart,ElbowStart-ShoulderStart,socket,x,new Vector2(.052f,.052f),new Vector2(.052f,.052f));
            x=Fillet(ElbowStart,WristStart-ElbowStart-1,hinge,x,new Vector2(.051f,.051f),new Vector2(.045f,.045f));
            Vector3 cuffX=Quaternion.Inverse(cuff.Rotation(1))*Across(handAxis,handRight);
            if(Vector3.Dot(cuffX,x)<0)cuffX=-cuffX; // Ellipse axes are unsigned.
            Vector3 mid=Vector3.Lerp(hinge.End,cuff.Start,.5f);
            Ring(WristStart-1,mid,fore,(x+cuffX).normalized,.043f,.043f,Vector3.zero,1);
            x=Fillet(WristStart,WristEnd-WristStart+1,cuff,cuffX,new Vector2(.042f,.042f),new Vector2(.030f*scale,.020f*scale));
            Vector3 tip=wrist+handAxis*(.019f*scale);
            Ring(Rings-1,tip,handAxis,x,.023f*scale,.015f*scale,Vector3.zero,1);
            Vertices[VertexCount-2]=seat;Vertices[VertexCount-1]=tip;
            Normals[VertexCount-2]=-mount;Normals[VertexCount-1]=handAxis;
            Tangents[VertexCount-2]=Tangents[VertexCount-1]=new Vector4(x.x,x.y,x.z,1);
            for(int r=0;r<Rings;r++)
            {
                int first=r*Stride;Vector3 center=Vector3.zero;
                for(int j=0;j<Sides;j++)center+=Vertices[first+j];
                center/=Sides;
                for(int j=0;j<Sides;j++)
                {
                    int at=first+j;var radial=(Vertices[at]-center).normalized;
                    var around=(Vertices[first+(j+1)%Sides]-Vertices[first+(j+Sides-1)%Sides]).normalized;
                    Normals[at]=radial;
                    Tangents[at]=new Vector4(around.x,around.y,around.z,1);
                }
                Normals[first+Sides]=Normals[first];Tangents[first+Sides]=Tangents[first];
            }
            int boundary=WristStart*Stride;
            System.Array.Copy(Vertices,boundary,Vertices,SleeveEnd,Stride);
            System.Array.Copy(Normals,boundary,Normals,SleeveEnd,Stride);
            System.Array.Copy(Tangents,boundary,Tangents,SleeveEnd,Stride);
            // Cover the long portions with the source sleeves. Their rigid
            // end caps are recessed into the continuous joint surface.
            UpperFrom=socket.End-upper*.012f;UpperTo=hinge.Start+upper*.012f;
            ForeFrom=hinge.End-fore*.012f;ForeTo=cuff.Start+fore*.010f;
        }
    }
}
#endif
