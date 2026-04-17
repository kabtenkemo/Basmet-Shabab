using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/important-contacts")]
public sealed class ImportantContactsController : ControllerBase
{
    private static readonly string[] AllowedDomains =
    [
        "حكومي",
        "إعلام",
        "تعليم",
        "صحة",
        "قطاع خاص",
        "مجتمع مدني",
        "شباب ورياضة",
        "تقني"
    ];

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ImportantContactsController> _logger;

    public ImportantContactsController(AppDbContext dbContext, ILogger<ImportantContactsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImportantContactResponse>>> List(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!IsAllowed(currentMember))
        {
            return Forbid();
        }

        var contacts = await _dbContext.ImportantContacts
            .AsNoTracking()
            .OrderBy(item => item.Domain)
            .ThenBy(item => item.FullName)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Important contacts list returned {Count} items.", contacts.Count);

        return Ok(contacts.Select(MapContact));
    }

    [HttpPost]
    public async Task<ActionResult<ImportantContactResponse>> Create([FromBody] ImportantContactCreateRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!IsAllowed(currentMember))
        {
            return Forbid();
        }

        var fullName = request.FullName.Trim();
        if (!HasAtLeastTwoNameParts(fullName))
        {
            return BadRequest(new { message = "الاسم الثنائي مطلوب." });
        }

        var phone = request.PhoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 8)
        {
            return BadRequest(new { message = "رقم الهاتف مطلوب ويجب أن يكون صحيحًا." });
        }

        var positionTitle = request.PositionTitle.Trim();
        if (string.IsNullOrWhiteSpace(positionTitle))
        {
            return BadRequest(new { message = "المنصب مطلوب." });
        }

        var domain = request.Domain.Trim();
        if (string.IsNullOrWhiteSpace(domain))
        {
            return BadRequest(new { message = "المجال مطلوب." });
        }

        var normalizedDomain = AllowedDomains.FirstOrDefault(item => string.Equals(item, domain, StringComparison.OrdinalIgnoreCase));
        if (normalizedDomain is null)
        {
            return BadRequest(new { message = "المجال غير صالح. اختر من القائمة المتاحة." });
        }

        var contact = new ImportantContact
        {
            FullName = fullName,
            PhoneNumber = phone,
            PositionTitle = positionTitle,
            Domain = normalizedDomain,
            CreatedByMemberId = currentMember.Id
        };

        _dbContext.ImportantContacts.Add(contact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), MapContact(contact));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!IsAllowed(currentMember))
        {
            return Forbid();
        }

        var contact = await _dbContext.ImportantContacts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        _dbContext.ImportantContacts.Remove(contact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members.FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }

    private static bool HasAtLeastTwoNameParts(string fullName)
    {
        return fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length >= 2;
    }

    private static bool IsAllowed(Member member)
    {
        return member.Role is MemberRole.President or MemberRole.VicePresident;
    }

    private static ImportantContactResponse MapContact(ImportantContact contact)
    {
        return new ImportantContactResponse(
            contact.Id,
            contact.FullName,
            contact.PhoneNumber,
            contact.PositionTitle,
            contact.Domain,
            contact.CreatedAtUtc);
    }
}
