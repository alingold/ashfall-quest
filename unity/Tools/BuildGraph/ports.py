"""Owned BuildGraph/1 data ports; no shared Unity contract replacements."""
from dataclasses import dataclass, field


@dataclass
class Assembly:
    name: str
    directory: str
    kind: str  # neutral, engine, editor
    sources: list = field(default_factory=list)
    references: list = field(default_factory=list)
    external: list = field(default_factory=list)
    guid: str = ""
    existing_path: str = ""
    definition: dict = field(default_factory=dict)


@dataclass
class Graph:
    assemblies: list
    inputs: list = field(default_factory=list)
    findings: list = field(default_factory=list)


class SnapshotCompiler:
    """compile(sources, fresh_output) -> recorded Toolchain/2 result, no engine."""
    def compile(self, sources, fresh_output):
        raise NotImplementedError
