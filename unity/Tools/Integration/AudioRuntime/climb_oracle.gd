extends SceneTree
# ROOT ONLY: stage in a Godot project with the exact pinned audio_bank.gd and
# its reference_audio_099.gd dependency. No call to sample()/authored override.
# Run twice in fresh output dirs; compare hashes. No production approval.
const SOURCE_SHA256 = "80acd1129c4a59ff4781fe9f69220e1b07f64d4e4c811fef1feff1fab4f05b34"
func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 2:
        push_error("Expected absolute pinned audio_bank.gd path and output directory")
        quit(2)
        return
    if FileAccess.get_sha256(args[0]) != SOURCE_SHA256:
        push_error("Pinned source hash mismatch")
        quit(3)
        return
    var bank = load(args[0])
    if bank == null:
        quit(4)
        return
    DirAccess.make_dir_recursive_absolute(args[1])
    for variant in range(2):
        var rng := RandomNumberGenerator.new()
        rng.seed = 917324 + "climb".hash() + variant * 1973
        var noise := PackedByteArray()
        noise.resize(9600 * 8)
        for i in range(9600):
            noise.encode_double(i * 8, rng.randf_range(-1.0, 1.0))
        var prefix: String = args[1].path_join("climb_%02d" % variant)
        var nf := FileAccess.open(prefix + ".noise-f64le", FileAccess.WRITE)
        nf.store_buffer(noise)
        nf.close()
        var wav: AudioStreamWAV = bank.synthesize("climb", variant)
        var pf := FileAccess.open(prefix + ".pcm-s16le", FileAccess.WRITE)
        pf.store_buffer(wav.data)
        pf.close()
        var info := {"source_sha256": SOURCE_SHA256, "godot": Engine.get_version_info(), "variant": variant, "seed": str(917324 + "climb".hash() + variant * 1973), "rate": wav.mix_rate, "frames": wav.data.size() / 2, "noise_sha256": FileAccess.get_sha256(prefix + ".noise-f64le"), "pcm_sha256": FileAccess.get_sha256(prefix + ".pcm-s16le"), "quarantined": true}
        var jf := FileAccess.open(prefix + ".json", FileAccess.WRITE)
        jf.store_string(JSON.stringify(info, "  "))
        jf.close()
    quit(0)
