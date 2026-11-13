"""Read-only disk sampling; receipts stay off the constrained system volume."""
import datetime
import json
import pathlib
import shutil
import time

GIB = 1024 ** 3


class SlotDiskGuard:
    def __init__(self, receipt, artifact_root="D:/"):
        self.receipt = pathlib.Path(receipt).resolve()
        if self.receipt.drive.upper() != "D:" or self.receipt.exists():
            raise ValueError("Fresh absolute D: disk-monitor receipt required")
        self.samples = []
        self.artifact_drive = pathlib.Path(artifact_root).resolve().drive.upper()
        if self.artifact_drive not in ("C:", "D:"):
            raise ValueError("Explicit C: or D: artifact destination required")
        self.worker_pid = None
        self.result = None

    @staticmethod
    def refusal(c_free, d_free, initial=False, artifact_drive="D:"):
        if initial:
            if artifact_drive == "C:":
                if c_free < 5 * GIB or d_free < GIB // 2:
                    return "Initial C-output Android gate refused: C<5GiB or D journal<512MiB"
            elif artifact_drive == "D:":
                if c_free < 4 * GIB or d_free < 2 * GIB:
                    return "Initial D-output Android gate refused: C<4GiB or D<2GiB"
            else:
                return "Unsupported artifact destination"
        elif c_free < GIB or d_free < GIB // 2:
            return "Live disk abort: C<1GiB or D<512MiB"
        return None

    def sample(self, initial=False):
        c_free = shutil.disk_usage("C:/").free
        d_free = shutil.disk_usage("D:/").free
        reason = self.refusal(c_free, d_free, initial, self.artifact_drive)
        self.samples.append(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                 cFreeBytes=c_free, dFreeBytes=d_free, refusal=reason))
        self.write()
        return reason

    def finish(self, result):
        self.result = result
        self.write()

    def write(self):
        data = dict(workerPid=self.worker_pid, artifactDrive=self.artifact_drive,
                    initialCMinimum=(5 if self.artifact_drive == "C:" else 4) * GIB,
                    initialDMinimum=GIB // 2 if self.artifact_drive == "C:" else 2 * GIB, abortCMinimum=GIB,
                    abortDMinimum=GIB // 2, samples=self.samples,
                    result=self.result, deletionsPerformed=False,
                    processPolicy="Wrapper reaps only its own Popen worker/tree")
        temp = self.receipt.with_suffix(".tmp")
        temp.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
        # Windows readers may briefly omit FILE_SHARE_DELETE. Keep the existing
        # valid snapshot while retrying this one metadata publication for <=.5s;
        # the disk sample itself has already been taken and checked in memory.
        for attempt in range(21):
            try:
                temp.replace(self.receipt)
                break
            except PermissionError:
                if attempt == 20:
                    raise
                time.sleep(.025)
