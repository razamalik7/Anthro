#!/usr/bin/env python3
import os, json, glob, datetime

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
archive_dir = os.path.join(ROOT_DIR, "docs", "archive", "claude")
md_dir = os.path.join(archive_dir, "readable")
os.makedirs(md_dir, exist_ok=True)

files = sorted(glob.glob(os.path.join(archive_dir, "*.jsonl")))
index_entries = []

for f in files:
    base = os.path.basename(f)
    session_id = base.replace(".jsonl", "")
    md_file = os.path.join(md_dir, f"{session_id}.md")
    
    first_time = None
    last_time = None
    message_count = 0
    user_prompts = []
    
    with open(f, "r", encoding="utf-8", errors="replace") as in_f, open(md_file, "w", encoding="utf-8") as out_f:
        out_f.write(f"# Claude Code Session: {session_id}\n\n")
        
        for line in in_f:
            try:
                obj = json.loads(line)
            except:
                continue
                
            t = obj.get("type")
            
            ts = obj.get("timestamp")
            if ts:
                try:
                    dt = datetime.datetime.fromtimestamp(ts / 1000.0) if ts > 1e11 else datetime.datetime.fromtimestamp(ts)
                    if not first_time: first_time = dt
                    last_time = dt
                except:
                    pass
                    
            if t == "user":
                message_count += 1
                msg = obj.get("message", {})
                content = msg.get("content", "")
                if isinstance(content, list):
                    text_parts = [item.get("text", "") for item in content if item.get("type") == "text"]
                    c_str = "\n".join(text_parts).strip()
                else:
                    c_str = str(content).strip()
                    
                if c_str and not c_str.startswith("[{"):
                    user_prompts.append(c_str[:120])
                    out_f.write(f"### User\n\n{c_str}\n\n---\n\n")
                    
            elif t == "assistant":
                message_count += 1
                msg = obj.get("message", {})
                content = msg.get("content", [])
                out_f.write("### Claude (Assistant)\n\n")
                if isinstance(content, list):
                    for item in content:
                        itype = item.get("type")
                        if itype == "text":
                            out_f.write(item.get("text", "") + "\n\n")
                        elif itype == "tool_use":
                            tool_name = item.get("name")
                            arg_preview = str(item.get("input", ""))[:120].replace("\n", " ")
                            out_f.write(f"> **Tool Call: `{tool_name}`** `{arg_preview}...`\n\n")
                elif isinstance(content, str):
                    out_f.write(content + "\n\n")
                out_f.write("---\n\n")
                
        time_str = f"{first_time.strftime('%Y-%m-%d %H:%M')} to {last_time.strftime('%H:%M')}" if first_time and last_time else "Unknown date"
        first_prompt = user_prompts[0] if user_prompts else "No user prompt"
        index_entries.append((base, time_str, message_count, len(user_prompts), first_prompt, md_file))

# Write index
index_file = os.path.join(archive_dir, "README.md")
with open(index_file, "w", encoding="utf-8") as idx_f:
    idx_f.write("# Claude Code Transcripts Archive\n\n")
    idx_f.write("This directory archives all historical Claude Code sessions, prompts, tool traces, and memory files from the project inception through October 15, 2026.\n\n")
    idx_f.write("## Sessions Index\n\n")
    idx_f.write("| Session File | Date / Time | Messages | User Turns | Initial Topic | Readable Markdown |\n")
    idx_f.write("|:---|:---:|:---:|:---:|:---|:---:|\n")
    for base, time_str, count, u_count, prompt, md_path in index_entries:
        rel_md = os.path.relpath(md_path, archive_dir).replace("\\", "/")
        prompt_clean = prompt.replace("|", "\\|").replace("\n", " ")
        idx_f.write(f"| `{base}` | {time_str} | {count} | {u_count} | {prompt_clean[:60]}... | [View Markdown]({rel_md}) |\n")
    idx_f.write("\n## Memory Directory\n\n")
    idx_f.write("The active standing preferences and project context are also synced into [`docs/agent-memory/`](../agent-memory/).\n")

print(f"Successfully processed {len(files)} sessions into {md_dir}")
