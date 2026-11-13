shader_type spatial;
render_mode cull_disabled;
void fragment(){
	vec3 p=vec3(UV.x,UV.y,UV2.x);
	float side=step(.95,abs(p.z-.5)*2.);
	float rib=.5+.5*cos(p.x*6.2831853*40.);
	vec3 paint=vec3(.23,.35,.39)*mix(.72,1.,smoothstep(.05,.4,rib)*side+1.-side);
	float border=1.-smoothstep(.008,.018,min(min(p.y,1.-p.y),min(p.x,1.-p.x)));
	float end=step(.97,abs(p.x-.5)*2.);
	float bars=end*(1.-smoothstep(.015,.025,min(abs(p.z-.25),abs(p.z-.75))));
	paint=mix(paint,vec3(.12,.17,.18),border*.7);
	paint=mix(paint,vec3(.48,.51,.49),bars);
	float stripe=side*step(.65,p.y)*step(p.y,.69)*step(.07,p.x)*step(p.x,.21);
	ALBEDO=mix(paint,vec3(.81,.77,.56),stripe);ROUGHNESS=.84;METALLIC=.35;
}
