FROM python:3.14-slim AS builder

WORKDIR /app
COPY server/pyproject.toml server/pyproject.toml
COPY server/src/ server/src/
RUN pip install --no-cache-dir --target /deps ./server

FROM python:3.14-slim

COPY --from=builder /deps /usr/local/lib/python3.14/site-packages
COPY --from=builder /deps/bin/* /usr/local/bin/

ENTRYPOINT ["unity-biome-mcp"]
