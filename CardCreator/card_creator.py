import json
from dataclasses import dataclass, asdict
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk


APP_TITLE = "Slay The HS Card Creator"
DEFAULT_EXPORT_DIR = Path(__file__).resolve().parent / "exports"
DEFAULT_EXPORT_PATH = DEFAULT_EXPORT_DIR / "custom_cards.json"
FILTER_ALL = "All"

PROFESSIONS = [
    "Warrior",
    "Paladin",
    "Druid",
    "Fighter",
    "Rogue",
    "General",
    "Slime",
    "Custom",
]

RARITIES = [
    "Starter",
    "Common",
    "Uncommon",
    "Rare",
    "Special",
]

CARD_TYPES = [
    "Attack",
    "Skill",
    "Power",
    "Status",
    "Curse",
]

TARGET_TYPES = [
    "Self",
    "Unit",
    "Tile",
    "Direction",
    "None",
]

CARD_MARK_COLORS = [
    ("None", ""),
    ("Blush", "#FDE2E4"),
    ("Peach", "#FEE7D6"),
    ("Butter", "#FFF4CC"),
    ("Mint", "#DFF7E2"),
    ("Sage", "#E6F3D8"),
    ("Sky", "#DCEEFF"),
    ("Ice", "#E3F6FF"),
    ("Lavender", "#EEE6FF"),
    ("Rose", "#FBE1F0"),
    ("Sand", "#F5EAD8"),
]
CARD_MARK_COLOR_NAMES = [name for name, _ in CARD_MARK_COLORS]
CARD_MARK_COLOR_MAP = {name: value for name, value in CARD_MARK_COLORS}
CARD_MARK_COLOR_VALUE_TO_NAME = {value: name for name, value in CARD_MARK_COLORS if value}

PROFESSION_ID_PARTS = {
    "Warrior": ("C", "01"),
    "Paladin": ("C", "02"),
    "Druid": ("C", "03"),
    "Fighter": ("C", "04"),
    "Rogue": ("C", "05"),
    "General": ("C", "06"),
    "Slime": ("M", "01"),
    "Custom": ("X", "01"),
}


@dataclass
class CardDraft:
    card_id: str
    name: str
    cost: str
    profession: str
    rarity: str
    card_type: str
    target_type: str
    cast_range: str
    effect_radius: str
    mark_color: str
    description: str


class CardCreatorApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title(APP_TITLE)
        self.root.geometry("1180x760")
        self.root.minsize(980, 660)

        self.cards: list[CardDraft] = []
        self.current_file: Path | None = None

        self.card_id_var = tk.StringVar()
        self.name_var = tk.StringVar()
        self.cost_var = tk.StringVar(value="1")
        self.profession_var = tk.StringVar(value=PROFESSIONS[0])
        self.default_profession_var = tk.StringVar(value=PROFESSIONS[0])
        self.rarity_var = tk.StringVar(value=RARITIES[0])
        self.card_type_var = tk.StringVar(value=CARD_TYPES[0])
        self.target_type_var = tk.StringVar(value="Unit")
        self.cast_range_var = tk.StringVar(value="1")
        self.effect_radius_var = tk.StringVar(value="0")
        self.mark_color_var = tk.StringVar(value=CARD_MARK_COLOR_NAMES[0])
        self.filter_type_var = tk.StringVar(value=FILTER_ALL)
        self.filter_rarity_var = tk.StringVar(value=FILTER_ALL)
        self.status_var = tk.StringVar(value="Ready")
        self.filtered_indices: list[int] = []

        self._build_ui()
        self._refresh_list()
        self._update_card_id_preview()

    def _build_ui(self) -> None:
        self.root.columnconfigure(0, weight=3)
        self.root.columnconfigure(1, weight=2)
        self.root.rowconfigure(0, weight=1)

        left = ttk.Frame(self.root, padding=12)
        left.grid(row=0, column=0, sticky="nsew")
        left.columnconfigure(0, weight=1)
        left.rowconfigure(2, weight=1)

        right = ttk.Frame(self.root, padding=(0, 12, 12, 12))
        right.grid(row=0, column=1, sticky="nsew")
        right.columnconfigure(0, weight=1)
        right.rowconfigure(1, weight=1)

        header = ttk.Frame(left)
        header.grid(row=0, column=0, sticky="ew", pady=(0, 10))
        header.columnconfigure(1, weight=1)

        ttk.Label(header, text="Card Drafts", font=("Segoe UI", 15, "bold")).grid(
            row=0, column=0, sticky="w"
        )
        ttk.Label(header, textvariable=self.status_var, foreground="#4b5563").grid(
            row=0, column=1, sticky="e"
        )

        filters = ttk.Frame(left)
        filters.grid(row=1, column=0, sticky="ew", pady=(0, 10))
        filters.columnconfigure(1, weight=1)
        filters.columnconfigure(3, weight=1)

        ttk.Label(filters, text="Filter Type").grid(row=0, column=0, sticky="w", padx=(0, 8))
        type_filter = ttk.Combobox(
            filters,
            textvariable=self.filter_type_var,
            values=[FILTER_ALL, *CARD_TYPES],
            state="readonly",
        )
        type_filter.grid(row=0, column=1, sticky="ew", padx=(0, 12))
        type_filter.bind("<<ComboboxSelected>>", self._on_filter_changed)

        ttk.Label(filters, text="Filter Rarity").grid(row=0, column=2, sticky="w", padx=(0, 8))
        rarity_filter = ttk.Combobox(
            filters,
            textvariable=self.filter_rarity_var,
            values=[FILTER_ALL, *RARITIES],
            state="readonly",
        )
        rarity_filter.grid(row=0, column=3, sticky="ew")
        rarity_filter.bind("<<ComboboxSelected>>", self._on_filter_changed)

        list_frame = ttk.Frame(left)
        list_frame.grid(row=2, column=0, sticky="nsew")
        list_frame.columnconfigure(0, weight=1)
        list_frame.rowconfigure(0, weight=1)

        self.card_list = tk.Listbox(
            list_frame,
            activestyle="none",
            font=("Segoe UI", 11),
            exportselection=False,
        )
        self.card_list.grid(row=0, column=0, sticky="nsew")
        self.card_list.bind("<<ListboxSelect>>", self._on_select)

        list_scroll = ttk.Scrollbar(list_frame, orient="vertical", command=self.card_list.yview)
        list_scroll.grid(row=0, column=1, sticky="ns")
        self.card_list.config(yscrollcommand=list_scroll.set)

        list_actions = ttk.Frame(left)
        list_actions.grid(row=3, column=0, sticky="ew", pady=(10, 0))
        for i in range(8):
            list_actions.columnconfigure(i, weight=1)

        ttk.Button(list_actions, text="New", command=self._clear_form).grid(row=0, column=0, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Add", command=self._add_card).grid(row=0, column=1, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Update", command=self._update_card).grid(row=0, column=2, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Delete", command=self._delete_card).grid(row=0, column=3, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Up", command=lambda: self._move_selected_card(-1)).grid(row=0, column=4, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Down", command=lambda: self._move_selected_card(1)).grid(row=0, column=5, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Load", command=self._load_cards).grid(row=0, column=6, padx=4, sticky="ew")
        ttk.Button(list_actions, text="Save", command=self._save_cards).grid(row=0, column=7, padx=4, sticky="ew")

        editor = ttk.LabelFrame(right, text="Card Editor", padding=12)
        editor.grid(row=0, column=0, sticky="nsew")
        editor.columnconfigure(1, weight=1)
        editor.rowconfigure(11, weight=1)

        ttk.Label(editor, text="Card ID").grid(row=0, column=0, sticky="w", pady=4)
        ttk.Entry(editor, textvariable=self.card_id_var, state="readonly").grid(row=0, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="Name").grid(row=1, column=0, sticky="w", pady=4)
        ttk.Entry(editor, textvariable=self.name_var).grid(row=1, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="Cost").grid(row=2, column=0, sticky="w", pady=4)
        ttk.Entry(editor, textvariable=self.cost_var).grid(row=2, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="Profession").grid(row=3, column=0, sticky="w", pady=4)
        profession_box = ttk.Combobox(
            editor,
            textvariable=self.profession_var,
            values=PROFESSIONS,
            state="readonly",
        )
        profession_box.grid(row=3, column=1, sticky="ew", pady=4)
        profession_box.bind("<<ComboboxSelected>>", self._on_profession_changed)

        ttk.Label(editor, text="Draft Profession").grid(row=4, column=0, sticky="w", pady=4)
        default_profession = ttk.Combobox(
            editor,
            textvariable=self.default_profession_var,
            values=PROFESSIONS,
            state="readonly",
        )
        default_profession.grid(row=4, column=1, sticky="ew", pady=4)
        default_profession.bind("<<ComboboxSelected>>", self._on_default_profession_changed)

        ttk.Label(editor, text="Rarity").grid(row=5, column=0, sticky="w", pady=4)
        ttk.Combobox(
            editor,
            textvariable=self.rarity_var,
            values=RARITIES,
            state="readonly",
        ).grid(row=5, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="Card Type").grid(row=6, column=0, sticky="w", pady=4)
        card_type_box = ttk.Combobox(
            editor,
            textvariable=self.card_type_var,
            values=CARD_TYPES,
            state="readonly",
        )
        card_type_box.grid(row=6, column=1, sticky="ew", pady=4)
        card_type_box.bind("<<ComboboxSelected>>", self._on_card_type_changed)

        ttk.Label(editor, text="Target Type").grid(row=7, column=0, sticky="w", pady=4)
        target_type_box = ttk.Combobox(
            editor,
            textvariable=self.target_type_var,
            values=TARGET_TYPES,
            state="readonly",
        )
        target_type_box.grid(row=7, column=1, sticky="ew", pady=4)
        target_type_box.bind("<<ComboboxSelected>>", self._on_target_type_changed)

        self.cast_range_label = ttk.Label(editor, text="Cast Range")
        self.cast_range_label.grid(row=8, column=0, sticky="w", pady=4)
        self.cast_range_entry = ttk.Entry(editor, textvariable=self.cast_range_var)
        self.cast_range_entry.grid(row=8, column=1, sticky="ew", pady=4)

        self.effect_radius_label = ttk.Label(editor, text="Effect Radius")
        self.effect_radius_label.grid(row=9, column=0, sticky="w", pady=4)
        self.effect_radius_entry = ttk.Entry(editor, textvariable=self.effect_radius_var)
        self.effect_radius_entry.grid(row=9, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="List Color").grid(row=10, column=0, sticky="w", pady=4)
        ttk.Combobox(
            editor,
            textvariable=self.mark_color_var,
            values=CARD_MARK_COLOR_NAMES,
            state="readonly",
        ).grid(row=10, column=1, sticky="ew", pady=4)

        ttk.Label(editor, text="Description").grid(row=11, column=0, sticky="nw", pady=4)
        self.description_text = tk.Text(editor, wrap="word", height=12, font=("Segoe UI", 11))
        self.description_text.grid(row=11, column=1, sticky="nsew", pady=4)

        info = ttk.LabelFrame(right, text="Export Format", padding=12)
        info.grid(row=1, column=0, sticky="nsew", pady=(12, 0))
        info.columnconfigure(0, weight=1)

        info_text = (
            "The tool saves a JSON file with this structure:\n\n"
            "{\n"
            '  "cards": [\n'
            "    {\n"
            '      "card_id": "C_01_001",\n'
            '      "name": "Whirlwind",\n'
            '      "cost": "1",\n'
            '      "profession": "Warrior",\n'
            '      "rarity": "Starter",\n'
            '      "card_type": "Attack",\n'
            '      "target_type": "Self",\n'
            '      "is_directional": false,\n'
            '      "cast_range": 0,\n'
            '      "effect_radius": 1,\n'
            '      "attack_range": 0,\n'
            '      "description": "Deal 6 damage to adjacent enemies."\n'
            "    }\n"
            "  ],\n"
            '  "draft_profession": "Warrior"\n'
            "}\n\n"
            "Cast Range = selection distance.\n"
            "Effect Radius = area centered on self or the chosen target.\n"
            "For Direction cards, Cast Range = direction length and Effect Radius = effect width.\n"
            "List Color adds a light background marker in the left card list."
        )
        ttk.Label(info, text=info_text, justify="left", foreground="#374151").grid(row=0, column=0, sticky="w")
        self._refresh_range_defaults()

    def _selected_index(self) -> int | None:
        selection = self.card_list.curselection()
        if not selection:
            return None
        visible_index = int(selection[0])
        if visible_index < 0 or visible_index >= len(self.filtered_indices):
            return None
        return self.filtered_indices[visible_index]

    def _card_from_form(self) -> CardDraft | None:
        name = self.name_var.get().strip()
        description = self.description_text.get("1.0", "end").strip()

        if not name:
            messagebox.showwarning(APP_TITLE, "Please enter a card name.")
            return None

        if not description:
            messagebox.showwarning(APP_TITLE, "Please enter a card description.")
            return None

        raw_cost = self.cost_var.get().strip().upper()
        if raw_cost != "X":
            try:
                int(raw_cost)
            except ValueError:
                messagebox.showwarning(APP_TITLE, "Cost must be an integer or X.")
                return None

        try:
            cast_range = int(self.cast_range_var.get().strip() or "0")
            effect_radius = int(self.effect_radius_var.get().strip() or "0")
        except ValueError:
            messagebox.showwarning(APP_TITLE, "Cast Range and Effect Radius must be integers.")
            return None

        if cast_range < 0 or effect_radius < 0:
            messagebox.showwarning(APP_TITLE, "Cast Range and Effect Radius must be 0 or higher.")
            return None

        return CardDraft(
            card_id=self.card_id_var.get().strip(),
            name=name,
            cost=raw_cost,
            profession=self.profession_var.get().strip(),
            rarity=self.rarity_var.get().strip(),
            card_type=self.card_type_var.get().strip(),
            target_type=self.target_type_var.get().strip() or "Self",
            cast_range=str(cast_range),
            effect_radius=str(effect_radius),
            mark_color=self._selected_mark_color_value(),
            description=description,
        )

    def _fill_form(self, card: CardDraft) -> None:
        self.card_id_var.set(card.card_id)
        self.name_var.set(card.name)
        self.cost_var.set(card.cost)
        self.profession_var.set(card.profession)
        self.rarity_var.set(card.rarity)
        self.card_type_var.set(card.card_type)
        self.target_type_var.set(card.target_type)
        self.cast_range_var.set(card.cast_range)
        self.effect_radius_var.set(card.effect_radius)
        self.mark_color_var.set(self._mark_color_name_for_value(card.mark_color))
        self.description_text.delete("1.0", "end")
        self.description_text.insert("1.0", card.description)
        self._refresh_range_defaults()

    def _clear_form(self) -> None:
        self.card_list.selection_clear(0, "end")
        self.card_id_var.set("")
        self.name_var.set("")
        self.cost_var.set("1")
        self.profession_var.set(self.default_profession_var.get().strip() or PROFESSIONS[0])
        self.rarity_var.set(RARITIES[0])
        self.card_type_var.set(CARD_TYPES[0])
        self.target_type_var.set("Unit")
        self.cast_range_var.set("1")
        self.effect_radius_var.set("0")
        self.mark_color_var.set(CARD_MARK_COLOR_NAMES[0])
        self.description_text.delete("1.0", "end")
        self.status_var.set("Ready")
        self._refresh_range_defaults()
        self._update_card_id_preview()

    def _on_default_profession_changed(self, _event: object) -> None:
        default_profession = self.default_profession_var.get().strip() or PROFESSIONS[0]
        if not self.name_var.get().strip():
            self.profession_var.set(default_profession)
        self._update_card_id_preview()
        self.status_var.set(f"Draft profession set to {default_profession}")

    def _on_profession_changed(self, _event: object) -> None:
        self._update_card_id_preview()

    def _on_card_type_changed(self, _event: object) -> None:
        if self.card_type_var.get().strip() == "Attack" and self.target_type_var.get().strip() == "None":
            self.target_type_var.set("Unit")
        self._refresh_range_defaults()

    def _on_target_type_changed(self, _event: object) -> None:
        self._refresh_range_defaults()

    def _refresh_range_defaults(self) -> None:
        target_type = self.target_type_var.get().strip() or "Self"
        self._update_range_field_labels(target_type)
        if target_type == "Self" and not self.cast_range_var.get().strip():
            self.cast_range_var.set("0")
        elif target_type in {"Unit", "Tile", "Direction"} and not self.cast_range_var.get().strip():
            self.cast_range_var.set("1")
        elif target_type == "None" and not self.cast_range_var.get().strip():
            self.cast_range_var.set("0")

        if not self.effect_radius_var.get().strip():
            self.effect_radius_var.set("0")

    def _update_range_field_labels(self, target_type: str) -> None:
        if target_type == "Direction":
            self.cast_range_label.config(text="Cast Range (Length)")
            self.effect_radius_label.config(text="Effect Radius (Width)")
            return

        self.cast_range_label.config(text="Cast Range")
        self.effect_radius_label.config(text="Effect Radius")

    @staticmethod
    def _is_directional_target_type(target_type: str) -> bool:
        return target_type == "Direction"

    def _refresh_list(self) -> None:
        self._reassign_card_ids()
        self.card_list.delete(0, "end")
        self.filtered_indices = []
        for index, card in enumerate(self.cards):
            if not self._matches_filters(card):
                continue
            label = (
                f"{card.card_id} | {card.name} | {card.card_type} | {card.profession} | {card.rarity} | "
                f"Target {card.target_type} | Cost {card.cost} | Cast {card.cast_range} | Radius {card.effect_radius}"
            )
            self.card_list.insert("end", label)
            visible_index = len(self.filtered_indices)
            self.card_list.itemconfig(visible_index, bg=card.mark_color or "white")
            self.filtered_indices.append(index)

    def _matches_filters(self, card: CardDraft) -> bool:
        type_filter = self.filter_type_var.get().strip()
        rarity_filter = self.filter_rarity_var.get().strip()

        if type_filter and type_filter != FILTER_ALL and card.card_type != type_filter:
            return False
        if rarity_filter and rarity_filter != FILTER_ALL and card.rarity != rarity_filter:
            return False

        return True

    def _on_filter_changed(self, _event: object) -> None:
        self._refresh_list()
        self.card_list.selection_clear(0, "end")
        self.status_var.set(f"Showing {len(self.filtered_indices)} of {len(self.cards)} cards")

    def _on_select(self, _event: object) -> None:
        index = self._selected_index()
        if index is None:
            return
        self._fill_form(self.cards[index])
        self.status_var.set(f"Selected {self.cards[index].card_id}")

    def _add_card(self) -> None:
        card = self._card_from_form()
        if card is None:
            return
        self.cards.append(card)
        self._refresh_list()
        self._clear_form()
        self.status_var.set(f"Added {card.name}")

    def _update_card(self) -> None:
        index = self._selected_index()
        if index is None:
            messagebox.showwarning(APP_TITLE, "Select a card to update.")
            return

        card = self._card_from_form()
        if card is None:
            return

        self.cards[index] = card
        self._refresh_list()
        if index in self.filtered_indices:
            visible_index = self.filtered_indices.index(index)
            self.card_list.selection_set(visible_index)
        self._update_card_id_preview()
        self.status_var.set(f"Updated {card.name}")

    def _delete_card(self) -> None:
        index = self._selected_index()
        if index is None:
            messagebox.showwarning(APP_TITLE, "Select a card to delete.")
            return

        removed = self.cards.pop(index)
        self._refresh_list()
        self._clear_form()
        self.status_var.set(f"Deleted {removed.name}")

    def _move_selected_card(self, offset: int) -> None:
        index = self._selected_index()
        if index is None:
            messagebox.showwarning(APP_TITLE, "Select a card to reorder.")
            return

        if offset == 0:
            return

        try:
            visible_index = self.filtered_indices.index(index)
        except ValueError:
            return

        target_visible_index = visible_index + offset
        if target_visible_index < 0 or target_visible_index >= len(self.filtered_indices):
            return

        card = self.cards.pop(index)
        target_index = self.filtered_indices[target_visible_index]
        self.cards.insert(target_index, card)
        new_index = self.cards.index(card)

        self._refresh_list()
        self._restore_selection(new_index)
        self.status_var.set(f"Moved {card.name}")

    def _save_cards(self) -> None:
        DEFAULT_EXPORT_DIR.mkdir(parents=True, exist_ok=True)
        initial = str(self.current_file or DEFAULT_EXPORT_PATH)
        file_path = filedialog.asksaveasfilename(
            title="Save Card Drafts",
            initialfile=Path(initial).name,
            initialdir=str(Path(initial).parent),
            defaultextension=".json",
            filetypes=[("JSON files", "*.json")],
        )
        if not file_path:
            return

        self._reassign_card_ids()
        payload = {
            "draft_profession": self._normalize_profession(self.default_profession_var.get()),
            "cards": [
                {
                    **asdict(card),
                    "is_directional": self._is_directional_target_type(card.target_type),
                    "cast_range": int(card.cast_range) if str(card.cast_range).strip() else 0,
                    "effect_radius": int(card.effect_radius) if str(card.effect_radius).strip() else 0,
                    "attack_range": int(card.cast_range) if str(card.cast_range).strip() else 0,
                }
                for card in self.cards
            ]
        }
        with open(file_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)

        self.current_file = Path(file_path)
        self.status_var.set(f"Saved {len(self.cards)} cards")
        messagebox.showinfo(APP_TITLE, f"Saved to:\n{self.current_file}")

    def _load_cards(self) -> None:
        DEFAULT_EXPORT_DIR.mkdir(parents=True, exist_ok=True)
        file_path = filedialog.askopenfilename(
            title="Load Card Drafts",
            initialdir=str((self.current_file or DEFAULT_EXPORT_PATH).parent),
            filetypes=[("JSON files", "*.json")],
        )
        if not file_path:
            return

        with open(file_path, "r", encoding="utf-8-sig") as handle:
            payload = json.load(handle)

        draft_profession = self._normalize_profession(payload.get("draft_profession", PROFESSIONS[0]))
        loaded_cards = []
        for raw in payload.get("cards", []):
            target_type = self._normalize_target_type(raw.get("target_type"), raw.get("card_type"))
            cast_range = raw.get("cast_range", raw.get("attack_range", 0))
            effect_radius = raw.get("effect_radius", 0)
            loaded_cards.append(
                CardDraft(
                    card_id=str(raw.get("card_id", "")).strip(),
                    name=str(raw.get("name", "")).strip(),
                    cost=str(raw.get("cost", "0")).strip().upper() or "0",
                    profession=str(raw.get("profession", "Custom")).strip() or "Custom",
                    rarity=str(raw.get("rarity", "Common")).strip() or "Common",
                    card_type=str(raw.get("card_type", "Attack")).strip() or "Attack",
                    target_type=target_type,
                    cast_range=str(cast_range).strip() or "0",
                    effect_radius=str(effect_radius).strip() or "0",
                    mark_color=self._normalize_mark_color(raw.get("mark_color")),
                    description=str(raw.get("description", "")).strip(),
                )
            )

        self.cards = loaded_cards
        self.current_file = Path(file_path)
        self.default_profession_var.set(draft_profession)
        self._refresh_list()
        self._clear_form()
        self.status_var.set(f"Loaded {len(self.cards)} cards")

    @staticmethod
    def _normalize_target_type(raw_target_type: object, raw_card_type: object) -> str:
        target_type = str(raw_target_type or "").strip().title()
        if target_type in TARGET_TYPES:
            return target_type

        card_type = str(raw_card_type or "").strip().title()
        if card_type == "Attack":
            return "Unit"
        return "Self"

    def _reassign_card_ids(self) -> None:
        counters: dict[tuple[str, str], int] = {}
        for card in self.cards:
            prefix, profession_code = self._profession_code_parts(card.profession)
            key = (prefix, profession_code)
            counters[key] = counters.get(key, 0) + 1
            card.card_id = f"{prefix}_{profession_code}_{counters[key]:03d}"

    def _update_card_id_preview(self) -> None:
        selected_index = self._selected_index()
        profession = self.profession_var.get().strip() or self.default_profession_var.get().strip() or PROFESSIONS[0]
        prefix, profession_code = self._profession_code_parts(profession)
        if selected_index is not None and 0 <= selected_index < len(self.cards):
            current_id = self.cards[selected_index].card_id
            self.card_id_var.set(current_id)
            return

        next_number = 1
        for card in self.cards:
            if self._profession_code_parts(card.profession) == (prefix, profession_code):
                next_number += 1
        self.card_id_var.set(f"{prefix}_{profession_code}_{next_number:03d}")

    @staticmethod
    def _profession_code_parts(profession: str) -> tuple[str, str]:
        return PROFESSION_ID_PARTS.get(profession, ("X", "99"))

    @staticmethod
    def _normalize_profession(raw_profession: object) -> str:
        profession = str(raw_profession or "").strip()
        if profession in PROFESSIONS:
            return profession
        return PROFESSIONS[0]

    def _restore_selection(self, index: int) -> None:
        if index < 0 or index >= len(self.cards):
            return

        if index not in self.filtered_indices:
            self.card_list.selection_clear(0, "end")
            self._fill_form(self.cards[index])
            self._update_card_id_preview()
            return

        visible_index = self.filtered_indices.index(index)
        self.card_list.selection_clear(0, "end")
        self.card_list.selection_set(visible_index)
        self.card_list.see(visible_index)
        self._fill_form(self.cards[index])
        self.status_var.set(f"Selected {self.cards[index].card_id}")

    def _selected_mark_color_value(self) -> str:
        return CARD_MARK_COLOR_MAP.get(self.mark_color_var.get().strip(), "")

    @staticmethod
    def _mark_color_name_for_value(color_value: str) -> str:
        normalized = CardCreatorApp._normalize_mark_color(color_value)
        return CARD_MARK_COLOR_VALUE_TO_NAME.get(normalized, CARD_MARK_COLOR_NAMES[0])

    @staticmethod
    def _normalize_mark_color(raw_color: object) -> str:
        color_value = str(raw_color or "").strip()
        if not color_value:
            return ""

        if color_value in CARD_MARK_COLOR_VALUE_TO_NAME:
            return color_value

        if color_value in CARD_MARK_COLOR_MAP:
            return CARD_MARK_COLOR_MAP[color_value]

        return ""


def main() -> None:
    root = tk.Tk()
    style = ttk.Style(root)
    try:
        style.theme_use("clam")
    except tk.TclError:
        pass
    CardCreatorApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
