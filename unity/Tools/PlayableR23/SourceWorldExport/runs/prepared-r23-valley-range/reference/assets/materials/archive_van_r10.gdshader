shader_type spatial;
render_mode cull_disabled;
void fragment(){
	vec3 p=vec3(UV.x,UV.y,UV2.x);
	vec3 normal=normalize(cross(dFdx(p),dFdy(p)));
	float side=step(.8,abs(normal.z));
	float ends=step(.8,abs(normal.x));
	vec3 paint=vec3(.34,.48,.55);
	paint*=.91+.09*smoothstep(.12,.9,p.y);
	float cab=max(step(.72,p.x)*step(p.x,.95),step(.05,p.x)*step(p.x,.28));
	float window=side*cab*step(.51,p.y)*step(p.y,.87);
	window=max(window,ends*step(.58,p.y)*step(p.y,.88));
	float wheel=step(p.y,.29)*max(1.-step(.13,abs(p.x-.19)),1.-step(.13,abs(p.x-.8)));
	float bumper=ends*step(p.y,.2);
	float seam=side*(1.-smoothstep(.002,.005,min(abs(p.x-.64),abs(p.y-.37))));
	vec3 color=mix(paint,vec3(.10,.16,.19),seam*.65);
	color=mix(color,vec3(.025,.065,.085),window);
	color=mix(color,vec3(.028,.032,.037),max(wheel,bumper));
	float lamp=ends*step(.3,p.y)*step(p.y,.42)*step(.29,abs(p.z-.5));
	color=mix(color,mix(vec3(.7,.035,.02),vec3(.92,.84,.58),step(.5,p.x)),lamp);
	ALBEDO=color;ROUGHNESS=mix(.72,.22,window);METALLIC=.2;
}
