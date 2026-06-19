import json
from pathlib import Path
from graphify.detect import detect

print("Running detection...")
result = detect(Path("."))
Path("graphify-out/.graphify_detect.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
print(f"Detected {result.get('total_files', 0)} files, {result.get('total_words', 0)} words")
