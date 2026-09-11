# Phase 1 spike: dump Autodesk Access ODIS LMDB state to find the
# "update available" signal. Copies DB files to temp (read-only safe)
# then dumps every table as key/value lines.
import lmdb, json, os, shutil, sys

SRC = r"C:\ProgramData\Autodesk\ODIS"
TMP = os.path.join(os.environ.get("TEMP", "/tmp"), "opencode", "odis-snapshot")
os.makedirs(TMP, exist_ok=True)

FILES = ["Install.db", "Package.db", "LocalCache.db", "data.mdb"]

def readable(b):
    try:
        s = b.decode("utf-8")
        if all(31 < ord(c) < 127 or c in "\n\r\t" for c in s.replace("\n","").replace("\r","")):
            return s
    except UnicodeDecodeError:
        pass
    return b.hex()[:120]

for name in FILES:
    src = os.path.join(SRC, name)
    dst = os.path.join(TMP, name)
    try:
        shutil.copy2(src, dst)
        shutil.copy2(src + "-lock", dst + "-lock") if os.path.exists(src + "-lock") else None
    except PermissionError as e:
        print(f"{name}: copy failed {e}", file=sys.stderr)
        continue
    print(f"===== {name} =====")
    try:
        env = lmdb.open(dst, readonly=True, lock=False, subdir=False, max_dbs=16)
        dbs = []
        with env.begin() as txn:
            cur = txn.cursor()
            try:
                while cur.next():
                    dbs.append(cur.key())
            except Exception:
                dbs = [b""]  # single-DB file
        for dbk in dbs:
            sub = env.open_db(dbk) if dbk and dbk != name else None
            with env.begin(db=sub) as txn:
                cur = txn.cursor()
                n = 0
                for k, v in cur:
                    ks, vs = readable(k), readable(v)
                    if len(vs) > 400:
                        vs = vs[:400] + f"...({len(v)}B)"
                    print(f"[{dbk.decode(errors='replace')}]" if dbk else "[main]")
                    print(f"  K: {ks}")
                    print(f"  V: {vs}")
                    n += 1
                print(f"  -- {n} entries in [{dbk.decode(errors='replace') if dbk else 'main'}]")
            if n and n > 2000:
                break
    except Exception as e:
        print(f"  open failed: {e}")
