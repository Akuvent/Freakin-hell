extends CPUParticles2D


# Connected to the particle system's finished signal in hit_spark.tscn.
# Once the particles have played out, free this spark node.
func _on_finished() -> void:
	queue_free()
