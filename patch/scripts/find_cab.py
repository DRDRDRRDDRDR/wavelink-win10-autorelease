import olefile, os

MSI = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\WaveLinkDriver_3.0.0.466_x64.msi"
OUT = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\drv_cab"
os.makedirs(OUT, exist_ok=True)

ole = olefile.OleFileIO(MSI)
found = []
for entry in ole.listdir():
    path = "/".join(entry)
    data = ole.openstream(path).read()
    if data[:4] == b"MSCF":
        print("CAB stream: %s  (%d bytes)" % (path, len(data)))
        outp = os.path.join(OUT, "embedded.cab")
        with open(outp, "wb") as f:
            f.write(data)
        print("  -> wrote", outp)
        found.append(outp)
    # also show raw utf-16 name for streams whose size > 100000 to eyeball the real name
    if len(data) > 100000:
        raw = None
        try:
            raw = bytes(path, "utf-16-le").decode("utf-16-le", "replace")
        except Exception:
            raw = path
        print("  big stream name(raw utf16): %r size=%d" % (path, len(data)))
ole.close()
print("found cabinets:", found)
