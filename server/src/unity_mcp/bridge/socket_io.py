from biome_mcp.transport.framing import (  # noqa: F401 (re-export)
    _TCP_KEEPALIVE_DARWIN,
    _TCP_KEEPCNT_DARWIN,
    _TCP_KEEPINTVL_DARWIN,
    MAX_FRAME_BYTES,
    apply_socket_options as _apply_socket_options,  # renamed in faf14eb
    frame_read,
    frame_read_with_timeout,
    frame_write,
)


class DomainReloadError(ConnectionError):
    """Unity signaled domain reload via going_away frame."""
