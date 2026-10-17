"""Download transparent animated stickers from Tenor search results.

Usage:
    python tools/fetch_tenor.py "bubu dudu sleep" [--limit 20] [--out incoming]

Files are saved as animated WebP with a transparent background. Review them,
then move the ones you like into animations/<character>/<state>/.
"""
import argparse
import html
import json
import os
import re
import sys
import urllib.parse
import urllib.request

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/128 Safari/537.36"


def get(url, attempts=3):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    for i in range(attempts):
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return r.read()
        except urllib.error.URLError:
            if i == attempts - 1:
                raise


def search(query):
    slug = re.sub(r"[^a-z0-9]+", "-", query.lower()).strip("-")
    page = get(f"https://tenor.com/search/{urllib.parse.quote(slug)}-stickers").decode("utf-8")
    m = re.search(r'<script id="store-cache"[^>]*>(.*?)</script>', page, re.S)
    if not m:
        return []
    store = json.loads(html.unescape(m.group(1)))
    results = []
    for entry in store.get("universal", {}).get("search", {}).values():
        results.extend(entry.get("results", []))
    return results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("query")
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..", "incoming"))
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    seen, saved = set(), 0
    for item in search(args.query):
        if saved >= args.limit:
            break
        fmt = item.get("media_formats", {}).get("webp_transparent")
        if not fmt or item["id"] in seen:
            continue
        seen.add(item["id"])
        name = os.path.basename(urllib.parse.urlparse(fmt["url"]).path)
        name = re.sub(r"[^A-Za-z0-9._-]", "_", urllib.parse.unquote(name))
        path = os.path.join(args.out, f"{item['id']}_{name}")
        if os.path.exists(path):
            continue
        try:
            with open(path, "wb") as f:
                f.write(get(fmt["url"]))
            saved += 1
            print("saved", path)
        except Exception as e:  # keep going on individual failures
            print("failed", fmt["url"], e, file=sys.stderr)
    print(f"{saved} new sticker(s) in {os.path.abspath(args.out)}")


if __name__ == "__main__":
    main()
