import sqlite3, os
path = os.path.join(os.environ["TEMP"], "opencode", "odis-snapshot", "Install.db")
con = sqlite3.connect(path)
con.row_factory = sqlite3.Row
cur = con.cursor()
for (t,) in cur.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall():
    cols = [r[1] for r in cur.execute(f"PRAGMA table_info({t})")]
    cnt = cur.execute(f"SELECT COUNT(*) FROM {t}").fetchone()[0]
    print(t, cnt, cols)
    if t in ("Install", "Bundle", "Version"):
        for r in cur.execute(f"SELECT * FROM {t} LIMIT 20").fetchall():
            print({k: str(r[k])[:120] for k in r.keys()})
