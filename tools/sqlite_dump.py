import sqlite3, shutil, os, json, sys

SRC = r"C:\ProgramData\Autodesk\ODIS"
TMP = os.path.join(os.environ["TEMP"], "opencode", "odis-snapshot")
os.makedirs(TMP, exist_ok=True)

for name in ["Install.db", "Package.db"]:
    src, dst = os.path.join(SRC, name), os.path.join(TMP, name)
    shutil.copy2(src, dst)
    con = sqlite3.connect(dst)
    con.row_factory = sqlite3.Row
    cur = con.cursor()
    print(f"\n========== {name} ==========")
    tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type IN ('table')")]
    for t in tables:
        cols = [r[1] for r in cur.execute(f"PRAGMA table_info({t})")]
        cnt = cur.execute(f"SELECT COUNT(*) FROM {t}").fetchone()[0]
        print(f"\nTABLE {t} ({cnt} rows) cols={cols}")
        if cnt == 0:
            continue
        rows = cur.execute(f"SELECT * FROM {t}").fetchmany(200)
        for r in rows[:50]:
            d = {k: (str(r[k])[:200] if r[k] is not None else None) for k in r.keys()}
            print(json.dumps(d, ensure_ascii=False))
