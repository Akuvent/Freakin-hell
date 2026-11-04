extends Area2D
## Kill zone — calls die() on fighters that enter.


func _on_body_entered(body: Node) -> void:
	if body.has_method("die"):
		body.die()
