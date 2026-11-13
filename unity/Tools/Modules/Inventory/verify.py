"""Reproduce neutral compiler and behavioral tests; writes receipts in owned evidence."""
import os
import subprocess
import sys
import inventory as tool

def main():
    tool.OUT.mkdir(parents=True,exist_ok=True)
    tools=tool.ROOT/'unity/Tools/Modules/Inventory'
    module=tool.ROOT/'unity/Assets/Ashfall/Modules/Inventory'
    exe=tool.OUT/'ContentIndexTests.exe'
    commands=[
        [sys.executable,'-B',str(tools/'inventory.py')],
        [sys.executable,'-B',str(tools/'test_inventory.py')],
        [sys.executable,'-B',str(tools/'test_reconciliation.py')],
        [sys.executable,'-B',str(tools/'reconcile_evidence.py')],
        ['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warnaserror+','/langversion:5','/target:exe','/out:'+str(exe),str(tool.ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'),str(module/'Ports.cs'),str(module/'ContentIndex.cs'),str(tools/'ContentIndexTests.cs')],
        [str(exe)],
        ['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warnaserror+','/langversion:5','/target:exe','/out:'+str(tool.OUT/'ApprovalAdversarialTests.exe'),str(tool.ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'),str(module/'Ports.cs'),str(module/'ContentIndex.cs'),str(tools/'ApprovalAdversarialTests.cs')],
        [str(tool.OUT/'ApprovalAdversarialTests.exe')],
    ]
    receipts=[]
    for argv in commands:
        result=subprocess.run(argv,cwd=str(tool.ROOT),capture_output=True,text=True,encoding='utf-8',errors='replace',env=dict(os.environ,PYTHONDONTWRITEBYTECODE='1'))
        receipts.append({'argv':argv,'windows_command_line':subprocess.list2cmdline(argv),'exit_code':result.returncode,'stdout':result.stdout,'stderr':result.stderr})
        tool.write(tool.EVIDENCE/'verification.json',receipts)
        print(result.stdout+result.stderr)
        if result.returncode: return result.returncode
    return 0

if __name__=='__main__': sys.exit(main())
