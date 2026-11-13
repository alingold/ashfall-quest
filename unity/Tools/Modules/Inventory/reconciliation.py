"""Repeatable, fail-closed small-source capture. No global filesystem transaction claim."""
import hashlib
from types import MappingProxyType
from reconciliation_ports import SourcePort

class SourceDrift(RuntimeError):
    pass

class FileSources(SourcePort):
    def __init__(self, root, assignment):
        self.root, self.assignment = root, assignment
    def members(self):
        paths = [self.assignment, self.root/'skins_pipeline/registry.json',
                 self.root/'docs/agent-runs/20261104-0020/lead-u/inventory/installed-map-catalog.json']
        for tree in ['quest-r22/source', 'game']:
            paths.extend((self.root/tree/'scripts').rglob('*.gd'))
            paths.append(self.root/tree/'assets/data/one_star_050.json')
        return sorted(paths)
    def read_bytes(self, path):
        return path.read_bytes()

class Snapshot:
    def __init__(self, port, files):
        self.port = port
        self.files = MappingProxyType(dict(files))
    @staticmethod
    def scan(port):
        names = sorted(port.members())
        if len(names) != len(set(names)):
            raise SourceDrift('Duplicate source identity')
        try:
            return {p: port.read_bytes(p) for p in names}
        except OSError as e:
            raise SourceDrift('Source disappeared during capture: '+str(e)) from e
    @classmethod
    def capture(cls, port):
        first = cls.scan(port)
        second = cls.scan(port)
        cls.compare(first, second)
        return cls(port, first)
    @staticmethod
    def compare(first, second):
        changes = [str(p) for p in sorted(set(first)|set(second)) if first.get(p) != second.get(p)]
        if changes:
            raise SourceDrift('Source drift; no publication: '+', '.join(changes))
    def validate(self):
        self.compare(self.files, self.scan(self.port))
    def text(self, path):
        return self.files[path].decode('utf-8-sig')
    def digest(self, path):
        return hashlib.sha256(self.files[path]).hexdigest()

def verify_receipt(receipt):
    """Consumers reject incomplete/partial publication, even with an older receipt."""
    from pathlib import Path
    seen = set()
    for row in receipt['outputs']:
        path = Path(row['path'])
        if path in seen:
            raise SourceDrift('Duplicate output identity: '+str(path))
        seen.add(path)
        try:
            data = path.read_bytes()
        except OSError as e:
            raise SourceDrift('Missing published output: '+str(path)) from e
        if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
            raise SourceDrift('Incomplete or modified publication: '+str(path))
    return True
