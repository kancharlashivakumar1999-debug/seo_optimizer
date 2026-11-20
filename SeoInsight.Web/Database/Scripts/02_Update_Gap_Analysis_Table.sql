-- ============================================================================
-- SEO INSIGHTS ENGINE - DATABASE UPDATE SCRIPT
-- Adds / Ensures the 'gap_analysis_results' table exists for RAG Analysis storage
-- ============================================================================

CREATE TABLE IF NOT EXISTS gap_analysis_results (
    result_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    task_id UUID NOT NULL,
    llm_model VARCHAR(100) NOT NULL DEFAULT 'gemini-3.5-flash-lite',
    target_intent_fit VARCHAR(50),
    overall_drawback_summary TEXT,
    target_analysis JSONB NOT NULL,
    competitor_deep_dive JSONB NOT NULL,
    gap_analysis JSONB NOT NULL,
    recommended_modifications JSONB NOT NULL,
    rank_prediction JSONB NOT NULL,
    raw_prompt TEXT,
    raw_response TEXT,
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_gap_results_run
        FOREIGN KEY (run_id) REFERENCES task_runs(run_id) ON DELETE CASCADE,
    CONSTRAINT fk_gap_results_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE,
    CONSTRAINT uq_run_gap_result
        UNIQUE (run_id)
);

-- Indexes for fast retrieval by run_id and deep JSON querying
CREATE INDEX IF NOT EXISTS idx_gap_analysis_run 
    ON gap_analysis_results (run_id);

CREATE INDEX IF NOT EXISTS idx_gap_analysis_gin 
    ON gap_analysis_results USING GIN (gap_analysis);

CREATE INDEX IF NOT EXISTS idx_recommendations_gin 
    ON gap_analysis_results USING GIN (recommended_modifications);
