"""Warrior deck / card-table builder UI.

Browse Feature card attributes, filter/sort by category, and assemble a deck
exportable for Battle Sandbox (`deckCardIds`).
"""

from __future__ import annotations

import json
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from card_catalog import CatalogCard, load_default_catalog, unique_values


APP_TITLE = "牌表生成器 · Deck Table Builder"
FILTER_ALL = "全部"
EXPORT_DIR = Path(__file__).resolve().parent / "exports"
DEFAULT_DECK_PATH = EXPORT_DIR / "sandbox_deck.json"

SORT_OPTIONS = [
    ("名称", "name"),
    ("费用", "cost"),
    ("稀有度", "rarity"),
    ("类别", "category"),
    ("所属体系", "system"),
    ("协同角色", "synergy"),
    ("分区", "section"),
    ("卡牌ID", "card_id"),
]

DETAIL_FIELDS = [
    ("卡牌ID", "card_id"),
    ("名称", "name"),
    ("职业", "profession"),
    ("费用", "cost"),
    ("范围", "range_text"),
    ("词缀", "keywords"),
    ("稀有度", "rarity"),
    ("类别", "category"),
    ("子类别", "subcategory"),
    ("所属体系", "system"),
    ("协同角色", "synergy"),
    ("过渡子类", "transition_subtype"),
    ("预期前置", "expected_prereq"),
    ("分区", "section"),
    ("备注", "note"),
    ("描述", "description"),
]

RARITY_ORDER = {
    "初始": 0,
    "Starter": 0,
    "Baseline": 1,
    "Common": 2,
    "Uncommon": 3,
    "Rare": 4,
}


class DeckTableBuilderApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title(APP_TITLE)
        self.root.geometry("1380x820")
        self.root.minsize(1100, 700)

        self.catalog: list[CatalogCard] = load_default_catalog()
        self.visible: list[CatalogCard] = list(self.catalog)
        self.deck: list[CatalogCard] = []
        self.current_file: Path | None = None
        self._sort_reverse = False

        self.search_var = tk.StringVar()
        self.filter_system_var = tk.StringVar(value=FILTER_ALL)
        self.filter_category_var = tk.StringVar(value=FILTER_ALL)
        self.filter_rarity_var = tk.StringVar(value=FILTER_ALL)
        self.filter_section_var = tk.StringVar(value=FILTER_ALL)
        self.filter_synergy_var = tk.StringVar(value=FILTER_ALL)
        self.sort_var = tk.StringVar(value=SORT_OPTIONS[0][0])
        self.status_var = tk.StringVar(value="Ready")
        self.deck_count_var = tk.StringVar(value="牌表：0 张")
        self.detail_vars = {key: tk.StringVar() for _, key in DETAIL_FIELDS}

        self._build_ui()
        self._reload_filter_choices()
        self._apply_filters()
        self._set_status(f"已加载 {len(self.catalog)} 张设计卡牌")

    def _build_ui(self) -> None:
        self.root.columnconfigure(0, weight=3)
        self.root.columnconfigure(1, weight=2)
        self.root.columnconfigure(2, weight=2)
        self.root.rowconfigure(0, weight=1)

        left = ttk.Frame(self.root, padding=10)
        left.grid(row=0, column=0, sticky="nsew")
        left.columnconfigure(0, weight=1)
        left.rowconfigure(2, weight=1)

        mid = ttk.Frame(self.root, padding=(0, 10, 10, 10))
        mid.grid(row=0, column=1, sticky="nsew")
        mid.columnconfigure(0, weight=1)
        mid.rowconfigure(1, weight=1)

        right = ttk.Frame(self.root, padding=(0, 10, 10, 10))
        right.grid(row=0, column=2, sticky="nsew")
        right.columnconfigure(0, weight=1)
        right.rowconfigure(1, weight=1)

        # --- Catalog toolbar ---
        title = ttk.Frame(left)
        title.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        title.columnconfigure(1, weight=1)
        ttk.Label(title, text="卡牌库", font=("Segoe UI", 14, "bold")).grid(row=0, column=0, sticky="w")
        ttk.Label(title, textvariable=self.status_var, foreground="#4b5563").grid(row=0, column=1, sticky="e")

        filters = ttk.LabelFrame(left, text="筛选 / 排序", padding=8)
        filters.grid(row=1, column=0, sticky="ew", pady=(0, 8))
        for col in range(4):
            filters.columnconfigure(col, weight=1)

        ttk.Label(filters, text="搜索").grid(row=0, column=0, sticky="w")
        search_entry = ttk.Entry(filters, textvariable=self.search_var)
        search_entry.grid(row=0, column=1, columnspan=3, sticky="ew", padx=(6, 0), pady=2)
        search_entry.bind("<KeyRelease>", lambda _e: self._apply_filters())

        self._add_filter(filters, 1, 0, "体系", self.filter_system_var)
        self._add_filter(filters, 1, 2, "类别", self.filter_category_var)
        self._add_filter(filters, 2, 0, "稀有度", self.filter_rarity_var)
        self._add_filter(filters, 2, 2, "分区", self.filter_section_var)
        self._add_filter(filters, 3, 0, "协同", self.filter_synergy_var)

        ttk.Label(filters, text="排序").grid(row=3, column=2, sticky="w", pady=2)
        sort_box = ttk.Combobox(
            filters,
            textvariable=self.sort_var,
            values=[label for label, _ in SORT_OPTIONS],
            state="readonly",
            width=12,
        )
        sort_box.grid(row=3, column=3, sticky="ew", padx=(6, 0), pady=2)
        sort_box.bind("<<ComboboxSelected>>", lambda _e: self._apply_filters())

        btns = ttk.Frame(filters)
        btns.grid(row=4, column=0, columnspan=4, sticky="ew", pady=(6, 0))
        ttk.Button(btns, text="刷新目录", command=self._reload_catalog).pack(side="left")
        ttk.Button(btns, text="反向排序", command=self._toggle_sort_dir).pack(side="left", padx=6)
        ttk.Button(btns, text="清空筛选", command=self._clear_filters).pack(side="left")

        # --- Catalog tree ---
        catalog_frame = ttk.Frame(left)
        catalog_frame.grid(row=2, column=0, sticky="nsew")
        catalog_frame.columnconfigure(0, weight=1)
        catalog_frame.rowconfigure(0, weight=1)

        columns = ("name", "cost", "system", "category", "rarity", "card_id")
        self.catalog_tree = ttk.Treeview(
            catalog_frame,
            columns=columns,
            show="headings",
            selectmode="extended",
        )
        headings = {
            "name": ("名称", 100),
            "cost": ("费", 40),
            "system": ("体系", 70),
            "category": ("类别", 60),
            "rarity": ("稀有度", 80),
            "card_id": ("ID", 150),
        }
        for key, (label, width) in headings.items():
            self.catalog_tree.heading(key, text=label, command=lambda k=key: self._sort_by_column(k))
            self.catalog_tree.column(key, width=width, anchor="w")

        yscroll = ttk.Scrollbar(catalog_frame, orient="vertical", command=self.catalog_tree.yview)
        self.catalog_tree.configure(yscrollcommand=yscroll.set)
        self.catalog_tree.grid(row=0, column=0, sticky="nsew")
        yscroll.grid(row=0, column=1, sticky="ns")
        self.catalog_tree.bind("<<TreeviewSelect>>", self._on_catalog_select)
        self.catalog_tree.bind("<Double-1>", lambda _e: self._add_selected_to_deck())

        catalog_actions = ttk.Frame(left)
        catalog_actions.grid(row=3, column=0, sticky="ew", pady=(8, 0))
        ttk.Button(catalog_actions, text="添加选中 → 牌表", command=self._add_selected_to_deck).pack(side="left")
        ttk.Button(catalog_actions, text="添加 ×4", command=lambda: self._add_selected_to_deck(4)).pack(
            side="left", padx=6
        )
        ttk.Button(catalog_actions, text="加载初始 9 张", command=self._load_starter_deck).pack(side="left")

        # --- Detail panel ---
        ttk.Label(mid, text="卡牌属性", font=("Segoe UI", 14, "bold")).grid(row=0, column=0, sticky="w", pady=(0, 8))
        detail = ttk.LabelFrame(mid, text="实时预览", padding=10)
        detail.grid(row=1, column=0, sticky="nsew")
        detail.columnconfigure(1, weight=1)

        for row, (label, key) in enumerate(DETAIL_FIELDS):
            ttk.Label(detail, text=label, width=10).grid(row=row, column=0, sticky="nw", pady=2)
            if key == "description":
                text = tk.Text(detail, height=8, wrap="word", font=("Segoe UI", 10))
                text.grid(row=row, column=1, sticky="nsew", pady=2)
                detail.rowconfigure(row, weight=1)
                self.description_text = text
                self.description_text.configure(state="disabled")
            else:
                entry = ttk.Entry(detail, textvariable=self.detail_vars[key], state="readonly")
                entry.grid(row=row, column=1, sticky="ew", pady=2)

        # --- Deck panel ---
        deck_header = ttk.Frame(right)
        deck_header.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        deck_header.columnconfigure(1, weight=1)
        ttk.Label(deck_header, text="当前牌表", font=("Segoe UI", 14, "bold")).grid(row=0, column=0, sticky="w")
        ttk.Label(deck_header, textvariable=self.deck_count_var).grid(row=0, column=1, sticky="e")

        deck_frame = ttk.Frame(right)
        deck_frame.grid(row=1, column=0, sticky="nsew")
        deck_frame.columnconfigure(0, weight=1)
        deck_frame.rowconfigure(0, weight=1)

        self.deck_tree = ttk.Treeview(
            deck_frame,
            columns=("idx", "name", "cost", "system", "card_id"),
            show="headings",
            selectmode="extended",
        )
        for key, label, width in (
            ("idx", "#", 36),
            ("name", "名称", 90),
            ("cost", "费", 40),
            ("system", "体系", 70),
            ("card_id", "ID", 140),
        ):
            self.deck_tree.heading(key, text=label)
            self.deck_tree.column(key, width=width, anchor="w")
        deck_scroll = ttk.Scrollbar(deck_frame, orient="vertical", command=self.deck_tree.yview)
        self.deck_tree.configure(yscrollcommand=deck_scroll.set)
        self.deck_tree.grid(row=0, column=0, sticky="nsew")
        deck_scroll.grid(row=0, column=1, sticky="ns")
        self.deck_tree.bind("<<TreeviewSelect>>", self._on_deck_select)

        deck_actions = ttk.Frame(right)
        deck_actions.grid(row=2, column=0, sticky="ew", pady=(8, 0))
        ttk.Button(deck_actions, text="移除选中", command=self._remove_selected_from_deck).pack(side="left")
        ttk.Button(deck_actions, text="上移", command=lambda: self._move_deck_item(-1)).pack(side="left", padx=4)
        ttk.Button(deck_actions, text="下移", command=lambda: self._move_deck_item(1)).pack(side="left")
        ttk.Button(deck_actions, text="清空牌表", command=self._clear_deck).pack(side="left", padx=4)

        io = ttk.Frame(right)
        io.grid(row=3, column=0, sticky="ew", pady=(8, 0))
        ttk.Button(io, text="导出 JSON", command=self._export_deck).pack(side="left")
        ttk.Button(io, text="导出 deckCardIds.txt", command=self._export_ids_txt).pack(side="left", padx=6)
        ttk.Button(io, text="导入牌表", command=self._import_deck).pack(side="left")

    def _add_filter(self, parent: ttk.LabelFrame, row: int, col: int, label: str, var: tk.StringVar) -> None:
        ttk.Label(parent, text=label).grid(row=row, column=col, sticky="w", pady=2)
        box = ttk.Combobox(parent, textvariable=var, state="readonly", width=12)
        box.grid(row=row, column=col + 1, sticky="ew", padx=(6, 8), pady=2)
        box.bind("<<ComboboxSelected>>", lambda _e: self._apply_filters())
        setattr(self, f"_filter_box_{label}", box)

    def _reload_catalog(self) -> None:
        self.catalog = load_default_catalog()
        self._reload_filter_choices()
        self._apply_filters()
        self._set_status(f"已重新加载 {len(self.catalog)} 张卡牌")

    def _reload_filter_choices(self) -> None:
        mapping = {
            "体系": (self.filter_system_var, unique_values(self.catalog, "system")),
            "类别": (self.filter_category_var, unique_values(self.catalog, "category")),
            "稀有度": (self.filter_rarity_var, unique_values(self.catalog, "rarity")),
            "分区": (self.filter_section_var, unique_values(self.catalog, "section")),
            "协同": (self.filter_synergy_var, unique_values(self.catalog, "synergy")),
        }
        for label, (var, values) in mapping.items():
            box: ttk.Combobox = getattr(self, f"_filter_box_{label}")
            box["values"] = [FILTER_ALL, *values]
            if var.get() not in box["values"]:
                var.set(FILTER_ALL)

    def _clear_filters(self) -> None:
        self.search_var.set("")
        self.filter_system_var.set(FILTER_ALL)
        self.filter_category_var.set(FILTER_ALL)
        self.filter_rarity_var.set(FILTER_ALL)
        self.filter_section_var.set(FILTER_ALL)
        self.filter_synergy_var.set(FILTER_ALL)
        self._apply_filters()

    def _toggle_sort_dir(self) -> None:
        self._sort_reverse = not self._sort_reverse
        self._apply_filters()

    def _sort_by_column(self, key: str) -> None:
        label = next((lbl for lbl, attr in SORT_OPTIONS if attr == key), None)
        if label:
            if self.sort_var.get() == label:
                self._sort_reverse = not self._sort_reverse
            else:
                self.sort_var.set(label)
                self._sort_reverse = False
            self._apply_filters()

    def _sort_key(self, card: CatalogCard):
        attr = next(attr for label, attr in SORT_OPTIONS if label == self.sort_var.get())
        value = getattr(card, attr, "") or ""
        if attr == "cost":
            try:
                return (0, int(value))
            except ValueError:
                return (1, value)
        if attr == "rarity":
            return (RARITY_ORDER.get(value, 50), value)
        return (0, str(value).lower())

    def _apply_filters(self) -> None:
        query = self.search_var.get().strip().lower()
        result: list[CatalogCard] = []
        for card in self.catalog:
            if self.filter_system_var.get() != FILTER_ALL and card.system != self.filter_system_var.get():
                continue
            if self.filter_category_var.get() != FILTER_ALL and card.category != self.filter_category_var.get():
                continue
            if self.filter_rarity_var.get() != FILTER_ALL and card.rarity != self.filter_rarity_var.get():
                continue
            if self.filter_section_var.get() != FILTER_ALL and card.section != self.filter_section_var.get():
                continue
            if self.filter_synergy_var.get() != FILTER_ALL and card.synergy != self.filter_synergy_var.get():
                continue
            if query and query not in card.search_blob():
                continue
            result.append(card)

        result.sort(key=self._sort_key, reverse=self._sort_reverse)
        self.visible = result
        self._refresh_catalog_tree()
        self._set_status(f"显示 {len(self.visible)} / {len(self.catalog)}")

    def _refresh_catalog_tree(self) -> None:
        self.catalog_tree.delete(*self.catalog_tree.get_children())
        for idx, card in enumerate(self.visible):
            self.catalog_tree.insert(
                "",
                "end",
                iid=str(idx),
                values=(card.name, card.cost, card.system, card.category, card.rarity, card.card_id or "—"),
            )

    def _refresh_deck_tree(self) -> None:
        self.deck_tree.delete(*self.deck_tree.get_children())
        for idx, card in enumerate(self.deck):
            self.deck_tree.insert(
                "",
                "end",
                iid=str(idx),
                values=(idx + 1, card.name, card.cost, card.system, card.card_id or "—"),
            )
        self.deck_count_var.set(f"牌表：{len(self.deck)} 张")

    def _selected_catalog_cards(self) -> list[CatalogCard]:
        cards: list[CatalogCard] = []
        for item in self.catalog_tree.selection():
            try:
                cards.append(self.visible[int(item)])
            except (ValueError, IndexError):
                continue
        return cards

    def _on_catalog_select(self, _event=None) -> None:
        cards = self._selected_catalog_cards()
        if cards:
            self._show_card(cards[-1])

    def _on_deck_select(self, _event=None) -> None:
        selection = self.deck_tree.selection()
        if not selection:
            return
        try:
            card = self.deck[int(selection[-1])]
        except (ValueError, IndexError):
            return
        self._show_card(card)

    def _show_card(self, card: CatalogCard) -> None:
        for _, key in DETAIL_FIELDS:
            if key == "description":
                continue
            self.detail_vars[key].set(getattr(card, key, "") or "—")
        self.description_text.configure(state="normal")
        self.description_text.delete("1.0", "end")
        self.description_text.insert("1.0", card.description or "—")
        self.description_text.configure(state="disabled")

    def _add_selected_to_deck(self, times: int = 1) -> None:
        cards = self._selected_catalog_cards()
        if not cards:
            messagebox.showinfo(APP_TITLE, "请先在左侧选中卡牌。")
            return
        for _ in range(max(1, times)):
            self.deck.extend(cards)
        self._refresh_deck_tree()
        missing = [c.name for c in cards if not c.card_id]
        if missing:
            self._set_status(f"已添加；缺 Unity id：{', '.join(dict.fromkeys(missing))}")
        else:
            self._set_status(f"已添加 {len(cards) * max(1, times)} 张到牌表")

    def _remove_selected_from_deck(self) -> None:
        indices = sorted((int(i) for i in self.deck_tree.selection()), reverse=True)
        for idx in indices:
            if 0 <= idx < len(self.deck):
                self.deck.pop(idx)
        self._refresh_deck_tree()

    def _move_deck_item(self, delta: int) -> None:
        selection = self.deck_tree.selection()
        if len(selection) != 1:
            return
        idx = int(selection[0])
        new_idx = idx + delta
        if new_idx < 0 or new_idx >= len(self.deck):
            return
        self.deck[idx], self.deck[new_idx] = self.deck[new_idx], self.deck[idx]
        self._refresh_deck_tree()
        self.deck_tree.selection_set(str(new_idx))
        self.deck_tree.focus(str(new_idx))

    def _clear_deck(self) -> None:
        if self.deck and not messagebox.askyesno(APP_TITLE, "清空当前牌表？"):
            return
        self.deck.clear()
        self._refresh_deck_tree()

    def _load_starter_deck(self) -> None:
        by_name = {card.name: card for card in self.catalog}
        plan = [("打击", 4), ("防御", 4), ("前进", 1)]
        built: list[CatalogCard] = []
        missing: list[str] = []
        for name, count in plan:
            card = by_name.get(name)
            if card is None:
                missing.append(name)
                continue
            built.extend([card] * count)
        if missing:
            messagebox.showwarning(APP_TITLE, f"找不到初始牌：{', '.join(missing)}")
            return
        self.deck = built
        self._refresh_deck_tree()
        self._set_status("已载入 MVP 初始 9 张")

    def _deck_payload(self) -> dict:
        return {
            "profession": "Warrior",
            "count": len(self.deck),
            "deckCardIds": [card.card_id or f"MISSING:{card.name}" for card in self.deck],
            "cards": [card.to_dict() for card in self.deck],
        }

    def _export_deck(self) -> None:
        if not self.deck:
            messagebox.showinfo(APP_TITLE, "牌表为空。")
            return
        EXPORT_DIR.mkdir(parents=True, exist_ok=True)
        path = filedialog.asksaveasfilename(
            title="导出牌表 JSON",
            initialdir=str(EXPORT_DIR),
            initialfile=DEFAULT_DECK_PATH.name,
            defaultextension=".json",
            filetypes=[("JSON", "*.json")],
        )
        if not path:
            return
        Path(path).write_text(json.dumps(self._deck_payload(), ensure_ascii=False, indent=2), encoding="utf-8")
        self.current_file = Path(path)
        self._set_status(f"已导出 {path}")

    def _export_ids_txt(self) -> None:
        if not self.deck:
            messagebox.showinfo(APP_TITLE, "牌表为空。")
            return
        EXPORT_DIR.mkdir(parents=True, exist_ok=True)
        path = filedialog.asksaveasfilename(
            title="导出 deckCardIds",
            initialdir=str(EXPORT_DIR),
            initialfile="sandbox_deckCardIds.txt",
            defaultextension=".txt",
            filetypes=[("Text", "*.txt")],
        )
        if not path:
            return
        lines = [card.card_id or f"# MISSING {card.name}" for card in self.deck]
        Path(path).write_text("\n".join(lines) + "\n", encoding="utf-8")
        self._set_status(f"已导出 id 列表 {path}")

    def _import_deck(self) -> None:
        path = filedialog.askopenfilename(
            title="导入牌表 JSON",
            initialdir=str(EXPORT_DIR),
            filetypes=[("JSON", "*.json")],
        )
        if not path:
            return
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        by_id = {card.card_id: card for card in self.catalog if card.card_id}
        by_name = {card.name: card for card in self.catalog}
        built: list[CatalogCard] = []

        ids = data.get("deckCardIds") or []
        if ids:
            for item in ids:
                if not isinstance(item, str):
                    continue
                if item in by_id:
                    built.append(by_id[item])
                elif item.startswith("MISSING:"):
                    name = item.split(":", 1)[1]
                    if name in by_name:
                        built.append(by_name[name])
        else:
            for raw in data.get("cards") or []:
                if not isinstance(raw, dict):
                    continue
                card_id = raw.get("card_id")
                name = raw.get("name")
                if card_id and card_id in by_id:
                    built.append(by_id[card_id])
                elif name and name in by_name:
                    built.append(by_name[name])

        self.deck = built
        self.current_file = Path(path)
        self._refresh_deck_tree()
        self._set_status(f"已导入 {len(self.deck)} 张 from {path}")

    def _set_status(self, text: str) -> None:
        self.status_var.set(text)


def main() -> None:
    root = tk.Tk()
    try:
        root.call("tk", "scaling", 1.2)
    except tk.TclError:
        pass
    style = ttk.Style(root)
    if "vista" in style.theme_names():
        style.theme_use("vista")
    DeckTableBuilderApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
