-- Flitflap Dual-Tier Database Initialization
-- Master Database (flitflap_master) is created by POSTGRES_DB
-- This script provisions the default tenant shard database

SELECT 'CREATE DATABASE flitflap_tenant_default'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'flitflap_tenant_default')\gexec

GRANT ALL PRIVILEGES ON DATABASE flitflap_master TO postgres;
GRANT ALL PRIVILEGES ON DATABASE flitflap_tenant_default TO postgres;
