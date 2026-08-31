import olefile, os

MSI = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\WaveLinkDriver_3.0.0.466_x64.msi"
OUT = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\drv_cab"
os.makedirs(OUT, exist_ok=True)

ole = olefile.OleFileIO(MSI)
print("=== OLE streams ===")
cands = []
for entry in ole.listdir():
    path = "/".join(entry)
    size = ole.get_size(path)
    print("  %s  (%d bytes)" % (path, size))
    low = path.lower()
    if low.endswith(".cab") or "media" in low:
        cands.append(path)

for path in cands:
    data = ole.openstream(path).read()
    sig = data[:4]
    print("CANDIDATE %s sig=%r" % (path, sig))
    if sig == b"MSCF":
        outp = os.path.join(OUT, path.replace("/", "_") + ".cab")
        with open(outp, "wb") as f:
            f.write(data)
        print("  -> valid CAB written:", outp, len(data))
ole.close()
print("done")
