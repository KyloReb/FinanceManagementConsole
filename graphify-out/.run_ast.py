import json
from pathlib import Path
from graphify.extract import collect_files, extract
from graphify.detect import detect

print("Re-running detection for fresh file list...")
result = detect(Path("."))
code_files_raw = result.get("files", {}).get("code", [])

cfs = []
for f in code_files_raw:
    p = Path(f)
    if p.is_dir():
        cfs.extend(collect_files(p))
    else:
        cfs.append(p)

print(f"Found {len(cfs)} code files. Running AST extraction with max_workers=1...")
if cfs:
    r = extract(cfs, cache_root=Path("."), parallel=False)
else:
    r = {"nodes": [], "edges": [], "input_tokens": 0, "output_tokens": 0}

open("graphify-out/.graphify_ast.json", "w", encoding="utf-8").write(
    json.dumps(r, indent=2, ensure_ascii=False)
)
print(f"AST: {len(r['nodes'])} nodes, {len(r['edges'])} edges")
