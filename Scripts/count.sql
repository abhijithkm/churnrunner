-- Pending locations for yesterday. The WHERE clause is copied exactly from the churn
-- scripts' location selection (including its unbracketed AND/OR), so LocationCount is
-- the number of locations the ASC and DESC scripts will actually pick.
DECLARE @daterun AS DATETIME = dbo.DateOnly(GETDATE());
DECLARE @LocationCount INT;
DECLARE @HalfAsc INT;
DECLARE @HalfDesc INT;

SELECT @LocationCount = COUNT(LocationId)
FROM Locations with (nolock) WHERE IsAvailableOnDashboard = 1 AND bDeleted IS NULL OR bDeleted = 0 and dtLastSync is not null
and locationid not in
(select locationid from CustomerUnlimitedData with(nolock) where dtRecord= @daterun - 1 and dtcreated> @daterun);

SET @HalfAsc = CEILING(@LocationCount / 2.0);
SET @HalfDesc = @LocationCount - @HalfAsc;

SELECT @LocationCount AS LocationCount,
       @HalfAsc AS HalfAsc,
       @HalfDesc AS HalfDesc;