from pathlib import Path
import hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');SRC=FROZEN/'source'
def sha(path):
 with Path(path).open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert sha(FROZEN/'SOURCE-MANIFEST.json')=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
PINS={r['path']:r['sha256']for r in json.loads((FROZEN/'SOURCE-MANIFEST.json').read_text())['files']}
USED={}
def source(relative):
 path=SRC/relative;assert sha(path)==PINS[relative],relative;USED[relative]=PINS[relative];return path
