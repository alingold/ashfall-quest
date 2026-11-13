"""Preserve small official MIT source inputs/identities for the implemented curve."""
from pathlib import Path
import hashlib,json,re,urllib.request
HERE=Path(__file__).resolve().parent;DEST=HERE/'official';DEST.mkdir(exist_ok=True)
URLS={
 'tonemap-4.5.glsl':'https://raw.githubusercontent.com/godotengine/godot/4.5/drivers/gles3/shaders/tonemap_inc.glsl',
 'tonemap-master.glsl':'https://raw.githubusercontent.com/godotengine/godot/master/drivers/gles3/shaders/tonemap_inc.glsl',
 'environment-4.5.h':'https://raw.githubusercontent.com/godotengine/godot/4.5/scene/resources/environment.h',
 'scene-4.5.glsl':'https://raw.githubusercontent.com/godotengine/godot/4.5/drivers/gles3/shaders/scene.glsl',
 'GODOT-LICENSE.txt':'https://raw.githubusercontent.com/godotengine/godot/4.5/LICENSE.txt'}
def main():
    rows={}
    for name,url in URLS.items():
        raw=urllib.request.urlopen(url,timeout=30).read();(DEST/name).write_bytes(raw)
        rows[name]=dict(url=url,sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw))
    old=(DEST/'tonemap-4.5.glsl').read_text();new=(DEST/'tonemap-master.glsl').read_text()
    for text in (old,new):
        filmic=text[text.index('vec3 tonemap_filmic'):text.index('// Adapted from https://github.com/TheRealMJP')]
        for token in ['exposure_bias = 2.0f','0.22f * exposure_bias * exposure_bias','0.30f * exposure_bias','0.10f','0.20f','0.01f','0.30f']:assert token in filmic
    assert 'float tonemap_exposure = 1.0;'in(DEST/'environment-4.5.h').read_text() and 'float tonemap_white = 1.0;'in(DEST/'environment-4.5.h').read_text()
    license=(DEST/'GODOT-LICENSE.txt').read_text()
    (HERE/'Staged/Resources/GODOT-LICENSE.txt').write_text(license,encoding='utf-8')
    (HERE/'source.json').write_text(json.dumps(dict(official=rows,whiteDefault=1,exposureDefault=1,filmicCoefficientParityWithMaster=True,masterDifference='white normalizer moved to CPU tonemapper_params.x; numerator/constants unchanged',compatibilityOrder='linear lighting + emission/ambient + fog; multiply exposure; filmic(max0,white); Godot linear_to_srgb; framebuffer write. Additive passes map separately.',frozenR6RendererDefault='mobile; Compatibility requires a launch override',capturedR23ValleyEnvironment='tonemap_mode2, exposure1, white1 in existing qualified environment.json'),indent=2)+'\n',encoding='utf-8')
    print('Official curve/default/order/license inputs preserved and coefficients verified; no engine.')
if __name__=='__main__':main()
