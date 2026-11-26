-- ==============================================================================
-- Script: Seed 75 Historical Task Runs for Task: 6112a201-8b50-e675-b54e-bfc85e19ec0c
-- Purpose: Tests the Runs vs. Ranks trend graph (sliding window: last 50 runs of 75)
-- Features:
--   1. 75 sequential runs spread over the past 30 days
--   2. Realistic ranking fluctuations between #3 and #35
--   3. Visibility scores correlated with average rank
--   4. Fully populated audit columns: started_at, finished_at, created_at, updated_at
-- ==============================================================================

DO $$
DECLARE
    v_task_id UUID := '6112a201-8b50-e675-b54e-bfc85e19ec0c'::UUID;
    v_keyword TEXT := 'seo ranking audit';
    v_location TEXT := 'Hyderabad, India';
    v_start_time TIMESTAMPTZ := NOW() - INTERVAL '30 days';
    v_run_time TIMESTAMPTZ;
    v_finish_time TIMESTAMPTZ;
    v_rank NUMERIC(5,2);
    v_visibility INT;
    v_top10 INT;
    v_duration NUMERIC(5,2);
    -- Simulated rank fluctuation points for 75 runs
    v_ranks NUMERIC[] := ARRAY[
        28.0, 26.5, 27.0, 25.0, 24.5, 26.0, 23.0, 22.5, 21.0, 23.5,
        20.0, 19.5, 21.0, 18.0, 17.5, 19.0, 16.0, 15.5, 17.0, 14.5,
        14.0, 15.0, 13.5, 12.0, 13.0, 11.5, 10.0, 11.0,  9.5,  8.0,
         9.0,  7.5,  8.0,  6.5,  5.0,  6.0,  7.5,  8.0,  9.5, 11.0,
        10.5, 12.0,  9.0,  8.5,  7.0,  6.5,  5.0,  4.5,  5.5,  4.0,
         3.5,  4.0,  5.0,  6.5,  7.0,  5.5,  4.5,  3.5,  4.0,  5.0,
         6.0,  5.5,  4.5,  3.5,  4.0,  3.0,  4.5,  5.0,  4.0,  3.5,
         4.0,  3.5,  4.5,  3.0,  3.5
    ];
BEGIN
    -- Ensure the target audit task exists; if not, create a placeholder task
    INSERT INTO public.audit_tasks (
        task_id, task_name, target_website, target_keyword, target_location,
        target_keywords, target_locations, check_engine, repeats_per_run, status,
        created_at, updated_at, created_by, updated_by, is_active
    ) VALUES (
        v_task_id, 'Benchmark Audit Task', 'sparity.com', v_keyword, v_location,
        jsonb_build_array(v_keyword), jsonb_build_array(v_location), 'live-serp-rag', 1, 'Active',
        v_start_time, NOW(), 'System', 'System', TRUE
    )
    ON CONFLICT (task_id) DO NOTHING;

    -- Insert 75 sequential runs with incremental timestamps and fluctuating ranks
    FOR i IN 1..75 LOOP
        -- Distribute 75 runs across the past 30 days (~9.6 hours apart)
        v_run_time := v_start_time + ((i - 1) * INTERVAL '9 hours 36 minutes');
        v_duration := ROUND((18.0 + (random() * 14.0))::NUMERIC, 2); -- 18 to 32 seconds
        v_finish_time := v_run_time + (v_duration || ' seconds')::INTERVAL;
        
        v_rank := v_ranks[i];
        
        -- Realistic visibility calculation: rank <= 10 -> high visibility
        IF v_rank <= 3.0 THEN
            v_visibility := 95 + FLOOR(random() * 6); -- 95 - 100%
            v_top10 := 1;
        ELSIF v_rank <= 10.0 THEN
            v_visibility := 70 + FLOOR(random() * 20); -- 70 - 89%
            v_top10 := 1;
        ELSE
            v_visibility := GREATEST(10, 60 - FLOOR(v_rank * 1.5));
            v_top10 := 0;
        END IF;

        INSERT INTO public.task_runs (
            run_id,
            task_id,
            target_keyword,
            target_location,
            run_number,
            run_status,
            run_note,
            implemented_changes,
            visibility_score,
            average_rank,
            total_keywords_checked,
            top10_hits,
            summary,
            duration_seconds,
            started_at,
            finished_at,
            created_at,
            updated_at,
            created_by,
            updated_by,
            is_active
        ) VALUES (
            gen_random_uuid(),
            v_task_id,
            v_keyword,
            v_location,
            i,
            'Completed',
            'Automated ranking check #' || i || ' with on-page optimization feedback',
            jsonb_build_array('Meta title optimized', 'H1 tag update', 'Content refresh'),
            v_visibility,
            v_rank,
            1,
            v_top10,
            'Live SERP check #' || i || ' completed. Rank position: #' || v_rank,
            v_duration,
            v_run_time,
            v_finish_time,
            v_run_time,
            v_finish_time,
            'System',
            'System',
            TRUE
        );
    END LOOP;

    RAISE NOTICE 'Successfully seeded 75 task runs for task_id: %', v_task_id;
END $$;
