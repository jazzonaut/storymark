-- Runs once, on first cluster init, via the postgres image's
-- /docker-entrypoint-initdb.d hook. pgvector ships in the pgvector/pgvector
-- image; this just enables the extension in the storymark database.
CREATE EXTENSION IF NOT EXISTS vector;
