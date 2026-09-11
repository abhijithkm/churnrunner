--Mister ascending populate
-- Runs as a plain batch (no CREATE PROCEDURE permission in this DB) -
-- {COUNT_ASC} is replaced with the real half-count as a plain integer
-- literal before this script is sent to the server; see ChurnJobRunner.
DECLARE @Half INT = {COUNT_ASC};
    DECLARE @Count AS INT, @Counter AS INT, @LocationIds AS UNIQUEIDENTIFIER, @TimezoneOffset AS INT;
    DECLARE @daterun AS DATETIME = dbo.DateOnly(GETDATE());
    SELECT @daterun,
           @daterun - 1;
    DECLARE @Locations TABLE (
        ID             INT              IDENTITY (1, 1),
        LocationId     UNIQUEIDENTIFIER,
        TimezoneOffset INT             );
    DECLARE @LocationCount AS INT;
    SELECT @LocationCount = COUNT(LocationId)
    FROM   Locations WITH (NOLOCK)
    WHERE  IsAvailableOnDashboard = 1
           AND (bDeleted IS NULL
                OR bDeleted = 0)
           AND dtLastSync IS NOT NULL
           AND LocationId NOT IN (SELECT LocationId
                                  FROM   CustomerUnlimitedData WITH (NOLOCK)
                                  WHERE  dtRecord = @daterun - 1
                                         AND dtcreated > @daterun);
    SELECT @LocationCount AS LocationCount,
           @Half AS Half;
    INSERT INTO @Locations (LocationId, TimezoneOffset)
    SELECT   TOP (@Half) LocationId,
                         UTCOffset
    FROM     Locations WITH (NOLOCK)
    WHERE    IsAvailableOnDashboard = 1
             AND bDeleted IS NULL
             OR bDeleted = 0
                AND dtLastSync IS NOT NULL
                AND locationid NOT IN (SELECT locationid
                                       FROM   CustomerUnlimitedData WITH (NOLOCK)
                                       WHERE  dtRecord = @daterun - 1
                                              AND dtcreated > @daterun)
    ORDER BY scode ASC;
    SELECT @Count = Count(ID)
    FROM   @Locations;
    SET @Counter = 1;
    SELECT @Count;

    -- Global temp table (tempdb) so a separate polling connection can read live
    -- progress without needing schema-change rights on this database.
    -- ASC and DESC run as two concurrent connections; both can see OBJECT_ID() as NULL
    -- at the same instant and both attempt CREATE TABLE, so whichever loses that race
    -- must swallow "already exists" (2714) rather than fail the whole run over it.
    IF OBJECT_ID('tempdb..##ChurnRunProgress') IS NULL
    BEGIN
        BEGIN TRY
            CREATE TABLE ##ChurnRunProgress (
                RunLabel       NVARCHAR(10) NOT NULL PRIMARY KEY,
                CurrentIndex   INT          NOT NULL,
                TotalCount     INT          NOT NULL,
                LastLocationId UNIQUEIDENTIFIER NULL,
                Status         NVARCHAR(20) NOT NULL,
                UpdatedAt      DATETIME     NOT NULL
            );
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() <> 2714
                THROW;
        END CATCH
    END

    IF EXISTS (SELECT 1 FROM ##ChurnRunProgress WHERE RunLabel = 'ASC')
        UPDATE ##ChurnRunProgress
        SET    CurrentIndex = 0, TotalCount = @Count, LastLocationId = NULL, Status = 'Running', UpdatedAt = GETUTCDATE()
        WHERE  RunLabel = 'ASC';
    ELSE
        INSERT INTO ##ChurnRunProgress (RunLabel, CurrentIndex, TotalCount, LastLocationId, Status, UpdatedAt)
        VALUES ('ASC', 0, @Count, NULL, 'Running', GETUTCDATE());
    WHILE (@Counter <= @Count)
        BEGIN
            WAITFOR DELAY '000:00:00.040';
            SELECT @LocationIds = LocationId,
                   @TimezoneOffset = TimezoneOffset
            FROM   @Locations
            WHERE  ID = @Counter;

            DECLARE @ProgressMsg NVARCHAR(400) = '[' + CAST(@Counter AS NVARCHAR(10)) + '/' + CAST(@Count AS NVARCHAR(10)) + '] Processing location (' + CAST(@LocationIds AS NVARCHAR(50)) + ')';
            RAISERROR('%s', 0, 1, @ProgressMsg) WITH NOWAIT;
            UPDATE ##ChurnRunProgress
            SET    CurrentIndex = @Counter, LastLocationId = @LocationIds, UpdatedAt = GETUTCDATE()
            WHERE  RunLabel = 'ASC';
            DECLARE @intEndOfDaySave AS INT = 1, @LocationId AS UNIQUEIDENTIFIER = @LocationIds, @DateTimeOffset AS INT = @TimezoneOffset;
            DECLARE @RecordId AS INT, @TotalMembers AS INT, @ActiveMemberss AS INT, @InActiveMemberss AS INT, @StartDate AS DATETIME;
            DECLARE @MinDateDate AS DATETIME, @DateDate AS DATETIME, @intFirstTime AS INT = NULL, @sDay AS NVARCHAR (50);
            DECLARE @LocalDate AS DATETIME;
            SELECT @LocalDate = DATEADD(minute, @DateTimeOffset, GETUTCDATE());
            SET @sDay = CONVERT (NVARCHAR (50), @LocalDate, 101);
            SET @MinDateDate = @daterun - 1;
            SET @StartDate = @MinDateDate;
            SELECT @StartDate,
                   @LocationIds;
            CREATE TABLE #NewReportData (
                LocationId UNIQUEIDENTIFIER,
                sDay NVARCHAR (50), --DONE      
                iSignUp INT, --DONE      
                iDropOff INT,
                iGrowth INT,
                iTotalMembers INT,
                iActiveMembers INT,
                iInActiveMembers INT,
                fMembershipRevenue FLOAT, --DONE          
                iCarsWashed INT, --DONE      
                fTotalWashRevenue FLOAT, --DONE      
                iMonthlyWashed INT, --DONE      
                fMonthlyWashes FLOAT,
                bEstimatedValues BIT,
                fRecurringRevenue FLOAT,
                iRecurringCharges INT,
                --iAvgDaysActive int,      
                iNewSignUp INT,
                iOldSignUp INT,
                iCancel INT,
                iDecline INT,
                iExpired INT,
                iMembersAdded INT,
                iMembersRemoved INT,
                iActive1Month INT,
                iActive2Month INT,
                iActive3Month INT,
                iActive4Month INT,
                iActive5Month INT,
                iActive6Month INT,
                iActive7Month INT,
                iActive8Month INT,
                iActive9Month INT,
                iActive10Month INT,
                iActive11Month INT,
                iActive12Month INT,
                iCancelEndsCurrentMonth INT,
                iCancelEndsNextMonth INT,
                dblAvgCustomerLifetime FLOAT,
                iHouseWashed INT, --, iPrepaidWashed int      
                WACreated INT,
                ImportCreated INT,
                VolumePerDay INT,
                VolumePerHour INT,
                VolumeHour INT,
                dblSalesAmount FLOAT,
                fRedeemWash FLOAT,
                actualactivemembers INT,
                iLocationChange INT
            );
            --DELETE @NewReportData      
            CREATE TABLE #TicketsDetailsTable (
                DATE DATETIME,
                TicketGuid BIGINT,
                RefID INT,
                Description NVARCHAR (255),
                QTY FLOAT,
                locationid UNIQUEIDENTIFIER,
                Customerguid BIGINT,
                smonthlylocation VARCHAR (MAX)
            );
            CREATE TABLE #TempTransactions (
                DATE DATE,
                sTransactionType VARCHAR (MAX),
                lTransactionTypeId INT,
                iCurrentActivePeriods INT,
                dtCreated DATETIME,
                dtMonthlyNextPay DATETIME,
                iTotalActivePeriods INT,
                smonthlylocation VARCHAR (MAX),
                Customerguid BIGINT
            );
            CREATE TABLE #tblCategory (
                CategoryID INT
            );
            CREATE TABLE #tblTerminalNameWithoutLocation (
                TerminalName NVARCHAR (255)
            );
            CREATE TABLE #TicketsDetailsSalesTable (
                DATE DATETIME,
                TicketGuid BIGINT,
                RefID INT,
                Description NVARCHAR (255),
                QTY FLOAT,
                locationid UNIQUEIDENTIFIER
            );
            CREATE TABLE #TempLocationChangeTransactions (
                DATE DATE,
                sTransactionType VARCHAR (MAX),
                lTransactionTypeId INT,
                iCurrentActivePeriods INT,
                dtCreated DATETIME,
                dtMonthlyNextPay DATETIME,
                iTotalActivePeriods INT,
                smonthlylocation VARCHAR (MAX),
                Customerguid BIGINT
            );
            DECLARE @MinDate AS DATETIME, @Date AS DATETIME;
            DECLARE @ActiveMembers AS INT, @InActiveMembers AS INT, @AvgDaysActive AS INT;
            DECLARE @TotalMembersToday AS INT, @ActiveMembersToday AS INT, @InActiveMembersToday AS INT;
            DECLARE @SignUp AS INT, @NewSignUp AS INT, @OldSignUp AS INT;
            DECLARE @DropOff AS INT, @Cancel AS INT, @Decline AS INT, @Expired AS INT, @MembersAdded AS INT, @MembersRemoved AS INT;
            DECLARE @CustomerLifeTimeLength AS NVARCHAR (50);
            DECLARE @Months AS INT, @Days AS INT;
            -- DECLARE @intFirstTime int, @sDay nvarchar(50)      
            DECLARE @Lasted1Month AS INT, @Lasted2Month AS INT, @Lasted3Month AS INT, @Lasted4Month AS INT, @Lasted5Month AS INT;
            DECLARE @Lasted6Month AS INT, @Lasted7Month AS INT, @Lasted8Month AS INT, @Lasted9Month AS INT, @Lasted10Month AS INT;
            DECLARE @Lasted11Month AS INT, @Lasted12Month AS INT;
            DECLARE @CancelEndsCurrentMonth AS INT, @CancelEndsNextMonth AS INT, @AvgCustomerLifetime AS FLOAT, @HouseWashed AS INT; --, @PrepaidWashed int      
            DECLARE @LocationCode AS NVARCHAR (50);
            DECLARE @TotalSignUp AS INT, @ReactiveMembers AS INT, @WACreated AS INT, @ImportedCount AS INT, @offset AS INT, @tStartDate AS DATETIME = @StartDate, @tLocationid AS UNIQUEIDENTIFIER = @LocationId, @ipaymentDeclineAttempts AS INT = 3;
            DECLARE @VolumePerDay AS INT;
            DECLARE @VolumePerHour AS INT;
            DECLARE @VolumeHour AS INT;
            DECLARE @dblSalesAmount AS DECIMAL (18, 2);
            DECLARE @previousdayactivemember AS INT, @previousDay AS DATETIME = NULL;
            DECLARE @MembersAddedByLocationchange AS INT, @MembersRemovedByLocationchange AS INT;
            CREATE TABLE #ticketDataFullTemp (
                Date DATETIME,
                ShiftNum INT,
                TicketID INT,
                TicketNum NVARCHAR (25),
                bPaid INT,
                bReversed INT,
                RefID INT,
                Description NVARCHAR (255),
                QTY FLOAT,
                BaseAmt FLOAT,
                NetAmt FLOAT,
                CarCount FLOAT,
                Extra FLOAT,
                GreeterID INT,
                CashierID INT,
                GreeterTerminalName NVARCHAR (50),
                CashierTerminalName NVARCHAR (50),
                TableLookUp NVARCHAR (25),
                CategoryID INT,
                InvCategoryID INT,
                CategoryName NVARCHAR (255),
                InvCategoryName NVARCHAR (255),
                PID INT,
                AccountCodeID INT,
                GSPrintOrder INT,
                TicketTaxed BIT,
                LocationId UNIQUEIDENTIFIER,
                Reference_GUID BIGINT,
                Ticketguid BIGINT
            );
            CREATE TABLE #lsdRefunds (
                Date DATETIME,
                ShiftNum INT,
                RefundID INT,
                PaymentTypeID INT,
                PaymentDescription NVARCHAR (50),
                TicketID INT,
                TicketNum NVARCHAR (25),
                bReversed INT,
                RefID INT,
                Description NVARCHAR (50),
                QTY FLOAT,
                BaseAmt FLOAT,
                NetAmt FLOAT,
                CarCount FLOAT,
                Extra FLOAT,
                GreeterID INT,
                CashierID INT,
                TerminalName NVARCHAR (50),
                TableLookUp NVARCHAR (25),
                CategoryID INT,
                InvCategoryID INT,
                CategoryName NVARCHAR (50),
                InvCategoryName NVARCHAR (50),
                PID INT,
                AccountCodeID INT,
                GSPrintOrder INT,
                locationid UNIQUEIDENTIFIER,
                TicketGuid BIGINT,
                Customerguid BIGINT
            );
            --declare @MonthlyFrequency int      
            SET @StartDate = @tStartDate;
            SET @LocationId = @tLocationid;
            DELETE #tblTerminalNameWithoutLocation;
            DELETE #tblCategory;
            INSERT INTO #tblTerminalNameWithoutLocation
            SELECT TerminalName
            FROM   fGetTerminalNamesList('All');
            INSERT INTO #tblCategory
            SELECT CategoryID
            FROM   fGetCategoryList('All');
            SELECT @LocationCode = sCode,
                   @offset = UTCOffset,
                   @ipaymentDeclineAttempts = ISNULL(iAutoChargeDeclineAttempts, 3)
            FROM   Locations WITH (NOLOCK)
            WHERE  LocationId = @LocationId;

            --declare @LocalDate datetime
            --SELECT @LocalDate =  DATEADD(minute,@offset,GETUTCDATE())      
            --SET @sDay = convert(nvarchar(50),@LocalDate,101)    
            /* Get current active and inactive members */
            /* Get current active and inactive members */
            SELECT @ActiveMembers = (SELECT Count(1)
                                     FROM   vwCustomerDetails AS cd
                                     WHERE  ISNULL(bDeleted, 0) = 0
                                            AND (bmonthlyactive = 1
                                                 OR (bmonthlyactive = 0
                                                     AND bmonthlydeclined = 1
                                                     AND ipaymentattempts <= @ipaymentDeclineAttempts))
                                            AND smonthlylocation = @LocationCode
                                            AND --and (bmonthlyhold = 0 or bmonthlyhold is null or bmonthlyhold = 1)         
                                            lmonthlyaccounttypeid > 0
                                            AND EXISTS (SELECT 1
                                                        FROM   VIPAccountTypes
                                                        WHERE  ISNULL(bDeleted, 0) = 0
                                                               AND lAccountType = cd.lMonthlyAccountTypeId
                                                               AND ISNULL(bMonthly, 0) = 1
                                                               AND ((lChargeFrequency IS NULL
                                                                     AND iDaysToExpire > 0)
                                                                    OR (lChargeFrequency > 0))));
            --
            DECLARE @tablename AS NVARCHAR (100) = '', @resultid AS BIGINT, @query AS NVARCHAR (MAX) = '';
            SET @tablename = 'MisterCarWashDFTest..vwCustomerDetails_bkp' + CAST (MONTH(getutcdate()) AS NVARCHAR (100)) + '_' + CAST (day(getutcdate()) AS NVARCHAR (100)) + '_' + CAST (Year(getutcdate()) AS NVARCHAR (100)) + '_2';
            SELECT @resultid = OBJECT_ID(@tablename, 'U');
            IF (isnull(@resultid, 0) = 0)
                BEGIN
                    SET @query = '
select  CustomerGUID
,lcustomerid
,smonthlylocation
,bmonthlyactive
,bmonthlydeclined
,ipaymentattempts
,lmonthlyaccounttypeid
,lCustomerCancelReasonId into ' + @tablename + '   from vwCustomerDetails cd where  ISNULL(bDeleted, 0) = 0 AND    
(bmonthlyactive = 1 or (bmonthlyactive = 0 and bmonthlydeclined = 1 and ipaymentattempts <= ' + CAST (@ipaymentDeclineAttempts AS NVARCHAR (100)) + ')  
) and smonthlylocation = ''' + @LocationCode + ''' and lmonthlyaccounttypeid > 0 AND Exists(SELECT 1 FROM VIPAccountTypes WHERE ISNULL(bDeleted, 0) = 0 AND  
lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1 AND ((lChargeFrequency IS NULL AND iDaysToExpire > 0) OR (lChargeFrequency > 0)))
    ';
                END
            ELSE
                BEGIN
                    SET @query = 'INSERT INTO ' + @tablename + '



select  CustomerGUID
,lcustomerid
,smonthlylocation
,bmonthlyactive
,bmonthlydeclined
,ipaymentattempts
,lmonthlyaccounttypeid
,lCustomerCancelReasonId  from vwCustomerDetails cd where  ISNULL(bDeleted, 0) = 0 AND    
(bmonthlyactive = 1 or (bmonthlyactive = 0 and bmonthlydeclined = 1 and ipaymentattempts <= ' + CAST (@ipaymentDeclineAttempts AS NVARCHAR (100)) + ')  
)  and smonthlylocation = ''' + @LocationCode + ''' and lmonthlyaccounttypeid > 0 AND Exists(SELECT 1 FROM VIPAccountTypes WHERE ISNULL(bDeleted, 0) = 0 AND  
lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1 AND ((lChargeFrequency IS NULL AND iDaysToExpire > 0) OR (lChargeFrequency > 0)))
    ';
                END
            EXECUTE (@query);
            --
            SELECT @InActiveMembers = (SELECT Count(1)
                                       FROM   Customers AS c WITH (NOLOCK)
                                              INNER JOIN
                                              CustomerDetails AS cd WITH (NOLOCK)
                                              ON c.CustomerGUID = cd.CustomerGUID
                                       WHERE  ISNULL(c.bdeleted, 0) = 0
                                              AND bmonthlyactive = 0
                                              AND ((bMonthlyDeclined = 0
                                                    OR bmonthlydeclined IS NULL)
                                                   OR (bMonthlyDeclined = 1
                                                       AND ipaymentattempts > @ipaymentDeclineAttempts))
                                              AND c.CustomerGUID IN (SELECT CustomerGUID
                                                                     FROM   CustomerUnlimitedTransactions WITH (NOLOCK)
                                                                     WHERE  bMonthlyActive = 1)
                                              AND smonthlylocation = @LocationCode);
            IF @ActiveMembers = NULL
                SET @ActiveMembers = 0;
            IF @InActiveMembers = NULL
                SET @InActiveMembers = 0;
            SET @ActiveMembersToday = @ActiveMembers;
            SET @InActiveMembersToday = @InActiveMembers;
            SET @TotalMembersToday = @ActiveMembersToday + @InActiveMembersToday;
            /* Get current active and inactive members */
            SET @MinDate = @StartDate;
            /* Generate new data */
            IF @MinDate IS NOT NULL
                BEGIN
                    SET @Date = @sDay;
                    INSERT INTO #TicketsDetailsTable (DATE, TicketGuid, RefID, Description, QTY, locationid, Customerguid)
                    SELECT   CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Tickets.dtCreated), 101) AS DATE),
                             TicketsDetails.TicketGUID,
                             TicketsDetails.Reference_ID AS RefID,
                             TicketsDetails.sDescription AS Description,
                             SUM(TicketsDetails.dblQTY) AS QTY,
                             TicketsDetails.LocationId,
                             Tickets.Customerguid
                    FROM     TicketsDetails WITH (NOLOCK)
                             --JOIN #locations l on l.locationId = TicketsDetails.LocationId           
                             LEFT OUTER JOIN
                             Tickets WITH (NOLOCK)
                             ON TicketsDetails.TicketGUID = Tickets.TicketGUID
                             LEFT OUTER JOIN
                             Surcharges
                             ON TicketsDetails.Reference_ID = Surcharges.Surcharge_ID
                             LEFT OUTER JOIN
                             Categories
                             ON Surcharges.lCategoryID = Categories.lCategoryID
                    WHERE    CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Tickets.dtCreated), 101) AS DATE) BETWEEN CAST (@MinDate AS DATE) AND CAST (@Date AS DATE)
                             AND Tickets.bPaid = 1
                             AND TicketsDetails.bPaid = 1
                             AND (Tickets.bReversed = 0
                                  OR Tickets.bReversed IS NULL)
                             AND (Tickets.bDeleted = 0
                                  OR Tickets.bDeleted IS NULL)
                             AND TicketsDetails.LocationId = @LocationId
                             AND -- MLASW-1711    
                             ((TicketsDetails.Detail_Type_ID = 3 -- AND Tickets.LocationId = @locationId               
                               AND EXISTS (SELECT 1
                                           FROM   vw_ssp_VIPAccountTypes AS vip
                                                  INNER JOIN
                                                  Surcharges
                                                  ON (vip.lSurchargeId = Surcharges.Surcharge_ID
                                                      OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
                                                  INNER JOIN
                                                  Categories
                                                  ON Surcharges.lCategoryID = Categories.lCategoryID
                                           WHERE  (vip.lSurchargeId = TicketsDetails.Reference_ID
                                                   OR vip.lFamilySurchargeId = TicketsDetails.Reference_ID)
                                                  AND ISNULL(lSurchargeId, 0) > 0
                                                  AND ISNULL(bMonthly, 0) = 1
                                                  AND (Categories.bExcludeFromDaily = 0
                                                       OR Categories.bExcludeFromDaily IS NULL)))
                              OR (TicketsDetails.Detail_Type_ID = 14
                                  AND TicketsDetails.sdescription LIKE '%ReActivation%'
                                  AND TicketsDetails.dblAmount >= 0
                                  AND EXISTS (SELECT 1
                                              FROM   vw_ssp_VIPAccountTypes AS vip
                                                     INNER JOIN
                                                     Surcharges
                                                     ON (vip.lSurchargeId = Surcharges.Surcharge_ID
                                                         OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
                                                     INNER JOIN
                                                     Categories
                                                     ON Surcharges.lCategoryID = Categories.lCategoryID
                                              WHERE  vip.lAccountType = TicketsDetails.Reference_ID
                                                     AND ISNULL(lSurchargeId, 0) > 0
                                                     AND ISNULL(bMonthly, 0) = 1
                                                     AND (Categories.bExcludeFromDaily = 0
                                                          OR Categories.bExcludeFromDaily IS NULL))))
                             AND ISNULL(Tickets.luserid, 0) <> 9999
                    GROUP BY CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Tickets.dtCreated), 101) AS DATE), TicketsDetails.LocationId, TicketsDetails.Detail_Type_ID, TicketsDetails.Reference_ID, TicketsDetails.sDescription, CASE WHEN TicketsDetails.Detail_Type_ID IN (11, 14) THEN TicketsDetails.dblAmount ELSE NULL END, TicketsDetails.TicketGUID, Tickets.Customerguid;
                    --Refund data   
                    DECLARE @StartUtc AS DATETIME = DATEADD(minute, -1 * @offset, @MinDate), @EndUtc AS DATETIME = GETUTCDATE();
                    INSERT INTO #ticketDataFullTemp
                    EXECUTE [dbo].[A_GetTicketDataFullChurn] @StartUtc, @EndUtc, 'ALL', 'ALL', 1, @LocationId;
                    INSERT INTO #lsdRefunds
                    SELECT DATE,
                           shiftnum,
                           Refundid,
                           PaymentTypeID,
                           PaymentDescription,
                           TicketID,
                           TicketNum,
                           bReversed,
                           RefID,
                           Description,
                           QTY,
                           BaseAmt,
                           NetAmt,
                           CarCount,
                           Extra,
                           GreeterID,
                           CashierID,
                           TerminalName,
                           TableLookUp,
                           CategoryID,
                           InvCategoryID,
                           CategoryName,
                           InvCategoryName,
                           PID,
                           AccountCodeID,
                           GSPrintOrder,
                           locationid,
                           TicketGuid,
                           Customerguid
                    FROM   fRefundDataFullChurn(@StartUtc, @EndUtc, 'ALL', 'ALL', @LocationId) AS tdf;
                    INSERT INTO #TicketsDetailsTable (DATE, TicketGuid, RefID, Description, QTY, locationid, Customerguid)
                    SELECT CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS DATE),
                           ticketguid,
                           Refid AS RefID,
                           Description AS Description,
                           QTY * -1 AS QTY,
                           LocationId,
                           Customerguid
                    FROM   #lsdRefunds
                    WHERE  ((tablelookup = 'Surcharges'
                             AND EXISTS (SELECT 1
                                         FROM   VIPAccountTypes AS vip
                                                INNER JOIN
                                                Surcharges
                                                ON (vip.lSurchargeId = Surcharges.Surcharge_ID
                                                    OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
                                                INNER JOIN
                                                Categories
                                                ON Surcharges.lCategoryID = Categories.lCategoryID
                                         WHERE  Surcharges.Surcharge_ID = refid
                                                AND ISNULL(Surcharges.Surcharge_ID, 0) > 0
                                                AND ISNULL(bMonthly, 0) = 1
                                                AND (Categories.bExcludeFromDaily = 0
                                                     OR Categories.bExcludeFromDaily IS NULL)))
                            OR (tablelookup = 'Unlimited (autocharge)'
                                AND Description LIKE '%ReActivation%'
                                AND NetAmt >= 0
                                AND EXISTS (SELECT 1
                                            FROM   VIPAccountTypes AS vip
                                                   INNER JOIN
                                                   Surcharges
                                                   ON (vip.lSurchargeId = Surcharges.Surcharge_ID
                                                       OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
                                                   INNER JOIN
                                                   Categories
                                                   ON Surcharges.lCategoryID = Categories.lCategoryID
                                            WHERE  vip.lAccountType = refid
                                                   AND ISNULL(lSurchargeId, 0) > 0
                                                   AND ISNULL(bMonthly, 0) = 1
                                                   AND (Categories.bExcludeFromDaily = 0
                                                        OR Categories.bExcludeFromDaily IS NULL))))
                           AND ISNULL(ticketguid, 0) <> 0;
                    UPDATE CustomerUnlimitedTransactions
                    SET    lTransactionTypeId = 8
                    WHERE  sTransactionType = 'AUTOCHARGE EXPIRED'
                           AND lTransactionTypeId IS NULL;
                    INSERT INTO #TempTransactions
                    SELECT CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtcreated), 101) AS Date,
                           sTransactionType,
                           lTransactionTypeId,
                           iCurrentActivePeriods,
                           dtCreated,
                           dtMonthlyNextPay,
                           iTotalActivePeriods,
                           smonthlylocation,
                           Customerguid
                    FROM   customerunlimitedtransactions AS cut WITH (NOLOCK)
                    WHERE  CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtcreated), 101) AS DATE) BETWEEN CAST (@MinDate AS DATE) AND CAST (@Date AS DATE)
                           AND smonthlylocation = @LocationCode --LocationId = @LocationId      
                           AND sTransactionType <> 'SYNC UPDATE';
                    INSERT INTO #TempLocationChangeTransactions
                    SELECT CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtcreated), 101) AS Date,
                           sTransactionType,
                           lTransactionTypeId,
                           iCurrentActivePeriods,
                           dtCreated,
                           dtMonthlyNextPay,
                           iTotalActivePeriods,
                           smonthlylocation,
                           Customerguid
                    FROM   customerunlimitedtransactions AS cut WITH (NOLOCK)
                    WHERE  CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtcreated), 101) AS DATE) BETWEEN CAST (@MinDate AS DATE) AND CAST (@Date AS DATE)
                           AND sReferenceNumber = @LocationCode --LocationId = @LocationId      
                           AND lTransactionTypeId = 9;
                    --select * from #TempTransactions
                    /*volume per day- getting sale tickets*/
                    INSERT INTO #ticketDataFullTemp (Date, ShiftNum, TicketID, TicketNum, bPaid, bReversed, RefID, Description, QTY, BaseAmt, NetAmt, CarCount, Extra, GreeterID, CashierID, GreeterTerminalName, CashierTerminalName, TableLookUp, CategoryID, InvCategoryID, CategoryName, InvCategoryName, PID, AccountCodeID, GSPrintOrder, LocationId, Ticketguid)
                    SELECT Date,
                           ShiftNum,
                           TicketID,
                           TicketNum,
                           1,
                           bReversed,
                           RefID,
                           Description,
                           QTY * -1,
                           -1 * BaseAmt,
                           -1 * NetAmt,
                           CarCount * -1,
                           Extra,
                           GreeterID,
                           CashierID,
                           TerminalName,
                           TerminalName,
                           CASE WHEN TableLookUp = 'Unlimited (autocharge)'
                                     AND Description LIKE '%ReActivation%' THEN 'Unlimited (Reactivation)' ELSE TableLookUp END AS TableLookUp,
                           CategoryID,
                           InvCategoryID,
                           CategoryName,
                           InvCategoryName,
                           PID,
                           AccountCodeID,
                           GSPrintOrder,
                           LocationId,
                           TicketGuid
                    FROM   #lsdRefunds;
                    UPDATE td
                    SET    customerguid = t.customerguid
                    FROM   #TicketsDetailsTable AS td
                           INNER JOIN
                           tickets AS t WITH (NOLOCK)
                           ON t.ticketguid = td.ticketguid
                    WHERE  isnull(td.customerguid, 0) = 0;
                    UPDATE td
                    SET    smonthlylocation = cd.sMonthlyLocation
                    FROM   #TicketsDetailsTable AS td
                           INNER JOIN
                           vwCustomerDetails AS cd WITH (NOLOCK)
                           ON cd.CustomerGUID = td.Customerguid;
                    INSERT INTO #TicketsDetailsSalesTable
                    SELECT   DATEADD(minute, @offset, Date) AS date,
                             TicketGUID,
                             RefID,
                             Description,
                             SUM(qty) AS QTY,
                             LocationId
                    FROM     #ticketDataFullTemp
                    WHERE    CarCount = 1
                    GROUP BY DATEADD(minute, @offset, Date), TicketGUID, RefID, Description, LocationId;
                    SET @previousDay = DATEADD(day, -1, @MinDate);
                    DECLARE @MinDateStart AS DATETIME = @MinDate, @dateend AS DATETIME = @Date, @ActualActivemembers AS INT;
                    SELECT @previousdayactivemember = iActiveMembers
                    FROM   CustomerUnlimitedData
                    WHERE  locationid = @LocationId
                           AND dtRecord = @previousDay;
                    SET @ActualActivemembers = @ActiveMembers;
                    SET @ActiveMembers = @previousdayactivemember;
                    WHILE @MinDateStart <= @dateend
                        BEGIN
                            SET @Date = @MinDateStart;
                            SET @DropOff = 0;
                            SET @SignUp = 0;
                            SET @AvgDaysActive = 0;
                            SET @NewSignUp = 0;
                            SET @OldSignUp = 0;
                            SET @Cancel = 0;
                            SET @Decline = 0;
                            SET @Expired = 0;
                            SET @MembersAdded = 0;
                            SET @MembersRemoved = 0;
                            SET @Lasted1Month = 0;
                            SET @Lasted2Month = 0;
                            SET @Lasted3Month = 0;
                            SET @Lasted4Month = 0;
                            SET @Lasted5Month = 0;
                            SET @Lasted6Month = 0;
                            SET @Lasted7Month = 0;
                            SET @Lasted8Month = 0;
                            SET @Lasted9Month = 0;
                            SET @Lasted10Month = 0;
                            SET @Lasted11Month = 0;
                            SET @Lasted12Month = 0;
                            SET @CancelEndsCurrentMonth = 0;
                            SET @CancelEndsNextMonth = 0;
                            SET @ReactiveMembers = 0;
                            SET @TotalSignUp = 0;
                            SET @WACreated = 0;
                            SET @ImportedCount = 0;
                            SET @VolumePerDay = 0;
                            SET @VolumeHour = 0;
                            SET @VolumePerHour = 0;
                            SET @MembersRemovedByLocationchange = 0;
                            SET @MembersAddedByLocationchange = 0;
                            SELECT @TotalSignUp = sum(QTY)
                            FROM   #TicketsDetailsTable
                            WHERE  Date = CAST (@Date AS DATETIME)
                                   AND smonthlylocation = @LocationCode;
                            SELECT @ReactiveMembers = sum(QTY)
                            FROM   #TicketsDetailsTable
                            WHERE  Description LIKE '%ReActivation%'
                                   AND Date = CAST (@Date AS DATETIME)
                                   AND smonthlylocation = @LocationCode;
                            -- SELECT @VolumePerHour =Max(QTY) FROM #TicketsDetailsSalesTable  WHERE CAST(CONVERT(NVARCHAR(50), Date, 101) as Date) = CAST(@Date as DATE)   
                            -- select top 1 @VolumePerHour=MAX(QTY),@VolumeHour = DATEPART(HH,Date)  from #TicketsDetailsSalesTable  
                            -- WHERE CAST(CONVERT(NVARCHAR(50), Date, 101) as Date) = CAST(@Date as DATE)  
                            --group by DATEPART(HH,Date)   order by  MAX(QTY) desc  
                            SELECT   TOP 1 @VolumePerHour = MAX(QTY),
                                           @VolumeHour = t.VolumeHour
                            FROM     (SELECT   SUM(QTY) AS qty,
                                               DATEPART(HH, Date) AS VolumeHour
                                      FROM     #TicketsDetailsSalesTable
                                      WHERE    CAST (CONVERT (NVARCHAR (50), Date, 101) AS DATE) = CAST (@Date AS DATE)
                                      GROUP BY DATEPART(HH, Date)) AS t
                            GROUP BY t.VolumeHour
                            ORDER BY MAX(QTY) DESC;
                            -- GET WA CREATED AND IMPORTED CUSTOMERS    
                            SELECT @WACreated = SUM(CASE WHEN CONVERT (VARCHAR (30), CustomerGUID) LIKE '999%' THEN 1 ELSE 0 END)
                            FROM   --,       
                                   --@ImportedCount = SUM(CASE WHEN sUpdatedBy like'%IMPORT%' THEN 1 ELSE 0 END)       
                                   vwCustomerDetails AS cd WITH (NOLOCK)
                            WHERE  CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtCreated), 101) AS DATE) = @Date
                                   AND sMonthlyLocation = @LocationCode
                                   AND bMonthlyActive = '1'
                                   AND lMonthlyAccountTypeId > 0
                                   AND EXISTS (SELECT 1
                                               FROM   VIPAccountTypes
                                               WHERE  lAccountType = cd.lMonthlyAccountTypeId
                                                      AND ISNULL(bMonthly, 0) = 1)
                                   AND customerguid NOT IN (SELECT customerguid
                                                            FROM   #TicketsDetailsTable);
                            --Imported Customers
                            SELECT @ImportedCount = count(1)
                            FROM   vwCustomerDetails AS cd WITH (NOLOCK)
                            WHERE  CAST (CONVERT (NVARCHAR (50), DATEADD(minute, @offset, dtImported), 101) AS DATE) = @Date
                                   AND sMonthlyLocation = @LocationCode
                                   AND bMonthlyActive = '1'
                                   AND lMonthlyAccountTypeId > 0
                                   AND EXISTS (SELECT 1
                                               FROM   VIPAccountTypes
                                               WHERE  lAccountType = cd.lMonthlyAccountTypeId
                                                      AND ISNULL(bMonthly, 0) = 1)
                                   AND customerguid NOT IN (SELECT customerguid
                                                            FROM   #TicketsDetailsTable);
                            --  SELECT @ImportedCount = COUNT(1) FROM vwCustomerDetails cd WHERE cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,dtCreated), 101) as date) = @Date AND sUpdatedBy like'%IMPORT%'    
                            --AND sMonthlyLocation = @LocationCode AND bMonthlyActive = '1' AND lMonthlyAccountTypeId > 0 AND    
                            --Exists(SELECT 1 FROM VIPAccountTypes WHERE lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1)    
                            SET @NewSignUp = isnull(@TotalSignUp, 0) - isnull(@ReactiveMembers, 0);
                            SET @OldSignUp = @ReactiveMembers;
                            SELECT @Cancel = sum(CASE WHEN (sTransactionType LIKE '%cancel%')
                                                           OR lTransactionTypeId IN (7) THEN 1 ELSE 0 END),
                                   @Decline = sum(CASE WHEN lTransactionTypeId IN (3) THEN 1 ELSE 0 END),
                                   @Expired = sum(CASE WHEN lTransactionTypeId = 8 THEN 1 ELSE 0 END),
                                   @MembersAdded = 0,
                                   @MembersRemoved = 0,
                                   @Lasted1Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 1) THEN 1 ELSE 0 END),
                                   @Lasted2Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 2) THEN 1 ELSE 0 END),
                                   @Lasted3Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 3) THEN 1 ELSE 0 END),
                                   @Lasted4Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 4) THEN 1 ELSE 0 END),
                                   @Lasted5Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 5) THEN 1 ELSE 0 END),
                                   @Lasted6Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 6) THEN 1 ELSE 0 END),
                                   @Lasted7Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 7) THEN 1 ELSE 0 END),
                                   @Lasted8Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 8) THEN 1 ELSE 0 END),
                                   @Lasted9Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                  AND iCurrentActivePeriods = 9) THEN 1 ELSE 0 END),
                                   @Lasted10Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                   AND iCurrentActivePeriods = 10) THEN 1 ELSE 0 END),
                                   @Lasted11Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                   AND iCurrentActivePeriods = 11) THEN 1 ELSE 0 END),
                                   @Lasted12Month = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3, 8)
                                                                   AND iCurrentActivePeriods >= 12) THEN 1 ELSE 0 END),
                                   @CancelEndsCurrentMonth = sum(CASE WHEN (lTransactionTypeId IN (2, 7))
                                                                           AND datepart(m, dtCreated) = datepart(m, dtMonthlyNextPay) THEN 1 ELSE 0 END),
                                   @CancelEndsNextMonth = sum(CASE WHEN (lTransactionTypeId IN (2, 7))
                                                                        AND datepart(m, dtCreated) <> datepart(m, dtMonthlyNextPay) THEN 1 ELSE 0 END),
                                   @AvgCustomerLifetime = sum(CASE WHEN (lTransactionTypeId IN (2, 7, 3)) THEN iTotalActivePeriods ELSE 0 END),
                                   @MembersAddedByLocationchange = SUM(CASE WHEN lTransactionTypeId IN (9) THEN 1 ELSE 0 END)
                            FROM   (SELECT *
                                    FROM   #TempTransactions
                                    WHERE  Date = CAST (@Date AS DATE)
                                           AND Customerguid NOT IN (SELECT Customerguid
                                                                    FROM   #TicketsDetailsTable
                                                                    WHERE  QTY < 0)) AS CU;
                            SELECT   @MembersRemovedByLocationchange = sum(CASE WHEN lTransactionTypeId IN (9) THEN 1 ELSE 0 END)
                            FROM     (SELECT *
                                      FROM   #TempLocationChangeTransactions
                                      WHERE  Date = CAST (@Date AS DATE)) AS CU
                            GROUP BY Date;
                            IF @NewSignUp IS NULL
                                SET @NewSignUp = 0;
                            IF @OldSignUp IS NULL
                                SET @OldSignUp = 0;
                            SET @SignUp = @NewSignUp + @OldSignUp;
                            SET @DropOff = @Decline + @Cancel + @Expired;
                            IF @AvgCustomerLifetime > 0
                               AND @DropOff > 0
                                SET @AvgCustomerLifetime = @AvgCustomerLifetime / @DropOff;
                            SET @ActiveMembers = @ActiveMembers + (isnull(@SignUp, 0) - ISNULL(@DropOff, 0)) + (ISNULL(@MembersAdded, 0) - ISNULL(@MembersRemoved, 0)) + ISNULL(@WACreated, 0) + ISNULL(@ImportedCount, 0) + ISNULL(@MembersAddedByLocationchange, 0) - ISNULL(@MembersRemovedByLocationchange, 0);
                            INSERT  INTO #NewReportData (LocationId, sDay, iSignUp, iDropOff, iGrowth, iTotalMembers, iActiveMembers, iInActiveMembers, fMembershipRevenue, iCarsWashed, fTotalWashRevenue, iMonthlyWashed, fMonthlyWashes, bEstimatedValues, fRecurringRevenue, iRecurringCharges, iNewSignUp, iOldSignUp, iCancel, iDecline, iExpired, iMembersAdded, iMembersRemoved, iActive1Month, iActive2Month, iActive3Month, iActive4Month, iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth, dblAvgCustomerLifetime, WACreated, ImportCreated, VolumePerDay, VolumeHour, VolumePerHour, iLocationChange)
                            --iAvgDaysActive,      
                            VALUES                     (@LocationId, CONVERT (NVARCHAR (50), @Date, 101), @SignUp, @DropOff, (@SignUp - @DropOff), (@ActiveMembers + @InActiveMembers), @ActiveMembers, @InActiveMembers, 0, 0, 0, 0, 0, 0, 0, 0, @NewSignUp, @OldSignUp, @Cancel, @Decline, @Expired, @MembersAdded, @MembersRemoved, @Lasted1Month, @Lasted2Month, @Lasted3Month, @Lasted4Month, @Lasted5Month, @Lasted6Month, @Lasted7Month, @Lasted8Month, @Lasted9Month, @Lasted10Month, @Lasted11Month, @Lasted12Month, @CancelEndsCurrentMonth, @CancelEndsNextMonth, @AvgCustomerLifetime, @WACreated, @ImportedCount, ISNULL(@VolumePerDay, 0), ISNULL(@VolumeHour, 0), ISNULL(@VolumePerHour, 0), ISNULL(@MembersAddedByLocationchange, 0) - ISNULL(@MembersRemovedByLocationchange, 0)); --@AvgDaysActive,      
                            --set @InActiveMembers = @InActiveMembers - (@DropOff - @OldSignUp) - (@MembersRemoved)      
                            IF @ActiveMembers < 0
                                SET @ActiveMembers = 0; -- set @MinDate =  @Date     
                            --if @InActiveMembers < 0 set @InActiveMembers = 0  -- set @MinDate =  @Date     
                            SET @MinDateStart = dateadd(d, 1, @MinDateStart);
                        END
                    SET @ActiveMembers = @ActualActivemembers;
                    WHILE @Date >= @MinDate
                        BEGIN
                            SET @DropOff = 0;
                            SET @SignUp = 0;
                            SET @AvgDaysActive = 0;
                            SET @NewSignUp = 0;
                            SET @OldSignUp = 0;
                            SET @Cancel = 0;
                            SET @Decline = 0;
                            SET @Expired = 0;
                            SET @MembersAdded = 0;
                            SET @MembersRemoved = 0;
                            SET @Lasted1Month = 0;
                            SET @Lasted2Month = 0;
                            SET @Lasted3Month = 0;
                            SET @Lasted4Month = 0;
                            SET @Lasted5Month = 0;
                            SET @Lasted6Month = 0;
                            SET @Lasted7Month = 0;
                            SET @Lasted8Month = 0;
                            SET @Lasted9Month = 0;
                            SET @Lasted10Month = 0;
                            SET @Lasted11Month = 0;
                            SET @Lasted12Month = 0;
                            SET @CancelEndsCurrentMonth = 0;
                            SET @CancelEndsNextMonth = 0;
                            SET @ReactiveMembers = 0;
                            SET @TotalSignUp = 0;
                            SET @WACreated = 0;
                            SET @ImportedCount = 0;
                            SET @VolumePerDay = 0;
                            SET @VolumeHour = 0;
                            SET @VolumePerHour = 0;
                            SET @MembersAddedByLocationchange = 0;
                            SELECT @SignUp = iSignUp,
                                   @DropOff = iDropOff,
                                   @MembersAdded = iMembersAdded,
                                   @MembersRemoved = iMembersRemoved,
                                   @WACreated = WACreated,
                                   @ImportedCount = ImportCreated,
                                   @OldSignUp = iOldSignUp,
                                   @MembersAddedByLocationchange = iLocationChange
                            FROM   #NewReportData
                            WHERE  sDay = CONVERT (NVARCHAR (50), @Date, 101);
                            UPDATE #NewReportData
                            SET    iTotalMembers       = @ActiveMembers + @InActiveMembers,
                                   actualactivemembers = @ActiveMembers,
                                   iInActiveMembers    = @InActiveMembers
                            WHERE  sDay = CONVERT (NVARCHAR (50), @Date, 101);
                            SET @ActiveMembers = @ActiveMembers - (ISNULL(@SignUp, 0) - ISNULL(@DropOff, 0)) - (ISNULL(@MembersAdded, 0) - ISNULL(@MembersRemoved, 0)) - ISNULL(@WACreated, 0) - ISNULL(@ImportedCount, 0) - ISNULL(@MembersAddedByLocationchange, 0);
                            SET @InActiveMembers = @InActiveMembers - (ISNULL(@DropOff, 0) - ISNULL(@OldSignUp, 0)) - (ISNULL(@MembersRemoved, 0));
                            IF @ActiveMembers < 0
                                SET @ActiveMembers = 0; -- set @MinDate =  @Date     
                            IF @InActiveMembers < 0
                                SET @InActiveMembers = 0; -- set @MinDate =  @Date     
                            SET @Date = dateadd(d, -1, @Date);
                        END
                END
            /* Generate new data */
            /** Let Add today date to create row of data to insert **/
            UPDATE cua
            SET    fRedeemWash       = TD.fMembershipRevenue,
                   fRecurringRevenue = TD.fRecurringRevenue
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             SUM(CASE WHEN TableLookUp = 'Unlimited (redeem)' THEN NetAmt ELSE 0 END) AS fMembershipRevenue,
                             SUM(CASE WHEN TableLookUp = 'Unlimited (autocharge)' THEN NetAmt ELSE 0 END) AS fRecurringRevenue
                    FROM     #ticketDataFullTemp
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, DATE), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    fMembershipRevenue = TD.fMembershipRevenue
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             SUM(NetAmt) AS fMembershipRevenue
                    FROM     #ticketDataFullTemp
                    WHERE    TableLookUp IN ('Surcharges', 'Unlimited (autocharge)', 'Unlimited (Reactivation)')
                             AND (TableLookUp <> 'Surcharges'
                                  OR (TableLookUp = 'Surcharges'
                                      AND EXISTS (SELECT 1
                                                  FROM   VIPAccountTypes AS vip
                                                         INNER JOIN
                                                         Surcharges
                                                         ON (vip.lSurchargeId = Surcharges.Surcharge_ID
                                                             OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
                                                         INNER JOIN
                                                         Categories
                                                         ON Surcharges.lCategoryID = Categories.lCategoryID
                                                  WHERE  (Surcharges.Surcharge_ID = RefID)
                                                         AND ISNULL(lSurchargeId, 0) > 0
                                                         AND ISNULL(bMonthly, 0) = 1
                                                         AND (Categories.bExcludeFromDaily = 0
                                                              OR Categories.bExcludeFromDaily IS NULL))))
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, DATE), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    iCarsWashed = TD.iCarsWashed
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             sum(CarCount) AS iCarsWashed
                    FROM     #ticketDataFullTemp
                    WHERE    CarCount <> 0
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    fTotalWashRevenue = TD.fTotalWashRevenue
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             SUM(NetAmt) AS fTotalWashRevenue
                    FROM     #ticketDataFullTemp
                    WHERE    pid = 1
                             AND Description <> 'Tax Refund'
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    dblSalesAmount = TD.dblSalesAmount
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             SUM(NetAmt) AS dblSalesAmount
                    FROM     #ticketDataFullTemp
                    WHERE    TableLookUp <> 'Unlimited (autocharge)'
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    iMonthlyWashed    = TD.iMonthlyWashed,
                   iRecurringCharges = TD.iRecurringCharges --, fMonthlyWashes = TD.MonthlyWashes       
            FROM   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101) AS Date,
                             SUM(CASE WHEN TableLookUp = 'Unlimited (autocharge)' THEN QTY ELSE 0 END) AS iRecurringCharges,
                             SUM(CASE WHEN TableLookUp = 'Unlimited (redeem)' THEN QTY ELSE 0 END) AS iMonthlyWashed
                    FROM     #ticketDataFullTemp
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, Date), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE cua
            SET    iHouseWashed = TD.iCarsWashed
            FROM   --,fTotalWashRevenue = TD.fTotalWashRevenue      
                   #NewReportData AS cua
                   INNER JOIN
                   (SELECT   CONVERT (NVARCHAR (50), DATEADD(minute, @offset, TicketsDetails.dtcreated), 101) AS Date,
                             COUNT(DATEADD(minute, @offset, TicketsDetails.dtcreated)) AS iCarsWashed
                    FROM     --,SUM(TicketsDetails.dblAmount) AS fTotalWashRevenue      
                             TicketsDetails WITH (NOLOCK), Tickets WITH (NOLOCK)
                    WHERE    TicketsDetails.TicketGUID = Tickets.TicketGUID
                             AND TicketsDetails.LocationId = @LocationId
                             AND DATEADD(minute, @offset, TicketsDetails.dtcreated) >= @MinDate
                             AND Detail_Type_ID = 1
                             AND TicketsDetails.bPaid = 1
                             AND (bReversed = 0
                                  OR bReversed IS NULL)
                             AND (bHouseAccount = 1)
                             AND Reference_ID IN (SELECT Service_ID
                                                  FROM   Services
                                                  WHERE  bCountAsCar = 1)
                    GROUP BY CONVERT (NVARCHAR (50), DATEADD(minute, @offset, TicketsDetails.dtcreated), 101)) AS TD
                   ON cua.sDay = TD.Date;
            UPDATE #NewReportData
            SET    fMonthlyWashes = CASE WHEN (iCarsWashed = 0) THEN 0 ELSE (CAST (imonthlywashed AS FLOAT) / CAST ((iCarsWashed) AS FLOAT) * 100.00) END;
            --SELECT LocationId, sDay,iSignUp ,iDropOff ,iGrowth ,iTotalMembers,iActiveMembers,iInActiveMembers ,fMembershipRevenue ,iCarsWashed ,fTotalWashRevenue ,    
            --   iMonthlyWashed ,fMonthlyWashes ,bEstimatedValues,fRecurringRevenue, iRecurringCharges,    
            --   --iAvgDaysActive,    
            --   iNewSignUp,iOldSignUp,iCancel,iDecline,iExpired, iMembersAdded, iMembersRemoved, iActive1Month, iActive2Month, iActive3Month, iActive4Month,     
            --                        iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth,     
            --      iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, WACreated, ImportCreated, VolumePerDay,VolumeHour,VolumePerHour,dblSalesAmount,fRedeemWash FROM #NewReportData    
            INSERT INTO [dbo].[CustomerUnlimitedData] (LocationId, [dtRecord], [iSignUp], [iDropOff], [iGrowth], [iTotalMembers], [iActiveMembers], [iInActiveMembers], [dblSignUpRevenue], [iCarsWashed], [dblTotalWashRevenue], [iMonthlyWashed], [dblMonthlyWashPCT], [bEstimatedValues], [dblRecurringRevenue], iRecurringCharges, iActivations, iReActivations, iCancel, iDecline, iExpired, iMembersAdded, iMembersRemoved, iActive1Month, iActive2Month, iActive3Month, iActive4Month, iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, dtCreated, dtUpdated, WASignUp, ImportSignUp, VolumePerDay, VolumePerHour, VolumeHour, dblSalesAmount, dblRedeemWash, actualactivemembers, iLocationChange)
            --iAvgDaysActive ,
            SELECT LocationId,
                   ISNULL(sDay, 0),
                   ISNULL(iSignUp, 0),
                   ISNULL(iDropOff, 0),
                   ISNULL(iGrowth, 0),
                   ISNULL(iTotalMembers, 0),
                   ISNULL(actualactivemembers, 0),
                   ISNULL(iInActiveMembers, 0),
                   ISNULL(fMembershipRevenue, 0),
                   ISNULL(iCarsWashed, 0),
                   ISNULL(fTotalWashRevenue, 0),
                   ISNULL(iMonthlyWashed, 0),
                   ISNULL(fMonthlyWashes, 0),
                   1,
                   ISNULL(fRecurringRevenue, 0),
                   ISNULL(iRecurringCharges, 0),
                   ISNULL(iNewSignUp, 0),
                   ISNULL(iOldSignUp, 0),
                   ISNULL(iCancel, 0),
                   ISNULL(iDecline, 0),
                   ISNULL(iExpired, 0),
                   ISNULL(iMembersAdded, 0),
                   ISNULL(iMembersRemoved, 0),
                   ISNULL(iActive1Month, 0),
                   ISNULL(iActive2Month, 0),
                   ISNULL(iActive3Month, 0),
                   ISNULL(iActive4Month, 0),
                   ISNULL(iActive5Month, 0),
                   ISNULL(iActive6Month, 0),
                   ISNULL(iActive7Month, 0),
                   ISNULL(iActive8Month, 0),
                   ISNULL(iActive9Month, 0),
                   ISNULL(iActive10Month, 0),
                   ISNULL(iActive11Month, 0),
                   ISNULL(iActive12Month, 0),
                   ISNULL(iCancelEndsCurrentMonth, 0),
                   ISNULL(iCancelEndsNextMonth, 0),
                   ISNULL(dblAvgCustomerLifetime, 0),
                   ISNULL(iHouseWashed, 0),
                   getdate(),
                   getdate(),
                   ISNULL(WACreated, 0),
                   ISNULL(ImportCreated, 0),
                   ISNULL(VolumePerDay, 0),
                   ISNULL(VolumePerHour, 0),
                   ISNULL(VolumeHour, 0),
                   ISNULL(dblSalesAmount, 0),
                   ISNULL(fRedeemWash, 0),
                   isnull(iActiveMembers, 0),
                   ISNULL(iLocationChange, 0)
            FROM   #NewReportData
            WHERE  sday <> @sDay;
            /* Check to see if this is the nightly save */
            IF @intEndOfDaySave = 1
                BEGIN
                    SELECT @RecordId = lRecordId,
                           @TotalMembers = iTotalMembers,
                           @ActiveMemberss = iActiveMembers,
                           @InActiveMemberss = iInActiveMembers
                    FROM   CustomerUnlimitedData
                    WHERE  dtRecord = @sDay
                           AND LocationId = @LocationId;
                    INSERT INTO CustomerUnlimitedTransactions (dtCreated, sTransactionType, sReferenceNumber, lReferenceId, lCustomerId, sCreatedBy, sTerminalName, lTransactionTypeId, iTotalActiveDays, iTotalInactiveDays, LocationId)
                    (SELECT getdate(),
                            'MEMBER COUNT: Active = ' + CONVERT (NVARCHAR (10), @ActiveMembers) + ' InActive = ' + CONVERT (NVARCHAR (10), @InActiveMembers),
                            '',
                            0,
                            0,
                            'CustomerUnlimitedData',
                            '',
                            6,
                            @ActiveMemberss,
                            @InActiveMemberss,
                            @LocationId);
                    IF @RecordId > 0
                        BEGIN
                            /*]
			   ,[iTotalMembers]
			   ,[iActiveMembers]
			   ,[iInActiveMembers]
			   ,[bEstimatedValues
			   ,dblRecurringRevenue]
			, iRecurringCharges*/
                            UPDATE CustomerUnlimitedData
                            SET    iSignUp                       = ISNULL(nrd.iSignUp, 0),
                                   iDropOff                      = ISNULL(nrd.iDropOff, 0),
                                   CustomerUnlimitedData.iGrowth = ISNULL(nrd.iGrowth, 0),
                                   dblSignUpRevenue              = ISNULL(nrd.fMembershipRevenue, 0),
                                   iCarsWashed                   = ISNULL(nrd.iCarsWashed, 0),
                                   dblTotalWashRevenue           = ISNULL(nrd.fTotalWashRevenue, 0),
                                   iMonthlyWashed                = ISNULL(nrd.iMonthlyWashed, 0),
                                   dblMonthlyWashPCT             = ISNULL(nrd.fMonthlyWashes, 0),
                                   iActivations                  = ISNULL(nrd.iNewSignUp, 0),
                                   iReActivations                = ISNULL(nrd.iOldSignUp, 0),
                                   iCancel                       = ISNULL(nrd.iCancel, 0),
                                   iDecline                      = ISNULL(nrd.iDecline, 0),
                                   iExpired                      = ISNULL(nrd.iExpired, 0),
                                   iMembersAdded                 = ISNULL(nrd.iMembersAdded, 0),
                                   iMembersRemoved               = ISNULL(nrd.iMembersRemoved, 0),
                                   iActive1Month                 = ISNULL(nrd.iActive1Month, 0),
                                   iActive2Month                 = ISNULL(nrd.iActive2Month, 0),
                                   iActive3Month                 = ISNULL(nrd.iActive3Month, 0),
                                   iActive4Month                 = ISNULL(nrd.iActive4Month, 0),
                                   iActive5Month                 = ISNULL(nrd.iActive5Month, 0),
                                   iActive6Month                 = ISNULL(nrd.iActive6Month, 0),
                                   iActive7Month                 = ISNULL(nrd.iActive7Month, 0),
                                   iActive8Month                 = ISNULL(nrd.iActive8Month, 0),
                                   iActive9Month                 = ISNULL(nrd.iActive9Month, 0),
                                   iActive10Month                = ISNULL(nrd.iActive10Month, 0),
                                   iActive11Month                = ISNULL(nrd.iActive11Month, 0),
                                   iActive12Month                = ISNULL(nrd.iActive12Month, 0),
                                   iCancelEndsCurrentMonth       = ISNULL(nrd.iCancelEndsCurrentMonth, 0),
                                   iCancelEndsNextMonth          = ISNULL(nrd.iCancelEndsNextMonth, 0),
                                   dblAvgCustomerLifetime        = ISNULL(nrd.dblAvgCustomerLifetime, 0),
                                   iHouseWashed                  = ISNULL(nrd.iHouseWashed, 0),
                                   dtUpdated                     = getdate(),
                                   VolumePerHour                 = ISNULL(nrd.VolumePerHour, 0),
                                   VolumeHour                    = ISNULL(nrd.VolumeHour, 0),
                                   dblSalesAmount                = isnull(nrd.dblSalesAmount, 0),
                                   dblRedeemWash                 = isnull(fRedeemWash, 0),
                                   actualactivemembers           = isnull(nrd.iActiveMembers, 0),
                                   WASignUp                      = ISNULL(WACreated, 0),
                                   iActiveMembers                = ISNULL(nrd.actualactivemembers, 0),
                                   iLocationChange               = ISNULL(nrd.iLocationChange, 0)
                            FROM   --@NewReportData nrd inner join customerunlimiteddata on dtrecord = sday
                                   (SELECT *
                                    FROM   #NewReportData
                                    WHERE  sday = @sDay) AS nrd
                            WHERE  lRecordId = @RecordId;
                        END
                    ELSE
                        BEGIN
                            --delete from CustomerUnlimitedData where dtRecord = @sDay and LocationId = @LocationId
                            INSERT INTO [dbo].[CustomerUnlimitedData] (LocationId, [dtRecord], [iSignUp], [iDropOff], [iGrowth], [iTotalMembers], [iActiveMembers], [iInActiveMembers], [dblSignUpRevenue], [iCarsWashed], [dblTotalWashRevenue], [iMonthlyWashed], [dblMonthlyWashPCT], [bEstimatedValues], [dblRecurringRevenue], iRecurringCharges, iActivations, iReActivations, iCancel, iDecline, iExpired, iMembersAdded, iMembersRemoved, iActive1Month, iActive2Month, iActive3Month, iActive4Month, iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, dtCreated, dtUpdated, VolumePerHour, VolumeHour, dblSalesAmount, dblRedeemWash, actualactivemembers, WASignUp, iLocationChange)
                            --iAvgDaysActive ,
                            SELECT LocationId,
                                   ISNULL(sDay, 0),
                                   ISNULL(iSignUp, 0),
                                   ISNULL(iDropOff, 0),
                                   ISNULL(iGrowth, 0),
                                   ISNULL(iTotalMembers, 0),
                                   ISNULL(actualactivemembers, 0),
                                   ISNULL(iInActiveMembers, 0),
                                   ISNULL(fMembershipRevenue, 0),
                                   ISNULL(iCarsWashed, 0),
                                   ISNULL(fTotalWashRevenue, 0),
                                   ISNULL(iMonthlyWashed, 0),
                                   ISNULL(fMonthlyWashes, 0),
                                   (CASE WHEN sday = @sDay THEN 0 ELSE 1 END),
                                   ISNULL(fRecurringRevenue, 0),
                                   ISNULL(iRecurringCharges, 0),
                                   ISNULL(--iAvgDaysActive,0) ,ISNULL(
                                   iNewSignUp, 0),
                                   ISNULL(iOldSignUp, 0),
                                   ISNULL(iCancel, 0),
                                   ISNULL(iDecline, 0),
                                   ISNULL(iExpired, 0),
                                   ISNULL(iMembersAdded, 0),
                                   ISNULL(iMembersRemoved, 0),
                                   ISNULL(iActive1Month, 0),
                                   ISNULL(iActive2Month, 0),
                                   ISNULL(iActive3Month, 0),
                                   ISNULL(iActive4Month, 0),
                                   ISNULL(iActive5Month, 0),
                                   ISNULL(iActive6Month, 0),
                                   ISNULL(iActive7Month, 0),
                                   ISNULL(iActive8Month, 0),
                                   ISNULL(iActive9Month, 0),
                                   ISNULL(iActive10Month, 0),
                                   ISNULL(iActive11Month, 0),
                                   ISNULL(iActive12Month, 0),
                                   ISNULL(iCancelEndsCurrentMonth, 0),
                                   ISNULL(iCancelEndsNextMonth, 0),
                                   ISNULL(dblAvgCustomerLifetime, 0),
                                   ISNULL(iHouseWashed, 0),
                                   getdate(),
                                   getdate(),
                                   isnull(VolumePerHour, 0),
                                   isnull(VolumeHour, 0),
                                   ISNULL(dblSalesAmount, 0),
                                   isnull(fRedeemWash, 0),
                                   isnull(iActiveMembers, 0),
                                   ISNULL(WACreated, 0),
                                   ISNULL(iLocationChange, 0)
                            FROM   #NewReportData
                            WHERE  sday = @sDay; --return
                        END
                END
            DROP TABLE #NewReportData;
            DROP TABLE #tblCategory;
            DROP TABLE #tblTerminalNameWithoutLocation;
            DROP TABLE #TicketsDetailsTable;
            DROP TABLE #TempTransactions;
            DROP TABLE #TicketsDetailsSalesTable;
            DROP TABLE #lsdRefunds;
            DROP TABLE #ticketDataFullTemp;
            DROP TABLE #TempLocationChangeTransactions;
            SELECT @LocationIds,
                   @Counter;
            SET @ProgressMsg = '[' + CAST(@Counter AS NVARCHAR(10)) + '/' + CAST(@Count AS NVARCHAR(10)) + '] Done.';
            RAISERROR('%s', 0, 1, @ProgressMsg) WITH NOWAIT;
            SET @Counter = @Counter + 1;
        END
    DECLARE @DoneMsg NVARCHAR(400) = 'ASC run complete: ' + CAST(@Count AS NVARCHAR(10)) + ' location(s) processed.';
    RAISERROR('%s', 0, 1, @DoneMsg) WITH NOWAIT;
    UPDATE ##ChurnRunProgress
    SET    CurrentIndex = @Count, Status = 'Complete', UpdatedAt = GETUTCDATE()
    WHERE  RunLabel = 'ASC';
    DELETE CustomerUnlimitedData
    WHERE  dtRecord = @daterun;


--delete from CustomerUnlimitedData  where dtRecord='2023-05-23'
--select * from CustomerUnlimitedData  where dtRecord='2023-05-17'

GO
