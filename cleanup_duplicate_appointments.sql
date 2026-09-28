-- Script to identify and clean up duplicate appointments
-- Run this BEFORE applying the migration

-- 1. First, let's see what duplicates exist
SELECT 
    "EmployeeId", 
    "AppointmentDate", 
    "StartTime", 
    "EndTime",
    COUNT(*) as duplicate_count,
    STRING_AGG("Id"::text, ', ') as appointment_ids
FROM "Appointments" 
WHERE "IsCancelled" IS NULL OR NOT "IsCancelled"
GROUP BY "EmployeeId", "AppointmentDate", "StartTime", "EndTime"
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC;

-- 2. Show details of duplicate appointments
WITH duplicates AS (
    SELECT 
        "EmployeeId", 
        "AppointmentDate", 
        "StartTime", 
        "EndTime"
    FROM "Appointments" 
    WHERE "IsCancelled" IS NULL OR NOT "IsCancelled"
    GROUP BY "EmployeeId", "AppointmentDate", "StartTime", "EndTime"
    HAVING COUNT(*) > 1
)
SELECT 
    a."Id",
    a."EmployeeId",
    a."ApplicationUserId",
    a."AppointmentDate",
    a."StartTime",
    a."EndTime",
    a."CreatedAt",
    a."IsCancelled",
    a."IsRelatedAppointment"
FROM "Appointments" a
INNER JOIN duplicates d ON 
    a."EmployeeId" = d."EmployeeId" AND
    a."AppointmentDate" = d."AppointmentDate" AND
    a."StartTime" = d."StartTime" AND
    a."EndTime" = d."EndTime"
WHERE a."IsCancelled" IS NULL OR NOT a."IsCancelled"
ORDER BY a."EmployeeId", a."AppointmentDate", a."StartTime", a."EndTime", a."CreatedAt";

-- 3. OPTIONAL: Clean up duplicates by keeping the most recent one
-- WARNING: This will delete duplicate appointments. Review the results above first!
/*
WITH duplicates_to_remove AS (
    SELECT "Id"
    FROM (
        SELECT 
            "Id",
            "EmployeeId", 
            "AppointmentDate", 
            "StartTime", 
            "EndTime",
            ROW_NUMBER() OVER (
                PARTITION BY "EmployeeId", "AppointmentDate", "StartTime", "EndTime" 
                ORDER BY "CreatedAt" DESC
            ) as rn
        FROM "Appointments" 
        WHERE "IsCancelled" IS NULL OR NOT "IsCancelled"
    ) ranked
    WHERE rn > 1
)
DELETE FROM "Appointments" 
WHERE "Id" IN (SELECT "Id" FROM duplicates_to_remove);
*/ 