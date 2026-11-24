-- ============================================================================
-- MIGRATION 03: Transition to Single Keyword & Single Location per Task
-- Adds dedicated target_keyword and target_location columns to audit_tasks
-- Migrates existing JSON data to the new columns, retaining backward compatibility
-- ============================================================================

ALTER TABLE audit_tasks 
    ADD COLUMN IF NOT EXISTS target_keyword VARCHAR(500) DEFAULT '',
    ADD COLUMN IF NOT EXISTS target_location VARCHAR(500) DEFAULT '';

-- Populate target_keyword and target_location from target_keywords / target_locations json arrays if available
UPDATE audit_tasks
SET 
    target_keyword = COALESCE(
        NULLIF(target_keywords->>0, ''),
        target_keyword,
        ''
    ),
    target_location = COALESCE(
        NULLIF(target_locations->>0, ''),
        target_location,
        ''
    )
WHERE (target_keyword IS NULL OR target_keyword = '')
   OR (target_location IS NULL OR target_location = '');

-- Also ensure task_runs has target_keyword and target_location for direct querying if desired
ALTER TABLE task_runs
    ADD COLUMN IF NOT EXISTS target_keyword VARCHAR(500) DEFAULT '',
    ADD COLUMN IF NOT EXISTS target_location VARCHAR(500) DEFAULT '';

UPDATE task_runs r
SET 
    target_keyword = t.target_keyword,
    target_location = t.target_location
FROM audit_tasks t
WHERE r.task_id = t.task_id
  AND (r.target_keyword IS NULL OR r.target_keyword = '');
