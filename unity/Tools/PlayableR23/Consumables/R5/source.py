"""Read-only published R6 snapshot. The directory name records the initial R5 port."""
from pathlib import Path
import hashlib,json,runpy
ROOT=Path(__file__).resolve().parents[5]
R6=Path('D:/ashfall-r6-staging/frozen-source-r6-v001')
MANIFEST=R6/'SOURCE-MANIFEST.json'
assert hashlib.sha256(MANIFEST.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111','Published R6 manifest identity'
INDEX={row['path']:row['sha256']for row in json.loads(MANIFEST.read_text())['files']}
CONTENT_ID='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
def read(relative):
 p=R6/'source'/relative
 assert hashlib.sha256(p.read_bytes()).hexdigest()==INDEX[relative],relative+' disagrees with frozen R6'
 return p
def verify(relative):
 p=read(relative);return dict(path=relative,resolved=str(p),sha256=INDEX[relative])
