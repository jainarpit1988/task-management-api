using System.Globalization;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using TaskManagement.Application.Common;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
using TaskManagement.Application.DTOs.Agent;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Reports;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Helpers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Services;

public sealed class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly ITaskRepository _tasks;
    private readonly ITaskUpdateRepository _updates;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IExcelUploadRepository _uploads;
    private readonly IExcelUploadErrorRepository _uploadErrors;
    private readonly IReportExportRepository _reportExports;
    private readonly ICurrentUser _currentUser;
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUserRepository users,
        ITaskRepository tasks,
        ITaskUpdateRepository updates,
        ITaskAssignmentRepository assignments,
        IExcelUploadRepository uploads,
        IExcelUploadErrorRepository uploadErrors,
        IReportExportRepository reportExports,
        ICurrentUser currentUser,
        AppDbContext db,
        IMapper mapper,
        ILogger<AdminService> logger)
    {
        _users = users;
        _tasks = tasks;
        _updates = updates;
        _assignments = assignments;
        _uploads = uploads;
        _uploadErrors = uploadErrors;
        _reportExports = reportExports;
        _currentUser = currentUser;
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserDto> CreateAgentAsync(CreateAgentRequestDto request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Email) && await _users.EmailExistsAsync(request.Email, ct))
            throw new AppException("Email already exists");

        if (!string.IsNullOrWhiteSpace(request.Mobile) && await _users.MobileExistsAsync(request.Mobile, ct))
            throw new AppException("Mobile already exists");

        var agent = new User
        {
            Name = request.Name.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
            Role = UserRole.AGENT,
            Status = UserStatus.ACTIVE,
            PasswordHash = PasswordHasher.Hash(request.Password)
        };

        await _users.AddAsync(agent, ct);
        return _mapper.Map<UserDto>(agent);
    }

    public async Task<PagedResult<UserDto>> ListAgentsAsync(AgentListFilterDto filter, PaginationQueryDto page, CancellationToken ct)
    {
        var (items, total) = await _users.ListAgentsAsync(filter.Search, filter.Status, page.Page, page.PageSize, ct);
        return new PagedResult<UserDto>
        {
            Items = items.Select(_mapper.Map<UserDto>).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total
        };
    }

    public async Task<long> EnqueueExcelUploadAsync(byte[] fileBytes, string fileName, CancellationToken ct)
    {
        if (fileBytes.Length <= 0) throw new AppException("Empty file");

        var startedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "EnqueueExcelUploadAsync started. userId={UserId} fileName={FileName} sizeBytes={SizeBytes}",
            _currentUser.UserId,
            fileName,
            fileBytes.Length);

        var anotherProcessing = await _db.ExcelUploads.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Status == ExcelUploadStatus.PROCESSING, ct);
        if (anotherProcessing)
            throw new AppException("Another Excel upload is currently processing. Please wait and try again.", 409);

        var archiveResult = await ArchiveOperationalDataAsync(ct);
        _logger.LogInformation(
            "EnqueueExcelUploadAsync archived existing data before upload. userId={UserId} counts={Counts}",
            _currentUser.UserId,
            string.Join(", ", archiveResult.ArchivedCounts.Select(kv => $"{kv.Key}={kv.Value}")));

        var upload = new ExcelUpload
        {
            FileName = fileName,
            FilePath = null,
            UploadedBy = _currentUser.UserId,
            Status = ExcelUploadStatus.QUEUED
        };

        await _uploads.AddAsync(upload, ct);
        await _uploads.SaveChangesAsync(ct);

        // Persist to disk for background processing
        var safeName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = $"upload_{upload.Id}.xlsx";

        var baseDir = Path.Combine(AppContext.BaseDirectory, "uploads", "excel");
        Directory.CreateDirectory(baseDir);
        var filePath = Path.Combine(baseDir, $"{upload.Id}_{safeName}");

        await File.WriteAllBytesAsync(filePath, fileBytes, ct);
        upload.FilePath = filePath;
        await _uploads.SaveChangesAsync(ct);

        ExcelUploadBackgroundQueue.Enqueue(upload.Id);

        _logger.LogInformation(
            "EnqueueExcelUploadAsync queued. uploadId={UploadId} filePath={FilePath} elapsedMs={ElapsedMs}",
            upload.Id,
            upload.FilePath,
            (DateTime.UtcNow - startedAt).TotalMilliseconds);

        return upload.Id;
    }

    public async Task ProcessExcelUploadAsync(long uploadId, CancellationToken ct)
    {
        _logger.LogInformation("ProcessExcelUploadAsync started. uploadId={UploadId}", uploadId);

        var upload = await _uploads.GetByIdAsync(uploadId, ct) ?? throw new AppException("Upload not found", 404);
        if (upload.Status == ExcelUploadStatus.COMPLETED)
            return;

        upload.Status = ExcelUploadStatus.PROCESSING;
        await _uploads.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(upload.FilePath) || !File.Exists(upload.FilePath))
            throw new AppException("Uploaded file not found on server");

        var bytes = await File.ReadAllBytesAsync(upload.FilePath, ct);
        await ProcessExcelBytesAsync(upload, bytes, ct);
    }

    // Backwards compatible entrypoint (if any other callers exist)
    public Task<long> UploadExcelAsync(byte[] fileBytes, string fileName, CancellationToken ct) =>
        EnqueueExcelUploadAsync(fileBytes, fileName, ct);

    private async Task<long> ProcessExcelBytesAsync(ExcelUpload upload, byte[] fileBytes, CancellationToken ct)
    {
        if (fileBytes.Length <= 0) throw new AppException("Empty file");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var startedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "ProcessExcelBytesAsync started. uploadId={UploadId} userId={UserId} fileName={FileName} sizeBytes={SizeBytes}",
            upload.Id,
            upload.UploadedBy,
            upload.FileName,
            fileBytes.Length);

        static string? Trunc(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s[..max];
        }

        static string? NullIfBlank(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }

        static decimal? ParseDecimalOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // remove commas/currency
            var cleaned = s.Trim().Replace(",", "");
            return decimal.TryParse(cleaned, out var v) ? v : null;
        }

        static int? ParseIntOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = s.Trim().Replace(",", "");
            return int.TryParse(cleaned, out var v) ? v : null;
        }

        static DateTime? ParseDateTimeOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var trimmed = s.Trim();
            if (DateTime.TryParse(trimmed, out var dt))
                return dt;
            if (double.TryParse(trimmed, out var oa))
                return DateTime.FromOADate(oa);
            return null;
        }

        static string NormalizeLookupKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return string.Concat(s.Trim().Where(ch => !char.IsWhiteSpace(ch))).ToLowerInvariant();
        }

        static async Task RetryAsync(Func<CancellationToken, Task> action, ILogger logger, string op, long uploadId, CancellationToken ct)
        {
            // 3 tries total (initial + 2 retries)
            var delayMs = 250;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await action(ct);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < 3)
                {
                    logger.LogWarning(ex, "Retryable failure. op={Op} uploadId={UploadId} attempt={Attempt}/3", op, uploadId, attempt);
                    await Task.Delay(delayMs, ct);
                    delayMs *= 2;
                }
            }
        }

        try
        {
            using var ms = new MemoryStream(fileBytes);

            ExcelPackage package;
            try
            {
                package = new ExcelPackage(ms);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "UploadExcelAsync invalid excel. uploadId={UploadId} fileName={FileName}",
                    upload.Id,
                    upload.FileName);

                upload.Status = ExcelUploadStatus.FAILED;
                await _uploadErrors.AddRangeAsync(new[]
                {
                    new ExcelUploadError
                    {
                        UploadId = upload.Id,
                        ExcelRowNumber = 0,
                        ErrorMessage = $"Invalid Excel file: {ex.Message}",
                        RawData = null
                    }
                }, ct);
                await _uploads.SaveChangesAsync(ct);
                throw new AppException($"Invalid Excel file: {ex.Message}");
            }

            using (package)
            {
                var ws = package.Workbook.Worksheets.FirstOrDefault()
                         ?? throw new AppException("No worksheet found");

                // Expected headers in row 1:
                // InternalId plus other Excel columns (A-K) depending on file format.
                var dimRows = ws.Dimension?.Rows ?? 0;
                var dimCols = ws.Dimension?.Columns ?? 0;

                static string NormHeader(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                    return string.Concat(s.Trim().Where(ch => !char.IsWhiteSpace(ch))).ToLowerInvariant();
                }

                var headerToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var headerLastCol = 0;
                for (var c = 1; c <= dimCols; c++)
                {
                    var cell = ws.Cells[1, c];
                    var h = cell.Text;
                    if (string.IsNullOrWhiteSpace(h))
                        h = cell.Value?.ToString();
                    if (string.IsNullOrWhiteSpace(h)) break;
                    headerLastCol = c;
                    var key = NormHeader(h);
                    if (!string.IsNullOrEmpty(key) && !headerToCol.ContainsKey(key))
                        headerToCol[key] = c;
                }

                int? Col(params string[] names)
                {
                    foreach (var n in names)
                    {
                        var key = NormHeader(n);
                        if (headerToCol.TryGetValue(key, out var col))
                            return col;
                    }
                    return null;
                }

                string Cell(int row, int? col)
                {
                    if (!col.HasValue) return string.Empty;
                    var cell = ws.Cells[row, col.Value];
                    var value = cell.Value;

                    if (value is DateTime dt)
                        return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                    if (value is double d)
                    {
                        var fmt = cell.Style.Numberformat.Format ?? string.Empty;
                        var looksLikeDate = fmt.Contains('d', StringComparison.OrdinalIgnoreCase)
                            || fmt.Contains('y', StringComparison.OrdinalIgnoreCase)
                            || (d is > 25000 and < 80000);
                        if (looksLikeDate)
                        {
                            try
                            {
                                return DateTime.FromOADate(d)
                                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                            }
                            catch
                            {
                                // fall through to numeric/text handling
                            }
                        }

                        // Prefer the stored numeric value (EPPlus Text can show scientific notation).
                        if (double.IsFinite(d) && Math.Abs(d - Math.Round(d)) < 0.0000001)
                            return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);

                        return d.ToString(CultureInfo.InvariantCulture);
                    }

                    if (value is float or decimal or long or int)
                        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(cell.Text))
                        return cell.Text.Trim();

                    return value?.ToString()?.Trim() ?? string.Empty;
                }

                bool RowHasContent(int row, int maxColsToScan)
                {
                    for (var c = 1; c <= Math.Min(dimCols, maxColsToScan); c++)
                    {
                        if (!string.IsNullOrWhiteSpace(Cell(row, c)))
                            return true;
                    }

                    return false;
                }

                DateTime? DateCell(int row, int? col)
                {
                    if (!col.HasValue) return null;
                    var cell = ws.Cells[row, col.Value];
                    DateTime? local = null;
                    if (cell.Value is DateTime dt) local = dt;
                    else if (cell.Value is double d)
                    {
                        try
                        {
                            var parsed = DateTime.FromOADate(d);
                            if (parsed.Year is >= 1900 and <= 2100)
                                local = parsed;
                        }
                        catch
                        {
                            // fall through
                        }
                    }

                    local ??= ParseDateTimeOrNull(Cell(row, col));
                    return IndiaDateTime.IstLocalToUtc(local);
                }

                // Duplicate plain "Hub" headers: first -> hub_name, second -> branch_hub.
                var hubColumns = new List<int>();
                for (var c = 1; c <= headerLastCol; c++)
                {
                    var headerText = Cell(1, c);
                    if (string.IsNullOrWhiteSpace(headerText)) break;
                    if (NormHeader(headerText) == "hub")
                        hubColumns.Add(c);
                }

                int? HubNameCol() =>
                    Col("Hub 1", "Hub1") ??
                    (hubColumns.Count > 0 ? hubColumns[0] : (int?)null) ??
                    Col("Hub", "HubName", "HUB");

                int? BranchHubCol() =>
                    Col("Hub 2", "Hub2") ??
                    (hubColumns.Count > 1 ? hubColumns[1] : (int?)null) ??
                    Col("Branch Hub", "BranchHub");

                var queryStatusLookups = await _db.QueryStatusLookups.AsNoTracking()
                    .Where(x => x.IsActive)
                    .ToListAsync(ct);
                var queryStatusByName = queryStatusLookups
                    .GroupBy(x => NormalizeLookupKey(x.QueryStatusLookupName))
                    .ToDictionary(g => g.Key, g => g.First().QueryStatusLookupId);
                var otherQueryStatusId = queryStatusLookups
                    .FirstOrDefault(x => NormalizeLookupKey(x.QueryStatusLookupName) == "other")
                    ?.QueryStatusLookupId;

                var defaultStatusLookupId = await _db.StatusLookups.AsNoTracking()
                    .Where(x => x.IsActive && x.LookupName == "Pending")
                    .Select(x => x.StatusLookupId)
                    .FirstOrDefaultAsync(ct);
                if (defaultStatusLookupId == 0)
                {
                    defaultStatusLookupId = await _db.StatusLookups.AsNoTracking()
                        .Where(x => x.IsActive)
                        .OrderBy(x => x.StatusLookupId)
                        .Select(x => x.StatusLookupId)
                        .FirstOrDefaultAsync(ct);
                }

                long? ResolveQueryStatusId(string? excelStatus)
                {
                    if (string.IsNullOrWhiteSpace(excelStatus)) return null;
                    var key = NormalizeLookupKey(excelStatus);
                    if (queryStatusByName.TryGetValue(key, out var id))
                        return id;

                    // Allow matching by description text when the sheet uses the long-form label.
                    var byDescription = queryStatusLookups.FirstOrDefault(x =>
                        NormalizeLookupKey(x.QueryStatusLookupDescription) == key);
                    return byDescription?.QueryStatusLookupId;
                }

                // EPPlus Dimension can be huge due to formatting; find last actual data row by scanning used headers.
                var lastDataRow = 1;
                if (dimRows > 1)
                {
                    var end = dimRows;
                    var maxCols = headerLastCol > 0 ? headerLastCol : Math.Min(dimCols, 20);
                    for (var r = end; r >= 2; r--)
                    {
                        if (RowHasContent(r, maxCols))
                        {
                            lastDataRow = r;
                            break;
                        }
                    }
                }

                upload.TotalRows = Math.Max(0, lastDataRow - 1);

                _logger.LogInformation(
                    "UploadExcelAsync worksheet loaded. uploadId={UploadId} sheetName={SheetName} dimRows={DimRows} dimCols={DimCols} lastDataRow={LastDataRow} totalRows={TotalRows}",
                    upload.Id,
                    ws.Name,
                    dimRows,
                    dimCols,
                    lastDataRow,
                    upload.TotalRows);

                const int batchSize = 500;
                var taskBatch = new List<TaskItem>(batchSize);
                var errorBatch = new List<ExcelUploadError>(batchSize);
                var seenInternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var agentEmailCache = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);

                async Task<long?> ResolveAgentIdByEmailAsync(string email)
                {
                    if (string.IsNullOrWhiteSpace(email)) return null;
                    var normalized = email.Trim();
                    if (agentEmailCache.TryGetValue(normalized, out var cached)) return cached;

                    var id = await _db.Users.AsNoTracking()
                        .Where(u => !u.IsDeleted && u.Role == UserRole.AGENT && u.Email != null && u.Email == normalized)
                        .Select(u => (long?)u.Id)
                        .FirstOrDefaultAsync(ct);

                    agentEmailCache[normalized] = id;
                    return id;
                }

                async Task FlushErrorsAsync()
                {
                    if (errorBatch.Count == 0) return;
                    await RetryAsync(
                        async innerCt =>
                        {
                            await _uploadErrors.AddRangeAsync(errorBatch, innerCt);
                            await _uploads.SaveChangesAsync(innerCt);
                        },
                        _logger,
                        op: "save_error_batch",
                        uploadId: upload.Id,
                        ct);
                    errorBatch.Clear();
                }

                async Task FlushTasksAsync()
                {
                    if (taskBatch.Count == 0) return;

                    // De-dupe within file/batch first (internal_id is unique in DB)
                    taskBatch = taskBatch
                        .GroupBy(x => x.InternalId)
                        .Select(g => g.First())
                        .ToList();

                    // De-dupe against existing DB records to prevent duplicates across retries/reruns.
                    var ids = taskBatch.Select(x => x.InternalId).ToList();
                    var existing = await _db.Tasks.AsNoTracking()
                        .Where(t => ids.Contains(t.InternalId))
                        .Select(t => t.InternalId)
                        .ToListAsync(ct);

                    if (existing.Count > 0)
                    {
                        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var dup in taskBatch.Where(x => existingSet.Contains(x.InternalId)).ToList())
                        {
                            _logger.LogInformation(
                                "UploadExcelAsync skipping duplicate InternalId already in DB. uploadId={UploadId} internalId={InternalId}",
                                upload.Id,
                                dup.InternalId);
                            taskBatch.Remove(dup);
                        }
                    }

                    if (taskBatch.Count == 0)
                    {
                        await FlushErrorsAsync();
                        return;
                    }

                    try
                    {
                        await RetryAsync(
                            async innerCt =>
                            {
                                await _tasks.AddRangeAsync(taskBatch, innerCt);
                                await _tasks.SaveChangesAsync(innerCt);

                                var assigned = taskBatch.Where(t => t.AssignedAgentId.HasValue).ToList();
                                if (assigned.Count > 0)
                                {
                                    var assignedAt = DateTime.UtcNow;
                                    var rows = assigned.Select(t => new TaskAssignment
                                    {
                                        TaskId = t.Id,
                                        AgentId = t.AssignedAgentId!.Value,
                                        AssignedBy = upload.UploadedBy,
                                        AssignedAt = assignedAt
                                    }).ToList();

                                    await _assignments.AddRangeAsync(rows, innerCt);
                                    await _tasks.SaveChangesAsync(innerCt);
                                }
                            },
                            _logger,
                            op: "save_task_batch",
                            uploadId: upload.Id,
                            ct);
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // isolate bad rows and continue
                        _logger.LogWarning(
                            dbEx,
                            "UploadExcelAsync task batch insert failed; falling back to per-row inserts. uploadId={UploadId} baseMessage={BaseMessage}",
                            upload.Id,
                            dbEx.GetBaseException().Message);

                        _db.ChangeTracker.Clear();

                    // Important: task entities created while the upload was tracked may have navigation fixups
                    // (e.g., CreatedFromUpload set). After Clear(), re-adding the task would cause EF to
                    // treat the referenced upload as a new entity and attempt to INSERT it, resulting in
                    // "Duplicate entry for key excel_uploads.PRIMARY".
                    static void StripNavigations(TaskItem t)
                    {
                        t.AssignedAgent = null;
                        t.LastUpdate = null;
                        t.CreatedFromUpload = null;
                        t.Updates = new List<TaskUpdate>();
                        t.Assignments = new List<TaskAssignment>();
                        t.Acknowledgements = new List<TaskAcknowledgement>();
                    }

                        foreach (var t in taskBatch)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                await RetryAsync(
                                    async innerCt =>
                                    {
                                    StripNavigations(t);
                                        await _db.Tasks.AddAsync(t, innerCt);
                                        await _db.SaveChangesAsync(innerCt);

                                        if (t.AssignedAgentId.HasValue)
                                        {
                                            await _db.TaskAssignments.AddAsync(new TaskAssignment
                                            {
                                                TaskId = t.Id,
                                                AgentId = t.AssignedAgentId.Value,
                                                AssignedBy = upload.UploadedBy,
                                                AssignedAt = DateTime.UtcNow
                                            }, innerCt);
                                            await _db.SaveChangesAsync(innerCt);
                                        }
                                    },
                                    _logger,
                                    op: "save_task_row",
                                    uploadId: upload.Id,
                                    ct);
                            }
                            catch (Exception ex)
                            {
                                upload.FailedRows++;
                                errorBatch.Add(new ExcelUploadError
                                {
                                    UploadId = upload.Id,
                                    ExcelRowNumber = 0,
                                    ErrorMessage = $"DB insert failed for InternalId={t.InternalId}: {ex.GetBaseException().Message}",
                                    RawData = t.RawData
                                });
                                _db.ChangeTracker.Clear();
                            }
                        }
                    }
                    finally
                    {
                        taskBatch.Clear();
                    }
                }

                for (var row = 2; row <= lastDataRow; row++)
                {
                    try
                    {
                        // Header-based mapping (supports varying Excel formats).
                        var customerName = Trunc(NullIfBlank(Cell(row, Col("Customer Name", "CustomerName"))), 255);
                        if (customerName is null)
                            continue;

                        var srNo = ParseIntOrNull(Cell(row, Col("Sr No", "SrNo", "Sr. No", "SR NO", "Sr_No")));
                        var hubName = Trunc(NullIfBlank(Cell(row, HubNameCol())), 100);
                        var appNo = Trunc(NullIfBlank(Cell(row, Col("Application Number", "ApplicationNo", "Application No", "Application"))), 100);
                        var entityName = Trunc(NullIfBlank(Cell(row, Col("Entity Name", "EntityName", "Entity"))), 255);
                        var loanType = Trunc(NullIfBlank(Cell(row, Col("Loan Type", "LoanType"))), 100);
                        var loanAmount = ParseDecimalOrNull(Cell(row, Col("Loan amount", "Loan Amount", "LoanAmount")));
                        var customerAddress = NullIfBlank(Cell(row, Col("Address", "Customer Address", "CustomerAddress")));
                        var location = Trunc(NullIfBlank(Cell(row, Col("Location"))), 255);
                        var pinCode = Trunc(NullIfBlank(Cell(row, Col("Pin Code", "PinCode", "Pincode", "PIN", "Pin"))), 45);
                        var branchHub = Trunc(NullIfBlank(Cell(row, BranchHubCol())), 100);
                        var mobileNo = Trunc(NullIfBlank(Cell(row, Col("Mobile No", "MobileNo", "Mobile"))), 20);
                        var visitDate = DateCell(row, Col("Visit date", "Visit Date", "VisitDate"));
                        var pdDate = DateCell(row, Col("PD date", "PD Date", "PdDate", "PD Date"));
                        var excelQueryStatus = NullIfBlank(Cell(row, Col("Status")));
                        var queryText = NullIfBlank(Cell(row, Col("Query")));
                        var agentEmail = Trunc(NullIfBlank(Cell(row, Col("Agent Email", "AgentEmail", "Mail ID", "MailID", "Mail Id"))), 150);

                        // InternalId rules:
                        // - Prefer explicit InternalId column if present
                        // - Else fall back to Application Number (usually unique)
                        // - Else fall back to Sr No
                        // - Else generate stable fallback
                        var internalId =
                            NullIfBlank(Cell(row, Col("InternalId", "Internal Id", "Internal"))) ??
                            appNo ??
                            (srNo.HasValue ? srNo.Value.ToString() : null);

                        if (internalId is not null && internalId.Length > 50)
                            internalId = internalId[..50];

                        // If InternalId is missing, generate a stable fallback so row can still be inserted.
                        // This avoids "failed records" while keeping uniqueness.
                        internalId ??= $"U{upload.Id:D6}R{row:D6}";

                        if (!seenInternalIds.Add(internalId))
                            continue;

                        long? resolvedAgentId = null;
                        if (!string.IsNullOrWhiteSpace(agentEmail))
                        {
                            resolvedAgentId = await ResolveAgentIdByEmailAsync(agentEmail);
                            if (!resolvedAgentId.HasValue)
                            {
                                _logger.LogWarning(
                                    "UploadExcelAsync agent not found; importing without assignment. uploadId={UploadId} row={Row} email={Email}",
                                    upload.Id,
                                    row,
                                    agentEmail);
                            }
                        }

                        long? queryStatusLookupId = null;
                        string? taskStatusOther = null;
                        if (!string.IsNullOrWhiteSpace(excelQueryStatus))
                        {
                            queryStatusLookupId = ResolveQueryStatusId(excelQueryStatus);
                            if (!queryStatusLookupId.HasValue)
                            {
                                _logger.LogWarning(
                                    "UploadExcelAsync unknown Status value; importing without task_status. uploadId={UploadId} row={Row} status={Status}",
                                    upload.Id,
                                    row,
                                    excelQueryStatus);
                            }
                            else
                            {
                                var isOther = otherQueryStatusId.HasValue &&
                                              queryStatusLookupId.Value == otherQueryStatusId.Value;
                                if (isOther)
                                {
                                    taskStatusOther = queryText;
                                    if (string.IsNullOrWhiteSpace(taskStatusOther))
                                    {
                                        _logger.LogWarning(
                                            "UploadExcelAsync Status is Other but Query is blank; importing without task_status_other. uploadId={UploadId} row={Row}",
                                            upload.Id,
                                            row);
                                        taskStatusOther = null;
                                    }
                                    else
                                    {
                                        taskStatusOther = Trunc(taskStatusOther, 255);
                                    }
                                }
                            }
                        }

                        var rawObj = new Dictionary<string, object?>
                        {
                            ["InternalId"] = internalId,
                            ["SrNo"] = srNo,
                            ["HubName"] = hubName,
                            ["ApplicationNo"] = appNo,
                            ["CustomerName"] = customerName,
                            ["EntityName"] = entityName,
                            ["LoanType"] = loanType,
                            ["LoanAmount"] = loanAmount,
                            ["CustomerAddress"] = customerAddress,
                            ["Location"] = location,
                            ["PinCode"] = pinCode,
                            ["BranchHub"] = branchHub,
                            ["MobileNo"] = mobileNo,
                            ["VisitDate"] = visitDate,
                            ["PdDate"] = pdDate,
                            ["QueryStatus"] = excelQueryStatus,
                            ["QueryStatusLookupId"] = queryStatusLookupId,
                            ["TaskStatusOther"] = taskStatusOther,
                            ["StatusLookupId"] = defaultStatusLookupId == 0 ? null : defaultStatusLookupId,
                            ["AgentEmail"] = agentEmail
                        };

                        taskBatch.Add(new TaskItem
                        {
                            InternalId = internalId,
                            SrNo = srNo,
                            HubName = hubName,
                            ApplicationNo = appNo,
                            CustomerName = customerName,
                            EntityName = entityName,
                            LoanType = loanType,
                            LoanAmount = loanAmount,
                            CustomerAddress = customerAddress,
                            Location = location,
                            PinCode = pinCode,
                            BranchHub = branchHub,
                            MobileNo = mobileNo,
                            VisitDate = visitDate,
                            PdDate = pdDate,
                            QueryStatusLookupId = queryStatusLookupId,
                            TaskStatusOther = taskStatusOther,
                            StatusLookupId = defaultStatusLookupId == 0 ? null : defaultStatusLookupId,
                            AssignedAgentId = resolvedAgentId,
                            Status = TaskStatus.NEW,
                            RawData = JsonSerializer.Serialize(rawObj),
                            CreatedFromUploadId = upload.Id
                        });

                        upload.SuccessRows++;

                        if (upload.SuccessRows % 500 == 0)
                        {
                            _logger.LogInformation(
                                "UploadExcelAsync progress. uploadId={UploadId} processedRow={Row} successRows={SuccessRows} failedRows={FailedRows}",
                                upload.Id,
                                row,
                                upload.SuccessRows,
                                upload.FailedRows);
                        }

                        if (taskBatch.Count >= batchSize)
                        {
                            await FlushTasksAsync();
                            await FlushErrorsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Don't fail the whole row due to missing/invalid optional cells.
                        // Record the issue but keep processing.
                        errorBatch.Add(new ExcelUploadError
                        {
                            UploadId = upload.Id,
                            ExcelRowNumber = row,
                            ErrorMessage = ex.Message,
                            RawData = JsonSerializer.Serialize(new
                            {
                                row,
                                a = ws.Cells[row, 1].Text,
                                b = ws.Cells[row, 2].Text,
                                c = ws.Cells[row, 3].Text,
                                d = ws.Cells[row, 4].Text,
                                e = ws.Cells[row, 5].Text,
                                f = ws.Cells[row, 6].Text,
                                g = ws.Cells[row, 7].Text,
                                h = ws.Cells[row, 8].Text,
                                i = ws.Cells[row, 9].Text,
                                j = ws.Cells[row, 10].Text,
                                k = ws.Cells[row, 11].Text
                            })
                        });
                    }
                }

                // final flush
                await FlushTasksAsync();
                await FlushErrorsAsync();
            }

            _logger.LogInformation(
                "UploadExcelAsync parse completed. uploadId={UploadId} totalRows={TotalRows} successRows={SuccessRows} failedRows={FailedRows} tasksToAdd={TasksToAdd} errorsToAdd={ErrorsToAdd} elapsedMs={ElapsedMs}",
                upload.Id,
                upload.TotalRows,
                upload.SuccessRows,
                upload.FailedRows,
                upload.SuccessRows,
                upload.FailedRows,
                (DateTime.UtcNow - startedAt).TotalMilliseconds);

            // Parsing issues are recorded in excel_upload_errors; we still consider the upload completed
            // as long as processing ran.
            var finalStatus = ExcelUploadStatus.COMPLETED;

            try
            {
                _logger.LogInformation(
                    "UploadExcelAsync saving to database. uploadId={UploadId} status={Status}",
                    upload.Id,
                    finalStatus);

                upload.Status = finalStatus;
                await _uploads.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(
                    dbEx,
                    "UploadExcelAsync DbUpdateException. uploadId={UploadId} baseMessage={BaseMessage}",
                    upload.Id,
                    dbEx.GetBaseException().Message);

                upload.Status = ExcelUploadStatus.FAILED;
                var finalError = new ExcelUploadError
                {
                    UploadId = upload.Id,
                    ExcelRowNumber = 0,
                    ErrorMessage = $"Database error while saving uploaded rows: {dbEx.GetBaseException().Message}",
                    RawData = null
                };
                await _uploadErrors.AddRangeAsync(new[] { finalError }, ct);
                await _uploads.SaveChangesAsync(ct);
                throw new AppException("Upload failed while saving to database. Please fix duplicates/invalid rows and retry.");
            }

            _logger.LogInformation(
                "UploadExcelAsync completed. uploadId={UploadId} status={Status} totalElapsedMs={ElapsedMs}",
                upload.Id,
                upload.Status,
                (DateTime.UtcNow - startedAt).TotalMilliseconds);

            return upload.Id;
        }
        catch (OperationCanceledException oce)
        {
            // App recycle / shutdown while processing: mark as QUEUED so it resumes on next start.
            _logger.LogWarning(oce, "UploadExcelAsync canceled; will retry later. uploadId={UploadId}", upload.Id);
            upload.Status = ExcelUploadStatus.QUEUED;
            try { await _uploads.SaveChangesAsync(CancellationToken.None); } catch { /* best effort */ }
            throw;
        }
        catch (AppException)
        {
            // already mapped to an HTTP status code by middleware
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "UploadExcelAsync unexpected exception. uploadId={UploadId} fileName={FileName}",
                upload.Id,
                upload.FileName);

            upload.Status = ExcelUploadStatus.FAILED;
            try
            {
                await _uploadErrors.AddRangeAsync(new[]
            {
                new ExcelUploadError
                {
                    UploadId = upload.Id,
                    ExcelRowNumber = 0,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    RawData = null
                }
            }, ct);
                await _uploads.SaveChangesAsync(ct);
            }
            catch
            {
                // ignore secondary failure while recording error
            }
            throw new AppException("Upload failed due to an unexpected server error. Please try again.");
        }
    }

    public async Task AssignTasksAsync(AssignTasksRequestDto request, CancellationToken ct)
    {
        var agent = await _users.GetByIdAsync(request.AgentId, ct);
        if (agent is null || agent.Role != UserRole.AGENT) throw new NotFoundException("Agent not found");

        var tasks = await _tasks.GetByIdsAsync(request.TaskIds, ct);
        if (tasks.Count != request.TaskIds.Distinct().Count())
            throw new AppException("One or more tasks not found");

        var now = DateTime.UtcNow;
        foreach (var t in tasks)
        {
            // Preserve the existing task status. Assignment changes ownership only;
            // the task's progress (NEW / PENDING / VISITED / FOLLOW_UP_REQUIRED / etc.)
            // must not be reset just because it was (re)allocated to an agent.
            t.AssignedAgentId = agent.Id;
            t.UpdatedAt = now;
        }

        var assignmentRows = tasks.Select(t => new TaskAssignment
        {
            TaskId = t.Id,
            AgentId = agent.Id,
            AssignedBy = _currentUser.UserId,
            AssignedAt = now
        }).ToList();

        await _assignments.AddRangeAsync(assignmentRows, ct);
        await _tasks.SaveChangesAsync(ct);
    }

    public async Task ReassignTasksAsync(ReassignTasksRequestDto request, CancellationToken ct)
    {
        var from = await _users.GetByIdAsync(request.FromAgentId, ct);
        var to = await _users.GetByIdAsync(request.ToAgentId, ct);
        if (from is null || from.Role != UserRole.AGENT) throw new NotFoundException("From agent not found");
        if (to is null || to.Role != UserRole.AGENT) throw new NotFoundException("To agent not found");

        var tasks = await _tasks.GetByIdsAsync(request.TaskIds, ct);
        var now = DateTime.UtcNow;
        foreach (var t in tasks)
        {
            if (t.AssignedAgentId != from.Id)
                throw new AppException($"Task {t.Id} is not assigned to agent {from.Id}");

            // Preserve the existing task status on reassignment as well.
            // Only ownership and acknowledgement (which is per-agent) are reset.
            t.AssignedAgentId = to.Id;
            t.Acknowledged = false;
            t.AcknowledgedAt = null;
            t.UpdatedAt = now;
        }

        var assignmentRows = tasks.Select(t => new TaskAssignment
        {
            TaskId = t.Id,
            AgentId = to.Id,
            AssignedBy = _currentUser.UserId,
            AssignedAt = now
        }).ToList();

        await _assignments.AddRangeAsync(assignmentRows, ct);
        await _tasks.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<TaskListItemDto>> GetTasksAsync(TaskFilterDto filter, PaginationQueryDto page, CancellationToken ct)
    {
        var (items, total) = await _tasks.ListAsync(
            filter.AgentId, filter.FromDate, filter.ToDate, filter.Status, filter.Search,
            page.Page, page.PageSize, ct);

        return new PagedResult<TaskListItemDto>
        {
            Items = items.Select(_mapper.Map<TaskListItemDto>).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total
        };
    }

    public async Task<TaskDetailsDto> GetTaskDetailsAsync(long taskId, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                   ?? throw new NotFoundException("Task not found");

        return _mapper.Map<TaskDetailsDto>(task);
    }

    public async Task<TaskDetailsDto> GetTaskDetailsForCallerAsync(long taskId, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                   ?? throw new NotFoundException("Task not found");

        if (_currentUser.Role == UserRole.AGENT && task.AssignedAgentId != _currentUser.UserId)
            throw new ForbiddenException("You cannot access tasks assigned to other agents");

        return _mapper.Map<TaskDetailsDto>(task);
    }

    public async Task<TaskDetailsDto> AddTaskFollowUpAsync(long taskId, AddTaskUpdateRequestDto request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

        if (_currentUser.Role == UserRole.AGENT && task.AssignedAgentId != _currentUser.UserId)
            throw new ForbiddenException("You cannot update tasks assigned to other agents");

        var update = new TaskUpdate
        {
            TaskId = taskId,
            AgentId = _currentUser.UserId,
            Status = request.Status,
            Comment = request.Comment,
            MeetingPersonName = request.MeetingPersonName,
            MeetingPersonMobile = request.MeetingPersonMobile,
            FollowupDate = request.FollowupDate
        };

        await _updates.AddAsync(update, ct);

        task.Status = Enum.TryParse<TaskStatus>(request.Status.ToString(), out var mapped)
            ? mapped
            : TaskStatus.PENDING;
        task.UpdatedAt = DateTime.UtcNow;

        await _tasks.SaveChangesAsync(ct);

        task.LastUpdateId = update.Id;
        await _tasks.SaveChangesAsync(ct);

        var updated = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                      ?? throw new NotFoundException("Task not found");

        return _mapper.Map<TaskDetailsDto>(updated);
    }

    public async Task<TaskDetailsDto> UpdateTaskAsync(long taskId, UpdateTaskRequestDto request, CancellationToken ct)
    {
        static string? NormalizeOtherText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var trimmed = text.Trim();
            return trimmed.Length <= 255 ? trimmed : trimmed[..255];
        }

        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

        // Allow ADMIN to update any task; AGENT can update only tasks assigned to them.
        if (_currentUser.Role == UserRole.AGENT && task.AssignedAgentId != _currentUser.UserId)
            throw new ForbiddenException("You cannot update tasks assigned to other agents");

        var now = DateTime.UtcNow;

        if (request.Status.HasValue)
            task.Status = request.Status.Value;

        if (request.DueDate.HasValue)
            task.DueDate = IndiaDateTime.IstDateOnlyToUtc(request.DueDate.Value);

        if (request.PdDate.HasValue)
            task.PdDate = IndiaDateTime.ToUtcForStorage(request.PdDate.Value);

        if (request.PdStatus.HasValue)
        {
            var pdStatusExists = await _db.StatusLookups.AsNoTracking()
                .AnyAsync(x => x.StatusLookupId == request.PdStatus.Value && x.IsActive, ct);
            if (!pdStatusExists)
                throw new AppException("Invalid pdStatus. Choose an active value from status_lookup.", 400);

            task.StatusLookupId = request.PdStatus.Value;
        }

        if (request.TaskStatusLookupId.HasValue)
        {
            var queryStatus = await _db.QueryStatusLookups.AsNoTracking()
                .FirstOrDefaultAsync(x => x.QueryStatusLookupId == request.TaskStatusLookupId.Value && x.IsActive, ct);
            if (queryStatus is null)
                throw new AppException("Invalid taskStatus. Choose an active value from query_status_lookup.", 400);

            var isOther = string.Equals(queryStatus.QueryStatusLookupName, "Other", StringComparison.OrdinalIgnoreCase);
            if (isOther && string.IsNullOrWhiteSpace(request.OtherText))
                throw new AppException("other_text is required when taskStatus is Other.", 400);

            task.QueryStatusLookupId = request.TaskStatusLookupId.Value;
        }

        if (request.OtherTextProvided)
            task.TaskStatusOther = NormalizeOtherText(request.OtherText);

        task.UpdatedAt = now;

        try
        {
            await _tasks.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Some environments store task status as enum('OPEN','IN_PROGRESS','CLOSED') in `tasks.status`,
            // while the EF model uses `current_status` with legacy values (NEW/PENDING/...).
            // If the DB rejects the status value, fall back to a direct SQL update using a safe mapping.
            // This makes the endpoint work across both schemas without requiring client changes.

            static string MapToOpenInProgressClosed(TaskStatus s) =>
                s switch
                {
                    TaskStatus.NEW => "OPEN",
                    TaskStatus.PENDING => "IN_PROGRESS",
                    TaskStatus.FOLLOW_UP_REQUIRED => "IN_PROGRESS",
                    TaskStatus.CLOSED => "CLOSED",
                    TaskStatus.CONVERTED => "CLOSED",
                    _ => "IN_PROGRESS"
                };

            var baseMsg = ex.GetBaseException().Message;

            if (request.Status.HasValue || request.DueDate.HasValue || request.PdDate.HasValue ||
                request.PdStatus.HasValue || request.TaskStatusLookupId.HasValue || request.OtherTextProvided)
            {
                var dueDate = request.DueDate.HasValue ? IndiaDateTime.IstDateOnlyToUtc(request.DueDate.Value) : (DateTime?)null;
                var pdDate = IndiaDateTime.ToUtcForStorage(request.PdDate);
                var dbNow = IndiaDateTime.ToDbValue(now);
                var dbDueDate = IndiaDateTime.ToDbValue(dueDate);
                var dbPdDate = IndiaDateTime.ToDbValue(pdDate);
                var pdStatusId = request.PdStatus;
                var taskStatusId = request.TaskStatusLookupId;
                var taskStatusOther = request.OtherTextProvided ? NormalizeOtherText(request.OtherText) : null;
                var mappedStatus = request.Status.HasValue
                    ? MapToOpenInProgressClosed(request.Status.Value)
                    : null;

                async Task TryRawUpdateAsync(string statusColumn)
                {
                    if (mappedStatus is null && dueDate is null && pdDate is null)
                        return;

                    try
                    {
                        // Use parameters to avoid SQL injection.
                        if (mappedStatus is not null && dueDate is not null && pdDate is not null)
                        {
#pragma warning disable EF1002
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `{statusColumn}` = @p1, `due_date` = @p2, `pd_date` = @p3 WHERE `id` = @p4;",
                                dbNow, mappedStatus, dbDueDate!.Value, dbPdDate!.Value, taskId, ct);
#pragma warning restore EF1002
                        }
                        else if (mappedStatus is not null && dueDate is not null)
                        {
#pragma warning disable EF1002
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `{statusColumn}` = @p1, `due_date` = @p2 WHERE `id` = @p3;",
                                dbNow, mappedStatus, dbDueDate!.Value, taskId, ct);
#pragma warning restore EF1002
                        }
                        else if (mappedStatus is not null && pdDate is not null)
                        {
#pragma warning disable EF1002
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `{statusColumn}` = @p1, `pd_date` = @p2 WHERE `id` = @p3;",
                                dbNow, mappedStatus, dbPdDate!.Value, taskId, ct);
#pragma warning restore EF1002
                        }
                        else if (dueDate is not null && pdDate is not null)
                        {
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `due_date` = @p1, `pd_date` = @p2 WHERE `id` = @p3;",
                                dbNow, dbDueDate!.Value, dbPdDate!.Value, taskId, ct);
                        }
                        else if (mappedStatus is not null)
                        {
#pragma warning disable EF1002
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `{statusColumn}` = @p1 WHERE `id` = @p2;",
                                dbNow, mappedStatus, taskId, ct);
#pragma warning restore EF1002
                        }
                        else if (dueDate is not null)
                        {
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `due_date` = @p1 WHERE `id` = @p2;",
                                dbNow, dbDueDate!.Value, taskId, ct);
                        }
                        else if (pdDate is not null)
                        {
                            await _db.Database.ExecuteSqlRawAsync(
                                $"UPDATE `tasks` SET `updated_at` = @p0, `pd_date` = @p1 WHERE `id` = @p2;",
                                dbNow, dbPdDate!.Value, taskId, ct);
                        }

                        // If one of the columns exists and the value is valid, we are done.
                    }
                    catch
                    {
                        // ignore and try alternate column name
                    }
                }

                await TryRawUpdateAsync("current_status");
                await TryRawUpdateAsync("status");

                if (pdStatusId.HasValue)
                {
                    try
                    {
                        await _db.Database.ExecuteSqlRawAsync(
                            "UPDATE `tasks` SET `updated_at` = @p0, `status` = @p1 WHERE `id` = @p2;",
                            dbNow, pdStatusId.Value, taskId, ct);
                    }
                    catch
                    {
                        // ignore; EF reload below will reflect whether update succeeded
                    }
                }

                if (taskStatusId.HasValue)
                {
                    try
                    {
                        if (taskStatusOther is not null)
                        {
                            await _db.Database.ExecuteSqlRawAsync(
                                "UPDATE `tasks` SET `updated_at` = @p0, `task_status` = @p1, `task_status_other` = @p2 WHERE `id` = @p3;",
                                dbNow, taskStatusId.Value, taskStatusOther, taskId, ct);
                        }
                        else
                        {
                            await _db.Database.ExecuteSqlRawAsync(
                                "UPDATE `tasks` SET `updated_at` = @p0, `task_status` = @p1 WHERE `id` = @p2;",
                                dbNow, taskStatusId.Value, taskId, ct);
                        }
                    }
                    catch
                    {
                        // ignore; EF reload below will reflect whether update succeeded
                    }
                }
                else if (request.OtherTextProvided)
                {
                    try
                    {
                        await _db.Database.ExecuteSqlRawAsync(
                            "UPDATE `tasks` SET `updated_at` = @p0, `task_status_other` = @p1 WHERE `id` = @p2;",
                            dbNow, taskStatusOther ?? string.Empty, taskId, ct);
                    }
                    catch
                    {
                        // ignore; EF reload below will reflect whether update succeeded
                    }
                }
            }
            else
            {
                throw new AppException("Provide at least one field: status, pdStatus, taskStatusId, pdDate, dueDate, or other_text.");
            }

            // If the raw update didn't work, bubble up the root cause for visibility.
            // (This will show e.g. "Unknown column ..." or enum truncation errors.)
            // Note: SaveChanges failed, so the EF-tracked entity may not match DB; we reload below.
            if (!string.IsNullOrWhiteSpace(baseMsg))
                _logger.LogWarning(ex, "UpdateTaskAsync SaveChanges failed; attempted raw fallback. taskId={TaskId} msg={Msg}", taskId, baseMsg);
        }

        // Return updated task details (including history) for convenience.
        var updated = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                      ?? throw new NotFoundException("Task not found");

        return _mapper.Map<TaskDetailsDto>(updated);
    }

    public async Task<(IReadOnlyList<TasksPerAgentReportRowDto> tasksPerAgent, StatusSummaryReportDto statusSummary)> GetReportsAsync(
        TaskFilterDto filter,
        CancellationToken ct)
    {
        // Base filtered tasks (soft-delete protected)
        var q = _db.Tasks.AsNoTracking();

        if (filter.AgentId.HasValue) q = q.Where(x => x.AssignedAgentId == filter.AgentId.Value);
        if (filter.Status.HasValue) q = q.Where(x => x.Status == filter.Status.Value);
        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.CreatedAt >= from);
        }

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
            q = q.Where(x => x.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(x =>
                x.InternalId.Contains(s) ||
                (x.ApplicationNo != null && x.ApplicationNo.Contains(s)) ||
                (x.CustomerName != null && x.CustomerName.Contains(s)) ||
                (x.MobileNo != null && x.MobileNo.Contains(s)) ||
                (x.EntityName != null && x.EntityName.Contains(s)));
        }

        var statusCounts = await q.GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var summary = new StatusSummaryReportDto
        {
            Counts = statusCounts.ToDictionary(x => x.Status, x => x.Count)
        };

        var perAgent = await q.Where(x => x.AssignedAgentId != null)
            .GroupBy(x => x.AssignedAgentId!.Value)
            .Select(g => new
            {
                AgentId = g.Key,
                Total = g.LongCount(),
                Open = g.LongCount(x => x.Status == TaskStatus.NEW),
                InProgress = g.LongCount(x => x.Status == TaskStatus.PENDING || x.Status == TaskStatus.FOLLOW_UP_REQUIRED),
                Closed = g.LongCount(x => x.Status == TaskStatus.CLOSED)
            })
            .ToListAsync(ct);

        var agentIds = perAgent.Select(x => x.AgentId).ToList();
        var agentMap = await _db.Users.AsNoTracking()
            .Where(x => agentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var rows = perAgent.Select(x => new TasksPerAgentReportRowDto
        {
            AgentId = x.AgentId,
            AgentName = agentMap.TryGetValue(x.AgentId, out var name) ? name : $"Agent {x.AgentId}",
            TotalTasks = x.Total,
            OpenTasks = x.Open,
            InProgressTasks = x.InProgress,
            ClosedTasks = x.Closed
        }).OrderByDescending(x => x.TotalTasks).ToList();

        return (rows, summary);
    }

    public async Task<(byte[] FileBytes, string FileName)> ExportReportsToExcelAsync(TaskFilterDto filter, CancellationToken ct)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        const long queryStatusRequiresOtherText1 = 1;
        const long queryStatusRequiresOtherText2 = 13;

        var tasks = await _tasks.ListAllAsync(
            filter.AgentId, filter.FromDate, filter.ToDate, filter.Status, filter.Search, ct);

        var statusLookupMap = await _db.StatusLookups.AsNoTracking()
            .ToDictionaryAsync(x => x.StatusLookupId, x => x.LookupName, ct);

        var queryStatusLookupMap = await _db.QueryStatusLookups.AsNoTracking()
            .ToDictionaryAsync(x => x.QueryStatusLookupId, x => x.QueryStatusLookupName, ct);

        var agentIds = tasks.Where(x => x.AssignedAgentId.HasValue)
            .Select(x => x.AssignedAgentId!.Value)
            .Distinct()
            .ToList();

        var agentMap = agentIds.Count == 0
            ? new Dictionary<long, User>()
            : await _db.Users.AsNoTracking()
                .Where(x => agentIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Sheet1");

        ws.Cells[1, 1].Value = "Sr No";
        ws.Cells[1, 2].Value = "Hub 1";
        ws.Cells[1, 3].Value = "Application Number";
        ws.Cells[1, 4].Value = "Customer Name";
        ws.Cells[1, 5].Value = "Entity Name";
        ws.Cells[1, 6].Value = "Loan Type";
        ws.Cells[1, 7].Value = "Loan amount";
        ws.Cells[1, 8].Value = "Address";
        ws.Cells[1, 9].Value = "Location";
        ws.Cells[1, 10].Value = "Pin Code";
        ws.Cells[1, 11].Value = "Hub 2";
        ws.Cells[1, 12].Value = "Mobile No";
        ws.Cells[1, 13].Value = "Status";
        ws.Cells[1, 14].Value = "Visit date";
        ws.Cells[1, 15].Value = "PD date";
        ws.Cells[1, 16].Value = "PD status";
        ws.Cells[1, 17].Value = "Task Status";
        ws.Cells[1, 18].Value = "Query";
        ws.Cells[1, 19].Value = "Agent Email";
        ws.Cells[1, 20].Value = "Agent Mobile No.";

        var row = 2;
        foreach (var task in tasks)
        {
            ws.Cells[row, 1].Value = task.SrNo;
            ws.Cells[row, 2].Value = task.HubName;
            ws.Cells[row, 3].Value = task.ApplicationNo;
            ws.Cells[row, 4].Value = task.CustomerName;
            ws.Cells[row, 5].Value = task.EntityName;
            ws.Cells[row, 6].Value = task.LoanType;
            ws.Cells[row, 7].Value = task.LoanAmount;
            ws.Cells[row, 8].Value = task.CustomerAddress;
            ws.Cells[row, 9].Value = task.Location;
            SetTextCell(ws.Cells[row, 10], task.PinCode);
            ws.Cells[row, 11].Value = task.BranchHub;
            SetTextCell(ws.Cells[row, 12], task.MobileNo);
            ws.Cells[row, 13].Value = task.Status.ToString();

            SetDateCell(ws.Cells[row, 14], task.VisitDate);
            SetDateCell(ws.Cells[row, 15], task.PdDate);

            ws.Cells[row, 16].Value = task.StatusLookupId.HasValue &&
                                      statusLookupMap.TryGetValue(task.StatusLookupId.Value, out var pdStatus)
                ? pdStatus
                : null;

            ws.Cells[row, 17].Value = task.QueryStatusLookupId.HasValue &&
                                      queryStatusLookupMap.TryGetValue(task.QueryStatusLookupId.Value, out var taskStatus)
                ? taskStatus
                : null;

            var includeQuery = task.QueryStatusLookupId is queryStatusRequiresOtherText1 or queryStatusRequiresOtherText2;
            ws.Cells[row, 18].Value = includeQuery ? task.TaskStatusOther : null;

            if (task.AssignedAgentId.HasValue && agentMap.TryGetValue(task.AssignedAgentId.Value, out var agent))
            {
                ws.Cells[row, 19].Value = agent.Email;
                SetTextCell(ws.Cells[row, 20], agent.Mobile);
            }

            row++;
        }

        ws.Cells[1, 1, Math.Max(row - 1, 1), 20].AutoFitColumns();

        var bytes = package.GetAsByteArray();
        var exportDate = IndiaDateTime.FromUtcToIst(DateTime.UtcNow);
        var fileName = $"export_{exportDate:ddMMyyyy}.xlsx";

        await _reportExports.AddAsync(new ReportExport
        {
            FileName = fileName,
            FilePath = null,
            GeneratedBy = _currentUser.UserId
        }, ct);
        await _reportExports.SaveChangesAsync(ct);

        return (bytes, fileName);
    }

    private static void SetTextCell(OfficeOpenXml.ExcelRange cell, string? value)
    {
        cell.Value = value;
        cell.Style.Numberformat.Format = "@";
    }

    private static void SetDateCell(OfficeOpenXml.ExcelRange cell, DateTime? utcValue)
    {
        if (!utcValue.HasValue)
            return;

        cell.Value = IndiaDateTime.FromUtcToIst(utcValue.Value);
        cell.Style.Numberformat.Format = "yyyy-mm-dd";
    }

    public async Task<ArchiveDataResultDto> ArchiveDataAsync(CancellationToken ct)
    {
        var hasActiveUploads = await _db.ExcelUploads.AsNoTracking()
            .AnyAsync(x =>
                x.Status == ExcelUploadStatus.QUEUED ||
                x.Status == ExcelUploadStatus.PROCESSING, ct);

        if (hasActiveUploads)
            throw new AppException("Cannot archive while Excel uploads are queued or processing.", 409);

        return await ArchiveOperationalDataAsync(ct);
    }

    private async Task<ArchiveDataResultDto> ArchiveOperationalDataAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var table in DataArchiveHelper.ArchiveTableNames)
            {
                counts[table] = await SoftDeleteTableAsync(table, now, ct);
            }

            var followupsTable = await DataArchiveHelper.ResolveTaskFollowupsTableNameAsync(_db, ct);
            if (!counts.ContainsKey(followupsTable))
                counts[followupsTable] = await SoftDeleteTableAsync(followupsTable, now, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        _logger.LogWarning(
            "ArchiveOperationalDataAsync completed by userId={UserId} counts={Counts}",
            _currentUser.UserId,
            string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}")));

        return new ArchiveDataResultDto
        {
            ArchivedAtUtc = now,
            ArchivedCounts = counts
        };
    }

    private async Task<int> SoftDeleteTableAsync(string table, DateTime archivedAtUtc, CancellationToken ct)
    {
        if (!await DataArchiveHelper.TableExistsAsync(_db, table, ct))
            return 0;

        var hasUpdatedAt = table is "tasks" or "excel_uploads";
        var sql = hasUpdatedAt
            ? $"UPDATE `{table}` SET `is_deleted` = 1, `updated_at` = @p0 WHERE `is_deleted` = 0"
            : $"UPDATE `{table}` SET `is_deleted` = 1 WHERE `is_deleted` = 0";

        return hasUpdatedAt
            ? await _db.Database.ExecuteSqlRawAsync(sql, archivedAtUtc, ct)
            : await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    public async Task<ExcelUploadHistoryDto> GetExcelUploadHistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var q = _db.ExcelUploads.AsNoTracking();
        var total = await q.LongCountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExcelUploadHistoryItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                Status = x.Status,
                TotalRows = x.TotalRows,
                SuccessRows = x.SuccessRows,
                FailedRows = x.FailedRows,
                CreatedAtUtc = x.CreatedAt,
                UploadedAtUtc = x.CreatedAt
            })
            .ToListAsync(ct);

        return new ExcelUploadHistoryDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ExcelUploadDetailsDto> GetExcelUploadDetailsAsync(long uploadId, int recentErrors = 50, CancellationToken ct = default)
    {
        if (recentErrors <= 0) recentErrors = 50;
        if (recentErrors > 200) recentErrors = 200;

        var upload = await _db.ExcelUploads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == uploadId, ct)
            ?? throw new NotFoundException("Upload not found");

        var errors = await _db.ExcelUploadErrors.AsNoTracking()
            .Where(x => x.UploadId == uploadId)
            .OrderByDescending(x => x.Id)
            .Take(recentErrors)
            .Select(x => new ExcelUploadErrorItemDto
            {
                Id = x.Id,
                ExcelRowNumber = x.ExcelRowNumber,
                ErrorMessage = x.ErrorMessage,
                CreatedAtUtc = x.CreatedAt
            })
            .ToListAsync(ct);

        return new ExcelUploadDetailsDto
        {
            Id = upload.Id,
            FileName = upload.FileName,
            Status = upload.Status,
            TotalRows = upload.TotalRows,
            SuccessRows = upload.SuccessRows,
            FailedRows = upload.FailedRows,
            CreatedAtUtc = upload.CreatedAt,
            UploadedAtUtc = upload.CreatedAt,
            RecentErrors = errors
        };
    }
}

