import re, io, os

p = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\src\AppxManifest.xml"
with io.open(p, "r", encoding="utf-8") as f:
    s = f.read()

# 1) Lower the two MinVersion gates (Win11 build 22000 -> Win10 2004 build 19041)
n1 = s.count('MinVersion="10.0.22000.0"')
s = s.replace('MinVersion="10.0.22000.0"', 'MinVersion="10.0.19041.0"')

# 2) Replace ONLY the <Identity> Publisher with our self-signed cert subject
#    (PackageDependency publishers must stay as Microsoft Corporation)
s2, k = re.subn(r'(<Identity[^>]*?)Publisher="[^"]*"',
                r'\1Publisher="CN=WaveLinkPatch"', s)
assert k == 1, f"Identity Publisher replaced {k} times (expected 1)"

# 3) Clean up the display name for consistency (cosmetic, not required)
s3 = s2.replace("<PublisherDisplayName>Corsair Memory, Inc.</PublisherDisplayName>",
                "<PublisherDisplayName>WaveLinkPatch</PublisherDisplayName>")

with io.open(p, "w", encoding="utf-8") as f:
    f.write(s3)

print("MinVersion(22000) occurrences replaced:", n1)
print("Publisher replaced:", k)
print("New MinVersion lines:")
for ln in s3.splitlines():
    if "MinVersion" in ln or "Publisher=" in ln:
        print("  ", ln.strip())

# 4) Remove the now-invalid old signature so makeappx repacks cleanly
sig = r"C:\Users\DR\WorkBuddy\2026-08-25-21-52-18\wavelink_patched\src\AppxSignature.p7x"
if os.path.exists(sig):
    os.remove(sig)
    print("Removed old AppxSignature.p7x")
else:
    print("No AppxSignature.p7x present")
