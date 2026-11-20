-- ============================================================================
-- SEO INSIGHTS ENGINE - PRODUCTION DATABASE SCHEMA (V2)
-- Architecture:
-- 1. UUIDv7 Sequential Primary Keys (B-Tree friendly, high write-throughput)
-- 2. Audit Trail Columns across every table (created_at, updated_at, created_by, is_active)
-- 3. Execution Run Isolation (task_runs mapping table with run_id)
-- 4. run_id linked to: serp_competitors, content_chunks, gap_analysis_results, rag_payloads
-- 5. Native real[] array for vector embeddings (Zero external C-extensions needed)
-- 6. Projected performance B-Tree, Compound & GIN Indexes
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 0. NATIVE POSTGRESQL UUIDv7 FUNCTION
-- RFC 9562 standard: 48-bit UNIX ms timestamp + 74-bit entropy
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION generate_uuid_v7()
RETURNS uuid AS $$
DECLARE
    unix_time_ms bytea;
    uuid_bytes bytea;
BEGIN
    -- Extract current Unix epoch milliseconds (48 bits / 6 bytes)
    unix_time_ms := substring(
        int8send(floor(extract(epoch FROM clock_timestamp()) * 1000)::bigint)
        FROM 3 FOR 6
    );

    -- Generate random bytes for remaining entropy (10 bytes)
    uuid_bytes := unix_time_ms || gen_random_bytes(10);

    -- Set version 7 in byte 7 (0111xxxx -> 0x70)
    uuid_bytes := set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & 15) | 112);

    -- Set variant 10xx in byte 9 (10xxxxxx -> 0x80)
    uuid_bytes := set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & 63) | 128);

    RETURN encode(uuid_bytes, 'hex')::uuid;
END;
$$ LANGUAGE plpgsql VOLATILE;

-- ----------------------------------------------------------------------------
-- 1. AUDIT TASKS TABLE (Root Target Asset / Editable Configuration)
-- ----------------------------------------------------------------------------
CREATE TABLE audit_tasks (
    task_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    task_name VARCHAR(255) NOT NULL,
    target_website VARCHAR(500) NOT NULL,
    target_keywords JSONB NOT NULL DEFAULT '[]'::jsonb,  -- Array of keywords: ["keyword 1", "keyword 2"]
    target_locations JSONB NOT NULL DEFAULT '[]'::jsonb, -- Array of locations: ["Hyderabad, India"]
    prompt_template TEXT,
    check_engine VARCHAR(50) DEFAULT 'live-serp-rag',
    repeats_per_run INT DEFAULT 1,
    status VARCHAR(30) NOT NULL DEFAULT 'Active'
        CHECK (status IN ('Active', 'Paused', 'Archived', 'Pending')),
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    updated_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- ----------------------------------------------------------------------------
-- 2. TASK RUNS TABLE (Execution Run Mapping & History)
-- Bridges task_id with every executed run instance
-- ----------------------------------------------------------------------------
CREATE TABLE task_runs (
    run_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    task_id UUID NOT NULL,
    run_number INT NOT NULL DEFAULT 1,
    run_status VARCHAR(30) NOT NULL DEFAULT 'Running'
        CHECK (run_status IN ('Running', 'Completed', 'Failed', 'Cancelled')),
    run_note TEXT,
    implemented_changes JSONB DEFAULT '[]'::jsonb,
    visibility_score INT DEFAULT 0,
    average_rank NUMERIC(5, 2),
    total_keywords_checked INT DEFAULT 0,
    top10_hits INT DEFAULT 0,
    summary TEXT,
    duration_seconds NUMERIC(8, 2) DEFAULT 0,
    started_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    finished_at TIMESTAMPTZ,
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    updated_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_task_runs_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- 3. SERP COMPETITORS TABLE (Linked to run_id & task_id)
-- Stores the live organic ranking competitors per execution run
-- ----------------------------------------------------------------------------
CREATE TABLE serp_competitors (
    competitor_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    task_id UUID NOT NULL,
    keyword VARCHAR(255) NOT NULL,
    location VARCHAR(255) NOT NULL,
    position INT NOT NULL CHECK (position >= 1 AND position <= 100),
    domain VARCHAR(255) NOT NULL,
    title TEXT NOT NULL,
    url TEXT NOT NULL,
    snippet TEXT,
    serp_highlights TEXT,
    is_target_site BOOLEAN NOT NULL DEFAULT FALSE,
    crawled_summary TEXT,
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_serp_competitors_run
        FOREIGN KEY (run_id) REFERENCES task_runs(run_id) ON DELETE CASCADE,
    CONSTRAINT fk_serp_competitors_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE,
    CONSTRAINT uq_run_keyword_position
        UNIQUE (run_id, keyword, position)
);

-- ----------------------------------------------------------------------------
-- 4. RAG INPUT PAYLOADS TABLE (Input Context snapshot per run)
-- Stores the exact prepared JSON payload constructed for AI analysis
-- ----------------------------------------------------------------------------
CREATE TABLE rag_input_payloads (
    payload_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    task_id UUID NOT NULL,
    target_keyword VARCHAR(255) NOT NULL,
    target_location VARCHAR(255) NOT NULL,
    target_website VARCHAR(500) NOT NULL,
    own_rank_position INT,
    top_competitors_json JSONB NOT NULL,
    people_also_ask JSONB DEFAULT '[]'::jsonb,
    ingested_chunks_json JSONB DEFAULT '[]'::jsonb,
    system_prompt TEXT NOT NULL,
    user_prompt TEXT NOT NULL,
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_rag_payloads_run
        FOREIGN KEY (run_id) REFERENCES task_runs(run_id) ON DELETE CASCADE,
    CONSTRAINT fk_rag_payloads_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- 5. CONTENT CHUNKS & VECTOR EMBEDDINGS (Linked to run_id & task_id)
-- Stores text chunks and embedding vectors (float[] / real[])
-- ----------------------------------------------------------------------------
CREATE TABLE content_chunks (
    chunk_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    task_id UUID NOT NULL,
    source_type VARCHAR(20) NOT NULL CHECK (source_type IN ('TargetPage', 'Competitor')),
    source_url TEXT NOT NULL,
    competitor_rank INT,
    heading_hierarchy VARCHAR(500),
    chunk_text TEXT NOT NULL,
    token_count INT,
    embedding real[], -- Native PostgreSQL float array (768 or 1536 dims)
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_chunks_run
        FOREIGN KEY (run_id) REFERENCES task_runs(run_id) ON DELETE CASCADE,
    CONSTRAINT fk_chunks_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- 6. GAP ANALYSIS RESULTS TABLE (AI Competitive Strategic Output per run)
-- Stores Gemini LLM JSON response & rank predictions linked to run_id
-- ----------------------------------------------------------------------------
CREATE TABLE gap_analysis_results (
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

-- ----------------------------------------------------------------------------
-- 7. ACTION ITEMS TABLE (Actionable Execution Plan tracking per run)
-- ----------------------------------------------------------------------------
CREATE TABLE action_items (
    action_id UUID PRIMARY KEY DEFAULT generate_uuid_v7(),
    run_id UUID NOT NULL,
    task_id UUID NOT NULL,
    priority INT NOT NULL DEFAULT 1,
    action_text TEXT NOT NULL,
    owner VARCHAR(50) NOT NULL CHECK (owner IN ('Content', 'Dev', 'SEO')),
    effort VARCHAR(20) NOT NULL CHECK (effort IN ('Low', 'Medium', 'High')),
    expected_impact VARCHAR(20) NOT NULL CHECK (expected_impact IN ('Low', 'Medium', 'High')),
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    completed_at TIMESTAMPTZ,
    
    -- Standard Audit Columns
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    created_by VARCHAR(100) DEFAULT 'System',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_action_items_run
        FOREIGN KEY (run_id) REFERENCES task_runs(run_id) ON DELETE CASCADE,
    CONSTRAINT fk_action_items_task
        FOREIGN KEY (task_id) REFERENCES audit_tasks(task_id) ON DELETE CASCADE
);

-- ============================================================================
-- 8. HIGH-PERFORMANCE INDEXES
-- ============================================================================

-- Audit Tasks
CREATE INDEX idx_audit_tasks_active ON audit_tasks (created_at DESC) WHERE is_active = TRUE;
CREATE INDEX idx_audit_tasks_website ON audit_tasks (target_website);

-- Task Runs
CREATE INDEX idx_task_runs_task_started ON task_runs (task_id, started_at DESC);
CREATE INDEX idx_task_runs_status ON task_runs (run_status);

-- SERP Competitors
CREATE INDEX idx_serp_competitors_run_pos ON serp_competitors (run_id, position ASC);
CREATE INDEX idx_serp_competitors_task ON serp_competitors (task_id);

-- RAG Payloads
CREATE INDEX idx_rag_payloads_run ON rag_input_payloads (run_id);
CREATE INDEX idx_rag_payloads_task ON rag_input_payloads (task_id);

-- Content Chunks
CREATE INDEX idx_chunks_run_source ON content_chunks (run_id, source_type);
CREATE INDEX idx_chunks_task ON content_chunks (task_id);

-- Gap Analysis Results
CREATE INDEX idx_gap_analysis_run ON gap_analysis_results (run_id);
CREATE INDEX idx_gap_analysis_gin ON gap_analysis_results USING GIN (gap_analysis);
CREATE INDEX idx_recommendations_gin ON gap_analysis_results USING GIN (recommended_modifications);

-- Action Items
CREATE INDEX idx_action_items_run_pri ON action_items (run_id, priority ASC) WHERE is_completed = FALSE;
