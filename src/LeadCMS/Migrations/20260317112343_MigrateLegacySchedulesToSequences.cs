using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacySchedulesToSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    grp RECORD;
    tmpl RECORD;
    new_sequence_id INT;
    step_pos INT;
    schedule_json JSONB;
    timing_json JSONB;
    sequence_source TEXT;
    step_source TEXT;
    enrollment_source_text TEXT;
    delivery_source_text TEXT;
    -- Cron parsing
    cron_parts TEXT[];
    cron_hour TEXT;
    cron_minute TEXT;
    cron_dow TEXT;
    send_at_time TEXT;
    allowed_days_json JSONB;
    -- Day/Time parsing
    day_values INT[];
    day_count INT;
    delay_value INT;
    time_str TEXT;
BEGIN
    -- ============================================================
    -- Migrate legacy EmailGroup/EmailSchedule to Sequence model.
    --
    -- Supported schedule formats:
    --   1. Cron:     {""Cron"": ""0 0 14 ? * TUE,THU""}
    --   2. Day/Time: {""Day"": ""5,14"", ""Time"": ""14:00:00""}
    --
    -- For each EmailGroup that has an EmailSchedule:
    --   - Creates a Sequence
    --   - Creates SequenceSteps from the group's EmailTemplates
    --   - Creates SequenceEnrollments from ContactEmailSchedule
    --   - Creates SequenceDeliveries from EmailLog
    --   - Updates summary counters on sequence and steps
    -- ============================================================

    FOR grp IN
        SELECT eg.id,
               eg.name,
               eg.language,
               es.id AS schedule_id,
               es.schedule,
               es.source AS schedule_source
        FROM email_group eg
        INNER JOIN email_schedule es ON es.group_id = eg.id
    LOOP
        sequence_source := COALESCE(NULLIF(grp.schedule_source, ''), 'email_schedule: ' || grp.schedule_id);

        -- Idempotency: skip groups already migrated
        IF EXISTS (
            SELECT 1 FROM sequence
            WHERE source = sequence_source
        ) THEN
            CONTINUE;
        END IF;

        schedule_json := grp.schedule::jsonb;

        -- --------------------------------------------------------
        -- 1. Create the Sequence
        -- --------------------------------------------------------
        INSERT INTO sequence (
            name, description, language, status,
            stop_on_reply, use_contact_time_zone, time_zone,
            active_enrollment_count, completed_enrollment_count, exited_enrollment_count,
            sent_count, failed_count,
            enrollment, utm_parameters,
            created_at, source
        ) VALUES (
            grp.name,
            grp.name || ' (' || grp.language || ')',
            grp.language,
            CASE
                WHEN EXISTS (
                    SELECT 1 FROM contact_email_schedule ces
                    WHERE ces.schedule_id = grp.schedule_id AND ces.status = 0
                ) THEN 1  -- Active
                ELSE 2    -- Paused
            END,
            false, true, 0,
            0, 0, 0, 0, 0,
            '{""Modes"": [""manual"", ""api""], ""ReentryPolicy"": 2}'::jsonb, NULL,
            NOW(), sequence_source
        ) RETURNING id INTO new_sequence_id;

        -- --------------------------------------------------------
        -- 2. Create SequenceSteps from templates in the group
        -- --------------------------------------------------------
        step_pos := 0;

        IF schedule_json ? 'Cron' THEN
            --
            -- Cron schedule, e.g. ""0 0 14 ? * TUE,THU""
            -- Fields: seconds minutes hours day-of-month month day-of-week
            --
            cron_parts := string_to_array(schedule_json->>'Cron', ' ');
            cron_minute := cron_parts[2];
            cron_hour   := cron_parts[3];
            cron_dow    := cron_parts[6];

            send_at_time := lpad(cron_hour, 2, '0') || ':' || lpad(cron_minute, 2, '0');

            -- Map abbreviated day names to full names
            SELECT jsonb_agg(
                CASE trim(d)
                    WHEN 'MON' THEN 'Monday'
                    WHEN 'TUE' THEN 'Tuesday'
                    WHEN 'WED' THEN 'Wednesday'
                    WHEN 'THU' THEN 'Thursday'
                    WHEN 'FRI' THEN 'Friday'
                    WHEN 'SAT' THEN 'Saturday'
                    WHEN 'SUN' THEN 'Sunday'
                END
            ) INTO allowed_days_json
            FROM unnest(string_to_array(cron_dow, ',')) AS d;

            FOR tmpl IN
                SELECT et.id, et.name, et.source
                FROM email_template et
                WHERE et.email_group_id = grp.id
                ORDER BY et.id
            LOOP
                step_source := COALESCE(NULLIF(tmpl.source, ''), 'email_template: ' || tmpl.id);

                -- Step 0: delay 0 (align to next allowed day at sendAt).
                -- Step N>0: delay 1 day (ensures advancement past the current cron fire).
                timing_json := jsonb_build_object(
                    'Delay', jsonb_build_object(
                        'Value', CASE WHEN step_pos = 0 THEN 0 ELSE 1 END,
                        'Unit', 'days'
                    ),
                    'SendAt', send_at_time,
                    'AllowedWeekDays', allowed_days_json
                );

                INSERT INTO sequence_step (
                    sequence_id, email_template_id, position, name, type,
                    timing, scheduled_count, sent_count, failed_count, skipped_count,
                    created_at, source
                ) VALUES (
                    new_sequence_id, tmpl.id, step_pos, tmpl.name,
                    0,
                    timing_json, 0, 0, 0, 0,
                    NOW(), step_source
                );

                step_pos := step_pos + 1;
            END LOOP;

        ELSIF schedule_json ? 'Day' THEN
            --
            -- Day/Time schedule, e.g. {""Day"": ""5,14"", ""Time"": ""14:00:00""}
            -- Day values are absolute days since contact creation.
            -- Convert to inter-step delays.
            --
            day_values := string_to_array(schedule_json->>'Day', ',')::int[];
            day_count  := array_length(day_values, 1);

            time_str     := schedule_json->>'Time';
            send_at_time := left(time_str, 5);  -- HH:MM:SS → HH:MM

            FOR tmpl IN
                SELECT et.id, et.name, et.source
                FROM email_template et
                WHERE et.email_group_id = grp.id
                ORDER BY et.id
            LOOP
                step_source := COALESCE(NULLIF(tmpl.source, ''), 'email_template: ' || tmpl.id);

                IF step_pos = 0 THEN
                    -- First step: delay = first day value (days since enrollment)
                    delay_value := day_values[1];
                ELSIF step_pos < day_count THEN
                    -- Subsequent steps: delay = difference between consecutive day values
                    delay_value := day_values[step_pos + 1] - day_values[step_pos];
                ELSE
                    -- More templates than day values: reuse the last interval
                    IF day_count >= 2 THEN
                        delay_value := day_values[day_count] - day_values[day_count - 1];
                    ELSE
                        delay_value := day_values[1];
                    END IF;
                END IF;

                timing_json := jsonb_build_object(
                    'Delay', jsonb_build_object('Value', delay_value, 'Unit', 'days'),
                    'SendAt', send_at_time
                );

                INSERT INTO sequence_step (
                    sequence_id, email_template_id, position, name, type,
                    timing, scheduled_count, sent_count, failed_count, skipped_count,
                    created_at, source
                ) VALUES (
                    new_sequence_id, tmpl.id, step_pos, tmpl.name,
                    0,
                    timing_json, 0, 0, 0, 0,
                    NOW(), step_source
                );

                step_pos := step_pos + 1;
            END LOOP;
        END IF;

        -- --------------------------------------------------------
        -- 3. Create SequenceEnrollments from ContactEmailSchedule
        -- --------------------------------------------------------
        INSERT INTO sequence_enrollment (
            sequence_id, contact_id, status, last_completed_step_id,
            entered_at, completed_at, exited_at, exit_reason,
            enrollment_source, enrollment_reason,
            created_at, source
        )
        SELECT
            new_sequence_id,
            ces.contact_id,
            -- Map legacy status → new status
            CASE ces.status
                WHEN 0 THEN 0   -- Pending  → Active
                WHEN 1 THEN 1   -- Completed → Completed
                ELSE 2          -- Failed/Unsubscribed → Exited
            END,
            -- Determine last completed step from sent email count
            CASE
                                WHEN sent_counts.cnt > 0 THEN (
                                        SELECT ss.id
                                        FROM sequence_step ss
                                        WHERE ss.sequence_id = new_sequence_id
                                            AND ss.position = sent_counts.cnt - 1
                                        LIMIT 1)
                ELSE NULL
            END,
            -- entered_at: normalize -infinity timestamps
            CASE
                WHEN ces.created_at > '1970-01-01'::timestamptz THEN ces.created_at
                WHEN c.created_at > '1970-01-01'::timestamptz THEN c.created_at
                ELSE NOW()
            END,
            -- completed_at
            CASE WHEN ces.status = 1 THEN
                CASE
                    WHEN ces.updated_at IS NOT NULL AND ces.updated_at > '1970-01-01'::timestamptz THEN ces.updated_at
                    ELSE NOW()
                END
                ELSE NULL
            END,
            -- exited_at
            CASE WHEN ces.status IN (2, 3) THEN
                CASE
                    WHEN ces.updated_at IS NOT NULL AND ces.updated_at > '1970-01-01'::timestamptz THEN ces.updated_at
                    ELSE NOW()
                END
                ELSE NULL
            END,
            -- exit_reason
            CASE ces.status
                WHEN 1 THEN 1   -- Completed
                WHEN 2 THEN 2   -- Failed
                WHEN 3 THEN 3   -- Unsubscribed
                ELSE 0          -- None
            END,
            3,   -- EnrollmentSource = Migration
            'Subscribed on ' || grp.name,
            NOW(),
            COALESCE(NULLIF(ces.source, ''), 'contact_email_schedule: ' || ces.id)
        FROM contact_email_schedule ces
        INNER JOIN contact c ON c.id = ces.contact_id
        LEFT JOIN LATERAL (
            SELECT COUNT(*) AS cnt
            FROM email_log el
            WHERE el.schedule_id = grp.schedule_id
              AND el.contact_id = ces.contact_id
              AND el.status = 1  -- Sent
        ) sent_counts ON true
        WHERE ces.schedule_id = grp.schedule_id;

        -- --------------------------------------------------------
        -- 4. Create SequenceDeliveries from EmailLog
        -- --------------------------------------------------------
        INSERT INTO sequence_delivery (
            sequence_id, sequence_enrollment_id, sequence_step_id, contact_id,
            status, scheduled_at, sent_at, email_log_id,
            created_at, source
        )
        SELECT
            new_sequence_id,
            -- Match delivery to the best enrollment by (sequence_id, contact_id)
            (SELECT e.id FROM sequence_enrollment e
             WHERE e.sequence_id = new_sequence_id
               AND e.contact_id = el.contact_id
             ORDER BY e.entered_at DESC, e.id DESC
             LIMIT 1),
            ss.id,
            el.contact_id,
            CASE el.status
                WHEN 1 THEN 1   -- Sent
                ELSE 2          -- Failed
            END,
            el.created_at,
            CASE WHEN el.status = 1 THEN el.created_at ELSE NULL END,
            el.id,
            NOW(),
            COALESCE(NULLIF(el.source, ''), 'email_log: ' || el.id)
        FROM email_log el
        INNER JOIN sequence_step ss
            ON ss.sequence_id = new_sequence_id
           AND ss.email_template_id = el.template_id
        INNER JOIN contact c ON c.id = el.contact_id
        INNER JOIN sequence_enrollment se
            ON se.sequence_id = new_sequence_id
           AND se.contact_id = el.contact_id
        WHERE el.schedule_id = grp.schedule_id
          AND el.template_id IS NOT NULL
          AND el.contact_id IS NOT NULL
        ON CONFLICT (sequence_enrollment_id, sequence_step_id) DO NOTHING;

        -- --------------------------------------------------------
        -- 5. Update summary counters on the Sequence
        -- --------------------------------------------------------
        UPDATE sequence SET
            active_enrollment_count    = (SELECT COUNT(*) FROM sequence_enrollment WHERE sequence_id = new_sequence_id AND status = 0),
            completed_enrollment_count = (SELECT COUNT(*) FROM sequence_enrollment WHERE sequence_id = new_sequence_id AND status = 1),
            exited_enrollment_count    = (SELECT COUNT(*) FROM sequence_enrollment WHERE sequence_id = new_sequence_id AND status = 2),
            sent_count                 = (SELECT COUNT(*) FROM sequence_delivery   WHERE sequence_id = new_sequence_id AND status = 1),
            failed_count               = (SELECT COUNT(*) FROM sequence_delivery   WHERE sequence_id = new_sequence_id AND status = 2)
        WHERE id = new_sequence_id;

        -- --------------------------------------------------------
        -- 6. Update step-level counters
        -- --------------------------------------------------------
        UPDATE sequence_step ss SET
            scheduled_count = COALESCE(c.scheduled, 0),
            sent_count      = COALESCE(c.sent, 0),
            failed_count    = COALESCE(c.failed, 0),
            skipped_count   = COALESCE(c.skipped, 0)
        FROM (
            SELECT
                d.sequence_step_id,
                COUNT(*) FILTER (WHERE d.status = 0) AS scheduled,
                COUNT(*) FILTER (WHERE d.status = 1) AS sent,
                COUNT(*) FILTER (WHERE d.status = 2) AS failed,
                COUNT(*) FILTER (WHERE d.status = 3) AS skipped
            FROM sequence_delivery d
            WHERE d.sequence_id = new_sequence_id
            GROUP BY d.sequence_step_id
        ) c
        WHERE ss.id = c.sequence_step_id
          AND ss.sequence_id = new_sequence_id;

    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM sequence_delivery;
DELETE FROM sequence_enrollment;
DELETE FROM sequence_step;
DELETE FROM sequence;
");
        }
    }
}
