// Diagnostic full four-part Paintball-family comparison allowing independent scale and rotation.
const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs');
function basis(r){let xx=0,xz=0,zz=0;for(let p of r.vertices){let x=p[0]-r.center[0],z=p[2]-r.center[2];xx+=x*x;xz+=x*z;zz+=z*z}let t=.5*Math.atan2(2*xz,xx-zz),x=[Math.cos(t),0,Math.sin(t)];return {x,z:S.cross(x,[0,1,0])};}
function points(r,c,b){return r.vertices.map(p=>{let d=S.sub(p,c);return [S.dot(d,b.x),d[1],S.dot(d,b.z)]})}
function size(q){return [0,1,2].map(i=>Math.max(...q.map(p=>p[i]))-Math.min(...q.map(p=>p[i])))}
let reference=S.scan('battlefield-paintball-gods-pitdevil').rows;
let inner=reference.find(r=>r.key==='p8ad59c41'&&r.triangles===12&&S.length(S.sub(r.center,[-3.960614,78.7428,26.2366]))<.01);
let refs=reference.filter(r=>['p8ad59c41','pd959969c'].includes(r.key)&&S.length(S.sub(r.center,inner.center))<.13),rb=basis(inner),rs=size(points(inner,inner.center,rb));
let rows=S.scan('alfa-aglae-souza').rows,results=[];
for(let candidate of rows.filter(r=>r.triangles===12&&r.vertices.length===12)){
 let b=basis(candidate),cs=size(points(candidate,candidate.center,b));if(cs.some((v,i)=>v<.001||v/rs[i]<.1||v/rs[i]>8))continue;
 let near=rows.filter(r=>r.triangles<=32&&S.length(S.sub(r.center,candidate.center))<Math.max(.2,cs[2]));let best=Infinity,parts=[];
 for(let sx of [-1,1])for(let sz of [-1,1]){
  let error=0,used=new Set(),found=[];
  for(let ref of refs){let rp=points(ref,inner.center,rb).map(p=>p.map((v,i)=>v/rs[i]));let options=near.filter(r=>!used.has(r)&&r.triangles===ref.triangles&&r.vertices.length===ref.vertices.length).map(r=>{let cp=points(r,candidate.center,b).map(p=>[p[0]/cs[0]*sx,p[1]/cs[1],p[2]/cs[2]*sz]);let e=Math.max(...cp.map(p=>Math.min(...rp.map(q=>S.length(S.sub(p,q))))));return {r,e}}).sort((a,b)=>a.e-b.e);if(!options.length){error=Infinity;break}error=Math.max(error,options[0].e);used.add(options[0].r);found.push(options[0].r)}
  if(error<best){best=error;parts=found}
 }
 if(best<.05)results.push({center:candidate.center,material:candidate.key,error:best,scale:cs.map((v,i)=>v/rs[i]),parts:parts.map(r=>({key:r.key,triangles:r.triangles,center:r.center,size:r.size})),status:'geometry comparison only; context required'});
}
fs.writeFileSync('evidence/alpha-affine-frame-probe-r19.json',JSON.stringify({reference:inner.center,results},null,2));console.log(results.map(r=>({center:r.center,error:r.error,scale:r.scale})));
