"""Existing Unity assembly ownership, indexed before resolving forward references."""
import json
from pathlib import PurePosixPath
import re
from ports import Assembly


def key(path):
    return str(path).replace('\\', '/').casefold()


def path_errors(paths):
    seen, errors = {}, []
    for path in paths:
        p = str(path).replace('\\', '/')
        components = p.split('/')
        reserved = r'(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])(?:\..*)?'
        if (str(path) != p or PurePosixPath(p).is_absolute() or
                any(x in ('.', '..', '') or x.endswith((' ', '.')) or
                    re.fullmatch(reserved, x, re.I) or
                    re.fullmatch(r'[^.]{1,6}~[0-9]+(?:\..*)?', x) or
                    any(ord(c) < 32 or c in '<>:"|?*' for c in x)
                    for x in components)):
            errors.append('unsafe-path:' + p)
        k = key(p)
        if k in seen:
            errors.append('case-path-collision:' + seen[k] + ':' + p)
        seen[k] = p
    # Also detect directories differing only by case even with distinct filenames.
    dirs = {}
    for path in paths:
        parts = str(path).replace('\\', '/').split('/')
        for i in range(1, len(parts)):
            d = '/'.join(parts[:i])
            if key(d) in dirs and dirs[key(d)] != d:
                errors.append('case-directory-collision:' + dirs[key(d)] + ':' + d)
            dirs[key(d)] = d
    return sorted(set(errors))


def index(files, root, external=()):
    definitions, boundaries, guids, names, errors = [], {}, {}, {}, []
    errors += path_errors([p.relative_to(root).as_posix() for p in files])
    if errors:
        return definitions, boundaries, errors
    metadata = {}
    for p in files:
        if p.suffix == '.meta':
            m = re.search(r'^guid:\s*([0-9a-fA-F]{32})\s*$', p.read_text(encoding='utf-8-sig'), re.M)
            if m:
                g = m[1].lower()
                if g in metadata:
                    errors.append('existing-duplicate-guid:' + str(p))
                metadata[g] = p
    for p in files:
        if p.suffix != '.asmdef':
            continue
        try:
            d = json.loads(p.read_text(encoding='utf-8-sig'))
        except (ValueError, UnicodeError):
            errors.append('invalid-asmdef-json:' + p.relative_to(root).as_posix())
            continue
        if (not isinstance(d, dict) or not isinstance(d.get('name'), str) or
                not isinstance(d.get('references', []), list) or
                any(not isinstance(r, str) for r in d.get('references', []))):
            errors.append('invalid-asmdef-fields:' + p.relative_to(root).as_posix())
            continue
        if len(d.get('references', [])) != len(set(d.get('references', []))):
            errors.append('duplicate-assembly-reference:' + p.relative_to(root).as_posix())
        name = d.get('name', '')
        meta = next((g for g, m in metadata.items() if str(m) == str(p) + '.meta'), '')
        rel = p.relative_to(root).as_posix()
        include = d.get('includePlatforms', [])
        kind = 'editor' if include == ['Editor'] else ('neutral' if d.get('noEngineReferences') else 'engine')
        a = Assembly(name, p.parent.relative_to(root).as_posix(), kind,
                     references=d.get('references', [])[:], guid=meta, existing_path=rel, definition=d)
        if not re.fullmatch(r'[A-Za-z_][A-Za-z_0-9.\-]*', name):
            errors.append('invalid-assembly-name:' + name)
        if name.casefold() in names:
            errors.append('duplicate-assembly:' + name)
        if not meta:
            errors.append('invalid-guid:' + name)
        names[name.casefold()] = a
        if meta in guids:
            errors.append('duplicate-guid:' + name)
        guids[meta] = a
        definitions.append(a)
        if a.directory in boundaries:
            errors.append('duplicate-boundary:' + a.directory)
        boundaries[a.directory] = a
    def resolve(ref):
        if ref.startswith('GUID:'):
            return guids.get(ref[5:].lower()) if re.fullmatch('[0-9a-fA-F]{32}', ref[5:]) else None
        return next((a for a in definitions if a.name == ref), None)
    for a in definitions:
        resolved = []
        for ref in a.references:
            target = resolve(ref)
            if target:
                resolved.append(target.name)
            elif ref not in external:
                errors.append('missing-reference:' + a.name + ':' + ref)
        a.references = resolved
    for p in files:
        if p.suffix != '.asmref':
            continue
        directory = p.parent.relative_to(root).as_posix()
        try:
            d = json.loads(p.read_text(encoding='utf-8-sig'))
            ref = d.get('reference') if isinstance(d, dict) else None
        except (ValueError, UnicodeError):
            ref = None
        target = resolve(ref) if isinstance(ref, str) else None
        if directory in boundaries:
            errors.append('duplicate-boundary:' + directory)
        boundaries[directory] = target
        if target is None:
            errors.append('dangling-asmref:' + p.relative_to(root).as_posix())
    return definitions, boundaries, errors


def owner(source, boundaries):
    p = PurePosixPath(source).parent
    for directory in (p, *p.parents):
        if str(directory) in boundaries:
            return True, boundaries[str(directory)]
    return False, None
