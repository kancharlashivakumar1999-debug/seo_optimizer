-- ============================================================================
-- MIGRATION 05: Create dedicated serp_request_debug_logs table
-- ============================================================================

CREATE TABLE IF NOT EXISTS serp_request_debug_logs (
    id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    url TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX IF NOT EXISTS idx_serp_request_debug_logs_run_id 
    ON serp_request_debug_logs(run_id);
