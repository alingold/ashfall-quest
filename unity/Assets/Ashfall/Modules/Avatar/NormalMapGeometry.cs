using System;
namespace Ashfall.Modules.Avatar
{
    // Admission for generated normal-map bases, using already reflected vertices and
    // reversed indices. No further coordinate or winding conversion occurs here.
    public static class NormalMapGeometry
    {
        public static void Validate(float[] vertices,float[] normals,float[] uv,int[] indices)
        {
            if(vertices==null || vertices.Length==0 || vertices.Length%3!=0 || normals==null ||
               normals.Length!=vertices.Length || uv==null || uv.Length!=vertices.Length/3*2 ||
               indices==null || indices.Length==0 || indices.Length%3!=0)
                throw new AvatarValidationException("Normal-map geometry shape");
            int count=vertices.Length/3; var supported=new bool[count];
            for(int i=0;i<vertices.Length;i++)
                if(!Finite(vertices[i]) || !Finite(normals[i]))throw new AvatarValidationException("Normal-map finite geometry");
            foreach(float u in uv)if(!Finite(u))throw new AvatarValidationException("Normal-map finite UV");
            for(int i=0;i<count;i++) {
                double x=normals[i*3],y=normals[i*3+1],z=normals[i*3+2];
                if(Math.Abs(x*x+y*y+z*z-1)>0.002)throw new AvatarValidationException("Normal-map unit normal");
            }
            for(int i=0;i<indices.Length;i+=3) {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                if(a<0||b<0||c<0||a>=count||b>=count||c>=count)throw new AvatarValidationException("Normal-map index");
                double ux=(double)vertices[b*3]-vertices[a*3],uy=(double)vertices[b*3+1]-vertices[a*3+1],uz=(double)vertices[b*3+2]-vertices[a*3+2];
                double vx=(double)vertices[c*3]-vertices[a*3],vy=(double)vertices[c*3+1]-vertices[a*3+1],vz=(double)vertices[c*3+2]-vertices[a*3+2];
                double x=uy*vz-uz*vy,y=uz*vx-ux*vz,z=ux*vy-uy*vx;
                double du=(double)uv[b*2]-uv[a*2],dv=(double)uv[b*2+1]-uv[a*2+1];
                double eu=(double)uv[c*2]-uv[a*2],ev=(double)uv[c*2+1]-uv[a*2+1];
                if(x*x+y*y+z*z>1e-24 && Math.Abs(du*ev-dv*eu)>1e-12)
                    supported[a]=supported[b]=supported[c]=true;
            }
            foreach(bool value in supported)if(!value)throw new AvatarValidationException("Normal-map vertex has no nondegenerate UV/geometry contribution");
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
