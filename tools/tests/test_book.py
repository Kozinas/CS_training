"""Regression checks for the single-source course/PDF workflow."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import book


class BookTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        (self.root / "docs").mkdir()
        (self.root / "labs").mkdir()
        (self.root / "docs/one.md").write_text("# One\n\n[Program](../PLAN.md)\n", encoding="utf-8")
        (self.root / "PLAN.md").write_text("# Program\n", encoding="utf-8")
        manifest = {"parts": [{"title": "Course", "chapters": [
            ["01", "One", "docs/one.md"], ["P", "Program", "PLAN.md"]]}]}
        (self.root / "book.json").write_text(json.dumps(manifest), encoding="utf-8")
        (self.root / "README.md").write_text(
            "# Course\n\n<!-- TOC:START -->\n<!-- TOC:END -->\n", encoding="utf-8")
        book.update_toc(self.root, write=True)

    def tearDown(self):
        self.directory.cleanup()

    def test_valid_minimal_book(self):
        count, files = book.check(self.root)
        self.assertEqual(count, 2)
        self.assertEqual(files, 3)

    def test_heading_and_short_listing_stay_on_same_page(self):
        from pypdf import PdfReader

        fonts = book.find_fonts(SimpleNamespace(font=None, bold_font=None, mono_font=None))
        source = self.root / "docs/one.md"
        parts = ["# One"]
        for index in range(36):
            parts.extend(["Filler paragraph for the page boundary."] * (index % 7))
            parts.append(f"## Boundary section {index:02}")
            listing = "\n".join([f"payload-{index:02}"] * (1 + index % 8))
            parts.append(f"```text\n{listing}\n```")
        source.write_text("\n\n".join(parts), encoding="utf-8")
        meta, chapters = book.load_book(self.root)
        meta.update(title="Layout test", subtitle="Short listings", standard="C#",
                    edition="test", scope="Page boundary regression")
        target = self.root / "layout.pdf"
        with patch.object(book, "ROOT", self.root):
            book.build_pdf(meta, chapters, target, fonts)
        pages = [page.extract_text() or "" for page in PdfReader(target).pages]
        for index in range(36):
            heading_pages = [i for i, text in enumerate(pages)
                             if f"Boundary section {index:02}" in text]
            code_pages = [i for i, text in enumerate(pages) if f"payload-{index:02}" in text]
            self.assertEqual(len(heading_pages), 1)
            self.assertEqual(heading_pages, code_pages, f"Orphan heading {index}")

    def test_orphan_chapter_cannot_be_omitted_from_pdf(self):
        (self.root / "docs/two.md").write_text("# Two\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unlisted chapter"):
            book.check(self.root)

    def test_orphan_lab_cannot_be_omitted_from_pdf(self):
        (self.root / "labs/lab.md").write_text("# Lab\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unlisted chapter"):
            book.check(self.root)

    def test_broken_local_link(self):
        (self.root / "docs/one.md").write_text("# One\n\n[Bad](missing.md)\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Broken link"):
            book.check(self.root)

    def test_broken_heading_anchor(self):
        (self.root / "docs/one.md").write_text("# One\n\n[Bad](../PLAN.md#absent)\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Missing anchor"):
            book.check(self.root)

    def test_duplicate_manifest_entry(self):
        data = json.loads((self.root / "book.json").read_text())
        data["parts"][0]["chapters"].append(["02", "Again", "docs/one.md"])
        (self.root / "book.json").write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "Duplicate chapter paths"):
            book.load_book(self.root)

    def test_escape_from_source_root(self):
        with self.assertRaisesRegex(ValueError, "escapes repository"):
            book.local_target(self.root / "docs/one.md", "../../outside.md", self.root)

    def test_generic_type_requires_code_markup(self):
        (self.root / "docs/one.md").write_text("# One\n\nUse List<T>.\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unsupported inline HTML"):
            book.check(self.root)

    def test_heading_slugs_are_unique(self):
        parsed = book.PARSER.parse("# Title\n\n## Повтор\n\n## Повтор\n")
        self.assertEqual([h[3] for h in book.headings(parsed)], ["title", "повтор", "повтор-1"])

    def test_missing_code_fence_is_reported(self):
        (self.root / "docs/one.md").write_text("# One\n\n```csharp\nint x = 1;\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unclosed code fence"):
            book.check(self.root)

    def test_actual_book_has_all_learning_pages(self):
        count, _ = book.check(allow_missing_artifacts=True)
        _, chapters = book.load_book()
        self.assertEqual(count, len(chapters))
        source = book.ROOT / "docs/01-foundations/01-platform.md"
        linked = book.ROOT / "docs/01-foundations/02-syntax.md"
        paths = {chapter.path.resolve() for chapter in chapters}
        self.assertEqual(book.resolve_book_link(source, "02-syntax.md", paths),
                         "#" + book.anchor_key(linked))


if __name__ == "__main__":
    unittest.main()
