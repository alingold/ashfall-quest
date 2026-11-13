import runpy
from pathlib import Path
# The checker imports this directory's verified frozen-R6 prepare adapter.
runpy.run_path(str(Path(__file__).resolve().parent.parent/'R5/check.py'),run_name='__main__')
