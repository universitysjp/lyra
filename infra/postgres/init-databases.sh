#!/usr/bin/env bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE identity_db;
    CREATE DATABASE catalog_db;
    CREATE DATABASE vector_db;
EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "catalog_db" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "vector_db" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL
