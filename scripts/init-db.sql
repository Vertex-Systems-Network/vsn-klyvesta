-- Initialize Klyvesta Database
-- This script runs automatically on first container start

-- Enable UUID extension for UUIDv7 support (handled in app layer for now)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create schemas if they don't exist
CREATE SCHEMA IF NOT EXISTS trading;
CREATE SCHEMA IF NOT EXISTS accounting;
CREATE SCHEMA IF NOT EXISTS compliance;
CREATE SCHEMA IF NOT EXISTS onboarding;

-- Grant permissions (adjust as needed for production)
GRANT ALL PRIVILEGES ON SCHEMA trading TO postgres;
GRANT ALL PRIVILEGES ON SCHEMA accounting TO postgres;
GRANT ALL PRIVILEGES ON SCHEMA compliance TO postgres;
GRANT ALL PRIVILEGES ON SCHEMA onboarding TO postgres;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'Klyvesta database initialized successfully';
    RAISE NOTICE 'Schemas created: trading, accounting, compliance, onboarding';
END $$;
