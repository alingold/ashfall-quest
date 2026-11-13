"""Owned tooling port. No shared or engine contracts are introduced."""
class SourcePort:
    def members(self):
        """Return exact small-source paths, including the recursive script membership."""
        raise NotImplementedError

    def read_bytes(self, path):
        """Read original bytes; implementation must not normalize source identities."""
        raise NotImplementedError
