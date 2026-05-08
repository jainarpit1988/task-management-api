using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using TaskManagement.Application.Common;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
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

                // EPPlus Dimension can be huge due to formatting; find last actual data row by scanning A-K.
                var lastDataRow = 1;
                if (dimRows > 1)
                {
                    var end = dimRows;
                    const int maxCols = 11; // A-K
                    for (var r = end; r >= 2; r--)
                    {
                        var hasAny = false;
                        for (var c = 1; c <= Math.Min(dimCols, maxCols); c++)
                        {
                            if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].Text))
                            {
                                hasAny = true;
                                break;
                            }
                        }

                        if (hasAny)
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
                            upload.FailedRows++;
                            errorBatch.Add(new ExcelUploadError
                            {
                                UploadId = upload.Id,
                                ExcelRowNumber = 0,
                                ErrorMessage = $"Duplicate InternalId already exists: {dup.InternalId}",
                                RawData = dup.RawData
                            });
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

                        foreach (var t in taskBatch)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                await RetryAsync(
                                    async innerCt =>
                                    {
                                        await _db.Tasks.AddAsync(t, innerCt);
                                        await _db.SaveChangesAsync(innerCt);
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
                        // A-K mapping:
                        // A: InternalId
                        // B: Hub -> hub_name
                        // C: Application Number -> application_no
                        // D: Customer Name -> customer_name
                        // E: Entity Name -> entity_name
                        // F: Loan Type -> loan_type
                        // G: Loan Amount -> loan_amount
                        // H: Address -> customer_address
                        // I: Location -> location
                        // J: Hub -> branch_hub
                        // K: Mobile NO -> mobile_no

                        var internalId = NullIfBlank(ws.Cells[row, 1].Text);
                        if (internalId is not null && internalId.Length > 50)
                            internalId = internalId[..50];

                        // If InternalId is missing, generate a stable fallback so row can still be inserted.
                        // This avoids "failed records" while keeping uniqueness.
                        internalId ??= $"U{upload.Id:D6}R{row:D6}";

                        var hubName = Trunc(NullIfBlank(ws.Cells[row, 2].Text), 100);
                        var appNo = Trunc(NullIfBlank(ws.Cells[row, 3].Text), 100);
                        var customerName = Trunc(NullIfBlank(ws.Cells[row, 4].Text), 255);
                        var entityName = Trunc(NullIfBlank(ws.Cells[row, 5].Text), 255);
                        var loanType = Trunc(NullIfBlank(ws.Cells[row, 6].Text), 100);
                        var loanAmount = ParseDecimalOrNull(ws.Cells[row, 7].Text);
                        var customerAddress = NullIfBlank(ws.Cells[row, 8].Text);
                        var location = Trunc(NullIfBlank(ws.Cells[row, 9].Text), 255);
                        var branchHub = Trunc(NullIfBlank(ws.Cells[row, 10].Text), 100);
                        var mobileNo = Trunc(NullIfBlank(ws.Cells[row, 11].Text), 20);

                        // Requirement: if Customer Name is blank, skip row (do not insert).
                        if (customerName is null)
                        {
                            upload.FailedRows++;
                            errorBatch.Add(new ExcelUploadError
                            {
                                UploadId = upload.Id,
                                ExcelRowNumber = row,
                                ErrorMessage = "Skipped row: Customer Name is blank",
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

                            if (errorBatch.Count >= batchSize)
                                await FlushErrorsAsync();

                            continue;
                        }

                        var rawObj = new Dictionary<string, object?>
                        {
                            ["InternalId"] = internalId,
                            ["HubName"] = hubName,
                            ["ApplicationNo"] = appNo,
                            ["CustomerName"] = customerName,
                            ["EntityName"] = entityName,
                            ["LoanType"] = loanType,
                            ["LoanAmount"] = loanAmount,
                            ["CustomerAddress"] = customerAddress,
                            ["Location"] = location,
                            ["BranchHub"] = branchHub,
                            ["MobileNo"] = mobileNo
                        };

                        taskBatch.Add(new TaskItem
                        {
                            InternalId = internalId,
                            SrNo = null,
                            HubName = hubName,
                            ApplicationNo = appNo,
                            CustomerName = customerName,
                            EntityName = entityName,
                            LoanType = loanType,
                            LoanAmount = loanAmount,
                            CustomerAddress = customerAddress,
                            Location = location,
                            BranchHub = branchHub,
                            MobileNo = mobileNo,
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

    public async Task<(IReadOnlyList<TasksPerAgentReportRowDto> tasksPerAgent, StatusSummaryReportDto statusSummary)> GetReportsAsync(
        TaskFilterDto filter,
        CancellationToken ct)
    {
        // Base filtered tasks (soft-delete protected)
        var q = _db.Tasks.AsNoTracking().Where(x => !x.IsDeleted);

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

        var (tasksPerAgent, statusSummary) = await GetReportsAsync(filter, ct);

        using var package = new ExcelPackage();

        var ws1 = package.Workbook.Worksheets.Add("TasksPerAgent");
        ws1.Cells[1, 1].Value = "AgentId";
        ws1.Cells[1, 2].Value = "AgentName";
        ws1.Cells[1, 3].Value = "TotalTasks";
        ws1.Cells[1, 4].Value = "Open";
        ws1.Cells[1, 5].Value = "InProgress";
        ws1.Cells[1, 6].Value = "Closed";

        var r = 2;
        foreach (var row in tasksPerAgent)
        {
            ws1.Cells[r, 1].Value = row.AgentId;
            ws1.Cells[r, 2].Value = row.AgentName;
            ws1.Cells[r, 3].Value = row.TotalTasks;
            ws1.Cells[r, 4].Value = row.OpenTasks;
            ws1.Cells[r, 5].Value = row.InProgressTasks;
            ws1.Cells[r, 6].Value = row.ClosedTasks;
            r++;
        }

        ws1.Cells[ws1.Dimension.Address].AutoFitColumns();

        var ws2 = package.Workbook.Worksheets.Add("StatusSummary");
        ws2.Cells[1, 1].Value = "Status";
        ws2.Cells[1, 2].Value = "Count";

        var rr = 2;
        foreach (var kv in statusSummary.Counts.OrderBy(x => x.Key.ToString()))
        {
            ws2.Cells[rr, 1].Value = kv.Key.ToString();
            ws2.Cells[rr, 2].Value = kv.Value;
            rr++;
        }

        ws2.Cells[ws2.Dimension.Address].AutoFitColumns();

        var bytes = package.GetAsByteArray();
        var fileName = $"report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        await _reportExports.AddAsync(new ReportExport
        {
            FileName = fileName,
            FilePath = null,
            GeneratedBy = _currentUser.UserId
        }, ct);
        await _reportExports.SaveChangesAsync(ct);

        return (bytes, fileName);
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

