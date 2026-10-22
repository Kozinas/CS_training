"""Build the ordered Markdown handbook and a linked, Unicode PDF offline."""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
from hashlib import sha256
from html import escape
import json
import os
from pathlib import Path
import re
import sys
from urllib.parse import quote, unquote, urlsplit

from markdown_it import MarkdownIt

ROOT = Path(__file__).resolve().parents[1]
PARSER = MarkdownIt("commonmark").enable("table")
TOC_START, TOC_END = "<!-- TOC:START -->", "<!-- TOC:END -->"


@dataclass(frozen=True)
class Chapter:
    number: str
    title: str
    path: Path
    part: str


def load_book(root: Path = ROOT):
    meta = json.loads((root / "book.json").read_text(encoding="utf-8"))
    chapters = [Chapter(n, t, root / p, part["title"])
                for part in meta["parts"] for n, t, p in part["chapters"]]
    paths = [c.path.resolve() for c in chapters]
    if len(set(paths)) != len(paths):
        raise ValueError("Duplicate chapter paths in book.json")
    if len({c.number for c in chapters}) != len(chapters):
        raise ValueError("Duplicate chapter numbers in book.json")
    for path in paths:
        allowed = (path.is_relative_to(root.resolve() / "docs")
                   or path.is_relative_to(root.resolve() / "labs")
                   or path == root.resolve() / "PLAN.md")
        if not allowed:
            raise ValueError(f"Book page must be inside docs/labs or PLAN.md: {path}")
    return meta, chapters


def plain_inline(token):
    return "".join(c.content for c in token.children or []
                   if c.type in ("text", "code_inline"))


def slugify(text: str):
    return re.sub(r"\s", "-", re.sub(r"[^\w\-\s]", "", text.lower()))


def headings(tokens):
    counts = Counter()
    result = []
    for i, token in enumerate(tokens):
        if token.type != "heading_open":
            continue
        title = plain_inline(tokens[i + 1])
        slug = slugify(title)
        count = counts[slug]
        counts[slug] += 1
        if count:
            slug += f"-{count}"
        result.append((i, int(token.tag[1:]), title, slug, token.map[0]))
    return result


def local_target(source: Path, href: str, root: Path = ROOT):
    parsed = urlsplit(href)
    if parsed.scheme or parsed.netloc:
        if parsed.scheme not in ("http", "https", "mailto"):
            raise ValueError(f"Unsupported URL scheme: {href}")
        return None
    path = (source.parent / unquote(parsed.path)).resolve() if parsed.path else source.resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f"Link escapes repository: {source}: {href}")
    return path, unquote(parsed.fragment)


def toc_text(chapters, root=ROOT):
    lines = []
    last = None
    for chapter in chapters:
        if chapter.part != last:
            lines += ["", f"### {chapter.part}", ""]
            last = chapter.part
        lines.append(f"- [{chapter.number}. {chapter.title}]({chapter.path.relative_to(root).as_posix()})")
    return "\n".join(lines) + "\n"


def update_toc(root=ROOT, write=False):
    _, chapters = load_book(root)
    path = root / "README.md"
    source = path.read_text(encoding="utf-8")
    if source.count(TOC_START) != 1 or source.count(TOC_END) != 1:
        raise ValueError("README must have exactly one TOC marker pair")
    before, rest = source.split(TOC_START)
    _, after = rest.split(TOC_END)
    expected = before + TOC_START + "\n" + toc_text(chapters, root) + TOC_END + after
    if write:
        path.write_text(expected, encoding="utf-8", newline="\n")
    return expected == source


def check(root=ROOT, allow_missing_artifacts=False):
    _, chapters = load_book(root)
    errors = []
    chapter_paths = {c.path.resolve() for c in chapters}
    actual = ({p.resolve() for folder in ("docs", "labs")
               for p in (root / folder).rglob("*.md")}
              | {(root / "PLAN.md").resolve()})
    for path in sorted(actual - chapter_paths):
        errors.append(f"Unlisted chapter: {path.relative_to(root)}")
    for path in sorted(chapter_paths - actual):
        errors.append(f"Missing chapter: {path.relative_to(root)}")
    if not update_toc(root):
        errors.append("README TOC is stale: run python tools/book.py toc")
    files = sorted(actual | {p.resolve() for p in root.glob("*.md")}
                   | {p.resolve() for p in (root / "examples").rglob("*.md")})
    anchors = {}
    parsed_files = {}
    for path in files:
        tokens = PARSER.parse(path.read_text(encoding="utf-8"))
        parsed_files[path] = tokens
        hs = headings(tokens)
        anchors[path] = {h[3] for h in hs}
        if path in chapter_paths and sum(h[1] == 1 for h in hs) != 1:
            errors.append(f"Expected exactly one H1: {path.relative_to(root)}")
        for token in tokens:
            for child in token.children or []:
                if child.type == "html_inline" and not child.content.strip().startswith("<!--"):
                    errors.append(f"Unsupported inline HTML in {path.relative_to(root)}: {child.content}")
            if token.type in ("html_block", "html_inline") and not token.content.strip().startswith("<!--"):
                errors.append(f"Unsupported HTML in {path.relative_to(root)}")
            if token.type == "fence" and token.map:
                lines = path.read_text(encoding="utf-8").splitlines()
                if not lines[token.map[1] - 1].strip().startswith(token.markup):
                    errors.append(f"Unclosed code fence in {path.relative_to(root)}:{token.map[0] + 1}")
    for path, tokens in parsed_files.items():
        for token in tokens:
            for child in token.children or []:
                if child.type not in ("link_open", "image"):
                    continue
                href = child.attrGet("href" if child.type == "link_open" else "src")
                try:
                    target = local_target(path, href, root)
                    if target is None:
                        continue
                    dest, fragment = target
                    if allow_missing_artifacts and dest in {
                        root / "dist/csharp.pdf", root / "dist/csharp.md"
                    }:
                        continue
                    if not dest.exists():
                        errors.append(f"Broken link in {path.relative_to(root)}: {href}")
                    elif fragment and dest.suffix == ".md":
                        if dest not in anchors:
                            anchors[dest] = {h[3] for h in headings(PARSER.parse(dest.read_text(encoding="utf-8")))}
                        if fragment not in anchors[dest]:
                            errors.append(f"Missing anchor in {path.relative_to(root)}: {href}")
                except ValueError as exc:
                    errors.append(str(exc))
    if errors:
        raise ValueError("\n".join(errors))
    return len(chapters), len(files)


def anchor_key(path: Path, slug=""):
    relative = path.resolve().relative_to(ROOT.resolve()).as_posix()
    return "d" + sha256(relative.encode()).hexdigest()[:12] + ("-" + slug if slug else "")


def resolve_book_link(source, href, chapter_paths, pdf=True):
    target = local_target(source, href)
    if target is None:
        return href
    path, fragment = target
    if path in chapter_paths:
        return "#" + anchor_key(path, fragment)
    relative = os.path.relpath(path, ROOT / "dist").replace(os.sep, "/")
    return quote(relative) + ("#" + quote(fragment) if fragment else "")


def merged_markdown(meta, chapters):
    chapter_paths = {c.path.resolve() for c in chapters}
    out = [f"# {meta['title']}\n\n{meta['subtitle']} · {meta['standard']} · {meta['edition']}\n"]
    out.append("## Оглавление\n")
    for c in chapters:
        out.append(f"- [{c.number}. {c.title}](#{anchor_key(c.path)})")
    for chapter in chapters:
        text = chapter.path.read_text(encoding="utf-8")
        tokens = PARSER.parse(text)
        inserts = {h[4]: f'<a id="{anchor_key(chapter.path, h[3])}"></a>' for h in headings(tokens)}
        link_spans = set()
        for token in tokens:
            if token.type == "inline" and token.map:
                link_spans.update(range(*token.map))
        lines = []
        for i, line in enumerate(text.splitlines()):
            if i in inserts:
                lines.append(inserts[i])
            if i in link_spans:
                line = re.sub(r"(!?\[[^\]]*\]\()([^\s)]+)(\))",
                    lambda m: m[1] + resolve_book_link(chapter.path, m[2], chapter_paths, False) + m[3], line)
            lines.append(line)
        out.append(f'\n<a id="{anchor_key(chapter.path)}"></a>\n\n' + "\n".join(lines) + "\n")
    return "\n".join(out)


def find_fonts(args):
    if any((args.font, args.bold_font, args.mono_font)):
        if not all((args.font, args.bold_font, args.mono_font)):
            raise ValueError("Provide all three: --font, --bold-font, --mono-font")
        paths = [Path(args.font), Path(args.bold_font), Path(args.mono_font)]
    else:
        windows = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
        linux = Path("/usr/share/fonts/truetype/dejavu")
        if (windows / "arial.ttf").exists():
            paths = [windows / p for p in ("arial.ttf", "arialbd.ttf", "consola.ttf")]
        else:
            paths = [linux / p for p in ("DejaVuSans.ttf", "DejaVuSans-Bold.ttf", "DejaVuSansMono.ttf")]
    if not all(p.is_file() for p in paths):
        raise ValueError("Cyrillic TTF fonts not found; install DejaVu or pass explicit font paths")
    return paths


def build_pdf(meta, chapters, target, fonts):
    from reportlab.lib import colors
    from reportlab.lib.enums import TA_LEFT
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.styles import ParagraphStyle
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.platypus import (BaseDocTemplate, Frame, PageTemplate, Paragraph,
                                   PageBreak, Preformatted, Spacer, Table, TableStyle,
                                   CondPageBreak, KeepTogether)
    from reportlab.platypus.tableofcontents import TableOfContents

    for name, path in zip(("Book", "BookBold", "Code"), fonts):
        pdfmetrics.registerFont(TTFont(name, str(path)))
    pdfmetrics.registerFontFamily("Book", normal="Book", bold="BookBold", italic="Book", boldItalic="BookBold")
    blue = colors.HexColor("#164b6b")
    base = ParagraphStyle("body", fontName="Book", fontSize=10, leading=14.8,
                          spaceAfter=8, alignment=TA_LEFT, splitLongWords=True)
    styles = {
        "body": base,
        "h1": ParagraphStyle("h1", parent=base, fontName="BookBold", fontSize=21,
                             leading=26, textColor=blue, spaceAfter=18, keepWithNext=True),
        "h2": ParagraphStyle("h2", parent=base, fontName="BookBold", fontSize=13,
                             leading=18, textColor=blue, spaceBefore=13, spaceAfter=8, keepWithNext=True),
        "h3": ParagraphStyle("h3", parent=base, fontName="BookBold", fontSize=11,
                             leading=15, spaceBefore=10, keepWithNext=True),
        "cell": ParagraphStyle("cell", parent=base, fontSize=8.1, leading=11, spaceAfter=0),
        "code": ParagraphStyle("code", fontName="Code", fontSize=8.1, leading=10.8,
                               backColor=colors.HexColor("#f1f4f7"), borderPadding=7,
                               spaceBefore=5, spaceAfter=12),
        "small": ParagraphStyle("small", parent=base, fontSize=8, leading=11, textColor=blue),
    }
    width, height = A4
    margin = 45
    content_width = width - 2 * margin

    class BookDocument(BaseDocTemplate):
        def afterFlowable(self, flowable):
            if hasattr(flowable, "book_heading"):
                level, label, key = flowable.book_heading
                self.canv.bookmarkPage(key)
                if level == 1:
                    self.canv.bookmarkPage(flowable.chapter_key)
                    self.notify("TOCEntry", (0, escape(label), self.page, key))
                self.canv.addOutlineEntry(label, key, level=level - 1, closed=False)

    def page_frame(canvas, doc):
        canvas.saveState()
        canvas.setFont("Book", 8)
        canvas.setFillColor(colors.HexColor("#667788"))
        canvas.drawString(margin, 24, "C# и .NET · Методичка и практикум")
        canvas.drawRightString(width - margin, 24, str(doc.page))
        canvas.restoreState()

    doc = BookDocument(str(target), pagesize=A4, leftMargin=margin, rightMargin=margin,
                       topMargin=42, bottomMargin=42, title=meta["title"],
                       author="CSharp course contributors", allowSplitting=True)
    doc.addPageTemplates(PageTemplate(id="book", frames=[Frame(
        margin, 42, content_width, height - 84, leftPadding=0, rightPadding=0,
        topPadding=0, bottomPadding=0)], onPage=page_frame))
    chapter_paths = {c.path.resolve() for c in chapters}

    def inline(token, source):
        chunks = []
        for child in token.children or []:
            kind = child.type
            if kind == "text": chunks.append(escape(child.content))
            elif kind == "code_inline": chunks.append(f'<font name="Code">{escape(child.content)}</font>')
            elif kind == "strong_open": chunks.append("<b>")
            elif kind == "strong_close": chunks.append("</b>")
            elif kind == "em_open": chunks.append("<i>")
            elif kind == "em_close": chunks.append("</i>")
            elif kind == "softbreak": chunks.append(" ")
            elif kind == "hardbreak": chunks.append("<br/>")
            elif kind == "link_open":
                href = resolve_book_link(source, child.attrGet("href"), chapter_paths)
                chunks.append(f'<a href="{escape(href, quote=True)}" color="#126185">')
            elif kind == "link_close": chunks.append("</a>")
            elif kind == "html_inline" and child.content.startswith("<!--"): pass
            else: raise ValueError(f"Unsupported inline token {kind} in {source}")
        return "".join(chunks)

    story = [Spacer(1, 90), Paragraph(escape(meta["title"]), styles["h1"]),
             Paragraph(escape(meta["subtitle"]), styles["h2"]),
             Paragraph(f'{escape(meta["standard"])}<br/>Редакция {escape(meta["edition"])}', base),
             Spacer(1, 30), Paragraph(
                 "Последовательное изучение языка C# и платформы .NET. "
                 "Теория, самостоятельные задания и сквозной проект. "
                 + escape(meta["scope"]) + " "
                 "Все написанные учебные страницы включены в эту книгу.", base), PageBreak(),
             Paragraph("Оглавление", styles["h1"])]
    toc = TableOfContents()
    toc.levelStyles = [ParagraphStyle("toc", parent=base, fontSize=10, leading=14,
                                    spaceBefore=3, spaceAfter=3)]
    story.append(toc)
    last_part = None
    for chapter in chapters:
        story.append(PageBreak())
        if chapter.part != last_part:
            story.append(Paragraph(escape(chapter.part), styles["small"]))
            last_part = chapter.part
        tokens = PARSER.parse(chapter.path.read_text(encoding="utf-8"))
        hs = {h[0]: h for h in headings(tokens)}
        stack = []
        pending_bullet = None
        i = 0
        while i < len(tokens):
            token = tokens[i]
            kind = token.type
            if kind == "heading_open":
                _, level, label, slug, _ = hs[i]
                if level > 3:
                    raise ValueError(f"Only H1-H3 supported: {chapter.path}")
                heading = Paragraph(inline(tokens[i + 1], chapter.path), styles[f"h{level}"])
                if i + 3 < len(tokens) and tokens[i + 3].type == "table_open":
                    # Keep the heading with a few rows, not with the whole long table.
                    story.append(CondPageBreak(90))
                    heading.keepWithNext = False
                heading.book_heading = level, label, anchor_key(chapter.path, slug)
                heading.chapter_key = anchor_key(chapter.path)
                story.append(heading)
                i += 3
                continue
            if kind == "table_open":
                rows, row = [], []
                i += 1
                while tokens[i].type != "table_close":
                    item = tokens[i]
                    if item.type == "tr_open": row = []
                    elif item.type == "inline": row.append(Paragraph(inline(item, chapter.path), styles["cell"]))
                    elif item.type == "tr_close": rows.append(row)
                    i += 1
                columns = len(rows[0])
                table = Table(rows, colWidths=[content_width / columns] * columns,
                              repeatRows=1, hAlign="LEFT")
                table.setStyle(TableStyle([
                    ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#dce8f0")),
                    ("VALIGN", (0, 0), (-1, -1), "TOP"),
                    ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#bdcbd5")),
                    ("LEFTPADDING", (0, 0), (-1, -1), 6),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                    ("TOPPADDING", (0, 0), (-1, -1), 5),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
                ]))
                story += [table, Spacer(1, 10)]
            elif kind in ("fence", "code_block"):
                max_chars = int((content_width - 14) / pdfmetrics.stringWidth("M", "Code", 8.1))
                block = Preformatted(token.content.rstrip("\n"), styles["code"],
                                     maxLineLength=max_chars, newLineChars="    ")
                # Keep short examples readable as a unit; long listings may span pages.
                story.append(KeepTogether([block]) if len(block.lines) <= 20 else block)
            elif kind in ("bullet_list_open", "ordered_list_open"):
                stack.append(None if kind == "bullet_list_open" else int(token.attrGet("start") or 1))
            elif kind in ("bullet_list_close", "ordered_list_close"):
                stack.pop()
            elif kind == "list_item_open":
                pending_bullet = "•" if stack[-1] is None else str(stack[-1]) + "."
                if stack[-1] is not None: stack[-1] += 1
            elif kind == "inline":
                style = base
                if stack:
                    style = ParagraphStyle("list", parent=base, leftIndent=16 * len(stack),
                                           bulletIndent=16 * (len(stack) - 1), bulletFontName="Book")
                story.append(Paragraph(inline(token, chapter.path), style, bulletText=pending_bullet))
                pending_bullet = None
            elif kind == "hr":
                story.append(Spacer(1, 10))
            elif kind == "html_block" and token.content.strip().startswith("<!--"):
                pass
            elif kind not in ("paragraph_open", "paragraph_close", "list_item_close",
                               "blockquote_open", "blockquote_close"):
                raise ValueError(f"Unsupported Markdown token {kind} in {chapter.path}")
            i += 1
    doc.multiBuild(story)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("toc")
    commands.add_parser("check")
    build = commands.add_parser("build")
    for name in ("font", "bold-font", "mono-font"):
        build.add_argument("--" + name)
    args = parser.parse_args()
    try:
        if args.command == "toc":
            update_toc(write=True)
            print("README table of contents updated")
        elif args.command == "check":
            count, files = check(allow_missing_artifacts=True)
            print(f"PASS: {count} chapters, {files} Markdown files; manifest, TOC, local links and anchors")
        else:
            check(allow_missing_artifacts=True)
            meta, chapters = load_book()
            output = ROOT / "dist"
            output.mkdir(exist_ok=True)
            # Replace complete artifacts only after successful rendering.
            pdf_tmp = output / "csharp.tmp.pdf"
            build_pdf(meta, chapters, pdf_tmp, find_fonts(args))
            markdown_tmp = output / "csharp.tmp.md"
            markdown_tmp.write_text(merged_markdown(meta, chapters), encoding="utf-8", newline="\n")
            pdf_tmp.replace(output / "csharp.pdf")
            markdown_tmp.replace(output / "csharp.md")
            check()
            print(f"Built {len(chapters)} pages/chapters: dist/csharp.pdf and dist/csharp.md")
        return 0
    except (ValueError, OSError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
