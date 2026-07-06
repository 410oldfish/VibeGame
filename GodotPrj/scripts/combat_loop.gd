extends Node2D

const HEX_SIZE: float = 42.0
const BOARD_ORIGIN: Vector2 = Vector2(360.0, 122.0)
const PLAYER: String = "player"
const ENEMY: String = "enemy"

const DIRECTIONS: Array[Vector2i] = [
	Vector2i(1, 0),
	Vector2i(1, -1),
	Vector2i(0, -1),
	Vector2i(-1, 0),
	Vector2i(-1, 1),
	Vector2i(0, 1),
]

var board_coords: Array[Vector2i] = []
var player: Dictionary = {}
var enemies: Array[Dictionary] = []
var draw_pile: Array[Dictionary] = []
var discard_pile: Array[Dictionary] = []
var hand: Array[Dictionary] = []
var selected_card_index: int = -1
var selected_tile: Vector2i = Vector2i(-999, -999)
var hovered_tile: Vector2i = Vector2i(-999, -999)
var phase: String = "player"
var battle_done: bool = false
var rng: RandomNumberGenerator = RandomNumberGenerator.new()
var log_lines: Array[String] = []

var canvas: CanvasLayer
var root_panel: Control
var turn_label: Label
var selected_label: Label
var player_label: Label
var enemy_label: Label
var pile_label: Label
var log_label: RichTextLabel
var hand_box: HBoxContainer
var end_turn_button: Button
var restart_button: Button


func _ready() -> void:
	rng.randomize()
	_build_ui()
	_new_battle()


func _process(_delta: float) -> void:
	var tile: Vector2i = _screen_to_axial(get_global_mouse_position())
	if tile != hovered_tile:
		hovered_tile = tile if board_coords.has(tile) else Vector2i(-999, -999)
		queue_redraw()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		if battle_done or phase != "player":
			return
		var tile: Vector2i = _screen_to_axial(get_global_mouse_position())
		if board_coords.has(tile):
			_handle_tile_click(tile)


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, get_viewport_rect().size), Color(0.09, 0.105, 0.12), true)
	for coord in board_coords:
		_draw_hex_tile(coord)
	for enemy in enemies:
		if enemy.hp > 0:
			_draw_unit(enemy)
	_draw_unit(player)


func _build_ui() -> void:
	canvas = CanvasLayer.new()
	add_child(canvas)

	root_panel = Control.new()
	root_panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_panel.mouse_filter = Control.MOUSE_FILTER_PASS
	canvas.add_child(root_panel)

	var top: PanelContainer = PanelContainer.new()
	top.position = Vector2(18, 14)
	top.size = Vector2(1244, 86)
	root_panel.add_child(top)

	var top_row: HBoxContainer = HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 18)
	top.add_child(top_row)

	turn_label = Label.new()
	turn_label.custom_minimum_size = Vector2(220, 0)
	turn_label.add_theme_font_size_override("font_size", 22)
	top_row.add_child(turn_label)

	player_label = Label.new()
	player_label.custom_minimum_size = Vector2(310, 0)
	top_row.add_child(player_label)

	enemy_label = Label.new()
	enemy_label.custom_minimum_size = Vector2(420, 0)
	top_row.add_child(enemy_label)

	pile_label = Label.new()
	pile_label.custom_minimum_size = Vector2(150, 0)
	top_row.add_child(pile_label)

	end_turn_button = Button.new()
	end_turn_button.text = "End Turn"
	end_turn_button.pressed.connect(_on_end_turn_pressed)
	top_row.add_child(end_turn_button)

	restart_button = Button.new()
	restart_button.text = "Restart"
	restart_button.pressed.connect(_new_battle)
	top_row.add_child(restart_button)

	selected_label = Label.new()
	selected_label.position = Vector2(20, 105)
	selected_label.size = Vector2(700, 30)
	selected_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root_panel.add_child(selected_label)

	var bottom: PanelContainer = PanelContainer.new()
	bottom.position = Vector2(18, 548)
	bottom.size = Vector2(900, 154)
	root_panel.add_child(bottom)

	hand_box = HBoxContainer.new()
	hand_box.add_theme_constant_override("separation", 10)
	bottom.add_child(hand_box)

	log_label = RichTextLabel.new()
	log_label.position = Vector2(936, 548)
	log_label.size = Vector2(326, 154)
	log_label.bbcode_enabled = false
	log_label.scroll_active = false
	root_panel.add_child(log_label)


func _new_battle() -> void:
	battle_done = false
	phase = "player"
	selected_card_index = -1
	selected_tile = Vector2i(-999, -999)
	log_lines.clear()
	_build_board()
	_build_units()
	_build_deck()
	_start_player_turn()
	_log("Battle started.")
	_refresh_ui()
	queue_redraw()


func _build_board() -> void:
	board_coords.clear()
	for q in range(0, 7):
		for r in range(0, 6):
			var center_q: float = 3.0
			var center_r: float = 2.5
			var nq: float = (float(q) - center_q) / center_q
			var nr: float = (float(r) - center_r) / center_r
			if nq * nq * 0.95 + nr * nr * 0.85 + nq * nr * 0.22 < 1.28:
				board_coords.append(Vector2i(q, r))


func _build_units() -> void:
	player = {
		"id": "hero",
		"name": "Hero",
		"faction": PLAYER,
		"coord": Vector2i(1, 2),
		"max_hp": 34,
		"hp": 34,
		"armor": 0,
		"max_energy": 3,
		"energy": 0,
		"draw_per_turn": 5,
	}
	enemies = [
		{
			"id": "raider",
			"name": "Raider",
			"faction": ENEMY,
			"coord": Vector2i(5, 2),
			"max_hp": 22,
			"hp": 22,
			"armor": 0,
			"intent": {},
		},
		{
			"id": "sentry",
			"name": "Sentry",
			"faction": ENEMY,
			"coord": Vector2i(4, 4),
			"max_hp": 18,
			"hp": 18,
			"armor": 0,
			"intent": {},
		},
	]


func _build_deck() -> void:
	draw_pile.clear()
	discard_pile.clear()
	hand.clear()
	for i in range(3):
		draw_pile.append(_card("strike"))
		draw_pile.append(_card("guard"))
	for i in range(2):
		draw_pile.append(_card("step"))
	draw_pile.append(_card("cleave"))
	draw_pile.append(_card("heavy"))
	_shuffle(draw_pile)


func _card(id: String) -> Dictionary:
	match id:
		"strike":
			return {
				"id": "strike",
				"name": "Strike",
				"type": "attack",
				"cost": 1,
				"amount": 6,
				"range": 1,
				"text": "6 damage",
				"color": Color(0.77, 0.24, 0.2),
			}
		"guard":
			return {
				"id": "guard",
				"name": "Guard",
				"type": "defend",
				"cost": 1,
				"amount": 5,
				"range": 0,
				"text": "5 armor",
				"color": Color(0.18, 0.48, 0.8),
			}
		"step":
			return {
				"id": "step",
				"name": "Step",
				"type": "move",
				"cost": 0,
				"amount": 2,
				"range": 2,
				"text": "move 2",
				"color": Color(0.28, 0.62, 0.34),
			}
		"cleave":
			return {
				"id": "cleave",
				"name": "Cleave",
				"type": "area",
				"cost": 2,
				"amount": 4,
				"range": 1,
				"text": "4 adjacent",
				"color": Color(0.88, 0.48, 0.22),
			}
		_:
			return {
				"id": "heavy",
				"name": "Heavy",
				"type": "attack",
				"cost": 2,
				"amount": 10,
				"range": 1,
				"text": "10 damage",
				"color": Color(0.58, 0.32, 0.78),
			}


func _start_player_turn() -> void:
	phase = "player"
	player.armor = 0
	player.energy = player.max_energy
	_prepare_enemy_intents()
	_draw_cards(player.draw_per_turn)
	_log("Player turn.")
	_refresh_ui()
	queue_redraw()


func _draw_cards(count: int) -> void:
	for i in range(count):
		if draw_pile.is_empty():
			if discard_pile.is_empty():
				return
			draw_pile.append_array(discard_pile)
			discard_pile.clear()
			_shuffle(draw_pile)
		hand.append(draw_pile.pop_back())


func _shuffle(cards: Array[Dictionary]) -> void:
	for i in range(cards.size() - 1, 0, -1):
		var j: int = rng.randi_range(0, i)
		var tmp: Dictionary = cards[i]
		cards[i] = cards[j]
		cards[j] = tmp


func _prepare_enemy_intents() -> void:
	for i in range(enemies.size()):
		if enemies[i].hp <= 0:
			continue
		var dist: int = _hex_distance(enemies[i].coord, player.coord)
		if dist <= 1:
			enemies[i].intent = {"type": "attack", "amount": 6}
		else:
			enemies[i].intent = {"type": "move", "amount": min(2, dist - 1)}


func _handle_tile_click(tile: Vector2i) -> void:
	if selected_card_index < 0 or selected_card_index >= hand.size():
		selected_tile = tile
		queue_redraw()
		return

	var card: Dictionary = hand[selected_card_index]
	if player.energy < card.cost:
		_log("Not enough energy.")
		return

	match card.type:
		"attack":
			var target: int = _enemy_at(tile)
			if target < 0:
				_log("No target.")
				return
			if _hex_distance(player.coord, enemies[target].coord) > card.range:
				_log("Out of range.")
				return
			_play_attack_card(card, target)
		"defend":
			_play_defend_card(card)
		"move":
			if _is_occupied(tile) or _hex_distance(player.coord, tile) > card.range:
				_log("Invalid move.")
				return
			_play_move_card(card, tile)
		"area":
			if _hex_distance(player.coord, tile) > card.range:
				_log("Out of range.")
				return
			_play_area_card(card)
		_:
			return
	_finish_card_play()


func _play_attack_card(card: Dictionary, target_index: int) -> void:
	player.energy -= card.cost
	_apply_damage(enemies[target_index], card.amount)
	_log("%s hits %s for %d." % [card.name, enemies[target_index].name, card.amount])
	_check_battle_end()


func _play_defend_card(card: Dictionary) -> void:
	player.energy -= card.cost
	player.armor += card.amount
	_log("%s gains %d armor." % [player.name, card.amount])


func _play_move_card(card: Dictionary, tile: Vector2i) -> void:
	player.energy -= card.cost
	player.coord = tile
	_log("%s moves." % player.name)


func _play_area_card(card: Dictionary) -> void:
	player.energy -= card.cost
	var hit_count: int = 0
	for i in range(enemies.size()):
		if enemies[i].hp > 0 and _hex_distance(player.coord, enemies[i].coord) <= 1:
			_apply_damage(enemies[i], card.amount)
			hit_count += 1
	_log("%s hits %d target(s)." % [card.name, hit_count])
	_check_battle_end()


func _finish_card_play() -> void:
	if selected_card_index >= 0 and selected_card_index < hand.size():
		discard_pile.append(hand[selected_card_index])
		hand.remove_at(selected_card_index)
	selected_card_index = -1
	_refresh_ui()
	queue_redraw()


func _on_card_pressed(index: int) -> void:
	if battle_done or phase != "player" or index < 0 or index >= hand.size():
		return
	selected_card_index = index
	var card: Dictionary = hand[index]
	if card.type == "defend":
		if player.energy >= card.cost:
			_play_defend_card(card)
			_finish_card_play()
		else:
			_log("Not enough energy.")
	else:
		_refresh_ui()
		queue_redraw()


func _on_end_turn_pressed() -> void:
	if battle_done or phase != "player":
		return
	_end_player_turn()


func _end_player_turn() -> void:
	selected_card_index = -1
	discard_pile.append_array(hand)
	hand.clear()
	phase = "enemy"
	_log("Enemy turn.")
	_refresh_ui()
	queue_redraw()
	await get_tree().create_timer(0.35).timeout
	await _run_enemy_turn()


func _run_enemy_turn() -> void:
	for i in range(enemies.size()):
		if battle_done:
			return
		if enemies[i].hp <= 0:
			continue
		enemies[i].armor = 0
		var intent: Dictionary = enemies[i].intent
		if intent.get("type", "") == "attack" and _hex_distance(enemies[i].coord, player.coord) <= 1:
			_apply_damage(player, intent.amount)
			_log("%s attacks for %d." % [enemies[i].name, intent.amount])
		else:
			_enemy_move_toward(i, int(intent.get("amount", 2)))
			if _hex_distance(enemies[i].coord, player.coord) <= 1:
				_apply_damage(player, 6)
				_log("%s closes and attacks." % enemies[i].name)
			else:
				_log("%s advances." % enemies[i].name)
		_check_battle_end()
		_refresh_ui()
		queue_redraw()
		await get_tree().create_timer(0.28).timeout
	if not battle_done:
		_start_player_turn()


func _enemy_move_toward(enemy_index: int, steps: int) -> void:
	for s in range(steps):
		var current: Vector2i = enemies[enemy_index].coord
		var best: Vector2i = current
		var best_distance: int = _hex_distance(current, player.coord)
		for dir in DIRECTIONS:
			var candidate: Vector2i = current + dir
			if not board_coords.has(candidate) or _is_occupied(candidate):
				continue
			var dist: int = _hex_distance(candidate, player.coord)
			if dist < best_distance:
				best = candidate
				best_distance = dist
		if best == current:
			return
		enemies[enemy_index].coord = best


func _apply_damage(unit: Dictionary, amount: int) -> void:
	var remaining: int = max(0, amount)
	var absorbed: int = min(unit.armor, remaining)
	unit.armor -= absorbed
	remaining -= absorbed
	unit.hp = max(0, unit.hp - remaining)


func _check_battle_end() -> void:
	if player.hp <= 0:
		battle_done = true
		phase = "done"
		_log("Defeat.")
		return
	for enemy in enemies:
		if enemy.hp > 0:
			return
	battle_done = true
	phase = "done"
	_log("Victory.")


func _refresh_ui() -> void:
	turn_label.text = "Turn: %s" % phase.capitalize()
	player_label.text = "Hero  HP %d/%d  Armor %d  Energy %d/%d" % [
		player.hp,
		player.max_hp,
		player.armor,
		player.energy,
		player.max_energy,
	]
	var enemy_parts: Array[String] = []
	for enemy in enemies:
		if enemy.hp <= 0:
			enemy_parts.append("%s defeated" % enemy.name)
		else:
			var intent: Dictionary = enemy.intent
			var intent_text: String = "?"
			if intent.get("type", "") == "attack":
				intent_text = "Attack %d" % intent.amount
			elif intent.get("type", "") == "move":
				intent_text = "Move %d" % intent.amount
			enemy_parts.append("%s HP %d/%d [%s]" % [enemy.name, enemy.hp, enemy.max_hp, intent_text])
	enemy_label.text = "\n".join(enemy_parts)
	pile_label.text = "Draw %d\nHand %d\nDiscard %d" % [draw_pile.size(), hand.size(), discard_pile.size()]
	selected_label.text = _selected_text()
	end_turn_button.disabled = battle_done or phase != "player"
	_refresh_hand_ui()
	_refresh_log_ui()


func _selected_text() -> String:
	if battle_done:
		return "Battle complete."
	if phase != "player":
		return "Enemy phase."
	if selected_card_index >= 0 and selected_card_index < hand.size():
		var card: Dictionary = hand[selected_card_index]
		return "Selected: %s  %s" % [card.name, card.text]
	return "Selected: none"


func _refresh_hand_ui() -> void:
	for child in hand_box.get_children():
		child.queue_free()
	for i in range(hand.size()):
		var card: Dictionary = hand[i]
		var button: Button = Button.new()
		button.custom_minimum_size = Vector2(110, 126)
		button.text = "%s\n%dE\n%s" % [card.name, card.cost, card.text]
		button.disabled = battle_done or phase != "player" or player.energy < card.cost
		button.modulate = Color(1, 1, 1) if i != selected_card_index else Color(1.25, 1.16, 0.78)
		button.add_theme_color_override("font_color", Color(0.96, 0.96, 0.92))
		var style: StyleBoxFlat = StyleBoxFlat.new()
		style.bg_color = card.color.darkened(0.26)
		style.border_color = Color(0.04, 0.045, 0.05)
		style.set_border_width_all(2)
		style.set_corner_radius_all(6)
		button.add_theme_stylebox_override("normal", style)
		button.add_theme_stylebox_override("pressed", style)
		button.pressed.connect(_on_card_pressed.bind(i))
		hand_box.add_child(button)


func _refresh_log_ui() -> void:
	log_label.text = "\n".join(log_lines)


func _log(line: String) -> void:
	log_lines.append(line)
	while log_lines.size() > 8:
		log_lines.pop_front()


func _draw_hex_tile(coord: Vector2i) -> void:
	var center: Vector2 = _axial_to_screen(coord)
	var points: PackedVector2Array = _hex_points(center)
	var base_color: Color = Color(0.23, 0.43, 0.34)
	if (coord.x + coord.y) % 3 == 0:
		base_color = Color(0.27, 0.49, 0.39)
	if coord == hovered_tile:
		base_color = base_color.lerp(Color(0.94, 0.82, 0.38), 0.42)
	if coord == selected_tile:
		base_color = base_color.lerp(Color(0.3, 0.68, 0.92), 0.45)
	if selected_card_index >= 0 and selected_card_index < hand.size():
		var card: Dictionary = hand[selected_card_index]
		if _is_target_hint(coord, card):
			base_color = base_color.lerp(Color(0.92, 0.5, 0.24), 0.45)
	draw_colored_polygon(points, base_color)
	var outline: PackedVector2Array = PackedVector2Array(points)
	outline.append(points[0])
	draw_polyline(outline, Color(0.08, 0.14, 0.13), 2.0)


func _draw_unit(unit: Dictionary) -> void:
	var center: Vector2 = _axial_to_screen(unit.coord)
	var color: Color = Color(0.22, 0.56, 0.95) if unit.faction == PLAYER else Color(0.86, 0.25, 0.22)
	draw_circle(center, 22.0, color)
	draw_circle(center, 22.0, Color(0.04, 0.05, 0.06), false, 3.0)
	var font: Font = ThemeDB.fallback_font
	var hp_text: String = "%d" % unit.hp
	draw_string(font, center + Vector2(-12, 6), hp_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)


func _is_target_hint(coord: Vector2i, card: Dictionary) -> bool:
	match card.type:
		"attack":
			return _enemy_at(coord) >= 0 and _hex_distance(player.coord, coord) <= card.range
		"move":
			return not _is_occupied(coord) and _hex_distance(player.coord, coord) <= card.range
		"area":
			return _hex_distance(player.coord, coord) <= card.range
	return false


func _hex_points(center: Vector2) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	for i in range(6):
		var angle: float = deg_to_rad(60.0 * float(i) + 30.0)
		points.append(center + Vector2(cos(angle), sin(angle)) * HEX_SIZE)
	return points


func _axial_to_screen(coord: Vector2i) -> Vector2:
	var x: float = HEX_SIZE * sqrt(3.0) * (float(coord.x) + float(coord.y) * 0.5)
	var y: float = HEX_SIZE * 1.5 * float(coord.y)
	return BOARD_ORIGIN + Vector2(x, y)


func _screen_to_axial(point: Vector2) -> Vector2i:
	var local: Vector2 = point - BOARD_ORIGIN
	var q: float = (sqrt(3.0) / 3.0 * local.x - 1.0 / 3.0 * local.y) / HEX_SIZE
	var r: float = (2.0 / 3.0 * local.y) / HEX_SIZE
	return _cube_round(q, r)


func _cube_round(q: float, r: float) -> Vector2i:
	var x: float = q
	var z: float = r
	var y: float = -x - z
	var rx: int = roundi(x)
	var ry: int = roundi(y)
	var rz: int = roundi(z)
	var x_diff: float = abs(float(rx) - x)
	var y_diff: float = abs(float(ry) - y)
	var z_diff: float = abs(float(rz) - z)
	if x_diff > y_diff and x_diff > z_diff:
		rx = -ry - rz
	elif y_diff > z_diff:
		ry = -rx - rz
	else:
		rz = -rx - ry
	return Vector2i(rx, rz)


func _hex_distance(a: Vector2i, b: Vector2i) -> int:
	var dq: int = a.x - b.x
	var dr: int = a.y - b.y
	var ds: int = (a.x + a.y) - (b.x + b.y)
	return int((abs(dq) + abs(dr) + abs(ds)) / 2)


func _enemy_at(coord: Vector2i) -> int:
	for i in range(enemies.size()):
		if enemies[i].hp > 0 and enemies[i].coord == coord:
			return i
	return -1


func _is_occupied(coord: Vector2i) -> bool:
	if player.hp > 0 and player.coord == coord:
		return true
	return _enemy_at(coord) >= 0
