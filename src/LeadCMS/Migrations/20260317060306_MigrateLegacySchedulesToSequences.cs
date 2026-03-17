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
    new_step_id INT;
    step_pos INT;
    schedule_json JSONB;
    timing_json JSONB;
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
    -- ============================================================

    FOR grp IN
        SELECT eg.id, eg.name, eg.language,
               es.id AS schedule_id, es.schedule
        FROM email_group eg
        INNER JOIN email_schedule es ON es.group_id = eg.id
    LOOP
        -- Idempotency: skip groups already migrated
        IF EXISTS (
            SELECT 1 FROM sequence
            WHERE source = 'migration'
              AND description = 'Migrated from legacy EmailGroup ID: ' || grp.id
                                || ', name: ' || grp.name || ' (' || grp.language || ')'
        ) THEN
            CONTINUE;
        END IF;

        schedule_json := grp.schedule::jsonb;

        -- --------------------------------------------------------
        -- 1. Create the Sequence
        -- --------------------------------------------------------
        INSERT INTO sequence (
            name, description, status,
            stop_on_reply, use_contact_time_zone, time_zone,
            active_enrollment_count, completed_enrollment_count, exited_enrollment_count,
            sent_count, failed_count,
            enrollment, utm_parameters,
            created_at, source
        ) VALUES (
            grp.name || ' (' || grp.language || ')',
            'Migrated from legacy EmailGroup ID: ' || grp.id
                || ', name: ' || grp.name || ' (' || grp.language || ')',
            CASE
                WHEN EXISTS (
                    SELECT 1 FROM contact_email_schedule ces
                    WHERE ces.schedule_id = grp.schedule_id AND ces.status = 0
                ) THEN 1  -- Active
                ELSE 2    -- Paused
            END,
            false, false, 0,
            0, 0, 0, 0, 0,
            NULL, NULL,
            NOW(), 'migration'
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
                SELECT et.id, et.name
                FROM email_template et
                WHERE et.email_group_id = grp.id
                ORDER BY et.id
            LOOP
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
                    sequence_id, email_template_id, position, step_key, type, title,
                    timing, created_at, source
                ) VALUES (
                    new_sequence_id, tmpl.id, step_pos, 'step-' || step_pos,
                    0, tmpl.name,
                    timing_json, NOW(), 'migration'
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
                SELECT et.id, et.name
                FROM email_template et
                WHERE et.email_group_id = grp.id
                ORDER BY et.id
            LOOP
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
                    sequence_id, email_template_id, position, step_key, type, title,
                    timing, created_at, source
                ) VALUES (
                    new_sequence_id, tmpl.id, step_pos, 'step-' || step_pos,
                    0, tmpl.name,
                    timing_json, NOW(), 'migration'
                );

                step_pos := step_pos + 1;
            END LOOP;
        END IF;

        -- --------------------------------------------------------
        -- 3. Create SequenceEnrollments from ContactEmailSchedule
        -- --------------------------------------------------------
        INSERT INTO sequence_enrollment (
            sequence_id, contact_id, status, last_completed_step_key,
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
                WHEN sent_counts.cnt > 0 THEN 'step-' || (sent_counts.cnt - 1)
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
            'Migrated from legacy EmailGroup: ' || grp.name,
            NOW(),
            'migration'
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
            sequence_id, sequence_step_id, contact_id,
            status, scheduled_at, sent_at, email_log_id,
            created_at, source
        )
        SELECT
            new_sequence_id,
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
            'migration'
        FROM email_log el
        INNER JOIN sequence_step ss
            ON ss.sequence_id = new_sequence_id
           AND ss.email_template_id = el.template_id
        INNER JOIN contact c ON c.id = el.contact_id
        WHERE el.schedule_id = grp.schedule_id
          AND el.template_id IS NOT NULL
          AND el.contact_id IS NOT NULL
        ON CONFLICT (sequence_id, sequence_step_id, contact_id) DO NOTHING;

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

    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all migrated data (identifiable by source = 'migration')
            migrationBuilder.Sql(@"
DELETE FROM sequence_delivery   WHERE source = 'migration';
DELETE FROM sequence_enrollment WHERE source = 'migration';
DELETE FROM sequence_step       WHERE source = 'migration';
DELETE FROM sequence            WHERE source = 'migration';
");
        }
    }
}
