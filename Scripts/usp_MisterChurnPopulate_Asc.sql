--Mister ascending populate
-- Runs as a plain batch (no CREATE PROCEDURE permission in this DB) -
-- {COUNT_ASC} is replaced with the real half-count as a plain integer
-- literal before this script is sent to the server; see ChurnJobRunner.
DECLARE @Half INT = {COUNT_ASC};
BEGIN
SET NOCOUNT ON

DECLARE @Count INT, @Counter INT, @LocationIds uniqueidentifier, @TimezoneOffset INT

DECLARE @daterun DATETIME = dbo.DateOnly(GETDATE())
SELECT @daterun, @daterun - 1 


DECLARE @Locations TABLE
(
ID INT IDENTITY(1,1),
LocationId uniqueidentifier,
TimezoneOffset INT
)
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
SELECT @Count = Count(ID) FROM @Locations
SET @Counter = 1

SELECT @Count

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
-- Runs once per script, not once per location
UPDATE CustomerUnlimitedTransactions SET lTransactionTypeId=8 WHERE sTransactionType ='AUTOCHARGE EXPIRED' AND lTransactionTypeId IS NULL

-- Read these once for all locations instead of once per location (no new indexes needed on the client DB).
-- The window is wide enough for any UTC offset; each location still filters to its exact local-date range.
DECLARE @WindowStartUtc datetime = DATEADD(day,-3,@daterun), @WindowEndUtc datetime = DATEADD(day,3,@daterun)

IF OBJECT_ID('tempdb..#CutWindow') IS NOT NULL DROP TABLE #CutWindow
SELECT dtCreated, sTransactionType, lTransactionTypeId, iCurrentActivePeriods, dtMonthlyNextPay, iTotalActivePeriods, smonthlylocation, sReferenceNumber, Customerguid
INTO #CutWindow
FROM CustomerUnlimitedTransactions WITH (NOLOCK)
WHERE dtCreated >= @WindowStartUtc AND dtCreated < @WindowEndUtc OPTION (RECOMPILE)
CREATE CLUSTERED INDEX IX_CutWindow_Location ON #CutWindow (smonthlylocation, dtCreated)
CREATE NONCLUSTERED INDEX IX_CutWindow_Reference ON #CutWindow (sReferenceNumber, lTransactionTypeId, dtCreated)

IF OBJECT_ID('tempdb..#ImportedWindow') IS NOT NULL DROP TABLE #ImportedWindow
SELECT CustomerGUID, dtImported
INTO #ImportedWindow
FROM vwCustomerDetails WITH (NOLOCK)
WHERE dtImported >= @WindowStartUtc AND dtImported < @WindowEndUtc OPTION (RECOMPILE)

-- Inactive members by location; each location applies its own decline-attempt limit below
IF OBJECT_ID('tempdb..#InactiveByLocation') IS NOT NULL DROP TABLE #InactiveByLocation
Select smonthlylocation, bMonthlyDeclined, ipaymentattempts, Count(1) AS Members
INTO #InactiveByLocation
from Customers c  WITH (NOLOCK)
JOIN CustomerDetails cd  WITH (NOLOCK) on c.CustomerGUID = cd.CustomerGUID
where ISNULL(c.bdeleted, 0) = 0 AND
bmonthlyactive = 0
and c.CustomerGUID in (select CustomerGUID from CustomerUnlimitedTransactions  WITH (NOLOCK) where bMonthlyActive = 1)
GROUP BY smonthlylocation, bMonthlyDeclined, ipaymentattempts

-- vw_ssp_VIPAccountTypes checks used by the sign-up ticket query. They depend only on Reference_ID,
-- so they are worked out once here instead of once per ticket row for every location.
IF OBJECT_ID('tempdb..#VipSignUpSurcharge') IS NOT NULL DROP TABLE #VipSignUpSurcharge
SELECT DISTINCT r.RefID
INTO #VipSignUpSurcharge
from vw_ssp_VIPAccountTypes vip
JOIN Surcharges ON (vip.lSurchargeId = Surcharges.Surcharge_ID OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
JOIN Categories ON Surcharges.lCategoryID = Categories.lCategoryID
CROSS APPLY (VALUES (vip.lSurchargeId), (vip.lFamilySurchargeId)) r(RefID)
where r.RefID IS NOT NULL AND ISNULL(lSurchargeId, 0) > 0 and ISNULL(bMonthly, 0) = 1
AND (Categories.bExcludeFromDaily = 0 or Categories.bExcludeFromDaily IS NULL)

IF OBJECT_ID('tempdb..#VipReactivationAccountType') IS NOT NULL DROP TABLE #VipReactivationAccountType
SELECT DISTINCT vip.lAccountType AS RefID
INTO #VipReactivationAccountType
from vw_ssp_VIPAccountTypes vip
JOIN Surcharges ON (vip.lSurchargeId = Surcharges.Surcharge_ID OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
JOIN Categories ON Surcharges.lCategoryID = Categories.lCategoryID
where ISNULL(lSurchargeId, 0) > 0 and ISNULL(bMonthly, 0) = 1
AND (Categories.bExcludeFromDaily = 0 or Categories.bExcludeFromDaily IS NULL)

-- Daily backup of active members. Create it once; the parallel asc/desc run may create it at the same moment (error 2714)
DECLARE @tablename nvarchar(100), @query nvarchar(max), @BackupRowCount int
SET @tablename='MisterCarWashDFTest..vwCustomerDetails_bkp'+CAST(MONTH(getutcdate()) as nvarchar(100))+'_'+CAST(day(getutcdate()) as nvarchar(100))+'_'+CAST(Year(getutcdate()) as nvarchar(100))+'_2'
IF OBJECT_ID(@tablename, 'U') IS NULL
BEGIN
	BEGIN TRY
		SET @query = 'select top 0 CustomerGUID,lcustomerid,smonthlylocation,bmonthlyactive,bmonthlydeclined,ipaymentattempts,lmonthlyaccounttypeid,lCustomerCancelReasonId into '+@tablename+' from vwCustomerDetails'
		EXEC (@query)
	END TRY
	BEGIN CATCH
		IF ERROR_NUMBER() <> 2714
		BEGIN
			DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE()
			RAISERROR(@ErrMsg, 16, 1)
		END
	END CATCH
END

-- Temp tables are created once and truncated per location
IF OBJECT_ID('tempdb..#NewReportData') IS NOT NULL DROP TABLE #NewReportData
IF OBJECT_ID('tempdb..#TicketsDetailsTable') IS NOT NULL DROP TABLE #TicketsDetailsTable
IF OBJECT_ID('tempdb..#TempTransactions') IS NOT NULL DROP TABLE #TempTransactions
IF OBJECT_ID('tempdb..#TicketsDetailsSalesTable') IS NOT NULL DROP TABLE #TicketsDetailsSalesTable
IF OBJECT_ID('tempdb..#TempLocationChangeTransactions') IS NOT NULL DROP TABLE #TempLocationChangeTransactions
IF OBJECT_ID('tempdb..#ticketDataFullTemp') IS NOT NULL DROP TABLE #ticketDataFullTemp
IF OBJECT_ID('tempdb..#lsdRefunds') IS NOT NULL DROP TABLE #lsdRefunds
CREATE TABLE #NewReportData       
 (      
  LocationId uniqueidentifier,      
    sDay nvarchar(50),    --DONE      
    iSignUp int,      --DONE      
    iDropOff int,           
    iGrowth int,         
    iTotalMembers int,       
    iActiveMembers int,      
    iInActiveMembers int,      
    fMembershipRevenue float,  --DONE          
    iCarsWashed int,     --DONE      
    fTotalWashRevenue float,   --DONE      
    iMonthlyWashed int,    --DONE      
    fMonthlyWashes float,      
    bEstimatedValues bit,      
    fRecurringRevenue float,        
    iRecurringCharges int,      
    --iAvgDaysActive int,      
    iNewSignUp int,      
    iOldSignUp int,      
    iCancel int,      
    iDecline int,      
    iExpired int, iMembersAdded int, iMembersRemoved int       
     , iActive1Month int, iActive2Month int, iActive3Month int, iActive4Month int,       
          iActive5Month int, iActive6Month int, iActive7Month int, iActive8Month int,       
    iActive9Month int, iActive10Month int, iActive11Month int, iActive12Month int,       
    iCancelEndsCurrentMonth int, iCancelEndsNextMonth int, dblAvgCustomerLifetime float,       
    iHouseWashed int,--, iPrepaidWashed int      
 WACreated INT,    
 ImportCreated INT,   
 VolumePerDay INT,  
  VolumePerHour INT,   
   VolumeHour INT,
   dblSalesAmount float,
    fRedeemWash float,
	actualactivemembers int,
	iLocationChange int
 )       

  CREATE TABLE #TicketsDetailsTable (DATE DATETIME,TicketGuid BIGINT, RefID INT, Description NVARCHAR(255), QTY FLOAT, locationid uniqueidentifier,Customerguid bigint,smonthlylocation VARCHAR(MAX))     
  CREATE TABLE #TempTransactions ( DATE DATE, sTransactionType VARCHAR(MAX), lTransactionTypeId INT,iCurrentActivePeriods INT, dtCreated DATETIME, dtMonthlyNextPay DATETIME, iTotalActivePeriods INT, smonthlylocation VARCHAR(MAX),Customerguid bigint)    
  CREATE TABLE #TicketsDetailsSalesTable (DATE DATETIME,TicketGuid BIGINT, RefID INT, Description NVARCHAR(255), QTY FLOAT, locationid uniqueidentifier)     
  CREATE TABLE #TempLocationChangeTransactions ( DATE DATE, sTransactionType VARCHAR(MAX), lTransactionTypeId INT,iCurrentActivePeriods INT, dtCreated DATETIME, dtMonthlyNextPay DATETIME, iTotalActivePeriods INT, smonthlylocation VARCHAR(MAX),Customerguid bigint)    

 CREATE TABLE #ticketDataFullTemp (Date DateTime,ShiftNum int,TicketID int,TicketNum nvarchar(25),bPaid int,bReversed int,      
 RefID int,Description nvarchar(255),QTY float,BaseAmt float,NetAmt float,      
 CarCount float,Extra float,GreeterID int,CashierID int,GreeterTerminalName nvarchar(50),CashierTerminalName nvarchar(50),      
 TableLookUp nvarchar(25),CategoryID int,InvCategoryID int,CategoryName nvarchar(255),InvCategoryName nvarchar(255),PID int,AccountCodeID int,GSPrintOrder int,      
 TicketTaxed bit, LocationId UNIQUEIDENTIFIER,Reference_GUID bigint,Ticketguid bigint)  
  
   CREATE TABLE  #lsdRefunds (Date DateTime,ShiftNum int,RefundID int,PaymentTypeID int,PaymentDescription nvarchar(50),TicketID int,TicketNum nvarchar(25),bReversed int,    
 RefID int,Description nvarchar(50),QTY float,BaseAmt float,NetAmt float,    
 CarCount float,Extra float,GreeterID int,CashierID int,TerminalName nvarchar(50),    
 TableLookUp nvarchar(25),CategoryID int,InvCategoryID int,CategoryName nvarchar(50),InvCategoryName nvarchar(50),PID int,AccountCodeID int,GSPrintOrder int, locationid uniqueidentifier,TicketGuid BIGINT,Customerguid bigint)    

WHILE( @Counter <= @Count)
BEGIN

WAITFOR DELAY '000:00:00.040'

SELECT @LocationIds = LocationId, @TimezoneOffset = TimezoneOffset FROM @Locations WHERE ID = @Counter

            DECLARE @ProgressMsg NVARCHAR(400) = '[' + CAST(@Counter AS NVARCHAR(10)) + '/' + CAST(@Count AS NVARCHAR(10)) + '] Processing location (' + CAST(@LocationIds AS NVARCHAR(50)) + ')';
            RAISERROR('%s', 0, 1, @ProgressMsg) WITH NOWAIT;
            UPDATE ##ChurnRunProgress
            SET    CurrentIndex = @Counter, LastLocationId = @LocationIds, UpdatedAt = GETUTCDATE()
            WHERE  RunLabel = 'ASC';


TRUNCATE TABLE #NewReportData
TRUNCATE TABLE #TicketsDetailsTable
TRUNCATE TABLE #TempTransactions
TRUNCATE TABLE #TicketsDetailsSalesTable
TRUNCATE TABLE #TempLocationChangeTransactions
TRUNCATE TABLE #ticketDataFullTemp
TRUNCATE TABLE #lsdRefunds
DECLARE @intEndOfDaySave int=1, @LocationId uniqueidentifier=@LocationIds,@DateTimeOffset INT = @TimezoneOffset     

	declare @RecordId int, @TotalMembers int, @ActiveMemberss int, @InActiveMemberss int,@StartDate DATETIME
	
		declare @MinDateDate datetime, @DateDate datetime,	@intFirstTime int=null, @sDay nvarchar(50)

     declare @LocalDate datetime
		select @LocalDate =  DATEADD(minute,@DateTimeOffset,GETUTCDATE())

		set @sDay = convert(nvarchar(50),@LocalDate,101)

			set @MinDateDate = @daterun - 1
			SET @StartDate =@MinDateDate 	

			select @StartDate,@LocationIds
    
  DECLARE @MinDate datetime, @Date datetime        
  DECLARE @ActiveMembers int, @InActiveMembers int, @AvgDaysActive int       
  DECLARE @TotalMembersToday int,@ActiveMembersToday int, @InActiveMembersToday int      
  DECLARE @SignUp int, @NewSignUp int, @OldSignUp int      
  DECLARE @DropOff int, @Cancel int, @Decline int,@Expired int, @MembersAdded int, @MembersRemoved int      
  DECLARE @CustomerLifeTimeLength nvarchar(50)      
  DECLARE @Months int, @Days int      
 -- DECLARE @intFirstTime int, @sDay nvarchar(50)      
  DECLARE @Lasted1Month int, @Lasted2Month int, @Lasted3Month int, @Lasted4Month int, @Lasted5Month int      
  DECLARE @Lasted6Month int, @Lasted7Month int, @Lasted8Month int, @Lasted9Month int, @Lasted10Month int      
  DECLARE @Lasted11Month int, @Lasted12Month int      
  DECLARE @CancelEndsCurrentMonth int, @CancelEndsNextMonth int, @AvgCustomerLifetime float, @HouseWashed int--, @PrepaidWashed int      
  DECLARE @LocationCode nvarchar(50)      
  DECLARE @TotalSignUp int,@ReactiveMembers int, @WACreated INT, @ImportedCount INT, @offset INT, @tStartDate DATETIME = @StartDate, @tLocationid UNIQUEIDENTIFIER = @LocationId ,@ipaymentDeclineAttempts INT = 3   
  DECLARE @VolumePerDay int  
  DECLARE @VolumePerHour int  
  DECLARE @VolumeHour int
  DECLARE @dblSalesAmount DECIMAL(18,2)
  DECLARE @previousdayactivemember int,@previousDay datetime=null
  DECLARE @MembersAddedByLocationchange int,@MembersRemovedByLocationchange int

    
  --declare @MonthlyFrequency int      
  SET @StartDate = @tStartDate    
  SET @LocationId = @tLocationid    
    
  select @LocationCode = sCode, @offset = UTCOffset,@ipaymentDeclineAttempts = ISNULL(iAutoChargeDeclineAttempts, 3) from Locations with(nolock) where LocationId = @LocationId      
      
  --declare @LocalDate datetime      
    
  --SELECT @LocalDate =  DATEADD(minute,@offset,GETUTCDATE())      
  --SET @sDay = convert(nvarchar(50),@LocalDate,101)    
       
 /* Get current active and inactive members */      
 /* Get current active and inactive members */      
 /* Active members: written to the daily backup table, counted from the same insert */
SET @query = N'INSERT INTO '+@tablename+N' (CustomerGUID,lcustomerid,smonthlylocation,bmonthlyactive,bmonthlydeclined,ipaymentattempts,lmonthlyaccounttypeid,lCustomerCancelReasonId)
select CustomerGUID,lcustomerid,smonthlylocation,bmonthlyactive,bmonthlydeclined,ipaymentattempts,lmonthlyaccounttypeid,lCustomerCancelReasonId
from vwCustomerDetails cd where ISNULL(bDeleted, 0) = 0 AND
(bmonthlyactive = 1 or (bmonthlyactive = 0 and bmonthlydeclined = 1 and ipaymentattempts <= @DeclineAttempts))
and smonthlylocation = @LocationCode and lmonthlyaccounttypeid > 0 AND Exists(SELECT 1 FROM VIPAccountTypes WHERE ISNULL(bDeleted, 0) = 0 AND
lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1 AND ((lChargeFrequency IS NULL AND iDaysToExpire > 0) OR (lChargeFrequency > 0)))
SET @RowCount = @@ROWCOUNT'
SET @BackupRowCount = 0
EXEC sp_executesql @query, N'@DeclineAttempts int, @LocationCode nvarchar(50), @RowCount int OUTPUT', @ipaymentDeclineAttempts, @LocationCode, @BackupRowCount OUTPUT
SET @ActiveMembers = @BackupRowCount
               
 Select @InActiveMembers =
 (
  Select ISNULL(SUM(Members), 0) from #InactiveByLocation
 where smonthlylocation = @LocationCode
 and ((bMonthlyDeclined = 0 or bmonthlydeclined is null) or (bMonthlyDeclined = 1 and ipaymentattempts > @ipaymentDeclineAttempts))
 )
      
 if @ActiveMembers = null set @ActiveMembers = 0
 if @InActiveMembers = null set @InActiveMembers = 0
      
 set @ActiveMembersToday = @ActiveMembers       
 set @InActiveMembersToday = @InActiveMembers      
 set @TotalMembersToday = @ActiveMembersToday + @InActiveMembersToday      
 /* Get current active and inactive members */      
      
 set @MinDate = @StartDate      
       
 /* Generate new data */      
      
  if @MinDate is not null       
  begin      
    
  set @Date = @sDay      
  -- UTC bounds of the local-date range, so the dtCreated filters can use an index
  declare @RangeStartUtc datetime = DATEADD(minute,-1*@offset,CAST(CAST(@MinDate as date) as datetime))
  declare @RangeEndUtc datetime = DATEADD(minute,-1*@offset,DATEADD(day,1,CAST(CAST(@Date as date) as datetime)))
    
  INSERT INTO #TicketsDetailsTable (DATE ,TicketGuid , RefID , Description , QTY , locationid ,Customerguid )          
  SELECT         
  cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,Tickets.dtCreated), 101) as date) ,      
  TicketsDetails.TicketGUID,          
  TicketsDetails.Reference_ID as RefID,          
  TicketsDetails.sDescription as Description,          
  SUM(TicketsDetails.dblQTY) as QTY,           
  TicketsDetails.LocationId,
  Tickets.Customerguid
  FROM TicketsDetails  WITH (NOLOCK)          
  --JOIN #locations l on l.locationId = TicketsDetails.LocationId           
  LEFT JOIN Tickets  WITH (NOLOCK) ON TicketsDetails.TicketGUID = Tickets.TicketGUID           
  LEFT JOIN Surcharges ON TicketsDetails.Reference_ID = Surcharges.Surcharge_ID             
  LEFT JOIN Categories ON  Surcharges.lCategoryID   = Categories.lCategoryID          
  WHERE       
  cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,Tickets.dtCreated), 101) as date)  BETWEEN   cast (@MinDate  as date) AND cast (@Date  as date)      
  AND Tickets.bPaid = 1           
  AND TicketsDetails.bPaid  = 1           
  AND (Tickets.bReversed = 0 or Tickets.bReversed IS NULL)          
  AND (Tickets.bDeleted = 0 or Tickets.bDeleted IS NULL)           
  AND TicketsDetails.LocationId  = @LocationId     
    -- MLASW-1711    
  AND      
  (  
 (  
  TicketsDetails.Detail_Type_ID = 3 -- AND Tickets.LocationId = @locationId               
    AND EXISTS (select 1 from #VipSignUpSurcharge v where v.RefID = TicketsDetails.Reference_ID)
  )   
  OR    
  (     
  TicketsDetails.Detail_Type_ID = 14  AND TicketsDetails.sdescription like '%ReActivation%'  AND TicketsDetails.dblAmount >= 0   
    AND EXISTS (select 1 from #VipReactivationAccountType v where v.RefID = TicketsDetails.Reference_ID)
       )  
 )  
   AND ISNULL(Tickets.luserid,0) <> 9999     
 GROUP BY          
  cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,Tickets.dtCreated), 101) as date),      
  TicketsDetails.LocationId,TicketsDetails.Detail_Type_ID, TicketsDetails.Reference_ID, TicketsDetails.sDescription,           
  CASE WHEN TicketsDetails.Detail_Type_ID IN (11,14) THEN TicketsDetails.dblAmount ELSE NULL END    ,TicketsDetails.TicketGUID ,Tickets.Customerguid      
 
 --Refund data   
  declare @StartUtc datetime = DATEADD(minute,-1*@offset,@MinDate),@EndUtc datetime =GETUTCDATE()  

    INSERT INTO #ticketDataFullTemp  
EXEC [dbo].[A_GetTicketDataFullChurn] @StartUtc,@EndUtc,'ALL','ALL',1, @LocationId 


  	  INSERT INTO #lsdRefunds
	select DATE,shiftnum,Refundid,PaymentTypeID,PaymentDescription,TicketID,TicketNum,bReversed,RefID,Description,QTY,BaseAmt,NetAmt
	,CarCount,Extra,GreeterID,CashierID,TerminalName,TableLookUp,CategoryID,InvCategoryID,CategoryName,InvCategoryName,PID,AccountCodeID,GSPrintOrder,locationid,TicketGuid,Customerguid
	  from   fRefundDataFullChurn(@StartUtc,@EndUtc,'ALL','ALL',@LocationId)tdf  




  INSERT INTO #TicketsDetailsTable (DATE ,TicketGuid , RefID , Description , QTY , locationid ,Customerguid )           
  SELECT cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,Date), 101) as date),  
  ticketguid,          
  Refid as RefID,          
  Description as Description,          
  QTY *-1 as QTY,           
  LocationId,
  Customerguid
  from   #lsdRefunds  
   where   
  
  ((  
  tablelookup ='Surcharges'              
    AND EXISTS (  
      select 1 from VIPAccountTypes vip        
        JOIN Surcharges ON (vip.lSurchargeId = Surcharges.Surcharge_ID Or vip.lFamilySurchargeId =Surcharges.Surcharge_ID)              
        JOIN Categories ON Surcharges.lCategoryID = Categories.lCategoryID          
        where Surcharges.Surcharge_ID = refid AND ISNULL(Surcharges.Surcharge_ID, 0) > 0 and ISNULL(bMonthly, 0) = 1        
        AND (Categories.bExcludeFromDaily = 0 or Categories.bExcludeFromDaily IS NULL)  
       )  
  )   
  OR    
  (     
 tablelookup ='Unlimited (autocharge)'  AND Description like '%ReActivation%'  AND NetAmt >= 0   
    AND EXISTS (  
       select 1 from VIPAccountTypes vip         
       JOIN Surcharges ON (vip.lSurchargeId = Surcharges.Surcharge_ID OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)          
       JOIN Categories ON Surcharges.lCategoryID = Categories.lCategoryID          
       where vip.lAccountType = refid AND ISNULL(lSurchargeId, 0) > 0 and ISNULL(bMonthly, 0) = 1        
       AND (Categories.bExcludeFromDaily = 0 or Categories.bExcludeFromDaily IS NULL)  
       )  
       ) )
 and ISNULL(ticketguid,0)<>0


  INSERT INTO #TempTransactions    
  select  CONVERT(NVARCHAR(50), DATEADD(minute,@offset,dtcreated), 101) as Date, sTransactionType, lTransactionTypeId,iCurrentActivePeriods, dtCreated, dtMonthlyNextPay, iTotalActivePeriods, smonthlylocation,Customerguid      
  from #CutWindow cut
  where
  dtcreated >= @RangeStartUtc AND dtcreated < @RangeEndUtc
  and smonthlylocation = @LocationCode --LocationId = @LocationId
  and sTransactionType <> 'SYNC UPDATE'  
  
    INSERT INTO #TempLocationChangeTransactions    
  select  CONVERT(NVARCHAR(50), DATEADD(minute,@offset,dtcreated), 101) as Date, sTransactionType, lTransactionTypeId,iCurrentActivePeriods, dtCreated, dtMonthlyNextPay, iTotalActivePeriods, smonthlylocation,Customerguid      
  from #CutWindow cut
  where
  dtcreated >= @RangeStartUtc AND dtcreated < @RangeEndUtc
  and sReferenceNumber = @LocationCode --LocationId = @LocationId
  and lTransactionTypeId=9

    --select * from #TempTransactions
  /*volume per day- getting sale tickets*/  
 INSERT INTO #ticketDataFullTemp (Date ,ShiftNum ,TicketID ,TicketNum ,bPaid ,bReversed ,      
 RefID ,Description ,QTY ,BaseAmt ,NetAmt ,      
 CarCount ,Extra ,GreeterID ,CashierID ,GreeterTerminalName ,CashierTerminalName ,      
 TableLookUp ,CategoryID ,InvCategoryID ,CategoryName ,InvCategoryName ,PID ,AccountCodeID ,GSPrintOrder, LocationId,Ticketguid) SELECT Date ,ShiftNum ,TicketID ,TicketNum ,1 ,bReversed ,      
 RefID ,Description ,QTY *- 1 ,-1*BaseAmt ,-1*NetAmt ,      
 CarCount * -1 ,Extra ,GreeterID ,CashierID ,TerminalName ,TerminalName ,      
CASE WHEN TableLookUp='Unlimited (autocharge)' AND Description like '%ReActivation%' THEN 'Unlimited (Reactivation)' ELSE TableLookUp END as TableLookUp  ,CategoryID ,InvCategoryID ,CategoryName ,InvCategoryName ,PID ,AccountCodeID ,GSPrintOrder ,LocationId,TicketGuid FROM #lsdRefunds    
     
 update td set customerguid=t.customerguid
 from #TicketsDetailsTable td
 join tickets t with(nolock) on t.ticketguid =td.ticketguid
 where isnull(td.customerguid,0)=0

  update td set smonthlylocation=cd.sMonthlyLocation
 from #TicketsDetailsTable td
 join vwCustomerDetails cd with(nolock) on cd.CustomerGUID = td.Customerguid
 


  INSERT INTO #TicketsDetailsSalesTable  
  SELECT DATEADD(minute,@offset,Date) as date ,    
    TicketGUID,        
    RefID,        
    Description,        
    SUM(qty) as QTY,         
   LocationId        
  FROM #ticketDataFullTemp
  where CarCount=1
  group by DATEADD(minute,@offset,Date),TicketGUID,RefID,Description,LocationId
 
 set @previousDay = DATEADD(day,-1,@MinDate) 
 declare @MinDateStart datetime = @MinDate,@dateend datetime=@Date,@ActualActivemembers int

 SELECT @previousdayactivemember=iActiveMembers                                                                           
from CustomerUnlimitedData where locationid=@LocationId and dtRecord=@previousDay     
                                                                        
set @ActualActivemembers =@ActiveMembers
set @ActiveMembers = @previousdayactivemember
       
  WHILE @MinDateStart<=@dateend    
  BEGIN  
   set @Date = @MinDateStart
    
    set @DropOff = 0      
    set @SignUp = 0      
    set @AvgDaysActive = 0      
    set @NewSignUp = 0      
    set @OldSignUp = 0      
    set @Cancel = 0      
    set @Decline = 0      
    set @Expired = 0      
    set @MembersAdded = 0       
    set @MembersRemoved = 0      
    set @Lasted1Month = 0      
    set @Lasted2Month = 0      
    set @Lasted3Month = 0      
    set @Lasted4Month = 0      
    set @Lasted5Month = 0      
    set @Lasted6Month = 0      
    set @Lasted7Month = 0      
    set @Lasted8Month = 0      
    set @Lasted9Month = 0      
    set @Lasted10Month = 0      
    set @Lasted11Month = 0       
    set @Lasted12Month = 0      
    set @CancelEndsCurrentMonth = 0       
    set @CancelEndsNextMonth = 0      
    set @ReactiveMembers=0    
 set @TotalSignUp=0    
 SET @WACreated = 0    
 SET @ImportedCount = 0    
 SET @VolumePerDay = 0  
 SET @VolumeHour = 0  
 SET @VolumePerHour = 0  
 SET @MembersRemovedByLocationchange=0
 SET @MembersAddedByLocationchange=0
  
  SELECT @TotalSignUp=sum(QTY) FROM #TicketsDetailsTable WHERE Date = CAST(@Date as DATETIME) and smonthlylocation=@LocationCode
  

  SELECT @ReactiveMembers=sum(QTY) FROM #TicketsDetailsTable WHERE Description like '%ReActivation%' AND Date = CAST(@Date as DATETIME)
  and smonthlylocation=@LocationCode
 -- SELECT @VolumePerHour =Max(QTY) FROM #TicketsDetailsSalesTable  WHERE CAST(CONVERT(NVARCHAR(50), Date, 101) as Date) = CAST(@Date as DATE)   
    
 -- select top 1 @VolumePerHour=MAX(QTY),@VolumeHour = DATEPART(HH,Date)  from #TicketsDetailsSalesTable  
 -- WHERE CAST(CONVERT(NVARCHAR(50), Date, 101) as Date) = CAST(@Date as DATE)  
 --group by DATEPART(HH,Date)   order by  MAX(QTY) desc  
  
   select top 1 @VolumePerHour=MAX(QTY),@VolumeHour = t.VolumeHour  from
  (SELECT SUM(QTY) as qty ,DATEPART(HH,Date) as VolumeHour FROM #TicketsDetailsSalesTable
  WHERE CAST(CONVERT(NVARCHAR(50), Date, 101) as Date) = CAST(@Date as DATE)  
   group by DATEPART(HH,Date)) t group by t.VolumeHour order by  MAX(QTY) desc  

  -- GET WA CREATED AND IMPORTED CUSTOMERS    
  SELECT @WACreated = SUM(CASE WHEN CONVERT(VARCHAR(30), CustomerGUID) LIKE '999%' THEN 1 ELSE 0 END)
  --,       
  --@ImportedCount = SUM(CASE WHEN sUpdatedBy like'%IMPORT%' THEN 1 ELSE 0 END)       
  FROM vwCustomerDetails cd  with(nolock)     
  WHERE cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,dtCreated), 101) as date) = @Date      
  AND sMonthlyLocation = @LocationCode       
  AND bMonthlyActive = '1'       
  AND lMonthlyAccountTypeId > 0       
  AND Exists(SELECT 1 FROM VIPAccountTypes WHERE lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1)      
  and cd.customerguid not in(select customerguid from #TicketsDetailsTable)

  --Imported Customers
  SELECT
    @ImportedCount = count(1)
  FROM #ImportedWindow iw
  JOIN vwCustomerDetails cd with(nolock) ON cd.CustomerGUID = iw.CustomerGUID
  WHERE iw.dtImported >= DATEADD(minute,-1*@offset,@Date) AND iw.dtImported < DATEADD(minute,-1*@offset,DATEADD(day,1,@Date))
  AND sMonthlyLocation = @LocationCode       
  AND bMonthlyActive = '1'       
  AND lMonthlyAccountTypeId > 0       
  AND Exists(SELECT 1 FROM VIPAccountTypes WHERE lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1)      
  and cd.customerguid not in(select customerguid from #TicketsDetailsTable)


  --  SELECT @ImportedCount = COUNT(1) FROM vwCustomerDetails cd WHERE cast(CONVERT(NVARCHAR(50), DATEADD(minute,@offset,dtCreated), 101) as date) = @Date AND sUpdatedBy like'%IMPORT%'    
  --AND sMonthlyLocation = @LocationCode AND bMonthlyActive = '1' AND lMonthlyAccountTypeId > 0 AND    
  --Exists(SELECT 1 FROM VIPAccountTypes WHERE lAccountType = cd.lMonthlyAccountTypeId AND ISNULL(bMonthly, 0) = 1)    
    
  SET @NewSignUp =  isnull(@TotalSignUp,0) - isnull(@ReactiveMembers,0)    
  SET @OldSignUp=@ReactiveMembers    
    
    select @Cancel = sum(case when (sTransactionType like '%cancel%') Or lTransactionTypeId IN (7) then 1 else 0 end)       
    ,@Decline = sum(case when lTransactionTypeId IN (3) then 1 else 0 end)      
       ,@Expired = sum(case when lTransactionTypeId = 8 then 1 else 0 end)      
    ,@MembersAdded = 0     
    ,@MembersRemoved = 0    
    ,@Lasted1Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 1) then 1 else 0 end)      
    ,@Lasted2Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 2) then 1 else 0 end)      
    ,@Lasted3Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 3) then 1 else 0 end)      
    ,@Lasted4Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 4) then 1 else 0 end)      
    ,@Lasted5Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 5) then 1 else 0 end)      
    ,@Lasted6Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 6) then 1 else 0 end)      
    ,@Lasted7Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 7) then 1 else 0 end)      
    ,@Lasted8Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 8) then 1 else 0 end)      
    ,@Lasted9Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 9) then 1 else 0 end)      
    ,@Lasted10Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 10) then 1 else 0 end)      
    ,@Lasted11Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods = 11) then 1 else 0 end)      
    ,@Lasted12Month = sum(case when (lTransactionTypeId in (2,7,3,8) and iCurrentActivePeriods >= 12) then 1 else 0 end)      
    ,@CancelEndsCurrentMonth = sum(case when (lTransactionTypeId in (2,7)) and datepart(m,dtCreated) = datepart(m,dtMonthlyNextPay) then 1 else 0 end)      
    ,@CancelEndsNextMonth = sum(case when (lTransactionTypeId in (2,7)) and datepart(m,dtCreated) <> datepart(m,dtMonthlyNextPay) then 1 else 0 end)      
    ,@AvgCustomerLifetime = sum(case when (lTransactionTypeId in (2,7,3)) then iTotalActivePeriods else 0 end)  
	,@MembersAddedByLocationchange=SUM(case when lTransactionTypeId IN (9) then 1 else 0 end)
    from (     
       SELECT * FROM #TempTransactions WHERE  Date = CAST(@Date as DATE)
	    and Customerguid not in(select Customerguid from #TicketsDetailsTable where QTY<0)
    ) CU   
	

	 select @MembersRemovedByLocationchange = sum(case WHEN lTransactionTypeId IN (9) then 1 else 0 end)

	from (     
       SELECT * FROM #TempLocationChangeTransactions WHERE  Date = CAST(@Date as DATE)    
	    
    ) CU  
          
    group by Date      
      
    if @NewSignUp is null set @NewSignUp = 0      
    if @OldSignUp is null set @OldSignUp = 0      
          
    set @SignUp = @NewSignUp + @OldSignUp      
    set @DropOff = @Decline + @Cancel+@Expired      
      
    if @AvgCustomerLifetime > 0 and @DropOff > 0      
     set @AvgCustomerLifetime = @AvgCustomerLifetime / @DropOff      
    
	set @ActiveMembers = @ActiveMembers + (isnull(@SignUp,0) - ISNULL(@DropOff,0)) + (ISNULL(@MembersAdded,0) - ISNULL(@MembersRemoved,0)) + ISNULL(@WACreated, 0) + ISNULL(@ImportedCount, 0) +ISNULL(@MembersAddedByLocationchange,0)-ISNULL(@MembersRemovedByLocationchange,0)   
	 
    Insert INTO #NewReportData (LocationId,sDay,iSignUp ,iDropOff ,iGrowth ,iTotalMembers,iActiveMembers,iInActiveMembers ,      
    fMembershipRevenue ,iCarsWashed ,fTotalWashRevenue, iMonthlyWashed ,fMonthlyWashes ,bEstimatedValues,fRecurringRevenue,iRecurringCharges,      
    --iAvgDaysActive,      
    iNewSignUp,iOldSignUp,iCancel,iDecline,iExpired,iMembersAdded,iMembersRemoved, iActive1Month, iActive2Month, iActive3Month       
                         , iActive4Month, iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month      
       , iActive10Month, iActive11Month,iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth,dblAvgCustomerLifetime, WACreated, ImportCreated, VolumePerDay,VolumeHour, VolumePerHour,iLocationChange)      
    Values(@LocationId, convert(nvarchar(50),@Date,101),@SignUp,@DropOff,(@SignUp - @DropOff),(@ActiveMembers + @InActiveMembers),@ActiveMembers,@InActiveMembers,      
    0,0,0,0,0,0,0,0,      
    --@AvgDaysActive,      
    @NewSignUp,@OldSignUp,@Cancel,@Decline,@Expired,@MembersAdded,@MembersRemoved, @Lasted1Month, @Lasted2Month, @Lasted3Month,       
    @Lasted4Month,@Lasted5Month,@Lasted6Month,@Lasted7Month,@Lasted8Month,@Lasted9Month,@Lasted10Month,@Lasted11Month,       
                         @Lasted12Month, @CancelEndsCurrentMonth, @CancelEndsNextMonth,@AvgCustomerLifetime, @WACreated, @ImportedCount, ISNULL(@VolumePerDay,0),ISNULL(@VolumeHour,0),ISNULL(@VolumePerHour,0),ISNULL(@MembersAddedByLocationchange,0)-ISNULL(@MembersRemovedByLocationchange,0))       
      
      
 
 --set @InActiveMembers = @InActiveMembers - (@DropOff - @OldSignUp) - (@MembersRemoved)      
     
    if @ActiveMembers < 0  set @ActiveMembers = 0  -- set @MinDate =  @Date     
    --if @InActiveMembers < 0 set @InActiveMembers = 0  -- set @MinDate =  @Date     
      
    set @MinDateStart = dateadd(d,1,@MinDateStart)      
   end      
  
set @ActiveMembers = @ActualActivemembers    
 WHILE @Date >=  @MinDate      
  BEGIN      
    
    set @DropOff = 0      
    set @SignUp = 0      
    set @AvgDaysActive = 0      
    set @NewSignUp = 0      
    set @OldSignUp = 0      
    set @Cancel = 0      
    set @Decline = 0      
    set @Expired = 0      
    set @MembersAdded = 0       
    set @MembersRemoved = 0      
    set @Lasted1Month = 0      
    set @Lasted2Month = 0      
    set @Lasted3Month = 0      
    set @Lasted4Month = 0      
    set @Lasted5Month = 0      
    set @Lasted6Month = 0      
    set @Lasted7Month = 0      
    set @Lasted8Month = 0      
    set @Lasted9Month = 0      
    set @Lasted10Month = 0      
    set @Lasted11Month = 0       
    set @Lasted12Month = 0      
    set @CancelEndsCurrentMonth = 0       
    set @CancelEndsNextMonth = 0      
    set @ReactiveMembers=0    
 set @TotalSignUp=0    
 SET @WACreated = 0    
 SET @ImportedCount = 0    
 SET @VolumePerDay = 0  
 SET @VolumeHour = 0  
 SET @VolumePerHour = 0 
 SET @MembersAddedByLocationchange=0
  
  SELECT @SignUp=iSignUp ,@DropOff=iDropOff,@MembersAdded=iMembersAdded,@MembersRemoved=iMembersRemoved,@WACreated=WACreated,@ImportedCount=ImportCreated,@OldSignUp=iOldSignUp,@MembersAddedByLocationchange=iLocationChange  from #NewReportData where sDay=convert(nvarchar(50),@Date,101)
     update  #NewReportData set iTotalMembers=@ActiveMembers+@InActiveMembers, actualactivemembers = @ActiveMembers,iInActiveMembers = @InActiveMembers  where sDay=convert(nvarchar(50),@Date,101)
 set @ActiveMembers = @ActiveMembers - (ISNULL(@SignUp,0) - ISNULL(@DropOff,0)) - (ISNULL(@MembersAdded,0) - ISNULL(@MembersRemoved,0)) - ISNULL(@WACreated, 0) - ISNULL(@ImportedCount, 0) -  ISNULL(@MembersAddedByLocationchange,0)
 set @InActiveMembers = @InActiveMembers - (ISNULL(@DropOff,0) - ISNULL(@OldSignUp,0)) - (ISNULL(@MembersRemoved,0))      
     
    if @ActiveMembers < 0  set @ActiveMembers = 0  -- set @MinDate =  @Date     
    if @InActiveMembers < 0 set @InActiveMembers = 0  -- set @MinDate =  @Date     
   
    set @Date = dateadd(d,-1,@Date)      
   end      
  end      
    
      

  /* Generate new data */      
      
      
   /** Let Add today date to create row of data to insert **/      
      
   -- One pass over #ticketDataFullTemp for all per-day ticket totals
   UPDATE cua
   SET fRedeemWash = TD.fRedeemWash, fRecurringRevenue = TD.fRecurringRevenue, fMembershipRevenue = TD.fMembershipRevenue,
       iCarsWashed = TD.iCarsWashed, fTotalWashRevenue = TD.fTotalWashRevenue, dblSalesAmount = TD.dblSalesAmount,
       iMonthlyWashed = TD.iMonthlyWashed, iRecurringCharges = TD.iRecurringCharges
   FROM #NewReportData cua
   INNER JOIN (
   SELECT sDay
   ,SUM(case when TableLookUp = 'Unlimited (redeem)' then NetAmt else 0 end) as fRedeemWash
   ,SUM(case when TableLookUp = 'Unlimited (autocharge)' then NetAmt else 0 end) as fRecurringRevenue
   ,SUM(case when IsMembershipRevenue = 1 then NetAmt else 0 end) as fMembershipRevenue
   ,SUM(case when CarCount <> 0 then CarCount else 0 end) as iCarsWashed
   ,SUM(case when pid = 1 and Description <> 'Tax Refund' then NetAmt else 0 end) as fTotalWashRevenue
   ,SUM(case when TableLookUp <> 'Unlimited (autocharge)' then NetAmt else 0 end) as dblSalesAmount
   ,SUM(case when TableLookUp = 'Unlimited (autocharge)' then QTY else 0 end) as iRecurringCharges
   ,SUM(case when TableLookUp = 'Unlimited (redeem)' then QTY else 0 end) as iMonthlyWashed
   FROM (
   SELECT CONVERT(NVARCHAR(50), DATEADD(minute,@offset,Date), 101) as sDay, TableLookUp, NetAmt, QTY, CarCount, pid, Description,
   case when TableLookUp in ('Unlimited (autocharge)','Unlimited (Reactivation)')
     OR (TableLookUp = 'Surcharges' and EXISTS (
       select 1 from VIPAccountTypes vip
        JOIN Surcharges ON (vip.lSurchargeId = Surcharges.Surcharge_ID OR vip.lFamilySurchargeId = Surcharges.Surcharge_ID)
        JOIN Categories ON Surcharges.lCategoryID = Categories.lCategoryID
        where (Surcharges.Surcharge_ID = RefID) AND ISNULL(lSurchargeId, 0) > 0 and ISNULL(bMonthly, 0) = 1
        AND (Categories.bExcludeFromDaily = 0 or Categories.bExcludeFromDaily IS NULL)
       ))
   then 1 else 0 end as IsMembershipRevenue
   FROM #ticketDataFullTemp
   ) t
   GROUP BY sDay
   ) TD ON cua.sDay = TD.sDay

      
       UPDATE cua      
SET iHouseWashed = TD.iCarsWashed      
 --,fTotalWashRevenue = TD.fTotalWashRevenue      
FROM #NewReportData cua      
INNER JOIN (SELECT      
  CONVERT(NVARCHAR(50), DATEADD(minute,@offset,TicketsDetails.dtcreated), 101) AS Date      
  ,COUNT(DATEADD(minute,@offset,TicketsDetails.dtcreated)) AS iCarsWashed      
  --,SUM(TicketsDetails.dblAmount) AS fTotalWashRevenue      
 FROM TicketsDetails WITH (NOLOCK),Tickets WITH (NOLOCK)      
 WHERE TicketsDetails.TicketGUID = Tickets.TicketGUID      
 and TicketsDetails.LocationId = @LocationId      
 and TicketsDetails.dtcreated >= DATEADD(minute,-1*@offset,@MinDate)
 AND Detail_Type_ID = 1      
 AND TicketsDetails.bPaid = 1      
 AND (bReversed = 0 or bReversed is Null)      
 And (bHouseAccount = 1)      
 AND Reference_ID IN (SELECT      
   Service_ID      
  FROM Services      
  WHERE bCountAsCar = 1)      
     
 GROUP BY CONVERT(NVARCHAR(50), DATEADD(minute,@offset,TicketsDetails.dtcreated), 101)) TD      
 ON cua.sDay = TD.Date OPTION (RECOMPILE)
      
      
      
 UPDATE #NewReportData      
SET fMonthlyWashes =      
CASE      
 WHEN (iCarsWashed = 0) THEN 0      
 ELSE (cast(imonthlywashed as float) / cast((iCarsWashed) as float) * 100.00)      
END      
      
     
 --SELECT LocationId, sDay,iSignUp ,iDropOff ,iGrowth ,iTotalMembers,iActiveMembers,iInActiveMembers ,fMembershipRevenue ,iCarsWashed ,fTotalWashRevenue ,    
 --   iMonthlyWashed ,fMonthlyWashes ,bEstimatedValues,fRecurringRevenue, iRecurringCharges,    
 --   --iAvgDaysActive,    
 --   iNewSignUp,iOldSignUp,iCancel,iDecline,iExpired, iMembersAdded, iMembersRemoved, iActive1Month, iActive2Month, iActive3Month, iActive4Month,     
 --                        iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth,     
 --      iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, WACreated, ImportCreated, VolumePerDay,VolumeHour,VolumePerHour,dblSalesAmount,fRedeemWash FROM #NewReportData    
INSERT INTO [dbo].[CustomerUnlimitedData]
           (LocationId
			,[dtRecord]
           ,[iSignUp]
           ,[iDropOff]
           ,[iGrowth]
           ,[iTotalMembers]
		   ,[iActiveMembers]
           ,[iInActiveMembers]
           ,[dblSignUpRevenue]
           ,[iCarsWashed]
           ,[dblTotalWashRevenue]
           ,[iMonthlyWashed]
           ,[dblMonthlyWashPCT]
           ,[bEstimatedValues]
           ,[dblRecurringRevenue]
        ,iRecurringCharges
		   ,--iAvgDaysActive ,
	   iActivations ,
	   iReActivations ,
	   iCancel ,
	   iDecline ,
	   iExpired, iMembersAdded, iMembersRemoved,  iActive1Month, iActive2Month, iActive3Month, iActive4Month, 
                         iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, dtCreated, dtUpdated, WASignUp, 
						 ImportSignUp, VolumePerDay,VolumePerHour,VolumeHour,dblSalesAmount,dblRedeemWash,actualactivemembers,iLocationChange
	   	)
		   Select LocationId ,ISNULL(sDay,0) ,ISNULL(iSignUp,0) ,ISNULL(iDropOff ,0) ,ISNULL(iGrowth,0) ,ISNULL(iTotalMembers,0) ,ISNULL(actualactivemembers,0) ,ISNULL(iInActiveMembers,0) ,ISNULL(fMembershipRevenue ,0) ,ISNULL(iCarsWashed,0) ,ISNULL(fTotalWashRevenue ,0) ,
		   ISNULL(iMonthlyWashed,0) ,ISNULL(fMonthlyWashes,0) ,1 ,ISNULL(fRecurringRevenue,0) ,ISNULL(iRecurringCharges,0) ,
		   ISNULL(iNewSignUp,0) ,ISNULL(iOldSignUp,0) ,ISNULL(iCancel,0) ,ISNULL(iDecline,0) ,ISNULL(iExpired,0) ,ISNULL( iMembersAdded,0) ,ISNULL( iMembersRemoved,0) ,ISNULL(  iActive1Month,0) ,ISNULL( iActive2Month,0) ,ISNULL( iActive3Month,0) ,ISNULL( iActive4Month,0) ,ISNULL( 
                         iActive5Month,0) ,ISNULL( iActive6Month,0) ,ISNULL( iActive7Month,0) ,ISNULL( iActive8Month,0) ,ISNULL( iActive9Month,0) ,ISNULL( iActive10Month,0) ,ISNULL( iActive11Month,0) ,ISNULL( iActive12Month,0) ,ISNULL( iCancelEndsCurrentMonth,0) ,ISNULL( iCancelEndsNextMonth,0) ,ISNULL( dblAvgCustomerLifetime,0) ,ISNULL( iHouseWashed,0) , getdate(), getdate(), ISNULL(WACreated, 0), ISNULL(ImportCreated, 0),
						 ISNULL(VolumePerDay,0),ISNULL(VolumePerHour,0),ISNULL(VolumeHour,0),ISNULL(dblSalesAmount,0),ISNULL(fRedeemWash,0),isnull(iActiveMembers,0),ISNULL(iLocationChange,0)
				from #NewReportData where sday <> @sDay

/* Check to see if this is the nightly save */
	if @intEndOfDaySave = 1
	begin

		select @RecordId = lRecordId, @TotalMembers = iTotalMembers, @ActiveMemberss = iActiveMembers, @InActiveMemberss = iInActiveMembers from CustomerUnlimitedData where dtRecord = @sDay and LocationId = @LocationId

		INSERT INTO CustomerUnlimitedTransactions (dtCreated, sTransactionType, sReferenceNumber, lReferenceId, lCustomerId, sCreatedBy, sTerminalName, lTransactionTypeId, iTotalActiveDays, iTotalInactiveDays, LocationId)
		(select getdate(), 'MEMBER COUNT: Active = ' + convert(nvarchar(10),@ActiveMembers) + ' InActive = ' + convert(nvarchar(10),@InActiveMembers),'',0,0,'CustomerUnlimitedData','',6, @ActiveMemberss, @InActiveMemberss, @LocationId)

		if @RecordId > 0 
			begin

					/*]
			   ,[iTotalMembers]
			   ,[iActiveMembers]
			   ,[iInActiveMembers]
			   ,[bEstimatedValues
			   ,dblRecurringRevenue]
			, iRecurringCharges*/
				update CustomerUnlimitedData 		
				set	iSignUp = ISNULL(nrd.iSignUp ,0),iDropOff = ISNULL(nrd.iDropOff ,0),CustomerUnlimitedData.iGrowth = ISNULL(nrd.iGrowth ,0),
					dblSignUpRevenue = ISNULL(nrd.fMembershipRevenue ,0),iCarsWashed = ISNULL(nrd.iCarsWashed ,0),
					dblTotalWashRevenue = ISNULL(nrd.fTotalWashRevenue ,0),iMonthlyWashed = ISNULL(nrd.iMonthlyWashed ,0),dblMonthlyWashPCT = ISNULL(nrd.fMonthlyWashes ,0),
			   		iActivations = ISNULL(nrd.iNewSignUp,0),iReActivations = ISNULL(nrd.iOldSignUp,0),iCancel = ISNULL(nrd.iCancel,0),iDecline = ISNULL(nrd.iDecline,0),
					iExpired = ISNULL(nrd.iExpired,0), iMembersAdded = ISNULL(nrd.iMembersAdded,0), iMembersRemoved =ISNULL(nrd.iMembersRemoved,0),  
					iActive1Month = ISNULL(nrd.iActive1Month,0), iActive2Month = ISNULL(nrd.iActive2Month,0), iActive3Month = ISNULL(nrd.iActive3Month,0), iActive4Month = ISNULL(nrd.iActive4Month,0), 
					iActive5Month = ISNULL(nrd.iActive5Month,0), iActive6Month = ISNULL(nrd.iActive6Month,0), iActive7Month = ISNULL(nrd.iActive7Month,0), iActive8Month = ISNULL(nrd.iActive8Month,0), 
					iActive9Month = ISNULL(nrd.iActive9Month,0), iActive10Month = ISNULL(nrd.iActive10Month,0), iActive11Month = ISNULL(nrd.iActive11Month,0), iActive12Month = ISNULL(nrd.iActive12Month,0),
					iCancelEndsCurrentMonth = ISNULL(nrd.iCancelEndsCurrentMonth,0), iCancelEndsNextMonth = ISNULL(nrd.iCancelEndsNextMonth,0), dblAvgCustomerLifetime = ISNULL(nrd.dblAvgCustomerLifetime,0), iHouseWashed = ISNULL(nrd.iHouseWashed,0), dtUpdated = getdate(),VolumePerHour = ISNULL(nrd.VolumePerHour,0),VolumeHour = ISNULL(nrd.VolumeHour,0),dblSalesAmount = isnull(nrd.dblSalesAmount,0),dblRedeemWash=isnull(fRedeemWash,0)	,actualactivemembers =isnull(nrd.iActiveMembers,0),WASignUp=ISNULL(WACreated, 0),
					iActiveMembers=ISNULL(nrd.actualactivemembers,0),iLocationChange=ISNULL(nrd.iLocationChange,0)
			  
				from --@NewReportData nrd inner join customerunlimiteddata on dtrecord = sday
				(select * from #NewReportData where sday = @sDay) nrd
				
				where lRecordId = @RecordId
					

			end
		else
			begin
	--delete from CustomerUnlimitedData where dtRecord = @sDay and LocationId = @LocationId

				INSERT INTO [dbo].[CustomerUnlimitedData]
			   (LocationId
				,[dtRecord]
			   ,[iSignUp]
			   ,[iDropOff]
			   ,[iGrowth]
			   ,[iTotalMembers]
			   ,[iActiveMembers]
			   ,[iInActiveMembers]
			   ,[dblSignUpRevenue]
			   ,[iCarsWashed]
			   ,[dblTotalWashRevenue]
			   ,[iMonthlyWashed]
			   ,[dblMonthlyWashPCT]
			   ,[bEstimatedValues]
			   ,[dblRecurringRevenue]
			, iRecurringCharges
			   ,--iAvgDaysActive ,
		   iActivations ,
		   iReActivations ,
		   iCancel ,
		   iDecline,
		   iExpired, iMembersAdded, iMembersRemoved ,  iActive1Month, iActive2Month, iActive3Month, iActive4Month, 
		   iActive5Month, iActive6Month, iActive7Month, iActive8Month, iActive9Month, iActive10Month, iActive11Month, iActive12Month, iCancelEndsCurrentMonth, iCancelEndsNextMonth, dblAvgCustomerLifetime, iHouseWashed, dtCreated, dtUpdated,VolumePerHour,VolumeHour,dblSalesAmount,dblRedeemWash,actualactivemembers,WASignUp,iLocationChange)
		  
		  
			   Select LocationId,ISNULL(sDay,0) ,ISNULL(iSignUp ,0) ,ISNULL(iDropOff ,0) ,ISNULL(iGrowth ,0) ,ISNULL(iTotalMembers,0) ,ISNULL(actualactivemembers,0) ,ISNULL(iInActiveMembers ,0) ,ISNULL(fMembershipRevenue ,0) ,ISNULL(iCarsWashed ,0) ,ISNULL(fTotalWashRevenue ,0) ,ISNULL(
					iMonthlyWashed ,0) ,ISNULL(fMonthlyWashes ,0) ,(case when sday = @sDay then 0 else 1 end) ,ISNULL(fRecurringRevenue,0) ,ISNULL(iRecurringCharges,0) ,ISNULL(
					--iAvgDaysActive,0) ,ISNULL(
					iNewSignUp,0) ,ISNULL(iOldSignUp,0) ,ISNULL(iCancel,0) ,ISNULL(iDecline,0) ,ISNULL(iExpired,0) ,ISNULL( iMembersAdded,0) ,ISNULL( iMembersRemoved,0) , ISNULL(iActive1Month,0) ,ISNULL( iActive2Month,0) ,ISNULL( iActive3Month,0) ,ISNULL( iActive4Month,0) ,ISNULL( 
							 iActive5Month,0) ,ISNULL( iActive6Month,0) ,ISNULL( iActive7Month,0) ,ISNULL( iActive8Month,0) ,ISNULL( iActive9Month,0) ,ISNULL( iActive10Month,0) ,ISNULL( iActive11Month,0) ,ISNULL( iActive12Month,0) ,ISNULL(iCancelEndsCurrentMonth,0) ,ISNULL( iCancelEndsNextMonth,0) ,ISNULL( dblAvgCustomerLifetime,0) ,ISNULL( iHouseWashed,0), getdate(), getdate(),isnull(VolumePerHour,0)
							 ,isnull(VolumeHour,0),ISNULL(dblSalesAmount,0),isnull(fRedeemWash,0),isnull(iActiveMembers,0),ISNULL(WACreated, 0),ISNULL(iLocationChange,0)
					from #NewReportData where sday = @sDay
			
			--return

			end
	end
	      
   


   SELECT @LocationIds,@Counter
            SET @ProgressMsg = '[' + CAST(@Counter AS NVARCHAR(10)) + '/' + CAST(@Count AS NVARCHAR(10)) + '] Done.';
            RAISERROR('%s', 0, 1, @ProgressMsg) WITH NOWAIT;
SET @Counter = @Counter + 1

 END
 DROP TABLE #NewReportData
 DROP TABLE #TicketsDetailsTable
 DROP TABLE #TempTransactions
 DROP TABLE #TicketsDetailsSalesTable
 DROP TABLE #lsdRefunds
 DROP TABLE #ticketDataFullTemp
 DROP TABLE #TempLocationChangeTransactions
 DROP TABLE #CutWindow
 DROP TABLE #ImportedWindow
 DROP TABLE #InactiveByLocation
 DROP TABLE #VipSignUpSurcharge
 DROP TABLE #VipReactivationAccountType


    DECLARE @DoneMsg NVARCHAR(400) = 'ASC run complete: ' + CAST(@Count AS NVARCHAR(10)) + ' location(s) processed.';
    RAISERROR('%s', 0, 1, @DoneMsg) WITH NOWAIT;
    UPDATE ##ChurnRunProgress
    SET    CurrentIndex = @Count, Status = 'Complete', UpdatedAt = GETUTCDATE()
    WHERE  RunLabel = 'ASC';
 DELETE FROM CustomerUnlimitedData  where dtRecord = @daterun
END
 
--delete from CustomerUnlimitedData  where dtRecord='2023-05-23'
--select * from CustomerUnlimitedData  where dtRecord='2023-05-17'

GO
