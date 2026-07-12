"""Parse design card tables and Unity W(...) id mappings into a catalog."""

from __future__ import annotations

import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DESIGN_CARD_DOCS = [
    ROOT / "design" / "策划案" / "职业系统" / "战士" / "Feature-战士卡牌.md",
]
DEFAULT_WARRIOR_CS = (
    ROOT
    / "VibeGame"
    / "Assets"
    / "Scripts"
    / "HexDemo"
    / "HexBattleCore.cs"
)

W_CALL_RE = re.compile(
    r'W\(\s*"(?P<id>warrior_[^"]+)"\s*,\s*"(?P<name>[^"]+)"',
    re.MULTILINE,
)

TABLE_HEADER_MARKERS = ("卡牌名称", "卡牌描述", "费用")


@dataclass
class CatalogCard:
    name: str
    description: str = ""
    cost: str = ""
    range_text: str = ""
    keywords: str = ""
    rarity: str = ""
    category: str = ""
    subcategory: str = ""
    system: str = ""
    synergy: str = ""
    transition_subtype: str = ""
    expected_prereq: str = ""
    note: str = ""
    section: str = ""
    profession: str = "Warrior"
    card_id: str = ""
    source: str = ""

    def search_blob(self) -> str:
        parts = [
            self.card_id,
            self.name,
            self.description,
            self.keywords,
            self.rarity,
            self.category,
            self.subcategory,
            self.system,
            self.synergy,
            self.transition_subtype,
            self.note,
            self.section,
        ]
        return " ".join(p for p in parts if p).lower()

    def to_dict(self) -> dict:
        return asdict(self)


def _clean_cell(raw: str) -> str:
    text = (raw or "").strip()
    text = text.replace("**", "")
    text = re.sub(r"`([^`]*)`", r"\1", text)
    return text.strip()


def _split_row(line: str) -> list[str]:
    line = line.strip()
    if not line.startswith("|"):
        return []
    parts = [p.strip() for p in line.strip("|").split("|")]
    return parts


def _is_separator(cells: list[str]) -> bool:
    if not cells:
        return False
    return all(re.fullmatch(r":?-{3,}:?", c.replace(" ", "")) is not None for c in cells if c)


def _guess_section(heading: str) -> str:
    heading = heading.strip()
    if "基础" in heading:
        return "基础"
    if "过渡" in heading:
        return "过渡"
    if "消耗" in heading:
        return "消耗"
    if "集中" in heading:
        return "集中"
    if "移动" in heading or "位移" in heading:
        return "移动"
    if "塞牌" in heading:
        return "塞牌"
    if "初始" in heading:
        return "初始"
    return heading or "未分类"


def parse_feature_markdown(path: Path, profession: str = "Warrior") -> list[CatalogCard]:
    if not path.exists():
        return []

    lines = path.read_text(encoding="utf-8").splitlines()
    cards: list[CatalogCard] = []
    current_heading = ""
    i = 0
    while i < len(lines):
        line = lines[i]
        heading_match = re.match(r"^#{1,4}\s+(.*)$", line.strip())
        if heading_match:
            current_heading = heading_match.group(1).strip()
            i += 1
            continue

        cells = _split_row(line)
        if len(cells) >= 3 and cells[0] == "卡牌名称" and "卡牌描述" in cells:
            headers = cells
            i += 1
            if i < len(lines) and _is_separator(_split_row(lines[i])):
                i += 1
            section = _guess_section(current_heading)
            while i < len(lines):
                row = _split_row(lines[i])
                if not row or row[0] == "卡牌名称":
                    break
                if _is_separator(row):
                    i += 1
                    continue
                if len(row) < 2 or not row[0] or set(row[0]) <= {"-", ":"}:
                    i += 1
                    continue

                def col(name: str, default: str = "") -> str:
                    try:
                        idx = headers.index(name)
                    except ValueError:
                        return default
                    return _clean_cell(row[idx]) if idx < len(row) else default

                name = col("卡牌名称")
                if not name:
                    i += 1
                    continue

                system = col("所属体系") or ("无" if section == "基础" else section)
                cards.append(
                    CatalogCard(
                        name=name,
                        description=col("卡牌描述"),
                        cost=col("费用"),
                        range_text=col("范围"),
                        keywords=col("词缀"),
                        rarity=col("稀有度"),
                        category=col("类别"),
                        subcategory=col("子类别"),
                        system=system,
                        synergy=col("协同角色"),
                        transition_subtype=col("过渡子类"),
                        expected_prereq=col("预期前置"),
                        note=col("备注"),
                        section=section,
                        profession=profession,
                        source=str(path),
                    )
                )
                i += 1
            continue
        i += 1

    return cards


def load_unity_name_to_id(cs_path: Path = DEFAULT_WARRIOR_CS) -> dict[str, str]:
    if not cs_path.exists():
        return {}
    text = cs_path.read_text(encoding="utf-8")
    mapping: dict[str, str] = {}
    for match in W_CALL_RE.finditer(text):
        mapping[match.group("name")] = match.group("id")
    return mapping


def enrich_with_ids(cards: Iterable[CatalogCard], name_to_id: dict[str, str]) -> list[CatalogCard]:
    enriched: list[CatalogCard] = []
    for card in cards:
        card.card_id = name_to_id.get(card.name, card.card_id)
        enriched.append(card)
    return enriched


def load_default_catalog() -> list[CatalogCard]:
    name_to_id = load_unity_name_to_id()
    cards: list[CatalogCard] = []
    for path in DEFAULT_DESIGN_CARD_DOCS:
        profession = "Warrior" if "战士" in path.as_posix() else "General"
        cards.extend(parse_feature_markdown(path, profession=profession))
    return enrich_with_ids(cards, name_to_id)


def unique_values(cards: Iterable[CatalogCard], attr: str) -> list[str]:
    values = sorted({getattr(card, attr, "") or "" for card in cards if getattr(card, attr, "")})
    return values
