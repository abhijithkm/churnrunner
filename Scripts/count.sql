DECLARE @daterun AS DATETIME = dbo.DateOnly(GETDATE());
DECLARE @LocationCount INT;
DECLARE @HalfAsc INT;
DECLARE @HalfDesc INT;

SELECT
    @LocationCount = COUNT(LocationId)
FROM Locations WITH (NOLOCK)
WHERE IsAvailableOnDashboard = 1
  AND (bDeleted IS NULL OR bDeleted = 0)
  AND dtLastSync IS NOT NULL
  AND LocationId NOT IN (
      SELECT LocationId
      FROM CustomerUnlimitedData WITH (NOLOCK)
      WHERE dtRecord = @daterun - 1
        AND dtcreated > @daterun
  );

SET @HalfAsc = CEILING(@LocationCount / 2.0);
SET @HalfDesc = @LocationCount - @HalfAsc;

SELECT @LocationCount AS LocationCount,
       @HalfAsc AS HalfAsc,
       @HalfDesc AS HalfDesc;