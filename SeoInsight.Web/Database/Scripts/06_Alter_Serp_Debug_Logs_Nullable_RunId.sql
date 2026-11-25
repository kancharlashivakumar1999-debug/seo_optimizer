-- ============================================================================
-- MIGRATION 06: Allow run_id to be nullable in serp_request_debug_logs
-- Allows logging directly from FetchSerpAsync even when standalone
-- ============================================================================

ALTER TABLE serp_request_debug_logs 
    ALTER COLUMN run_id DROP NOT NULL;
