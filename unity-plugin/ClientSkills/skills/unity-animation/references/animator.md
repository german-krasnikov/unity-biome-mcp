# Animator Controllers

## State Machine Workflow

1. Read the controller with `animator(action="get", path=...)`.
2. Add parameters and states.
3. Add transitions with explicit conditions.
4. Set the default state.
5. Read the controller again and verify every name and condition.

```text
batch(
  commands="""
animator action=add_param path=/Character params="Speed:float:0; Grounded:bool:true"
animator action=add_state path=/Character states="Idle:Idle.anim; Move:Move.anim"
animator action=add_transition path=/Character source=Idle target=Move conditions="Speed>0.1; Grounded" duration=0.15 has_exit_time=false
animator action=set_default path=/Character state=Idle
animator action=get path=/Character
""",
  on_error="stop"
)
```

For blend trees, choose the blend type first, then supply matching parameters
and children. Inspect the result before adding dependent transitions.

Bad: generating many transitions without first reading state and parameter
names.

Good: build one logical path, read it back, then extend the graph.

Animator commands may create or modify a controller asset. Treat a stopped
batch as a partial asset operation and inspect the controller before retrying.
