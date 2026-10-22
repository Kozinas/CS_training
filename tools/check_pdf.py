"""Check the complete PDF: text, chapter destinations, links, fonts and bookmarks."""
from pathlib import Path
import sys

from pypdf import PdfReader
from book import ROOT, load_book


def main():
    reader = PdfReader(ROOT / "dist/csharp.pdf")
    meta, chapters = load_book()
    texts = [page.extract_text() or "" for page in reader.pages]
    normalized = " ".join("\n".join(texts).split())
    errors = []
    for chapter in chapters:
        if " ".join(chapter.title.split()) not in normalized:
            errors.append("Missing chapter: " + chapter.title)
    for marker in ("C#", "Оглавление", "Практика", "TryParse", "count=3",
                   "Два обязательных способа изучения", "Главы 11–60"):
        if marker not in normalized:
            errors.append("Missing text marker: " + marker)
    if "\ufffd" in normalized or "\x00" in normalized:
        errors.append("Replacement/null character in PDF")
    internal = external = relative = 0
    embedded = set()
    for page in reader.pages:
        for ref in page.get("/Annots", []):
            annotation = ref.get_object()
            action = annotation.get("/A", {})
            if "/Dest" in annotation or action.get("/S") == "/GoTo":
                internal += 1
            if action.get("/S") == "/URI":
                uri = str(action.get("/URI", ""))
                if uri.startswith(("https:", "http:")):
                    external += 1
                else:
                    relative += 1
            destination = annotation.get("/Dest")
            if isinstance(destination, list) and destination:
                target_id = getattr(destination[0], "idnum", None)
                page_ids = {p.indirect_reference.idnum for p in reader.pages}
                if target_id is not None and target_id not in page_ids:
                    errors.append("Internal link targets a missing page")
        resources = page.get("/Resources", {})
        for ref in resources.get("/Font", {}).values():
            font = ref.get_object()
            descriptor = font.get("/FontDescriptor")
            if descriptor:
                descriptor = descriptor.get_object()
                if "/FontFile2" in descriptor or "/FontFile3" in descriptor:
                    embedded.add(str(font.get("/BaseFont")))
    if internal < len(chapters):
        errors.append("Too few internal links")
    if not external:
        errors.append("No source links")
    if len(embedded) < 3:
        errors.append("Expected embedded regular, bold and code fonts")
    if not reader.outline:
        errors.append("No bookmarks")
    if errors:
        raise ValueError("\n".join(errors))
    print(f"PASS PDF: {len(reader.pages)} pages, {len(chapters)} sections, "
          f"{internal} internal / {external} web / {relative} repository links; "
          f"{len(embedded)} embedded fonts; Cyrillic and bookmarks")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
