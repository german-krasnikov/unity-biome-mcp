"""Ownership model for live Unity integration tests.

The model is deliberately independent from pytest and the TCP bridge so its
diff and cleanup decisions can be unit tested without a running Editor.
"""


from dataclasses import dataclass, field
import base64


_MAX_TRANSIENT_ID = (1 << 64) - 1
_MIN_LEGACY_INSTANCE_ID = -(1 << 31)


def _normalize_transient_id(value: str) -> str:
    """Return the process-local Unity object ID in unsigned wire form.

    Unity 6000.0 exposes signed 32-bit InstanceID values. The MCP wire contract
    uses their unsigned decimal representation so identity remains lossless on
    every platform and does not depend on Python integer-width assumptions.
    """
    token = str(value).strip()
    if not token:
        raise ValueError("empty transient object ID")
    try:
        raw = int(token, 10)
    except ValueError as exc:
        raise ValueError(f"invalid transient object ID: {value!r}") from exc
    if raw < 0:
        if raw < _MIN_LEGACY_INSTANCE_ID:
            raise ValueError(f"legacy InstanceID is outside Int32 range: {value!r}")
        raw += 1 << 64
    if raw <= 0 or raw > _MAX_TRANSIENT_ID:
        raise ValueError(f"transient object ID is outside UInt64 range: {value!r}")
    return str(raw)


def _decode(value: str) -> str:
    return base64.b64decode(value.encode("ascii"), validate=True).decode("utf-8")


def _is_path_within_root(path: str, root: str) -> bool:
    if not path or not root or "\\" in path:
        return False
    if any(part in {"", ".", ".."} for part in path.split("/")):
        return False
    return path == root or path.startswith(root + "/")


@dataclass(frozen=True)
class SceneState:
    path: str
    name: str
    handle: int
    is_dirty: bool
    is_active: bool

    @property
    def identity(self) -> str:
        return self.path or f"<unsaved:{self.handle}>"


@dataclass(frozen=True)
class ObjectState:
    global_id: str
    transient_id: str
    scene_path: str
    hierarchy_path: str
    name: str

    def __post_init__(self) -> None:
        object.__setattr__(
            self,
            "transient_id",
            _normalize_transient_id(self.transient_id),
        )

    @property
    def identity(self) -> str:
        if self.global_id:
            return f"global:{self.global_id}"
        return f"transient:{self.transient_id}"


@dataclass(frozen=True)
class UnityStateSnapshot:
    is_playing: bool
    scenes: tuple[SceneState, ...]
    objects: tuple[ObjectState, ...]
    assets: tuple[str, ...] = ()
    time_scale: float = 1.0

    @classmethod
    def parse(cls, payload: str) -> "UnityStateSnapshot":
        playing: bool | None = None
        time_scale: float | None = None
        scenes: list[SceneState] = []
        objects: list[ObjectState] = []
        assets: list[str] = []
        for line_number, line in enumerate(payload.splitlines(), 1):
            if not line:
                continue
            fields = line.split("\t")
            try:
                if fields[0] == "P" and len(fields) == 2:
                    playing = fields[1] == "1"
                elif fields[0] == "T" and len(fields) == 2:
                    time_scale = float(fields[1])
                elif fields[0] == "S" and len(fields) == 7:
                    scenes.append(SceneState(
                        path=_decode(fields[1]),
                        name=_decode(fields[2]),
                        handle=int(fields[3]),
                        is_dirty=fields[4] == "1",
                        is_active=fields[5] == "1",
                    ))
                elif fields[0] == "O" and len(fields) == 7:
                    objects.append(ObjectState(
                        global_id=_decode(fields[1]),
                        transient_id=fields[2],
                        scene_path=_decode(fields[3]),
                        hierarchy_path=_decode(fields[4]),
                        name=_decode(fields[5]),
                    ))
                elif fields[0] == "A" and len(fields) == 2:
                    assets.append(_decode(fields[1]))
                else:
                    raise ValueError("unknown record shape")
            except (ValueError, UnicodeError) as exc:
                raise ValueError(
                    f"invalid Unity snapshot record at line {line_number}: {line!r}"
                ) from exc
        if playing is None:
            raise ValueError("Unity snapshot has no play-state record")
        if time_scale is None:
            raise ValueError("Unity snapshot has no time-scale record")
        scene_ids = [scene.identity for scene in scenes]
        object_ids = [obj.identity for obj in objects]
        if len(scene_ids) != len(set(scene_ids)):
            raise ValueError("Unity snapshot contains duplicate scene identities")
        if len(object_ids) != len(set(object_ids)):
            raise ValueError("Unity snapshot contains duplicate object identities")
        if len(assets) != len(set(assets)):
            raise ValueError("Unity snapshot contains duplicate asset paths")
        return cls(playing, tuple(scenes), tuple(objects), tuple(assets), time_scale)


@dataclass
class OwnershipPolicy:
    scene_paths: set[str] = field(default_factory=set)
    asset_paths: set[str] = field(default_factory=set)
    object_ids: set[str] = field(default_factory=set)
    object_paths: set[tuple[str, str]] = field(default_factory=set)
    reset_scene_path: str = ""
    allowed_path_root: str = ""
    allowed_play_mode_target: bool | None = None

    def _allows_registered_path(self, path: str) -> bool:
        return not self.allowed_path_root or _is_path_within_root(
            path,
            self.allowed_path_root,
        )

    def owns_scene_path(self, path: str) -> bool:
        return (
            bool(path)
            and path in self.scene_paths
            and self._allows_registered_path(path)
        )

    def owns_asset_path(self, path: str) -> bool:
        return (
            bool(path)
            and path in self.asset_paths
            and self._allows_registered_path(path)
        )

    def owns_scene(self, scene: SceneState) -> bool:
        return self.owns_scene_path(scene.path)

    def owns_object(self, obj: ObjectState) -> bool:
        if self.allowed_path_root and not _is_path_within_root(
            obj.scene_path,
            self.allowed_path_root,
        ):
            return False
        return (
            obj.identity in self.object_ids
            or (obj.scene_path, obj.hierarchy_path) in self.object_paths
            or self.owns_scene_path(obj.scene_path)
        )


@dataclass(frozen=True)
class OwnershipPlan:
    owned_added_objects: tuple[ObjectState, ...]
    owned_added_scenes: tuple[SceneState, ...]
    owned_added_assets: tuple[str, ...]
    unowned_added_objects: tuple[ObjectState, ...]
    unowned_added_scenes: tuple[SceneState, ...]
    unowned_added_assets: tuple[str, ...]
    unowned_missing_objects: tuple[ObjectState, ...]
    unowned_missing_scenes: tuple[SceneState, ...]
    unowned_missing_assets: tuple[str, ...]
    unowned_changed_objects: tuple[tuple[ObjectState, ObjectState], ...]
    unowned_dirty_scenes: tuple[SceneState, ...]
    play_mode_changed: bool
    play_mode_transition_allowed: bool
    time_scale_changed: bool
    reset_owned_scene: bool

    @property
    def has_unowned_state(self) -> bool:
        return any((
            self.unowned_added_objects,
            self.unowned_added_scenes,
            self.unowned_added_assets,
            self.unowned_missing_objects,
            self.unowned_missing_scenes,
            self.unowned_missing_assets,
            self.unowned_changed_objects,
            self.unowned_dirty_scenes,
        ))

    @property
    def violations(self) -> tuple[str, ...]:
        messages: list[str] = []
        if self.play_mode_changed and not self.play_mode_transition_allowed:
            messages.append("Play/Edit mode changed during the test")
        if self.time_scale_changed:
            messages.append("Time.timeScale changed during the test")
        messages.extend(
            f"unowned object added: {obj.identity} {obj.scene_path}:{obj.hierarchy_path}"
            for obj in self.unowned_added_objects
        )
        messages.extend(
            f"unowned scene added: {scene.identity}"
            for scene in self.unowned_added_scenes
        )
        messages.extend(
            f"unowned asset added: {path}"
            for path in self.unowned_added_assets
        )
        messages.extend(
            f"baseline object removed: {obj.identity} {obj.scene_path}:{obj.hierarchy_path}"
            for obj in self.unowned_missing_objects
        )
        messages.extend(
            f"baseline scene removed: {scene.identity}"
            for scene in self.unowned_missing_scenes
        )
        messages.extend(
            f"baseline asset removed: {path}"
            for path in self.unowned_missing_assets
        )
        messages.extend(
            f"unowned object moved or renamed: {before.identity}"
            for before, _after in self.unowned_changed_objects
        )
        messages.extend(
            f"unowned scene became dirty: {scene.identity}"
            for scene in self.unowned_dirty_scenes
        )
        return tuple(messages)


def build_ownership_plan(
    before: UnityStateSnapshot,
    after: UnityStateSnapshot,
    policy: OwnershipPolicy,
) -> OwnershipPlan:
    before_scenes = {scene.identity: scene for scene in before.scenes}
    after_scenes = {scene.identity: scene for scene in after.scenes}
    before_objects = {obj.identity: obj for obj in before.objects}
    after_objects = {obj.identity: obj for obj in after.objects}

    added_scenes = [after_scenes[key] for key in after_scenes.keys() - before_scenes]
    missing_scenes = [before_scenes[key] for key in before_scenes.keys() - after_scenes]
    added_objects = [after_objects[key] for key in after_objects.keys() - before_objects]
    missing_objects = [before_objects[key] for key in before_objects.keys() - after_objects]
    added_assets = sorted(set(after.assets) - set(before.assets))
    missing_assets = sorted(set(before.assets) - set(after.assets))

    changed_objects: list[tuple[ObjectState, ObjectState]] = []
    for key in before_objects.keys() & after_objects.keys():
        old = before_objects[key]
        new = after_objects[key]
        if (
            old.scene_path != new.scene_path
            or old.hierarchy_path != new.hierarchy_path
            or old.name != new.name
        ):
            changed_objects.append((old, new))

    dirty_scenes: list[SceneState] = []
    for key in before_scenes.keys() & after_scenes.keys():
        old = before_scenes[key]
        new = after_scenes[key]
        if not old.is_dirty and new.is_dirty and not policy.owns_scene(new):
            dirty_scenes.append(new)

    owned_added_objects = [obj for obj in added_objects if policy.owns_object(obj)]
    owned_added_scenes = [scene for scene in added_scenes if policy.owns_scene(scene)]
    unowned_changed = [
        pair for pair in changed_objects
        if not policy.owns_object(pair[0]) and not policy.owns_object(pair[1])
    ]

    reset_path = policy.reset_scene_path
    reset_owned_scene = bool(reset_path) and (
        any(scene.path == reset_path and scene.is_dirty for scene in after.scenes)
        or any(obj.scene_path == reset_path for obj in added_objects + missing_objects)
        or any(
            old.scene_path == reset_path or new.scene_path == reset_path
            for old, new in changed_objects
        )
        or reset_path in missing_assets
    )

    play_mode_changed = before.is_playing != after.is_playing
    play_mode_transition_allowed = (
        play_mode_changed
        and policy.allowed_play_mode_target is not None
        and after.is_playing == policy.allowed_play_mode_target
    )

    return OwnershipPlan(
        owned_added_objects=tuple(owned_added_objects),
        owned_added_scenes=tuple(owned_added_scenes),
        owned_added_assets=tuple(
            path for path in added_assets if policy.owns_asset_path(path)
        ),
        unowned_added_objects=tuple(
            obj for obj in added_objects if not policy.owns_object(obj)
        ),
        unowned_added_scenes=tuple(
            scene for scene in added_scenes if not policy.owns_scene(scene)
        ),
        unowned_added_assets=tuple(
            path for path in added_assets if not policy.owns_asset_path(path)
        ),
        unowned_missing_objects=tuple(
            obj for obj in missing_objects if not policy.owns_object(obj)
        ),
        unowned_missing_scenes=tuple(
            scene for scene in missing_scenes if not policy.owns_scene(scene)
        ),
        unowned_missing_assets=tuple(
            path for path in missing_assets if not policy.owns_asset_path(path)
        ),
        unowned_changed_objects=tuple(unowned_changed),
        unowned_dirty_scenes=tuple(dirty_scenes),
        play_mode_changed=play_mode_changed,
        play_mode_transition_allowed=play_mode_transition_allowed,
        time_scale_changed=before.time_scale != after.time_scale,
        reset_owned_scene=reset_owned_scene,
    )


def _needs_owned_scene_reset(
    policy: OwnershipPolicy,
    plan: OwnershipPlan,
    after_is_playing: bool,
) -> bool:
    """True when the owned reset-scene needs a reset before finishing the test.

    `plan` must already be built from the raw state captured immediately
    after the test body ran, before any of this restore pass's own
    play-mode/time-scale side effects can change it — this function never
    rebuilds the plan itself, it only reads `plan.reset_owned_scene`. Play
    Mode always needs a reset: ephemeral runtime-only objects (no
    global_id) can appear and vanish during Play without ever showing up
    in a snapshot diff. Outside Play Mode, reuse the plan's own
    `reset_owned_scene` field — the same plan-derived mutation signal the
    post-cleanup verification already checks — instead of inventing a
    second diff mechanism.
    """
    if not policy.reset_scene_path:
        return False
    if after_is_playing:
        return True
    return plan.reset_owned_scene
