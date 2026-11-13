extends SceneTree
func _initialize():
	var args=OS.get_cmdline_user_args();var rows=JSON.parse_string(FileAccess.get_file_as_string(args[-2]));var pack:=PCKPacker.new()
	if not rows is Array or rows.size()!=105:quit(1);return
	if pack.pck_start(args[-1])!=OK:quit(1);return
	for row in rows:
		if pack.add_file("res://"+str(row.path),str(row.source))!=OK:push_error(str(row.path));quit(1);return
	if pack.flush()!=OK:quit(1);return
	print("PREPARED_MAP_PACK_R10 maps=",rows.size());quit()
