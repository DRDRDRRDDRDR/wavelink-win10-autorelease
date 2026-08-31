import msilib, os

MSI = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\WaveLinkDriver_3.0.0.466_x64.msi"
OUT = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\drv_cab"
os.makedirs(OUT, exist_ok=True)

db = msilib.OpenDatabase(MSI, msilib.MSIDBOPEN_READONLY)

def dump(sql):
    print("=== SQL:", sql)
    v = db.OpenView(sql)
    v.Execute(None)
    while True:
        rec = v.Fetch()
        if not rec:
            break
        parts = []
        for i in range(rec.GetFieldCount()):
            try:
                val = rec.GetString(i)
            except Exception as e:
                try:
                    val = rec.GetInteger(i)
                except Exception as e2:
                    val = "<err:%s>" % e
            parts.append("%d=%r" % (i, val))
        print("  ", ", ".join(parts))
    v.Close()

dump("SELECT * FROM `Media`")

# Candidate embedded cabinet stream names
candidates = ["Media.cab", "26", "media.cab", "#Media.cab"]
for name in candidates:
    try:
        st = db.OpenStream(name)
        data = b""
        while True:
            ch = st.Read(65536)
            if not ch:
                break
            data += ch
        print("STREAM OPEN OK name=%r size=%d" % (name, len(data)))
        if data[:4] == b"MSCF":
            print("  -> valid CAB signature (MSCF) at offset 0")
            outp = os.path.join(OUT, "embedded_" + name.replace("#", "") + ".cab")
            with open(outp, "wb") as f:
                f.write(data)
            print("  -> wrote", outp)
        else:
            print("  -> not a CAB at offset 0 (first4=%r)" % data[:4])
    except Exception as e:
        print("STREAM open failed name=%r : %s" % (name, e))
